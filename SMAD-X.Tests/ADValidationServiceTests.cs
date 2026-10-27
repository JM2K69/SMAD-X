using SMADX.Models;
using SMADX.Services;

namespace SMADX.Tests;

public class ADValidationServiceTests
{
    private readonly ADValidationService _service = new();

    [Theory]
    [InlineData("jdoe")]
    [InlineData("Domain Admins")]
    [InlineData("Équipe-RH_2026")]
    public void ValidateName_AcceptsValidNames(string name)
    {
        Assert.True(_service.ValidateName(name, out var error));
        Assert.Null(error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a/b")]
    [InlineData("a,b")]
    [InlineData("a=b")]
    [InlineData("a\"b")]
    [InlineData("a*b")]
    public void ValidateName_RejectsInvalidNames(string name)
    {
        Assert.False(_service.ValidateName(name, out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void ValidateName_RejectsMoreThan64Characters()
    {
        Assert.True(_service.ValidateName(new string('a', 64), out _));
        Assert.False(_service.ValidateName(new string('a', 65), out _));
    }

    /// <summary>Supérieurs possibles du schéma d'AD : où chaque type d'objet peut être créé.</summary>
    [Theory]
    [InlineData("domain", ADObjectType.OrganizationalUnit, true)]
    [InlineData("domain", ADObjectType.Container, true)]
    [InlineData("domain", ADObjectType.User, true)]
    [InlineData("domain", ADObjectType.Policy, false)]          // une GPO va dans CN=Policies,CN=System
    [InlineData("domain", ADObjectType.Domain, false)]
    [InlineData("ou", ADObjectType.OrganizationalUnit, true)]
    [InlineData("ou", ADObjectType.Container, true)]            // AD accepte un conteneur sous une OU
    [InlineData("ou", ADObjectType.User, true)]
    [InlineData("ou", ADObjectType.PasswordSettingsObject, false)]
    [InlineData("users", ADObjectType.User, true)]
    [InlineData("users", ADObjectType.OrganizationalUnit, false)] // jamais d'OU dans un conteneur
    [InlineData("users", ADObjectType.Container, true)]
    [InlineData("users", ADObjectType.PasswordSettingsObject, false)]
    [InlineData("policies", ADObjectType.Policy, true)]
    [InlineData("pso", ADObjectType.PasswordSettingsObject, true)]
    [InlineData("pso", ADObjectType.Policy, false)]
    [InlineData("user", ADObjectType.User, false)]
    [InlineData("group", ADObjectType.User, false)]
    public void ValidateContainerRule_FollowsTheAdSchema(string parent, ADObjectType child, bool expected)
    {
        var domain = new ADDataService().CreateFreshDomainStructure("corp.local");
        var all = TestHelpers.Flatten(domain).ToList();
        var target = parent switch
        {
            "domain" => domain,
            "ou" => all.First(o => o.Type == ADObjectType.OrganizationalUnit),
            "users" => all.First(o => o is { Name: "Users", Type: ADObjectType.Container }),
            "policies" => all.First(ADValidationService.IsPoliciesContainer),
            "pso" => all.First(ADValidationService.IsPasswordSettingsContainer),
            "user" => all.First(o => o.Type == ADObjectType.User),
            _ => all.First(o => o.Type == ADObjectType.Group)
        };

        Assert.Equal(expected, _service.ValidateContainerRule(target, child, out var error));
        Assert.Equal(expected, error == null);
    }

    /// <summary>Portées des groupes (AGDLP) : un global ne contient que du global, un universel refuse le domaine local.</summary>
    [Theory]
    [InlineData(ADGroupScope.Global, ADGroupScope.Global, true)]
    [InlineData(ADGroupScope.DomainLocal, ADGroupScope.Global, false)]
    [InlineData(ADGroupScope.Universal, ADGroupScope.Global, false)]
    [InlineData(ADGroupScope.Global, ADGroupScope.Universal, true)]
    [InlineData(ADGroupScope.Universal, ADGroupScope.Universal, true)]
    [InlineData(ADGroupScope.DomainLocal, ADGroupScope.Universal, false)]
    [InlineData(ADGroupScope.Global, ADGroupScope.DomainLocal, true)]
    [InlineData(ADGroupScope.Universal, ADGroupScope.DomainLocal, true)]
    [InlineData(ADGroupScope.DomainLocal, ADGroupScope.DomainLocal, true)]
    public void ValidateMembership_FollowsGroupScopes(ADGroupScope member, ADGroupScope group, bool expected)
    {
        var inner = new ADObject("inner", ADObjectType.Group) { GroupScope = member };
        var outer = new ADObject("outer", ADObjectType.Group) { GroupScope = group };

        Assert.Equal(expected, _service.ValidateMembership(inner, outer, out var error));
        Assert.Equal(expected, error == null);
    }

    [Fact]
    public void ValidateMembership_AcceptsAccountsAndGmsa_RefusesTheRest()
    {
        var group = new ADObject("g", ADObjectType.Group) { GroupScope = ADGroupScope.Global };

        Assert.True(_service.ValidateMembership(new ADObject("u", ADObjectType.User), group, out _));
        Assert.True(_service.ValidateMembership(new ADObject("gmsa", ADObjectType.GMSA), group, out _));
        Assert.False(_service.ValidateMembership(new ADObject("ou", ADObjectType.OrganizationalUnit), group, out _));
        Assert.False(_service.ValidateMembership(group, group, out _));
        Assert.False(_service.ValidateMembership(new ADObject("u", ADObjectType.User), new ADObject("u2", ADObjectType.User), out _));
    }

    /// <summary>Les domaines par défaut respectent eux-mêmes ces règles.</summary>
    [Fact]
    public void DefaultDomains_RespectPlacementAndNesting()
    {
        var data = new ADDataService();
        foreach (var domain in new[] { data.CreateFreshDomainStructure("corp.local"), data.CreateLabBaseStructure("corp.local"), data.CreateSampleStructure() })
        {
            var all = TestHelpers.Flatten(domain).ToList();
            foreach (var o in all.Where(o => o.Parent != null))
                Assert.True(_service.ValidateContainerRule(o.Parent!, o.Type, out var error), $"{o.DistinguishedName} : {error}");
            foreach (var o in all)
                foreach (var name in o.MemberOf)
                    if (all.FirstOrDefault(g => g.Type == ADObjectType.Group && g.Name == name) is { } group)
                        Assert.True(_service.ValidateMembership(o, group, out var error), $"{o.Name} → {name} : {error}");
        }
    }
}
