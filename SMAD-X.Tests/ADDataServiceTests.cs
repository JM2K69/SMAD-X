using System.Text.RegularExpressions;
using SMADX.Models;
using SMADX.Services;
using static SMADX.Tests.TestHelpers;

namespace SMADX.Tests;

public class ADDataServiceTests
{
    private readonly ADDataService _service = new();

    public ADDataServiceTests() => TierConfigurationService.Instance.ResetToDefaults();

    public static TheoryData<string> DefaultDomains() => new() { "fresh", "lab", "sample" };

    private ADObject Domain(string kind) => kind switch
    {
        "fresh" => _service.CreateFreshDomainStructure("corp.local"),
        "lab" => _service.CreateLabBaseStructure("corp.local"),
        _ => _service.CreateSampleStructure()
    };

    /// <summary>Comme dans AD, un nom de compte (utilisateur, groupe, ordinateur, gMSA) est unique dans le domaine.</summary>
    [Theory]
    [MemberData(nameof(DefaultDomains))]
    public void DefaultDomains_HaveUniqueAccountNames(string kind)
    {
        var duplicates = Flatten(Domain(kind))
            .Where(o => o.Type is ADObjectType.User or ADObjectType.Group or ADObjectType.Computer or ADObjectType.GMSA)
            .GroupBy(o => o.Name, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);

        Assert.Empty(duplicates);
    }

    [Theory]
    [MemberData(nameof(DefaultDomains))]
    public void DefaultDomains_HaveTheBuiltInObjectsOfARealDomain(string kind)
    {
        var domain = Domain(kind);
        var builtin = Flatten(domain).Single(o => o is { Name: "Builtin", Type: ADObjectType.Container });

        Assert.Contains(builtin.Children, g => g.Name == "Print Operators" && g.AdminCount);
        Assert.Contains(builtin.Children, g => g.Name == "Pre-Windows 2000 Compatible Access" && g.GroupScope == ADGroupScope.DomainLocal);
        Assert.Contains(Flatten(domain), o => o is { Name: "AdminSDHolder", Parent.Name: "System" });
        Assert.NotNull(domain.PSOMinPasswordLength); // politique de mot de passe du domaine
    }

    [Fact]
    public void FreshDomain_HasOnlyWhatAnInstallationCreates()
    {
        var names = Flatten(_service.CreateFreshDomainStructure("corp.local")).Select(o => o.Name).ToHashSet();
        var lab = Flatten(_service.CreateLabBaseStructure("corp.local")).Select(o => o.Name).ToHashSet();

        foreach (var added in new[] { "WORKSTATION-01", "GPO-Baseline-DC", "GPO-Paris-Workstations", "GG-HelpDesk", "GG-Tier1-Operators" })
        {
            Assert.DoesNotContain(added, names);
            Assert.Contains(added, lab); // la base des exercices les garde
        }
        // Les ACE par défaut d'un AD neuf (FidelityTests les compare à un vrai domaine) : sur la racine et les conteneurs,
        // les GPO par défaut, et les comptes et groupes protégés une fois SDProp passé
        var fresh = _service.CreateFreshDomainStructure("corp.local");
        var aces = Flatten(fresh).SelectMany(o => o.Delegations.Select(d => $"{o.Name}|{d.TrusteeName}|{d.Effective}")).ToList();
        Assert.Contains("corp.local|Domain Controllers|Replication", aces);
        Assert.Contains("corp.local|Key Admins|WriteKeyCredentialLink", aces);
        Assert.Contains("AdminSDHolder|Enterprise Admins|WriteDacl", aces);
        Assert.Contains("Computers|Account Operators|ComputerManagement", aces);
        Assert.Contains("Domain Admins|Enterprise Admins|WriteDacl", aces);
        Assert.Contains("Default Domain Policy|Domain Admins|WriteDacl", aces);
        Assert.All(Flatten(fresh).Where(o => o.Delegations.Count > 0), o => Assert.True(
            o.Type is ADObjectType.Domain or ADObjectType.Container or ADObjectType.OrganizationalUnit or ADObjectType.Policy || o.AdminCount, o.Name));
        // Une machine jointe au domaine est administrée par Domain Admins, un DC par Builtin Administrators
        Assert.All(Flatten(fresh).Where(o => o.Type == ADObjectType.Computer), c => Assert.Equal(["Administrators"], c.LocalAdmins!));
        Assert.All(Flatten(fresh).Where(o => o.Type == ADObjectType.Computer), c => Assert.Null(c.LapsPasswordExpiration));
        Assert.Equal(7, _service.CreateFreshDomainStructure("corp.local").PSOMinPasswordLength); // valeur par défaut d'AD
    }

    [Fact]
    public async Task ServerVersion_IsSavedWithTheDomain_AndUnknownForAnOlderDocument()
    {
        var path = Path.Combine(Path.GetTempPath(), $"smadx-version-{Guid.NewGuid():N}.smad-x.json");
        try
        {
            var domain = _service.CreateFreshDomainStructure("corp.local", version: WindowsServerVersion.Server2019);
            Assert.True(await _service.SaveDocumentAsync(new ADRootDocument { Domain = domain }, path));
            Assert.Equal(1, Regex.Count(await File.ReadAllTextAsync(path), "\"ServerVersion\""));
            Assert.Equal(WindowsServerVersion.Server2019, (await _service.LoadDocumentAsync(path)).Document!.Domain.ServerVersion);

            domain.ServerVersion = null;
            Assert.True(await _service.SaveDocumentAsync(new ADRootDocument { Domain = domain }, path));
            Assert.DoesNotContain("ServerVersion", await File.ReadAllTextAsync(path));
            Assert.Null((await _service.LoadDocumentAsync(path)).Document!.Domain.ServerVersion);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void SampleDomain_NamedAdminAccount_IsInDomainAdmins_ThroughItsGroup()
    {
        var context = new SMADX.Services.Audit.AuditContext(_service.CreateSampleStructure(), DateTime.Now);
        var admin = context.Objects.Single(o => o.Name == "adm-lmartin");

        Assert.Contains(context.EffectiveGroups(admin), g => g.Name == "Domain Admins");
    }

    [Fact]
    public void SwitchingLanguage_TranslatesOnlyDefaultDescriptions()
    {
        var loc = LocalizationService.Instance;
        var previous = loc.CurrentCulture.Name;
        loc.SetLanguage("fr-FR");
        var domain = _service.CreateSampleStructure();
        var jdoe = Find(domain, "jdoe");
        jdoe.Description = "Compte de test : **ne pas supprimer** (note de l'apprenant)";
        try
        {
            loc.SetLanguage("en-US");
            _service.UpdateDescriptionsForCurrentLanguage(domain);

            Assert.Equal(loc.Translate("Desc.Users.Krbtgt", "en-US"), Find(domain, "krbtgt").Description);
            Assert.Equal(loc.Translate("Desc.Sample.Domain", "en-US"), domain.Description);
            // Le groupe des administrateurs Tier 0 de l'OU Admin garde sa propre description
            var adminGroup = Flatten(domain).Single(o => o.Name == "GG-Tier0-Admins");
            Assert.Equal(loc.Translate("Desc.Sample.AdminDomainAdmins", "en-US"), adminGroup.Description);
            Assert.Equal("Compte de test : **ne pas supprimer** (note de l'apprenant)", jdoe.Description);
        }
        finally
        {
            loc.SetLanguage(previous);
        }
    }

    [Fact]
    public void SwitchingLanguage_TranslatesTheDescriptionOfANewDomain()
    {
        var loc = LocalizationService.Instance;
        var previous = loc.CurrentCulture.Name;
        loc.SetLanguage("fr-FR");
        var domain = _service.CreateFreshDomainStructure("corp.local");
        try
        {
            loc.SetLanguage("en-US");
            _service.UpdateDescriptionsForCurrentLanguage(domain);

            Assert.Equal(string.Format(loc.Translate("Desc.Domain", "en-US"), "corp.local"), domain.Description);
        }
        finally
        {
            loc.SetLanguage(previous);
        }
    }

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
        var result = await _service.LoadDocumentAsync(path);

        Assert.Equal(DocumentLoadStatus.Loaded, result.Status);
        Assert.False(result.WasMigrated);
        var loaded = result.Document!;
        Assert.Equal(ADRootDocument.CurrentVersion, loaded.Version);
        var jdoe = Find(loaded.Domain, "jdoe");
        Assert.Equal(["Helpdesk"], jdoe.MemberOf);
        Assert.Same(Find(loaded.Domain, "Corp"), jdoe.Parent);
        Assert.Equal(["GPO-Workstations"], Find(loaded.Domain, "Corp").LinkedGPOs);
        Assert.Equal(RightCategory.PasswordReset, Assert.Single(Find(loaded.Domain, "Corp").Delegations).RightCategory);
        Assert.Equal("Default-First-Site-Name", Assert.Single(loaded.SitesTopology!.Sites).Name);
    }

    [Fact]
    public async Task ApplyTierConfiguration_RestoresCustomTiers()
    {
        TierConfigurationService.Instance.AddTier(new TierConfiguration("Tier Cloud", "#123456", "Azure", 3));
        var path = TempFile(".smad-x.json");
        await _service.SaveDocumentAsync(new ADRootDocument { Domain = BuildSmallDomain() }, path);
        TierConfigurationService.Instance.ResetToDefaults();

        var result = await _service.LoadDocumentAsync(path);
        _service.ApplyTierConfiguration(result.Document!);

        Assert.Equal("#123456", TierConfigurationService.Instance.GetTierColor("Tier Cloud"));
    }

    [Fact]
    public async Task Load_DoesNotChangeApplicationTiers()
    {
        // La fenêtre Timeline charge des fichiers juste pour les comparer
        var path = TempFile(".json");
        await _service.SaveToFileAsync(BuildSmallDomain(), path);
        TierConfigurationService.Instance.AddTier(new TierConfiguration("Tier Cloud", "#123456", "Azure", 3));

        await _service.LoadDocumentAsync(path);

        Assert.Equal(4, TierConfigurationService.Instance.Tiers.Count);
    }

    [Fact]
    public async Task LoadV1_IsMigratedAndApplyResetsCustomTiers()
    {
        var path = TempFile(".json");
        await _service.SaveToFileAsync(BuildSmallDomain(), path);
        TierConfigurationService.Instance.AddTier(new TierConfiguration("Tier Cloud", "#123456", "Azure", 3));

        var result = await _service.LoadDocumentAsync(path);
        _service.ApplyTierConfiguration(result.Document!);

        Assert.Equal(1, result.FileVersion);
        Assert.True(result.WasMigrated);
        Assert.Equal(ADRootDocument.CurrentVersion, result.Document!.Version);
        Assert.Equal(3, TierConfigurationService.Instance.Tiers.Count);
    }

    [Fact]
    public async Task Load_MissingFile_ReportsNotFound()
    {
        var result = await _service.LoadDocumentAsync(TempFile(".json"));

        Assert.Equal(DocumentLoadStatus.NotFound, result.Status);
        Assert.Null(result.Document);
    }
}
