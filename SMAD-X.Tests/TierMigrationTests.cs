using SMADX.Models;
using SMADX.Services;
using SMADX.Services.Audit;
using SMADX.Services.Tiering;
using SMADX.ViewModels;
using static SMADX.Tests.TestHelpers;

namespace SMADX.Tests;

/// <summary>
/// Migration d'un domaine classique vers un modèle de cloisonnement, et simulation de son effet sur une copie. Le domaine
/// d'exemple (contoso.com) a deux chemins hors Tier 0 : un administrateur du Tier 0 a une session sur un serveur que le
/// groupe GG-IT-Servers contrôle.
/// </summary>
public class TierMigrationTests
{
    private static readonly DateTime Now = new(2026, 11, 2);

    public TierMigrationTests() => TierConfigurationService.Instance.ResetToDefaults();

    public static TheoryData<string> Models => new() { "microsoft", "hardenad" };

    private static TierModelDefinition Model(string id) => TierModelCatalog.Find(id)!;

    [Theory]
    [MemberData(nameof(Models))]
    public void Simulation_OnTheSampleDomain_CutsThePaths_AndLeavesTheDomainUntouched(string id)
    {
        var domain = new ADDataService().CreateSampleStructure();
        var json = System.Text.Json.JsonSerializer.Serialize(domain, ADDataService.SerializerOptions);

        var impact = TierImpactSimulator.Simulate(domain, Model(id), Now);

        Assert.Equal(2, impact.Before.PathsOutsideTier0.Count);
        Assert.Empty(impact.After.PathsOutsideTier0);
        Assert.Equal(2, impact.CutPaths.Count);
        var blocked = Assert.Single(impact.After.BlockedSessions);
        Assert.Equal(("adm-lmartin", "SRV-FILE-01"), (blocked.Account.Name, blocked.Computer.Name));
        Assert.True(impact.After.Audit.Score > impact.Before.Audit.Score);
        Assert.Contains(impact.FixedFindings, f => f.Rule.Id == "TIER-04");
        Assert.DoesNotContain(impact.After.Audit.Findings, f => f.Rule.Id is "TIER-05" or "TIER-06" or "TIER-07" or "TIER-08");
        // La simulation travaille sur une copie
        Assert.Equal(json, System.Text.Json.JsonSerializer.Serialize(domain, ADDataService.SerializerOptions));
        Assert.DoesNotContain("Tier 1 Legacy", TierConfigurationService.Instance.Tiers.Select(t => t.Name));
    }

    [Theory]
    [MemberData(nameof(Models))]
    public void Plan_NeverMakesAGroupJoinAMoreSensitiveTier(string id)
    {
        var domain = new ADDataService().CreateSampleStructure();
        var result = TierMigrationService.Migrate(domain, Model(id), Now, registerTiers: false);
        var c = new AuditContext(domain, Now);

        Assert.All(result.Steps.Where(s => s.Action == TierMigrationAction.AddMember), s =>
            Assert.True(AuditContext.TierLevel(c.Principal(s.To)!) >= AuditContext.TierLevel(c.Principal(s.ObjectName)!), $"{s.ObjectName} -> {s.To}"));
    }

    [Fact]
    public void Microsoft_TheHelpdeskKeepsItsRights_ThroughOneModelGroup()
    {
        var domain = new ADDataService().CreateSampleStructure();
        var result = TierMigrationService.Migrate(domain, Model("microsoft"), Now, registerTiers: false);

        var helpdesk = Assert.Single(result.Steps, s => s.Action == TierMigrationAction.AddMember && s.ObjectName == "GG-HelpDesk");
        Assert.Equal("Tier2HelpdeskOperators", helpdesk.To);
        Assert.Contains("Tier2HelpdeskOperators", Find(domain, "GG-HelpDesk").MemberOf);
        // Les groupes restreints de la GPO remplacent les administrateurs locaux des serveurs : GG-IT-Servers les garde
        Assert.Contains(result.Steps, s => s is { Action: TierMigrationAction.AddMember, ObjectName: "GG-IT-Servers", To: "Tier1ServerOperators" });
        Assert.Contains(new AuditContext(domain, Now).Policies.LocalAdministrators(Find(domain, "SRV-APP-01")), a => a.Name == "Tier1ServerOperators");
        Assert.Equal("Tier Model Administration/Tier 0/Tier 0 Accounts", TierModelCompliance.OuPath(Find(domain, "adm-lmartin").Parent));
    }

    [Fact]
    public void HardenAd_ARightWithoutEquivalent_IsReportedNotWidened()
    {
        var domain = new ADDataService().CreateSampleStructure();
        var result = TierMigrationService.Migrate(domain, Model("hardenad"), Now, registerTiers: false);

        Assert.DoesNotContain(result.Steps, s => s.Action == TierMigrationAction.AddMember && s.ObjectName == "GG-HelpDesk");
        Assert.Contains(result.Issues, i => i.ObjectName == "GG-HelpDesk" && i.Reason.Contains("Harden_T12/Users"));
    }

    [Fact]
    public void ObjectsWithoutTier_AreClassifiedByRole_AndReceiveTheTierOfTheirOu()
    {
        var domain = new ADDataService().CreateFreshDomainStructure("corp.local", enableTiering: false);
        var paris = Add(domain, new ADObject("Paris", ADObjectType.OrganizationalUnit));
        Add(paris, new ADObject("jdupont", ADObjectType.User));
        Add(paris, new ADObject("SRV-PARIS", ADObjectType.Computer));
        Add(paris, new ADObject("PC-01", ADObjectType.Computer));
        Add(paris, new ADObject("ancien", ADObjectType.User) { AccountFlags = ADAccountFlags.AccountDisabled });

        var result = TierMigrationService.Migrate(domain, Model("microsoft"), Now, registerTiers: false);

        string Place(string name) => TierModelCompliance.OuPath(Find(domain, name).Parent)!;
        Assert.Equal("Tier 2 End-User Accounts", Place("jdupont"));
        Assert.Equal("Tier 1 Member Servers", Place("SRV-PARIS"));
        Assert.Equal("Tier 2 End-User Devices", Place("PC-01"));
        Assert.Equal("Tier 2 End-User Accounts/Disabled End-Users Accounts", Place("ancien"));
        Assert.Equal("Tier 1", Find(domain, "SRV-PARIS").Tier);
        Assert.Contains(result.Steps, s => s.ObjectName == "SRV-PARIS" && s.Reason.Contains(LocalizationService.Instance["Tiering.Source.Assumed"]));
        Assert.DoesNotContain(new ADSecurityAuditService().Run(domain, Now).Findings, f => f.Rule.Id == "TIER-06");
    }

    [Theory]
    [MemberData(nameof(Models))]
    public void Migration_CanBeReplayed(string id)
    {
        var domain = new ADDataService().CreateSampleStructure();
        TierMigrationService.Migrate(domain, Model(id), Now, registerTiers: false);

        var again = TierMigrationService.Migrate(domain, Model(id), Now, registerTiers: false);

        Assert.Equal(0, again.Applied.Total);
        Assert.Empty(again.Steps);
    }

    [Fact]
    public async Task Window_ComparesBothModels_AndMigratesTheSelectedOne()
    {
        var domain = new ADDataService().CreateSampleStructure();
        TierModelDefinition? migrated = null;
        var vm = new TierImpactViewModel(() => domain, m => { migrated = m; TierMigrationService.Migrate(domain, m, Now, registerTiers: false); return Task.FromResult(true); });
        await vm.Loading;

        Assert.Equal(["microsoft", "hardenad"], vm.Impacts.Select(i => i.Impact.Model.Id));
        Assert.Equal(9, vm.Rows.Count);
        Assert.Equal(["2", "0", "0"], new[] { vm.Rows[2].Before, vm.Rows[2].First, vm.Rows[2].Second });
        Assert.Equal("microsoft", vm.SelectedImpact!.Impact.Model.Id);
        Assert.NotEmpty(vm.Steps);
        Assert.Equal(2, vm.CutPaths.Count);
        Assert.Empty(vm.RemainingPaths);

        vm.SelectedImpact = vm.Impacts[1];
        await vm.MigrateCommand.ExecuteAsync(null);

        Assert.Equal("hardenad", migrated!.Id);
        Assert.Equal("hardenad", domain.TierModel);
        Assert.Equal("hardenad", vm.SelectedImpact!.Impact.Model.Id);
        Assert.Equal("0", vm.Rows[2].Before); // simulée à nouveau : le domaine n'a plus de chemin hors Tier 0
    }
}
