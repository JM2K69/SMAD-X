using SMADX.Models;
using SMADX.Services;
using SMADX.Services.AttackPaths;
using SMADX.Services.Audit;
using SMADX.Services.Tiering;
using SMADX.ViewModels;
using static SMADX.Tests.TestHelpers;

namespace SMADX.Tests;

/// <summary>
/// Effets des GPO sur les chemins d'attaque : refus d'ouverture de session, administrateurs locaux, filtre WMI, priorité
/// des GPO. Le scénario type : le support est administrateur local des postes, un administrateur du domaine ouvre une
/// session sur l'un d'eux ; le cloisonnement doit couper ce chemin.
/// </summary>
public class GroupPolicyEffectsTests
{
    private static readonly DateTime Now = new(2026, 11, 2);

    public GroupPolicyEffectsTests() => TierConfigurationService.Instance.ResetToDefaults();

    /// <summary>Modèle, OU des postes, des comptes du support, des administrateurs du Tier 0, groupe d'administration des postes, GPO qui refuse le Tier 0.</summary>
    public static TheoryData<string, string, string, string, string, string> Scenarios => new()
    {
        { "microsoft", "Tier 2 End-User Devices", "Tier 2 End-User Accounts", "Tier Model Administration/Tier 0/Tier 0 Accounts", "Tier2LocalDeviceOperators", "Tier 2 EUD Account Restrictions" },
        { "hardenad", "Harden_T12/Workstations", "Harden_T12/Users", "_Administration/UsersT0", "L-S-T2_LocalAdmins_Workstations", "HAD-LoginRestrictions-T2" }
    };

    private static (ADObject Domain, ADObject Helpdesk, ADObject Admin, ADObject Pc) Tiered(
        string model, string workstations, string users, string admins, string localAdmins)
    {
        var domain = new ADDataService().CreateFreshDomainStructure("corp.local");
        TierModelService.Apply(domain, TierModelCatalog.Find(model)!, Now);
        var helpdesk = Add(TierModelService.Ou(domain, users)!, new ADObject("helpdesk", ADObjectType.User) { Tier = "Tier 2" });
        helpdesk.MemberOf.Add(localAdmins);
        var admin = Add(TierModelService.Ou(domain, admins)!, new ADObject("adm-paul", ADObjectType.User) { Tier = "Tier 0" });
        admin.MemberOf.Add("Domain Admins");
        // Un poste joint au domaine : Domain Admins dans ses administrateurs locaux ; l'administrateur y a ouvert une session
        var pc = Add(TierModelService.Ou(domain, workstations)!, new ADObject("PC-042", ADObjectType.Computer)
        {
            Tier = "Tier 2", LocalAdmins = ["Domain Admins"], Sessions = ["adm-paul"]
        });
        return (domain, helpdesk, admin, pc);
    }

    private static List<AuditFinding> Findings(ADObject domain, string rule) =>
        new ADSecurityAuditService().Run(domain, Now).Findings.Where(f => f.Rule.Id == rule).ToList();

    [Theory]
    [MemberData(nameof(Scenarios))]
    public void TierModel_DeniesTheAdminSession_AndCutsThePathThroughTheWorkstation(
        string model, string workstations, string users, string admins, string localAdmins, string gpoName)
    {
        var (domain, helpdesk, admin, pc) = Tiered(model, workstations, users, admins, localAdmins);
        var service = new AttackPathService(domain, Now);

        Assert.Null(service.PathFrom(helpdesk));
        var blocked = Assert.Single(service.BlockedSessions);
        Assert.Equal((admin, pc), (blocked.Account, blocked.Computer));
        Assert.Contains(blocked.Gpos, g => g.Name == gpoName);
        Assert.Empty(Findings(domain, "TIER-04"));
    }

    [Theory]
    [MemberData(nameof(Scenarios))]
    public void WithoutTheLocalLogonDenial_TheSessionExposesTheAdmin(
        string model, string workstations, string users, string admins, string localAdmins, string gpoName)
    {
        var (domain, helpdesk, admin, _) = Tiered(model, workstations, users, admins, localAdmins);
        // Le Bureau à distance reste refusé, la session locale non : l'administrateur peut ouvrir une session
        Find(domain, gpoName).GpoDenyInteractiveLogon = null;
        var service = new AttackPathService(domain, Now);

        var path = service.PathFrom(helpdesk);
        Assert.NotNull(path);
        Assert.Equal([AttackEdgeKind.MemberOf, AttackEdgeKind.AdminTo, AttackEdgeKind.HasSession, AttackEdgeKind.MemberOf, AttackEdgeKind.PrivilegedGroup],
            path.Steps.Select(s => s.Kind));
        Assert.Contains("GPO", path.Steps[1].Detail);
        Assert.Empty(service.BlockedSessions);
        Assert.Equal(["adm-paul"], Findings(domain, "TIER-04").Select(f => f.Object.Name));
    }

    [Fact]
    public void Microsoft_RestrictedGroups_ReplaceTheLocalAdministrators()
    {
        var (domain, _, _, pc) = Tiered("microsoft", "Tier 2 End-User Devices", "Tier 2 End-User Accounts",
            "Tier Model Administration/Tier 0/Tier 0 Accounts", "Tier2LocalDeviceOperators");
        var admins = new AuditContext(domain, Now).Policies.LocalAdministrators(pc);

        var admin = Assert.Single(admins);
        Assert.Equal(("Tier2LocalDeviceOperators", "Tier 2 EUD Account Restrictions"), (admin.Name, admin.Gpo!.Name));
    }

    [Fact]
    public void HardenAd_Preferences_AddLocalAdministrators_AndWmiFiltersSeparateServersFromWorkstations()
    {
        var (domain, _, _, pc) = Tiered("hardenad", "Harden_T12/Workstations", "Harden_T12/Users", "_Administration/UsersT0", "L-S-T2_LocalAdmins_Workstations");
        var server = Add(TierModelService.Ou(domain, "Harden_T12/Servers")!, new ADObject("SRV-01", ADObjectType.Computer) { Tier = "Tier 1", Sessions = ["adm-t1"] });
        var t1Admin = Add(TierModelService.Ou(domain, "_Administration/UsersT1")!, new ADObject("adm-t1", ADObjectType.User) { Tier = "Tier 1" });
        t1Admin.MemberOf.Add("G-S-T1_Administrators");
        pc.Sessions!.Add("adm-t1");
        var policies = new AuditContext(domain, Now).Policies;

        Assert.Equal(["Domain Admins", "L-S-T2_LocalAdmins_Workstations"], policies.LocalAdministrators(pc).Select(a => a.Name));
        Assert.Contains(policies.Applied(server), g => g.Name == "HAD-LoginRestrictions-T1");
        Assert.DoesNotContain(policies.Applied(server), g => g.Name == "HAD-LoginRestrictions-T2");
        Assert.Contains(policies.LocalAdministrators(server), a => a.Name == "L-S-T1_LocalAdmins_Servers");
        // Un administrateur du Tier 1 ouvre une session sur ses serveurs, pas sur les postes du Tier 2
        Assert.Empty(policies.LogonDeniedBy(Find(domain, "adm-t1"), server));
        Assert.Equal(["HAD-LoginRestrictions-T2"], policies.LogonDeniedBy(Find(domain, "adm-t1"), pc).Select(g => g.Name));
    }

    /// <summary>Domaine neuf avec OU=Postes/OU=Compta, et une GPO par OU.</summary>
    private static (ADObject Domain, ADObject Postes, ADObject Compta) TwoLevels()
    {
        var domain = new ADDataService().CreateFreshDomainStructure("corp.local");
        var postes = Add(domain, new ADObject("Postes", ADObjectType.OrganizationalUnit));
        var compta = Add(postes, new ADObject("Compta", ADObjectType.OrganizationalUnit));
        var policies = Flatten(domain).First(ADValidationService.IsPoliciesContainer);
        Add(policies, new ADObject("Refus Tier 0", ADObjectType.Policy) { GpoDenyInteractiveLogon = ["Domain Admins"], GpoDenyRemoteInteractiveLogon = ["Domain Admins"] });
        Add(policies, new ADObject("Refus invités", ADObjectType.Policy) { GpoDenyInteractiveLogon = ["Guests"], GpoDenyRemoteInteractiveLogon = ["Guests"] });
        postes.LinkedGPOs.Add("Refus Tier 0");
        compta.LinkedGPOs.Add("Refus invités");
        return (domain, postes, compta);
    }

    [Fact]
    public void TheGpoLinkedClosestToTheComputer_Wins()
    {
        var (domain, postes, compta) = TwoLevels();
        var pc1 = Add(postes, new ADObject("PC-1", ADObjectType.Computer));
        var pc2 = Add(compta, new ADObject("PC-2", ADObjectType.Computer));
        var policies = new AuditContext(domain, Now).Policies;
        var administrator = Find(domain, "Administrator");

        Assert.Equal(["Refus Tier 0"], policies.LogonDeniedBy(administrator, pc1).Select(g => g.Name));
        // La GPO de l'OU Compta définit les mêmes droits : elle remplace la liste de l'OU parente
        Assert.Empty(policies.LogonDeniedBy(administrator, pc2));
        // De l'OU de l'ordinateur jusqu'à la racine du domaine (Default Domain Policy)
        Assert.Equal(["Refus invités", "Refus Tier 0", "Default Domain Policy"], policies.Applied(pc2).Select(g => g.Name));
    }

    [Fact]
    public void AWmiFilter_SkipsAComputerOfUnknownType()
    {
        var (domain, postes, _) = TwoLevels();
        Find(domain, "Refus Tier 0").GpoComputerFilter = GpoComputerFilter.Workstations;
        var unknown = Add(postes, new ADObject("PC-1", ADObjectType.Computer));
        var workstation = Add(postes, new ADObject("PC-2", ADObjectType.Computer) { Tier = "Tier 2" });
        var policies = new AuditContext(domain, Now).Policies;

        Assert.DoesNotContain(policies.Applied(unknown), g => g.Name == "Refus Tier 0");
        Assert.Contains(policies.Applied(workstation), g => g.Name == "Refus Tier 0");
    }

    [Fact]
    public void Tier05_ReportsAModelGpoSettingThatWasRemoved()
    {
        var domain = new ADDataService().CreateFreshDomainStructure("corp.local");
        TierModelService.Apply(domain, TierModelCatalog.Find("hardenad")!, Now);
        var gpo = Find(domain, "HAD-LoginRestrictions-T2");
        gpo.GpoDenyInteractiveLogon!.Remove("Domain Admins");

        var finding = Assert.Single(Findings(domain, "TIER-05"));
        Assert.Same(gpo, finding.Object);
        Assert.Contains("Domain Admins", finding.Detail);
    }

    [Fact]
    public void GpoSettings_AreSavedWithTheDocument_AndCopied()
    {
        var gpo = new ADObject("Refus", ADObjectType.Policy)
        {
            GpoDenyInteractiveLogon = ["Domain Admins"], GpoLocalAdmins = ["Support"], GpoReplaceLocalAdmins = true,
            GpoComputerFilter = GpoComputerFilter.Servers
        };
        var json = System.Text.Json.JsonSerializer.Serialize(gpo, ADDataService.SerializerOptions);
        var back = System.Text.Json.JsonSerializer.Deserialize<ADObject>(json, ADDataService.SerializerOptions)!;
        var copy = gpo.Clone();

        Assert.Matches("\"GpoComputerFilter\":\\s*\"Servers\"", json);
        Assert.DoesNotContain("GpoDenyRemoteInteractiveLogon", json);
        foreach (var o in new[] { back, copy })
        {
            Assert.Equal(["Domain Admins"], o.GpoDenyInteractiveLogon!);
            Assert.Equal(["Support"], o.GpoLocalAdmins!);
            Assert.True(o.GpoReplaceLocalAdmins);
            Assert.Equal(GpoComputerFilter.Servers, o.GpoComputerFilter);
        }
        Assert.DoesNotContain("\"Gpo", System.Text.Json.JsonSerializer.Serialize(new ADObject("PC", ADObjectType.Computer), ADDataService.SerializerOptions));
    }

    [Fact]
    public void SecurityPanel_EditsGpoSettings_AndShowsTheirEffectOnAComputer()
    {
        var (domain, postes, _) = TwoLevels();
        var gpo = Find(domain, "Refus Tier 0");
        var pc = Add(postes, new ADObject("PC-1", ADObjectType.Computer) { Sessions = ["Administrator"] });
        var gpoPanel = new ObjectSecurityViewModel(gpo);

        Assert.True(gpoPanel.IsPolicy && gpoPanel.HasSecurityAttributes && !gpoPanel.HasAdminCount);
        gpoPanel.GpoLocalAdminsText = "Support\n support ";
        gpoPanel.GpoComputerFilterChoice = "Workstations";
        gpoPanel.GpoDenyRemoteInteractiveLogonText = "  ";
        Assert.Equal(["Support"], gpo.GpoLocalAdmins!);
        Assert.Equal(GpoComputerFilter.Workstations, gpo.GpoComputerFilter);
        Assert.Null(gpo.GpoDenyRemoteInteractiveLogon);

        gpoPanel.GpoComputerFilterChoice = string.Empty;
        gpoPanel.GpoDenyRemoteInteractiveLogonText = "Domain Admins";
        var pcPanel = new ObjectSecurityViewModel(pc);
        Assert.Equal("Support (GPO Refus Tier 0)", pcPanel.GpoLocalAdminsEffect);
        Assert.Equal("Administrator (GPO Refus Tier 0)", pcPanel.BlockedSessionsEffect);
    }
}
