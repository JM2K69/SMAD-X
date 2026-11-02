using SMADX.Models;
using SMADX.Services;
using SMADX.Services.AttackPaths;
using SMADX.Services.Audit;
using SMADX.ViewModels;
using static SMADX.Tests.TestHelpers;

namespace SMADX.Tests;

/// <summary>
/// Silos d'authentification : un compte d'un silo appliqué n'ouvre de session que sur les ordinateurs du silo. Dans le
/// domaine d'exemple, l'administrateur du Tier 0 adm-lmartin a une session sur le serveur SRV-FILE-01, que GG-IT-Servers
/// contrôle : deux chemins hors Tier 0.
/// </summary>
public class AuthenticationSiloTests
{
    private static readonly DateTime Now = new(2026, 11, 2);

    public AuthenticationSiloTests() => TierConfigurationService.Instance.ResetToDefaults();

    private static ADObject SampleWithSilo(bool enforced, params string[] allowedFrom)
    {
        var domain = new ADDataService().CreateSampleStructure();
        domain.AuthenticationSilos =
        [
            new ADAuthenticationSilo { Name = "Tier0", Enforced = enforced, Members = ["adm-lmartin", "Administrator", "Domain Controllers"], AllowedFrom = [.. allowedFrom] }
        ];
        return domain;
    }

    private static List<AuditFinding> Findings(ADObject domain, string rule) =>
        new ADSecurityAuditService().Run(domain, Now).Findings.Where(f => f.Rule.Id == rule).ToList();

    [Fact]
    public void AnEnforcedSilo_DeniesTheSessionOutsideTheSilo_AndCutsThePaths()
    {
        var domain = SampleWithSilo(enforced: true);
        var paths = new AttackPathService(domain, Now);

        Assert.Empty(paths.PathsOutsideTier0());
        var blocked = Assert.Single(paths.BlockedSessions);
        Assert.Equal(("adm-lmartin", "SRV-FILE-01", "Tier0"), (blocked.Account.Name, blocked.Computer.Name, blocked.Silo!.Name));
        Assert.Equal(["silo Tier0"], blocked.Causes);
        Assert.Empty(Findings(domain, "TIER-04"));
        Assert.Empty(Findings(domain, "TIER-09"));
        // Les contrôleurs de domaine sont dans le silo : la session y reste possible
        Assert.Null(new AuditContext(domain, Now).Silos.LogonDeniedBy(Find(domain, "adm-lmartin"), Find(domain, "DC01")));
    }

    [Fact]
    public void InAuditMode_TheSiloDeniesNothing_AndTier09ReportsIt()
    {
        var domain = SampleWithSilo(enforced: false);

        Assert.Equal(2, new AttackPathService(domain, Now).PathsOutsideTier0().Count);
        Assert.Single(Findings(domain, "TIER-04"));
        var tier09 = Findings(domain, "TIER-09");
        Assert.Equal(["Administrator", "adm-lmartin"], tier09.Select(f => f.Object.Name).Order(StringComparer.Ordinal));
        Assert.Contains("Tier0", tier09[0].Detail);
    }

    [Fact]
    public void AComputerAllowedByThePolicy_AcceptsTheSession_AndAnUnrestrictedPolicyAcceptsAll()
    {
        Assert.Empty(new AttackPathService(SampleWithSilo(true, "SRV-FILE-01"), Now).BlockedSessions);
        Assert.Empty(new AttackPathService(SampleWithSilo(true, "Everyone"), Now).BlockedSessions);
        Assert.Single(new AttackPathService(SampleWithSilo(true, "WKS-001"), Now).BlockedSessions);
    }

    [Fact]
    public void Tier09_IsSilentWithoutSilo_AndReportsAPrivilegedAccountLeftOut()
    {
        var domain = new ADDataService().CreateSampleStructure();
        Assert.Empty(Findings(domain, "TIER-09"));

        domain = SampleWithSilo(enforced: true);
        domain.AuthenticationSilos![0].Members.Remove("Administrator");
        var finding = Assert.Single(Findings(domain, "TIER-09"));
        Assert.Equal("Administrator", finding.Object.Name);
    }

    [Fact]
    public void Silos_AreSavedWithTheDomain_AndCopied()
    {
        var domain = SampleWithSilo(true, "PAW-01");
        var json = System.Text.Json.JsonSerializer.Serialize(domain, ADDataService.SerializerOptions);
        var back = System.Text.Json.JsonSerializer.Deserialize<ADObject>(json, ADDataService.SerializerOptions)!;

        foreach (var silo in new[] { back.AuthenticationSilos![0], domain.Clone().AuthenticationSilos![0] })
        {
            Assert.Equal(("Tier0", true), (silo.Name, silo.Enforced));
            Assert.Equal(["adm-lmartin", "Administrator", "Domain Controllers"], silo.Members);
            Assert.Equal(["PAW-01"], silo.AllowedFrom);
        }
        Assert.DoesNotContain("AuthenticationSilos", System.Text.Json.JsonSerializer.Serialize(new ADDataService().CreateSampleStructure(), ADDataService.SerializerOptions));
    }

    [Fact]
    public void Editor_NamesNewSilos_ChecksNames_AndCleansTheLists()
    {
        var editor = new AuthenticationSilosViewModel(null);
        editor.AddCommand.Execute(null);
        editor.AddCommand.Execute(null);
        Assert.Equal(["Tier0", "Silo2"], editor.Silos.Select(s => s.Name));

        editor.Selected!.Name = " tier0 ";
        Assert.Contains("Tier0", editor.Error);
        editor.Selected.Name = "";
        Assert.NotNull(editor.Error);
        editor.RemoveCommand.Execute(null);
        Assert.Null(editor.Error);

        editor.Selected!.MembersText = " adm-x \n\nADM-X\nDC01";
        editor.Selected.Enforced = true;
        var silo = Assert.Single(editor.Result());
        Assert.Equal(["adm-x", "DC01"], silo.Members);
        Assert.True(silo.Enforced);
    }

    [Fact]
    public void SecurityPanel_ShowsTheSiloOfAnAccount_AndTheSessionItDenies()
    {
        var domain = SampleWithSilo(enforced: true);

        Assert.Contains("Tier0", new ObjectSecurityViewModel(Find(domain, "adm-lmartin")).SiloEffect);
        Assert.False(new ObjectSecurityViewModel(Find(domain, "jdoe")).HasSiloEffect);
        Assert.Equal("adm-lmartin (silo Tier0)", new ObjectSecurityViewModel(Find(domain, "SRV-FILE-01")).BlockedSessionsEffect);
    }
}
