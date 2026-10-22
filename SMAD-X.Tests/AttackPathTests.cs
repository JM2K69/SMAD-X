using SMADX.Models;
using SMADX.Services;
using SMADX.Services.AttackPaths;
using SMADX.ViewModels;
using static SMADX.Tests.TestHelpers;

namespace SMADX.Tests;

public class AttackPathServiceTests
{
    private static readonly DateTime Now = new(2026, 10, 22);

    public AttackPathServiceTests() => TierConfigurationService.Instance.ResetToDefaults();

    /// <summary>Domaine minimal : Users (Domain Admins, Domain Users), un DC, une OU Corp avec jdoe et un groupe Helpdesk.</summary>
    private static ADObject Domain()
    {
        var domain = new ADObject("contoso.com", ADObjectType.Domain) { Tier = "Tier 0" };
        var users = Add(domain, new ADObject("Users", ADObjectType.Container));
        Add(users, new ADObject("Domain Admins", ADObjectType.Group) { Tier = "Tier 0", AdminCount = true, Owner = "Domain Admins" });
        Add(users, new ADObject("Domain Users", ADObjectType.Group) { Owner = "Domain Admins" });
        var dcOu = Add(domain, new ADObject("Domain Controllers", ADObjectType.OrganizationalUnit) { Tier = "Tier 0" });
        Add(dcOu, new ADObject("DC01", ADObjectType.Computer) { Tier = "Tier 0", AccountFlags = ADAccountFlags.TrustedForDelegation });
        var corp = Add(domain, new ADObject("Corp", ADObjectType.OrganizationalUnit) { Tier = "Tier 2" });
        Add(corp, new ADObject("jdoe", ADObjectType.User) { Tier = "Tier 2" });
        Add(corp, new ADObject("Helpdesk", ADObjectType.Group) { Tier = "Tier 2" });
        return domain;
    }

    private static IReadOnlyList<AttackPath> Paths(ADObject domain) => new AttackPathService(domain, Now).PathsToDomainControl();

    private static AttackPath? PathFrom(ADObject domain, string name) => new AttackPathService(domain, Now).PathFrom(Find(domain, name));

    private static AttackPath? AnyUserPath(ADObject domain) => Paths(domain).SingleOrDefault(p => p.Start.IsAnyUser);

    [Fact]
    public void CleanDomain_HasNoPathFromAnyUser()
    {
        var domain = Domain();

        Assert.Null(AnyUserPath(domain));
        Assert.Null(PathFrom(domain, "jdoe"));
        Assert.Null(PathFrom(domain, "Helpdesk"));
    }

    [Fact]
    public void DomainAdminMember_ReachesDomainInTwoSteps()
    {
        var domain = Domain();
        var admin = Add(Find(domain, "Corp"), new ADObject("adm-alice", ADObjectType.User) { Tier = "Tier 0", AdminCount = true });
        admin.MemberOf.Add("Domain Admins");

        var path = PathFrom(domain, "adm-alice")!;

        Assert.Equal([AttackEdgeKind.MemberOf, AttackEdgeKind.PrivilegedGroup], path.Steps.Select(s => s.Kind));
        Assert.Same(domain, path.Steps[^1].To.Object);
    }

    [Fact]
    public void KerberoastableAccountInNestedGroup_IsReachableFromAnyUser()
    {
        var domain = Domain();
        var sqlAdmins = Add(Find(domain, "Corp"), new ADObject("SQL Admins", ADObjectType.Group));
        sqlAdmins.MemberOf.Add("Domain Admins");
        var svc = Add(Find(domain, "Corp"), new ADObject("svc-sql", ADObjectType.User));
        svc.ServicePrincipalNames.Add("MSSQLSvc/sql01.contoso.com:1433");
        svc.MemberOf.Add("SQL Admins");

        var path = AnyUserPath(domain)!;

        Assert.Equal(
            [AttackEdgeKind.Kerberoasting, AttackEdgeKind.MemberOf, AttackEdgeKind.MemberOf, AttackEdgeKind.PrivilegedGroup],
            path.Steps.Select(s => s.Kind));
        Assert.Equal(["svc-sql", "SQL Admins", "Domain Admins", "contoso.com"], path.Steps.Select(s => s.To.Label));
        // Les chemins depuis « tout utilisateur » passent en tête de liste
        Assert.True(Paths(domain)[0].Start.IsAnyUser);
    }

    [Fact]
    public void DisabledKerberoastableAccount_IsNotAnEntryPoint()
    {
        var domain = Domain();
        var svc = Add(Find(domain, "Corp"), new ADObject("svc-old", ADObjectType.User) { AccountFlags = ADAccountFlags.AccountDisabled });
        svc.ServicePrincipalNames.Add("HTTP/old.contoso.com");
        svc.MemberOf.Add("Domain Admins");

        Assert.Null(AnyUserPath(domain));
    }

    [Theory]
    [InlineData(ADAccountFlags.DontRequirePreauth, AttackEdgeKind.AsRepRoasting)]
    [InlineData(ADAccountFlags.PasswordNotRequired, AttackEdgeKind.EmptyPassword)]
    public void WeakPrivilegedAccount_IsReachableFromAnyUser(ADAccountFlags flag, AttackEdgeKind kind)
    {
        var domain = Domain();
        var admin = Add(Find(domain, "Corp"), new ADObject("adm-bob", ADObjectType.User) { AccountFlags = flag });
        admin.MemberOf.Add("Domain Admins");

        Assert.Equal(kind, AnyUserPath(domain)!.Steps[0].Kind);
    }

    [Fact]
    public void PasswordResetDelegation_ReachesUnprotectedAdminOnly()
    {
        var domain = Domain();
        var corp = Find(domain, "Corp");
        corp.Delegations.Add(new ADDelegation { TrusteeName = "Helpdesk", Right = "Reset Password", RightCategory = RightCategory.PasswordReset });
        var admin = Add(corp, new ADObject("adm-carol", ADObjectType.User) { AdminCount = true });
        admin.MemberOf.Add("Domain Admins");

        // adminCount = 1 : AdminSDHolder coupe l'héritage, la délégation de l'OU ne s'applique pas
        Assert.Null(PathFrom(domain, "Helpdesk"));

        admin.AdminCount = false;
        var path = PathFrom(domain, "Helpdesk")!;
        Assert.Equal(AttackEdgeKind.ResetPassword, path.Steps[0].Kind);
        Assert.Same(admin, path.Steps[0].To.Object);
    }

    [Fact]
    public void WriteMembersDelegation_OnGroupContainer_ReachesPrivilegedGroup()
    {
        var domain = Domain();
        var corp = Find(domain, "Corp");
        var ops = Add(corp, new ADObject("Ops Admins", ADObjectType.Group));
        ops.MemberOf.Add("Domain Admins");
        corp.Delegations.Add(new ADDelegation { TrusteeName = "jdoe", Right = "WriteProperty:member", RightCategory = RightCategory.AttributeWrite });

        var path = PathFrom(domain, "jdoe")!;

        Assert.Equal(AttackEdgeKind.WriteMembers, path.Steps[0].Kind);
        Assert.Same(ops, path.Steps[0].To.Object);
    }

    [Fact]
    public void FullControlForDomainUsers_StartsFromAnyUser()
    {
        var domain = Domain();
        var corp = Find(domain, "Corp");
        var admin = Add(corp, new ADObject("adm-dave", ADObjectType.User));
        admin.MemberOf.Add("Domain Admins");
        corp.Delegations.Add(new ADDelegation { TrusteeName = "Domain Users", Right = "GenericAll", RightCategory = RightCategory.FullControl });

        var path = AnyUserPath(domain)!;

        Assert.Equal(AttackEdgeKind.FullControl, path.Steps[0].Kind);
        Assert.Same(admin, path.Steps[0].To.Object);
    }

    [Fact]
    public void InheritedDelegation_IsNotCountedTwice()
    {
        var domain = Domain();
        var corp = Find(domain, "Corp");
        var admin = Add(corp, new ADObject("adm-erin", ADObjectType.User));
        admin.MemberOf.Add("Domain Admins");
        admin.Delegations.Add(new ADDelegation { TrusteeName = "jdoe", Right = "GenericAll", RightCategory = RightCategory.FullControl, IsInherited = true });

        Assert.Null(PathFrom(domain, "jdoe"));
    }

    [Fact]
    public void Owner_ControlsTheObject()
    {
        var domain = Domain();
        Find(domain, "Domain Admins").Owner = "jdoe";

        var path = PathFrom(domain, "jdoe")!;

        Assert.Equal([AttackEdgeKind.Owner, AttackEdgeKind.PrivilegedGroup], path.Steps.Select(s => s.Kind));
    }

    [Fact]
    public void UnconstrainedDelegationOnServer_LeadsToDomain_ButNotOnDc()
    {
        var domain = Domain();
        var srv = Add(Find(domain, "Corp"), new ADObject("SRV01", ADObjectType.Computer) { AccountFlags = ADAccountFlags.TrustedForDelegation });

        var path = PathFrom(domain, "SRV01")!;

        Assert.Equal(AttackEdgeKind.UnconstrainedDelegation, Assert.Single(path.Steps).Kind);
        Assert.DoesNotContain(new AttackPathService(domain, Now).Edges, e => e.Kind == AttackEdgeKind.UnconstrainedDelegation && e.From.Label == "DC01");
        Assert.NotNull(srv);
    }

    [Theory]
    [InlineData("ldap/DC01.contoso.com")]
    [InlineData("cifs/DC01")]
    [InlineData("HOST/dc01.contoso.com:445")]
    public void ConstrainedDelegationToDcService_LeadsThroughTheDc(string spn)
    {
        var domain = Domain();
        var web = Add(Find(domain, "Corp"), new ADObject("WEB01", ADObjectType.Computer));
        web.AllowedToDelegateTo.Add(spn);

        var path = PathFrom(domain, "WEB01")!;

        Assert.Equal([AttackEdgeKind.ConstrainedDelegationToDc, AttackEdgeKind.DomainController], path.Steps.Select(s => s.Kind));
    }

    [Fact]
    public void ConstrainedDelegationToMemberServer_IsNotAPath()
    {
        var domain = Domain();
        var web = Add(Find(domain, "Corp"), new ADObject("WEB01", ADObjectType.Computer));
        web.AllowedToDelegateTo.Add("http/intranet.contoso.com");

        Assert.Null(PathFrom(domain, "WEB01"));
    }

    [Fact]
    public void GpoControlledByUser_ReachesAccountsOfLinkedOu()
    {
        var domain = Domain();
        var adminsOu = Add(domain, new ADObject("Admins", ADObjectType.OrganizationalUnit));
        var admin = Add(adminsOu, new ADObject("adm-frank", ADObjectType.User));
        admin.MemberOf.Add("Domain Admins");
        Add(Find(domain, "Users"), new ADObject("Admin Logon", ADObjectType.Policy) { Owner = "jdoe" });
        adminsOu.LinkedGPOs.Add("Admin Logon");

        var path = PathFrom(domain, "jdoe")!;

        Assert.Equal([AttackEdgeKind.Owner, AttackEdgeKind.GpoLink, AttackEdgeKind.MemberOf, AttackEdgeKind.PrivilegedGroup], path.Steps.Select(s => s.Kind));
    }

    [Fact]
    public void GroupLoop_DoesNotHangAndPathsAreShortest()
    {
        var domain = Domain();
        var corp = Find(domain, "Corp");
        var a = Add(corp, new ADObject("GroupA", ADObjectType.Group));
        var b = Add(corp, new ADObject("GroupB", ADObjectType.Group));
        a.MemberOf.Add("GroupB");
        b.MemberOf.Add("GroupA");
        b.MemberOf.Add("Domain Admins");
        Find(domain, "jdoe").MemberOf.Add("GroupA");

        var path = PathFrom(domain, "jdoe")!;

        Assert.Equal(["GroupA", "GroupB", "Domain Admins", "contoso.com"], path.Steps.Select(s => s.To.Label));
    }

    [Fact]
    public void SampleDomain_HasPathsAndNoneCrash()
    {
        var domain = new ADDataService().CreateFreshDomainStructure("corp.local");

        var paths = Paths(domain);

        Assert.NotEmpty(paths);
        Assert.All(paths, p => Assert.Same(domain, p.Steps[^1].To.Object));
    }
}

public class AttackPathsViewModelTests
{
    public AttackPathsViewModelTests() => TierConfigurationService.Instance.ResetToDefaults();

    private static ADObject Domain()
    {
        var domain = new ADObject("contoso.com", ADObjectType.Domain) { Tier = "Tier 0" };
        var users = Add(domain, new ADObject("Users", ADObjectType.Container));
        Add(users, new ADObject("Domain Admins", ADObjectType.Group) { Tier = "Tier 0", AdminCount = true });
        var corp = Add(domain, new ADObject("Corp", ADObjectType.OrganizationalUnit) { Tier = "Tier 2" });
        var admin = Add(corp, new ADObject("adm-alice", ADObjectType.User) { Tier = "Tier 0", AdminCount = true });
        admin.MemberOf.Add("Domain Admins");
        var svc = Add(corp, new ADObject("svc-sql", ADObjectType.User) { Tier = "Tier 2" });
        svc.ServicePrincipalNames.Add("MSSQLSvc/sql01");
        svc.MemberOf.Add("Domain Admins");
        return domain;
    }

    [Fact]
    public void Tier0Sources_AreHiddenUntilAsked()
    {
        var domain = Domain();
        var vm = new AttackPathsViewModel(() => domain, _ => { }, new FakeDialogService());

        Assert.DoesNotContain(vm.Paths, p => p.IsTier0);
        Assert.Contains(vm.Paths, p => p.Label == "svc-sql");
        Assert.True(vm.Paths[0].Path.Start.IsAnyUser);
        Assert.NotEmpty(vm.Steps); // la première source est sélectionnée

        vm.ShowTier0 = true;

        Assert.Contains(vm.Paths, p => p.Label == "Domain Admins");
        Assert.Contains(vm.Paths, p => p.Label == "adm-alice");
    }

    [Fact]
    public void SelectingPath_ListsNumberedSteps()
    {
        var domain = Domain();
        var vm = new AttackPathsViewModel(() => domain, _ => { }, new FakeDialogService());

        vm.SelectedPath = vm.Paths.Single(p => p.Path.Start.IsAnyUser);

        Assert.Equal([1, 2, 3], vm.Steps.Select(s => s.Number));
        Assert.Equal("svc-sql", vm.Steps[0].To);
        Assert.All(vm.Steps, s => Assert.False(string.IsNullOrWhiteSpace(s.KindLabel)));
        Assert.DoesNotContain(vm.Steps, s => s.KindLabel.StartsWith("AttackPath."));
    }

    [Fact]
    public void CleanDomain_SaysThereIsNoPath()
    {
        var domain = BuildSmallDomain();
        var vm = new AttackPathsViewModel(() => domain, _ => { }, new FakeDialogService());

        Assert.True(vm.HasNoPath);
        Assert.False(vm.HasSelection);
        Assert.False(vm.ShowInTreeCommand.CanExecute(null));
        Assert.False(vm.ShowInGraphCommand.CanExecute(null));
    }

    [Fact]
    public void ShowInTree_SelectsStepTargetOrSource()
    {
        var domain = Domain();
        ADObject? shown = null;
        var vm = new AttackPathsViewModel(() => domain, o => shown = o, new FakeDialogService());
        vm.SelectedPath = vm.Paths.Single(p => p.Label == "svc-sql");

        vm.ShowInTreeCommand.Execute(null);
        Assert.Same(Find(domain, "svc-sql"), shown);

        vm.SelectedStep = vm.Steps[0];
        vm.ShowInTreeCommand.Execute(null);
        Assert.Same(Find(domain, "Domain Admins"), shown);
    }

    [Fact]
    public void ShowInGraph_HighlightsThePathObjects()
    {
        var domain = Domain();
        var dialogs = new FakeDialogService();
        var vm = new AttackPathsViewModel(() => domain, _ => { }, dialogs);
        vm.SelectedPath = vm.Paths.Single(p => p.Path.Start.IsAnyUser);

        vm.ShowInGraphCommand.Execute(null);

        // « Tout utilisateur » n'est pas un objet du graphe : le chemin surligné commence au compte de service
        Assert.Equal(
            new[] { "svc-sql", "Domain Admins", "contoso.com" }.Select(n => Find(domain, n).Id.ToString()),
            dialogs.LastGraph!.HighlightedPath);
    }
}
