using SMADX.Models;
using SMADX.Services;
using SMADX.Services.AttackPaths;
using SMADX.Services.Audit;
using SMADX.ViewModels;
using static SMADX.Tests.TestHelpers;

namespace SMADX.Tests;

/// <summary>
/// Silos d'authentification : un silo associe des identités aux ordinateurs depuis lesquels elles s'authentifient. Dans le
/// domaine d'exemple, l'administrateur du Tier 0 adm-lmartin a une session sur le serveur SRV-FILE-01, que GG-IT-Servers
/// contrôle : deux chemins hors Tier 0.
/// </summary>
public class AuthenticationSiloTests
{
    private static readonly DateTime Now = new(2026, 11, 2);

    public AuthenticationSiloTests() => TierConfigurationService.Instance.ResetToDefaults();

    /// <param name="protectedUsers">Les identités du silo sont membres de Protected Users : NTLM ne contourne pas le silo.</param>
    private static ADObject SampleWithSilo(bool enforced, bool protectedUsers = true, params string[] computers)
    {
        var domain = new ADDataService().CreateSampleStructure();
        domain.AuthenticationSilos =
        [
            new ADAuthenticationSilo { Name = "Tier0", Enforced = enforced, Members = ["adm-lmartin", "Administrator"], Computers = ["Domain Controllers", .. computers] }
        ];
        if (protectedUsers)
            foreach (var name in new[] { "adm-lmartin", "Administrator" })
                Find(domain, name).MemberOf.Add("Protected Users");
        return domain;
    }

    private static List<AuditFinding> Findings(ADObject domain, string rule) =>
        new ADSecurityAuditService().Run(domain, Now).Findings.Where(f => f.Rule.Id == rule).ToList();

    [Fact]
    public void AnEnforcedSilo_DeniesTheSessionOutsideItsComputers_AndCutsThePaths()
    {
        var domain = SampleWithSilo(enforced: true);
        var paths = new AttackPathService(domain, Now);

        Assert.Empty(paths.PathsOutsideTier0());
        var blocked = Assert.Single(paths.BlockedSessions);
        Assert.Equal(("adm-lmartin", "SRV-FILE-01", "Tier0"), (blocked.Account.Name, blocked.Computer.Name, blocked.Silo!.Name));
        Assert.Equal(["silo Tier0"], blocked.Causes);
        Assert.Empty(Findings(domain, "TIER-04"));
        Assert.Empty(Findings(domain, "TIER-09"));
        // Les contrôleurs de domaine sont des ordinateurs du silo : la session y reste possible
        Assert.Null(new AuditContext(domain, Now).Silos.LogonDeniedBy(Find(domain, "adm-lmartin"), Find(domain, "DC01")));
    }

    [Fact]
    public void OutsideProtectedUsers_NtlmBypassesTheSilo_AndTier09ReportsIt()
    {
        var domain = SampleWithSilo(enforced: true, protectedUsers: false);

        Assert.Equal(2, new AttackPathService(domain, Now).PathsOutsideTier0().Count);
        var tier09 = Findings(domain, "TIER-09");
        Assert.Equal(["Administrator", "adm-lmartin"], tier09.Select(f => f.Object.Name).Order(StringComparer.Ordinal));
        Assert.Contains("Protected Users", tier09[0].Detail);
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
    public void AComputerOfTheSilo_AcceptsTheSession_AndAnUnrestrictedPolicyAcceptsAll()
    {
        Assert.Empty(new AttackPathService(SampleWithSilo(true, true, "SRV-FILE-01"), Now).BlockedSessions);
        Assert.Empty(new AttackPathService(SampleWithSilo(true, true, "Everyone"), Now).BlockedSessions);
        Assert.Single(new AttackPathService(SampleWithSilo(true, true, "WKS-001"), Now).BlockedSessions);
    }

    [Fact]
    public void Tier09_IsSilentWithoutSilo_AndReportsAPrivilegedAccountLeftOut()
    {
        Assert.Empty(Findings(new ADDataService().CreateSampleStructure(), "TIER-09"));

        var domain = SampleWithSilo(enforced: true);
        domain.AuthenticationSilos![0].Members.Remove("Administrator");
        var finding = Assert.Single(Findings(domain, "TIER-09"));
        Assert.Equal("Administrator", finding.Object.Name);
    }

    [Fact]
    public void Silos_AreSavedWithTheDomain_AndCopied()
    {
        var domain = SampleWithSilo(true, true, "PAW-01");
        var json = System.Text.Json.JsonSerializer.Serialize(domain, ADDataService.SerializerOptions);
        var back = System.Text.Json.JsonSerializer.Deserialize<ADObject>(json, ADDataService.SerializerOptions)!;

        foreach (var silo in new[] { back.AuthenticationSilos![0], domain.Clone().AuthenticationSilos![0] })
        {
            Assert.Equal(("Tier0", true), (silo.Name, silo.Enforced));
            Assert.Equal(["adm-lmartin", "Administrator"], silo.Members);
            Assert.Equal(["Domain Controllers", "PAW-01"], silo.Computers);
        }
        Assert.DoesNotContain("AuthenticationSilos", System.Text.Json.JsonSerializer.Serialize(new ADDataService().CreateSampleStructure(), ADDataService.SerializerOptions));
    }

    [Fact]
    public void Editor_BrowsesTheDirectory_ForIdentitiesAndComputers()
    {
        var domain = new ADDataService().CreateSampleStructure();
        var editor = new AuthenticationSilosViewModel(null, domain);
        editor.AddCommand.Execute(null);

        var identities = editor.PickMembers();
        Assert.Equal(new[] { "Dialog.Picker.Users", "Dialog.Picker.Groups", "Dialog.Picker.ServiceAccounts" }.Select(k => LocalizationService.Instance[k]), identities.Kinds.Select(k => k.Label));
        Assert.Contains(identities.Entries, e => e.Name == "adm-lmartin" && e.Location == "Admin");
        Assert.DoesNotContain(identities.Entries, e => e.Object.Type == ADObjectType.Computer);
        identities.Kinds[1].IsChecked = false;
        Assert.DoesNotContain(identities.Entries, e => e.Object.Type == ADObjectType.Group);
        identities.Kinds[1].IsChecked = true;
        identities.Filter = "lmartin";
        Assert.Equal(["adm-lmartin"], identities.Entries.Select(e => e.Name));
        identities.Entries.Single().IsSelected = true;
        identities.Filter = "";
        identities.Entries.First(e => e.Name == "GG-Tier0-Admins").IsSelected = true;
        editor.AddMembers(identities.SelectedNames);

        var computers = editor.PickComputers();
        Assert.Equal(new[] { "Dialog.Picker.Computers", "Dialog.Picker.ComputerGroups" }.Select(k => LocalizationService.Instance[k]), computers.Kinds.Select(k => k.Label));
        Assert.DoesNotContain(computers.Entries, e => e.Object.Type == ADObjectType.User);
        // Côté ordinateurs, seuls les groupes d'ordinateurs : pas Domain Admins
        Assert.DoesNotContain(computers.Entries, e => e.Name == "Domain Admins");
        computers.Entries.First(e => e.Name == "Domain Controllers").IsSelected = true;
        computers.Kinds[1].IsChecked = false;
        Assert.All(computers.Entries, e => Assert.Equal(ADObjectType.Computer, e.Object.Type));
        Assert.Contains(computers.Entries, e => e.Name == "DC01");
        editor.AddComputers(computers.SelectedNames);

        var silo = Assert.Single(editor.Result());
        Assert.Equal(["adm-lmartin", "GG-Tier0-Admins"], silo.Members);
        Assert.Equal(["Domain Controllers"], silo.Computers);
        // Ce qui est dans le silo n'est plus proposé
        Assert.DoesNotContain(editor.PickMembers().Entries, e => e.Name == "adm-lmartin");
        // adm-lmartin n'est pas dans Protected Users : NTLM contournerait le silo
        Assert.Contains("adm-lmartin", editor.ProtectedUsersWarning);

        editor.Selected!.SelectedMember = "adm-lmartin";
        editor.RemoveMemberCommand.Execute(null);
        Assert.Equal(["GG-Tier0-Admins"], editor.Selected.Members);
    }

    [Fact]
    public void Editor_ChecksNames()
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
    }

    [Fact]
    public void SecurityPanel_ShowsTheSiloOfAnAccount_AndTheSessionItDenies()
    {
        var domain = SampleWithSilo(enforced: true);

        Assert.Contains("Tier0", new ObjectSecurityViewModel(Find(domain, "adm-lmartin")).SiloEffect);
        Assert.False(new ObjectSecurityViewModel(Find(domain, "jdoe")).HasSiloEffect);
        Assert.Equal("adm-lmartin (silo Tier0)", new ObjectSecurityViewModel(Find(domain, "SRV-FILE-01")).BlockedSessionsEffect);
        Assert.Contains("Protected Users", new ObjectSecurityViewModel(Find(SampleWithSilo(true, protectedUsers: false), "adm-lmartin")).SiloEffect);
    }
}
