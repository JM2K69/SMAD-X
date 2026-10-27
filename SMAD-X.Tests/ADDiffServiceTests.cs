using SMADX.Models;
using SMADX.Services;
using static SMADX.Tests.TestHelpers;

namespace SMADX.Tests;

public class ADDiffServiceTests
{
    private static List<ADChangeItem> Compare(ADObject before, ADObject after) =>
        ADDiffService.Compare(new DomainSnapshot { Root = before }, new DomainSnapshot { Root = after });

    [Fact]
    public void IdenticalTrees_HaveNoChanges()
    {
        Assert.Empty(Compare(BuildSmallDomain(), BuildSmallDomain()));
    }

    [Fact]
    public void DetectsAddedAndRemovedObjects()
    {
        var before = BuildSmallDomain();
        var after = BuildSmallDomain();
        Add(Find(after, "Corp"), new ADObject("asmith", ADObjectType.User));
        var helpdesk = Find(after, "Helpdesk");
        helpdesk.Parent!.Children.Remove(helpdesk);

        var changes = Compare(before, after);

        Assert.Contains(changes, c => c.ChangeType == ChangeType.Added && c.ObjectName == "asmith");
        Assert.Contains(changes, c => c.ChangeType == ChangeType.Removed && c.ObjectName == "Helpdesk");
    }

    [Fact]
    public void DetectsTierAndMembershipChanges()
    {
        var before = BuildSmallDomain();
        var after = BuildSmallDomain();
        Find(after, "Corp").Tier = "Tier 1";
        Find(after, "jdoe").MemberOf.Add("Domain Admins");

        var changes = Compare(before, after);

        var tier = Assert.Single(changes, c => c.ChangeCategory == ChangeCategory.Structure);
        Assert.Contains(tier.ChangedFields, f => f is { Field: "Tier", OldValue: "Tier 2", NewValue: "Tier 1" });
        var member = Assert.Single(changes, c => c.ChangeCategory == ChangeCategory.MemberOf);
        Assert.Equal("jdoe", member.ObjectName);
    }

    [Fact]
    public void DuplicateDelegations_DoNotThrow()
    {
        var before = BuildSmallDomain();
        var after = BuildSmallDomain();
        foreach (var root in new[] { before, after })
        {
            var ou = Find(root, "Corp");
            ou.Delegations.Add(new ADDelegation { TrusteeName = "Helpdesk", Right = "ResetPassword", TargetDN = ou.DistinguishedName });
            ou.Delegations.Add(new ADDelegation { TrusteeName = "Helpdesk", Right = "ResetPassword", TargetDN = ou.DistinguishedName });
        }

        Assert.Empty(Compare(before, after));
    }

    [Fact]
    public void SiblingsWithTheSameDn_DoNotThrow()
    {
        // Un fichier retouché à la main peut contenir deux frères de même nom
        var before = BuildSmallDomain();
        var after = BuildSmallDomain();
        Add(Find(before, "Corp"), new ADObject("jdoe", ADObjectType.User));
        Add(Find(after, "Corp"), new ADObject("jdoe", ADObjectType.User));
        Add(Find(after, "Corp"), new ADObject("jdoe", ADObjectType.User));

        var change = Assert.Single(Compare(before, after));

        Assert.Equal(ChangeType.Added, change.ChangeType);
        Assert.Equal("CN=jdoe,OU=Corp,DC=contoso,DC=com", change.DistinguishedName);
    }

    [Fact]
    public void RenamedObject_IsOneChangeThatKeepsItsFormerDn()
    {
        var before = BuildSmallDomain();
        var after = BuildSmallDomain();
        Find(after, "Helpdesk").Id = Find(before, "Helpdesk").Id;
        Find(after, "Helpdesk").Name = "GG-HelpDesk";
        UpdateDns(after);

        var change = Assert.Single(Compare(before, after));

        Assert.Equal(ChangeType.Modified, change.ChangeType);
        Assert.Equal("CN=Helpdesk,OU=Corp,DC=contoso,DC=com", change.PreviousDistinguishedName);
        Assert.Contains(change.ChangedFields, f => f is { Field: "Name", OldValue: "Helpdesk", NewValue: "GG-HelpDesk" });
    }

    [Fact]
    public void DelegationBecomingDirect_IsReported()
    {
        var before = BuildSmallDomain();
        var after = BuildSmallDomain();
        var dn = Find(before, "Corp").DistinguishedName;
        Find(before, "Corp").Delegations.Add(new ADDelegation { TrusteeName = "Helpdesk", Right = "GenericAll", TargetDN = dn, IsInherited = true });
        Find(after, "Corp").Delegations.Add(new ADDelegation { TrusteeName = "Helpdesk", Right = "GenericAll", TargetDN = dn, IsInherited = false });

        var change = Assert.Single(Compare(before, after));

        Assert.Equal(ChangeCategory.Delegation, change.ChangeCategory);
        Assert.Contains(change.ChangedFields, f => f.Field == "Delegation +");
        Assert.Contains(change.ChangedFields, f => f.Field == "Delegation -");
    }
}
