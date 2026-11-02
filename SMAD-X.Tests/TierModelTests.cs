using SMADX.Models;
using SMADX.Services;
using SMADX.Services.AttackPaths;
using SMADX.Services.Audit;
using SMADX.Services.Tiering;
using static SMADX.Tests.TestHelpers;

namespace SMADX.Tests;

public class TierModelTests
{
    private static readonly DateTime Now = new(2026, 11, 2);

    public TierModelTests() => TierConfigurationService.Instance.ResetToDefaults();

    public static TheoryData<string> Models => new() { "microsoft", "hardenad" };

    private static TierModelDefinition Model(string id) => TierModelCatalog.Find(id)!;

    private static ADObject FreshWith(string id)
    {
        var domain = new ADDataService().CreateFreshDomainStructure("corp.local");
        TierModelService.Apply(domain, Model(id), Now);
        return domain;
    }

    [Fact]
    public void Catalog_HasBothModels_WithTheirSource()
    {
        Assert.Equal(["microsoft", "hardenad"], TierModelCatalog.All.Select(m => m.Id));
        Assert.All(TierModelCatalog.All, m =>
        {
            Assert.StartsWith("https://github.com/", m.Source.Repository);
            Assert.Matches("^[0-9a-f]{40}$", m.Source.Commit);
            Assert.Equal("MIT", m.Source.License);
            Assert.StartsWith("Copyright (c)", m.Source.Copyright);
            Assert.NotEmpty(m.Source.Version);
        });
    }

    [Theory]
    [MemberData(nameof(Models))]
    public void Model_ReferencesOnlyItsOwnObjects(string id)
    {
        var model = Model(id);
        var ous = model.OrganizationalUnits.Select(o => o.Path).ToHashSet();
        var principals = model.Groups.Select(g => g.Name).Concat(model.Accounts.Select(a => a.Name)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.All(model.OrganizationalUnits.Where(o => o.Path.Contains('/')), o => Assert.Contains(o.Path[..o.Path.LastIndexOf('/')], ous));
        Assert.All(model.Groups.Select(g => g.Path).Concat(model.Accounts.Select(a => a.Path)), p => Assert.Contains(p, ous));
        Assert.All(model.Delegations, d => { Assert.Contains(d.Target, ous); Assert.Contains(d.Trustee, principals); });
        Assert.All(model.Groups.SelectMany(g => g.MemberOf).Concat(model.Accounts.SelectMany(a => a.MemberOf)), g => Assert.Contains(g, principals));
        // Un lien de GPO vise une OU du modèle, la racine ou l'OU Domain Controllers d'un domaine neuf
        Assert.All(model.GroupPolicies.SelectMany(g => g.Links), l => Assert.True(l.Target is "" or "Domain Controllers" || ous.Contains(l.Target), l.Target));
    }

    [Theory]
    [MemberData(nameof(Models))]
    public void Apply_CreatesTheWholeModel_AndCanBeReplayed(string id)
    {
        var model = Model(id);
        var domain = new ADDataService().CreateFreshDomainStructure("corp.local");

        var report = TierModelService.Apply(domain, model, Now);

        Assert.Equal(model.OrganizationalUnits.Count, report.OrganizationalUnits);
        Assert.Equal(model.Groups.Count, report.Groups);
        Assert.Equal(model.Accounts.Count, report.Accounts);
        Assert.Equal(model.GroupPolicies.Count, report.GroupPolicies);
        Assert.Equal(model.Delegations.Count, report.Delegations);
        Assert.Equal(model.GroupPolicies.Sum(g => g.Links.Count(l => l.Enabled)), report.Links);
        Assert.Equal(id, domain.TierModel);
        Assert.Equal(model.Source.Version, domain.TierModelVersion);
        Assert.All(model.OrganizationalUnits, o => Assert.Equal(o.Tier, TierModelService.Ou(domain, o.Path)!.Tier));

        Assert.Equal(0, TierModelService.Apply(domain, model, Now).Total);
    }

    [Fact]
    public void HardenAd_Tier0Managers_AreEnterpriseAdmins_Protected_AndSeenAfterSdProp()
    {
        var domain = FreshWith("hardenad");
        var managers = Find(domain, "G-S-T0_Managers");
        var account = Find(domain, "T0M-AHARDEN");

        Assert.Contains("Enterprise Admins", managers.MemberOf);
        Assert.Contains("Protected Users", managers.MemberOf);
        Assert.Contains("G-S-T0_Managers", account.MemberOf);
        Assert.True(managers.AdminCount);
        Assert.True(account.AdminCount);
        Assert.Equal("Domain Admins", account.Owner);
        Assert.Contains(account.Delegations, d => d.TrusteeName == "Enterprise Admins" && d.Right == "WriteDacl");
        Assert.Contains("Tier 1 Legacy", TierConfigurationService.Instance.Tiers.Select(t => t.Name));
    }

    [Theory]
    [MemberData(nameof(Models))]
    public void Apply_OnAFreshDomain_OpensNoPathOutsideTier0_AndBreaksNoTierRule(string id)
    {
        var fresh = new ADDataService().CreateFreshDomainStructure("corp.local");
        var before = new ADSecurityAuditService().Run(fresh, Now).FailedRules.Select(r => r.Id).ToHashSet();

        var domain = FreshWith(id);

        Assert.Empty(new AttackPathService(domain, Now).PathsOutsideTier0());
        var report = new ADSecurityAuditService().Run(domain, Now);
        var failed = report.FailedRules.Select(r => r.Id).ToHashSet();
        Assert.True(!failed.Any(r => r.StartsWith("TIER-")),
            string.Join("\n", report.Findings.Where(f => f.Rule.Id.StartsWith("TIER-")).Select(f => $"{f.Rule.Id} {f.Object.Name} {f.Object.DistinguishedName}")));
        Assert.Empty(failed.Except(before));
    }

    private static List<AuditFinding> Findings(ADObject domain, string rule) =>
        new ADSecurityAuditService().Run(domain, Now).Findings.Where(f => f.Rule.Id == rule).ToList();

    [Theory]
    [MemberData(nameof(Models))]
    public void Compliance_AnIntactModel_HasNoFinding(string id)
    {
        var domain = FreshWith(id);
        Assert.All(new[] { "TIER-05", "TIER-06", "TIER-07", "TIER-08" }, rule => Assert.Empty(Findings(domain, rule)));
    }

    [Fact]
    public void Compliance_WithoutModel_RulesStaySilent()
    {
        var sample = new ADDataService().CreateSampleStructure();
        Assert.All(new[] { "TIER-05", "TIER-06", "TIER-07", "TIER-08" }, rule => Assert.Empty(Findings(sample, rule)));
    }

    [Fact]
    public void Tier05_ReportsARemovedDelegation_AndAnUnlinkedRestriction()
    {
        var domain = FreshWith("microsoft");
        var accounts = TierModelService.Ou(domain, "Tier 2 End-User Accounts")!;
        accounts.Delegations.Remove(accounts.Delegations.First(d => d.TrusteeName == "Tier2HelpdeskOperators" && d.Right == "ResetPassword"));
        var servers = TierModelService.Ou(domain, "Tier 1 Member Servers")!;
        servers.LinkedGPOs.Remove("Tier 1 Servers Account Restrictions");

        var findings = Findings(domain, "TIER-05");

        Assert.Contains(findings, f => f.Object == accounts && f.Detail.Contains("Tier2HelpdeskOperators : ResetPassword"));
        Assert.Contains(findings, f => f.Object == servers && f.Detail.Contains("Tier 1 Servers Account Restrictions"));
        Assert.Equal(2, findings.Count);
    }

    [Fact]
    public void Tier06To08_ReportObjectsTheModelHasNotPlacedYet()
    {
        var domain = FreshWith("hardenad");
        var users = Flatten(domain).First(o => o is { Name: "Users", Type: ADObjectType.Container });
        Add(users, new ADObject("jdupont", ADObjectType.User));                                                  // conteneur par défaut
        Add(TierModelService.Ou(domain, "Provisioning/Computers")!, new ADObject("PC-NEUF", ADObjectType.Computer)); // transit
        var admin = Add(TierModelService.Ou(domain, "Harden_T12/Users")!, new ADObject("adm-paul", ADObjectType.User));
        admin.MemberOf.Add("Domain Admins");                                                                      // Tier 0 chez le Tier 2

        Assert.Equal(["jdupont"], Findings(domain, "TIER-06").Select(f => f.Object.Name));
        Assert.Equal(["PC-NEUF"], Findings(domain, "TIER-07").Select(f => f.Object.Name));
        Assert.Equal(["adm-paul"], Findings(domain, "TIER-08").Select(f => f.Object.Name));
    }

    /// <summary>
    /// Fidélité : le modèle HardenAD appliqué à un domaine neuf a les OU, les groupes (noms, emplacements, descriptions)
    /// et les GPO d'un vrai domaine durci par HardenAD, exporté par le script d'import de SMAD-X
    /// (Fidelity/hardenad-harden.ad.smad-x.json). Les comptes et les délégations n'y figurent pas sous une forme
    /// comparable : l'export vient d'un script d'import plus ancien.
    /// </summary>
    [Fact]
    public async Task HardenAd_MatchesARealHardenedDomain()
    {
        var result = await new ADDataService().LoadDocumentAsync(Path.Combine(AppContext.BaseDirectory, "Fidelity", "hardenad-harden.ad.smad-x.json"));
        var real = result.Document!.Domain;
        var simulated = new ADDataService().CreateFreshDomainStructure(real.Name);
        TierModelService.Apply(simulated, Model("hardenad"), Now);

        static string PathOf(ADObject o) =>
            o.Parent is { Type: not ADObjectType.Domain } parent ? $"{PathOf(parent)}/{o.Name}" : o.Name;
        static HashSet<string> Ous(ADObject d) => Flatten(d).Where(o => o.Type == ADObjectType.OrganizationalUnit).Select(PathOf).ToHashSet();
        static HashSet<string> Groups(ADObject d) => Flatten(d).Where(o => o.Type == ADObjectType.Group && (o.Name.StartsWith("L-S-") || o.Name.StartsWith("G-S-")))
            .Select(o => $"{PathOf(o)} | {o.Description}").ToHashSet();
        static HashSet<string> Gpos(ADObject d) => Flatten(d).Where(o => o.Type == ADObjectType.Policy).Select(o => o.Name).ToHashSet();

        Assert.Equal(Ous(real).Order(), Ous(simulated).Order());
        Assert.Equal(292, Groups(real).Count);
        Assert.Equal(Groups(real).Order(), Groups(simulated).Order());
        Assert.Equal(Gpos(real).Order(), Gpos(simulated).Order());
    }

    [Fact]
    public void TierModel_IsSavedWithTheDomain()
    {
        var domain = FreshWith("microsoft");
        var json = System.Text.Json.JsonSerializer.Serialize(domain, ADDataService.SerializerOptions);
        var back = System.Text.Json.JsonSerializer.Deserialize<ADObject>(json, ADDataService.SerializerOptions)!;

        Assert.Equal("microsoft", back.TierModel);
        Assert.Equal("v2.1.0", back.TierModelVersion);
        Assert.DoesNotContain("TierModel", System.Text.Json.JsonSerializer.Serialize(new ADDataService().CreateFreshDomainStructure("corp.local"), ADDataService.SerializerOptions));
    }
}
