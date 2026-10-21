using SMADX.Models;
using SMADX.Services;
using static SMADX.Tests.TestHelpers;

namespace SMADX.Tests;

public class ADDataServiceTests
{
    private readonly ADDataService _service = new();

    public ADDataServiceTests() => TierConfigurationService.Instance.ResetToDefaults();

    [Theory]
    [InlineData("Builtin")]
    [InlineData("Users")]
    [InlineData("Computers")]
    [InlineData("Domain Controllers")]
    [InlineData("Domain Admins")]
    [InlineData("Enterprise Admins")]
    [InlineData("Protected Users")]
    [InlineData("krbtgt")]
    [InlineData("DC01")]
    public void FreshDomain_ContainsDefaultObjects(string name)
    {
        var root = _service.CreateFreshDomainStructure("corp.local");

        Assert.Contains(Flatten(root), o => o.Name == name);
    }

    [Fact]
    public void FreshDomain_ComputesDistinguishedNames()
    {
        var root = _service.CreateFreshDomainStructure("corp.local");

        Assert.Equal("DC=corp,DC=local", root.DistinguishedName);
        // « Domain Controllers » existe aussi comme groupe dans CN=Users : on cible l'OU
        var dcOu = Flatten(root).Single(o => o is { Name: "Domain Controllers", Type: ADObjectType.OrganizationalUnit });
        Assert.Equal("OU=Domain Controllers,DC=corp,DC=local", dcOu.DistinguishedName);
        Assert.Equal("CN=krbtgt,CN=Users,DC=corp,DC=local", Find(root, "krbtgt").DistinguishedName);
    }

    [Theory]
    [InlineData("Administrators")]
    [InlineData("Server Operators")]
    [InlineData("Account Operators")]
    [InlineData("Backup Operators")]
    [InlineData("Domain Admins")]
    [InlineData("DC01")]
    public void FreshDomain_PrivilegedObjectsAreTier0(string name)
    {
        var root = _service.CreateFreshDomainStructure("corp.local", enableTiering: true);

        Assert.Equal("Tier 0", Find(root, name).Tier);
    }

    [Fact]
    public void FreshTopology_HasOnlyDefaultSiteWithDc01()
    {
        var topology = _service.CreateFreshTopology("corp.local");

        var site = Assert.Single(topology.Sites);
        Assert.Equal("Default-First-Site-Name", site.Name);
        Assert.Equal(["DC01.corp.local"], site.DomainControllers);
        Assert.Equal("DEFAULTIPSITELINK", Assert.Single(topology.SiteLinks).Name);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTripsDocument()
    {
        var root = BuildSmallDomain();
        Find(root, "jdoe").MemberOf.Add("Helpdesk");
        Find(root, "Corp").LinkedGPOs.Add("GPO-Workstations");
        Find(root, "Corp").Delegations.Add(new ADDelegation
        {
            TrusteeName = "Helpdesk",
            TrusteeType = TrusteeType.Group,
            TargetDN = "OU=Corp,DC=contoso,DC=com",
            Right = "ResetPassword",
            RightCategory = RightCategory.PasswordReset
        });
        var document = new ADRootDocument { Domain = root, SitesTopology = _service.CreateFreshTopology("contoso.com") };
        var path = TempFile(".smad-x.json");

        Assert.True(await _service.SaveDocumentAsync(document, path));
        var loaded = await _service.LoadDocumentAsync(path);

        Assert.NotNull(loaded);
        Assert.Equal(2, loaded.Version);
        var jdoe = Find(loaded.Domain, "jdoe");
        Assert.Equal(["Helpdesk"], jdoe.MemberOf);
        Assert.Same(Find(loaded.Domain, "Corp"), jdoe.Parent);
        Assert.Equal(["GPO-Workstations"], Find(loaded.Domain, "Corp").LinkedGPOs);
        Assert.Equal(RightCategory.PasswordReset, Assert.Single(Find(loaded.Domain, "Corp").Delegations).RightCategory);
        Assert.Equal("Default-First-Site-Name", Assert.Single(loaded.SitesTopology!.Sites).Name);
    }

    [Fact]
    public async Task Load_RestoresCustomTiers()
    {
        TierConfigurationService.Instance.AddTier(new TierConfiguration("Tier Cloud", "#123456", "Azure", 3));
        var path = TempFile(".smad-x.json");
        await _service.SaveDocumentAsync(new ADRootDocument { Domain = BuildSmallDomain() }, path);
        TierConfigurationService.Instance.ResetToDefaults();

        await _service.LoadDocumentAsync(path);

        Assert.Equal("#123456", TierConfigurationService.Instance.GetTierColor("Tier Cloud"));
    }

    [Fact]
    public async Task LoadV1_ResetsCustomTiers()
    {
        var path = TempFile(".json");
        await _service.SaveToFileAsync(BuildSmallDomain(), path);
        TierConfigurationService.Instance.AddTier(new TierConfiguration("Tier Cloud", "#123456", "Azure", 3));

        var loaded = await _service.LoadDocumentAsync(path);

        Assert.Equal(1, loaded!.Version);
        Assert.Equal(3, TierConfigurationService.Instance.Tiers.Count);
    }

    [Fact]
    public async Task Load_MissingFile_ReturnsNull()
    {
        Assert.Null(await _service.LoadDocumentAsync(TempFile(".json")));
    }
}
