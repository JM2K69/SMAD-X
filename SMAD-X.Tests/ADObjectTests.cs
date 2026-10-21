using SMADX.Models;
using static SMADX.Tests.TestHelpers;

namespace SMADX.Tests;

public class ADObjectTests
{
    [Fact]
    public void UpdateDistinguishedName_BuildsDnFromHierarchy()
    {
        var domain = BuildSmallDomain();

        Assert.Equal("DC=contoso,DC=com", domain.DistinguishedName);
        Assert.Equal("OU=Corp,DC=contoso,DC=com", Find(domain, "Corp").DistinguishedName);
        Assert.Equal("CN=jdoe,OU=Corp,DC=contoso,DC=com", Find(domain, "jdoe").DistinguishedName);
    }

    [Fact]
    public void Clone_CopiesRelationsAndPsoSettings()
    {
        var pso = new ADObject("PSO-Admins", ADObjectType.PasswordSettingsObject)
        {
            SiteName = "Site-Paris",
            PSOPrecedence = 10,
            PSOMinPasswordLength = 15,
            PSOComplexityEnabled = true,
            PSOLockoutThreshold = 5
        };
        pso.MemberOf.Add("Domain Admins");
        pso.LinkedGPOs.Add("GPO-Baseline");
        pso.PSOAppliesTo.Add("Tier0-Admins");
        pso.Delegations.Add(new ADDelegation { TrusteeName = "Helpdesk", Right = "ResetPassword", TargetDN = "OU=Corp" });

        var clone = pso.Clone();

        Assert.NotEqual(pso.Id, clone.Id);
        Assert.Equal(["Domain Admins"], clone.MemberOf);
        Assert.Equal(["GPO-Baseline"], clone.LinkedGPOs);
        Assert.Equal(["Tier0-Admins"], clone.PSOAppliesTo);
        Assert.Equal("Site-Paris", clone.SiteName);
        Assert.Equal(10, clone.PSOPrecedence);
        Assert.Equal(15, clone.PSOMinPasswordLength);
        Assert.True(clone.PSOComplexityEnabled);
        Assert.Equal(5, clone.PSOLockoutThreshold);
        Assert.Equal("ResetPassword", Assert.Single(clone.Delegations).Right);
    }

    [Fact]
    public void Clone_IsIndependentFromSource()
    {
        var user = new ADObject("jdoe", ADObjectType.User);
        user.MemberOf.Add("Helpdesk");
        user.Delegations.Add(new ADDelegation { TrusteeName = "a", TargetDN = "x" });

        var clone = user.Clone();
        clone.MemberOf.Add("Domain Admins");
        clone.Delegations[0].TargetDN = "y";

        Assert.Single(user.MemberOf);
        Assert.Equal("x", user.Delegations[0].TargetDN);
    }

    [Fact]
    public void Clone_CopiesChildrenRecursively()
    {
        var ou = Find(BuildSmallDomain(), "Corp");

        var clone = ou.Clone();

        Assert.Equal(2, clone.Children.Count);
        Assert.All(clone.Children, c => Assert.Same(clone, c.Parent));
    }
}
