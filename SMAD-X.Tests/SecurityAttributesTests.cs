using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using SMADX.Models;
using SMADX.Services;
using SMADX.ViewModels;
using static SMADX.Tests.TestHelpers;

namespace SMADX.Tests;

public class BuiltInSecurityDefaultsTests
{
    private readonly ADObject _domain;

    public BuiltInSecurityDefaultsTests()
    {
        TierConfigurationService.Instance.ResetToDefaults();
        _domain = new ADDataService().CreateFreshDomainStructure("corp.local");
    }

    private ADObject Get(string name, ADObjectType type) => Flatten(_domain).First(o => o.Name == name && o.Type == type);

    [Fact]
    public void AccountFlags_MatchActiveDirectoryUserAccountControlBits()
    {
        Assert.Equal(0x2, (int)ADAccountFlags.AccountDisabled);
        Assert.Equal(0x20, (int)ADAccountFlags.PasswordNotRequired);
        Assert.Equal(0x10000, (int)ADAccountFlags.PasswordNeverExpires);
        Assert.Equal(0x80000, (int)ADAccountFlags.TrustedForDelegation);
        Assert.Equal(0x100000, (int)ADAccountFlags.NotDelegated);
        Assert.Equal(0x400000, (int)ADAccountFlags.DontRequirePreauth);
        Assert.Equal(0x1000000, (int)ADAccountFlags.TrustedToAuthForDelegation);
    }

    [Fact]
    public void Krbtgt_IsDisabledProtectedWithKadminSpn()
    {
        var krbtgt = Get("krbtgt", ADObjectType.User);

        Assert.Equal(ADAccountFlags.AccountDisabled, krbtgt.AccountFlags);
        Assert.True(krbtgt.AdminCount);
        Assert.Equal(["kadmin/changepw"], krbtgt.ServicePrincipalNames);
    }

    [Fact]
    public void BuiltInUsers_HaveRealUserAccountControl()
    {
        Assert.Equal(ADAccountFlags.PasswordNeverExpires, Get("Administrator", ADObjectType.User).AccountFlags);
        Assert.Equal(ADAccountFlags.AccountDisabled | ADAccountFlags.PasswordNotRequired | ADAccountFlags.PasswordNeverExpires,
            Get("Guest", ADObjectType.User).AccountFlags);
        Assert.True(Get("Administrator", ADObjectType.User).AdminCount);
        Assert.False(Get("Guest", ADObjectType.User).AdminCount);
    }

    [Fact]
    public void DomainController_HasUnconstrainedDelegationAndDcSpns()
    {
        var dc = Get("DC01", ADObjectType.Computer);

        Assert.Equal(ADAccountFlags.TrustedForDelegation, dc.AccountFlags);
        Assert.Contains("ldap/DC01.corp.local", dc.ServicePrincipalNames);
        Assert.Contains("GC/DC01.corp.local/corp.local", dc.ServicePrincipalNames);
        Assert.True(dc.AdminCount);
    }

    [Fact]
    public void Workstation_HasHostSpnsButNoDelegation()
    {
        var workstation = Get("WORKSTATION-01", ADObjectType.Computer);

        Assert.Equal(ADAccountFlags.None, workstation.AccountFlags);
        Assert.Contains("HOST/WORKSTATION-01.corp.local", workstation.ServicePrincipalNames);
        Assert.DoesNotContain(workstation.ServicePrincipalNames, s => s.StartsWith("ldap/"));
    }

    [Theory]
    [InlineData("Administrators", ADGroupScope.DomainLocal, true)]   // Builtin
    [InlineData("Domain Admins", ADGroupScope.Global, true)]
    [InlineData("Domain Users", ADGroupScope.Global, false)]
    [InlineData("Enterprise Admins", ADGroupScope.Universal, true)]
    [InlineData("Schema Admins", ADGroupScope.Universal, true)]
    [InlineData("DnsAdmins", ADGroupScope.DomainLocal, false)]
    [InlineData("Protected Users", ADGroupScope.Global, false)]
    public void Groups_HaveRealScopeAndAdminCount(string name, ADGroupScope scope, bool adminCount)
    {
        var group = Get(name, ADObjectType.Group);

        Assert.Equal(scope, group.GroupScope);
        Assert.Equal(ADGroupCategory.Security, group.GroupCategory);
        Assert.Equal(adminCount, group.AdminCount);
    }

    [Fact]
    public void Objects_AreOwnedByDomainAdmins()
    {
        Assert.All(Flatten(_domain).Where(o => o != _domain), o => Assert.Equal("Domain Admins", o.Owner));
    }

    [Fact]
    public void SampleGmsa_HasSpn()
    {
        var gmsa = Flatten(new ADDataService().CreateSampleStructure()).First(o => o.Type == ADObjectType.GMSA);

        Assert.Equal([$"HTTP/{gmsa.Name}.contoso.com"], gmsa.ServicePrincipalNames);
    }
}

public class SecurityAttributesModelTests
{
    private static ADObject ServiceAccount()
    {
        var svc = new ADObject("svc-sql", ADObjectType.User)
        {
            AccountFlags = ADAccountFlags.DontRequirePreauth | ADAccountFlags.TrustedToAuthForDelegation,
            AdminCount = true,
            Owner = "Helpdesk"
        };
        svc.ServicePrincipalNames.Add("MSSQLSvc/sql01.contoso.com:1433");
        svc.AllowedToDelegateTo.Add("cifs/fs01.contoso.com");
        return svc;
    }

    [Fact]
    public void Clone_LikeActiveDirectoryCopy_KeepsOptionsButNotSpnAdminCountOrOwner()
    {
        var clone = ServiceAccount().Clone();

        Assert.Equal(ADAccountFlags.DontRequirePreauth | ADAccountFlags.TrustedToAuthForDelegation, clone.AccountFlags);
        Assert.Equal(["cifs/fs01.contoso.com"], clone.AllowedToDelegateTo);
        Assert.Empty(clone.ServicePrincipalNames);
        Assert.False(clone.AdminCount);
        Assert.Null(clone.Owner);
    }

    [Fact]
    public async Task SecurityAttributes_RoundTripThroughJson()
    {
        var domain = BuildSmallDomain();
        Add(Find(domain, "Corp"), ServiceAccount());
        var group = Find(domain, "Helpdesk");
        group.GroupScope = ADGroupScope.DomainLocal;
        group.GroupCategory = ADGroupCategory.Distribution;
        var service = new ADDataService();
        var path = TempFile(".smad-x.json");

        await service.SaveDocumentAsync(new ADRootDocument { Domain = domain }, path);
        var loaded = (await service.LoadDocumentAsync(path)).Document!.Domain;

        var svc = Find(loaded, "svc-sql");
        Assert.Equal(ADAccountFlags.DontRequirePreauth | ADAccountFlags.TrustedToAuthForDelegation, svc.AccountFlags);
        Assert.Equal(["MSSQLSvc/sql01.contoso.com:1433"], svc.ServicePrincipalNames);
        Assert.Equal(["cifs/fs01.contoso.com"], svc.AllowedToDelegateTo);
        Assert.True(svc.AdminCount);
        Assert.Equal("Helpdesk", svc.Owner);
        Assert.Equal(ADGroupScope.DomainLocal, Find(loaded, "Helpdesk").GroupScope);
        Assert.Equal(ADGroupCategory.Distribution, Find(loaded, "Helpdesk").GroupCategory);
        // Les drapeaux s'écrivent par leur nom : le fichier reste lisible par un humain
        Assert.Contains("\"DontRequirePreauth, TrustedToAuthForDelegation\"", await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task FileWithoutSecurityAttributes_LoadsWithDefaults()
    {
        var path = TempFile(".smad-x.json");
        await File.WriteAllTextAsync(path, """{ "Version": 2, "Domain": { "Name": "old.local", "Type": "Domain", "Children": [ { "Name": "jdoe", "Type": "User" } ] } }""");

        var user = (await new ADDataService().LoadDocumentAsync(path)).Document!.Domain.Children[0];

        Assert.Equal(ADAccountFlags.None, user.AccountFlags);
        Assert.Empty(user.ServicePrincipalNames);
        Assert.False(user.AdminCount);
        Assert.Null(user.GroupScope);
        Assert.Null(user.Owner);
    }

    [Fact]
    public void Diff_ReportsSecurityChangesFlagByFlag()
    {
        var before = BuildSmallDomain();
        var after = BuildSmallDomain();
        var jdoe = Find(after, "jdoe");
        jdoe.AccountFlags = ADAccountFlags.DontRequirePreauth;
        jdoe.ServicePrincipalNames.Add("HTTP/intranet");
        jdoe.Owner = "Helpdesk";

        var change = Assert.Single(ADDiffService.Compare(new DomainSnapshot { Root = before }, new DomainSnapshot { Root = after }));

        Assert.Equal(ChangeCategory.Security, change.ChangeCategory);
        Assert.Contains(change.ChangedFields, f => f.Field == "DontRequirePreauth +");
        Assert.Contains(change.ChangedFields, f => f is { Field: "SPN +", NewValue: "HTTP/intranet" });
        Assert.Contains(change.ChangedFields, f => f is { Field: "Owner", NewValue: "Helpdesk" });
    }
}

public class ObjectSecurityViewModelTests
{
    [Fact]
    public void Flags_ToggleOnlyTheirBit()
    {
        var user = new ADObject("jdoe", ADObjectType.User) { AccountFlags = ADAccountFlags.PasswordNeverExpires };
        var vm = new ObjectSecurityViewModel(user);
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.DontRequirePreauth = true;
        vm.PasswordNeverExpires = false;

        Assert.Equal(ADAccountFlags.DontRequirePreauth, user.AccountFlags);
        Assert.Equal(["DontRequirePreauth", "PasswordNeverExpires"], changed);
    }

    [Fact]
    public void SpnText_IsTrimmedAndDeduplicated()
    {
        var computer = new ADObject("SRV01", ADObjectType.Computer);
        var vm = new ObjectSecurityViewModel(computer);

        vm.ServicePrincipalNamesText = "  HTTP/srv01 \r\n\r\nhttp/SRV01\nMSSQLSvc/srv01:1433\n";

        Assert.Equal(["HTTP/srv01", "MSSQLSvc/srv01:1433"], computer.ServicePrincipalNames);
        Assert.Equal($"HTTP/srv01{Environment.NewLine}MSSQLSvc/srv01:1433", vm.ServicePrincipalNamesText);
    }

    [Fact]
    public void Group_ExposesScopeCategoryButNoAccountOptions()
    {
        var group = new ADObject("GG-Test", ADObjectType.Group);
        var vm = new ObjectSecurityViewModel(group);

        vm.GroupScope = ADGroupScope.Universal;
        vm.Owner = "  Helpdesk ";

        Assert.True(vm.IsGroup);
        Assert.False(vm.IsAccount);
        Assert.Equal(ADGroupScope.Universal, group.GroupScope);
        Assert.Equal("Helpdesk", group.Owner);
    }

    [Fact]
    public void MainViewModel_ExposesSecurityOfSelection()
    {
        var main = new MainWindowViewModel(new FakeDialogService());

        main.SelectedNode = main.RootNodes[0].Children.First(c => c.Data.Name == "Users");
        main.SelectedNode = main.SelectedNode.Children.First(c => c.Data.Name == "krbtgt");

        Assert.NotNull(main.SelectedSecurity);
        Assert.True(main.SelectedSecurity.AccountDisabled);
        Assert.Equal("kadmin/changepw", main.SelectedSecurity.ServicePrincipalNamesText);
    }
}

public class SecurityPowerShellTests
{
    private static async Task<string> ExportAsync(ADObject root)
    {
        var path = TempFile(".ps1");
        Assert.True(await new ADPowerShellExportService().ExportToPowerShellAsync(root, path));
        return await File.ReadAllTextAsync(path);
    }

    [Fact]
    public async Task Export_CreatesGroupsWithTheirScope()
    {
        var script = await ExportAsync(new ADDataService().CreateSampleStructure());

        Assert.Contains("-Name \"Enterprise Admins\" -SamAccountName \"Enterprise Admins\" -Path \"CN=Users,DC=contoso,DC=com\" -GroupScope \"Universal\" -GroupCategory \"Security\"", script);
    }

    [Fact]
    public async Task Export_AppliesAccountSecurityAdditively()
    {
        var domain = BuildSmallDomain();
        var jdoe = Find(domain, "jdoe");
        jdoe.AccountFlags = ADAccountFlags.DontRequirePreauth | ADAccountFlags.AccountDisabled;
        jdoe.ServicePrincipalNames.Add("HTTP/intranet.contoso.com");

        var script = await ExportAsync(domain);

        Assert.Contains("Set-ADAccountSecurityIfNeeded -Identity \"jdoe\" -DistinguishedName \"CN=jdoe,OU=Corp,DC=contoso,DC=com\"", script);
        Assert.Contains("-Flags @{ DoesNotRequirePreAuth = $true }", script); // « désactivé » n'est pas exporté
        Assert.Contains("-Spns @(\"HTTP/intranet.contoso.com\")", script);
        Assert.Contains("-Add @{ servicePrincipalName = $newSpns }", script);
        Assert.DoesNotContain("-Replace", script);
    }

    [Fact]
    public async Task Export_NeverTouchesKrbtgtOrDomainControllers()
    {
        var script = await ExportAsync(new ADDataService().CreateSampleStructure());

        // Le $ final d'un compte ordinateur est échappé dans la chaîne PowerShell
        Assert.DoesNotContain("-Identity \"krbtgt\"", script);
        Assert.DoesNotContain("-Identity \"DC01`$\"", script);
        Assert.Contains("-Identity \"SRV-FILE-01`$\"", script);
    }

    [Theory]
    [MemberData(nameof(ADPowerShellExportServiceTests.Shells), MemberType = typeof(ADPowerShellExportServiceTests))]
    public async Task ImportScript_ParsesWithoutErrors(string shell)
    {
        var path = TempFile(".ps1");
        Assert.True(await new ADImportPowerShellService().GenerateImportScriptAsync(path));

        var output = await PowerShellRunner.RunAsync(shell, "-Command",
            "$t=$null;$e=$null;" +
            $"[void][System.Management.Automation.Language.Parser]::ParseFile('{path}',[ref]$t,[ref]$e);" +
            "$e | ForEach-Object { \"$($_.Extent.StartLineNumber): $($_.Message)\" }");

        Assert.True(string.IsNullOrWhiteSpace(output), $"Erreurs d'analyse {shell} :\n{output}");
    }

    /// <summary>
    /// Exécute la vraie fonction Build-NodeJson du script d'import, avec les cmdlets AD remplacées par
    /// des bouchons, sur un compte AS-REP roastable avec SPN ; le JSON produit doit se relire en ADObject.
    /// </summary>
    [Theory]
    [MemberData(nameof(ADPowerShellExportServiceTests.Shells), MemberType = typeof(ADPowerShellExportServiceTests))]
    public async Task ImportScript_EmitsSecurityAttributesAsSmadxJson(string shell)
    {
        var scriptPath = TempFile(".ps1");
        Assert.True(await new ADImportPowerShellService().GenerateImportScriptAsync(scriptPath));
        var script = await File.ReadAllTextAsync(scriptPath);

        // Les fonctions et tables d'aide, sans les prérequis ni la collecte sur un vrai domaine
        var helpers = script[script.IndexOf("function ConvertTo-JsonString", StringComparison.Ordinal)..script.IndexOf("# ── 4. Collect GPOs", StringComparison.Ordinal)];
        var builder = script[script.IndexOf("function Build-NodeJson", StringComparison.Ordinal)..script.IndexOf("# ── 9. Domain root", StringComparison.Ordinal)];

        var harness = new StringBuilder()
            .AppendLine("Set-StrictMode -Version 2.0")
            .AppendLine("$ErrorActionPreference = 'Stop'")
            .AppendLine("[Console]::OutputEncoding = [Text.Encoding]::UTF8")
            .AppendLine("function Get-ADObject { @() }")
            .AppendLine("$gpoMap = @{}; $psoObjects = @()")
            .AppendLine(helpers)
            .AppendLine(builder)
            .AppendLine("$user = [pscustomobject]@{")
            .AppendLine("    Description = 'Compte de service'; WhenCreated = $null; WhenChanged = $null; MemberOf = @()")
            .AppendLine("    userAccountControl = 0x410200   # NORMAL_ACCOUNT + DONT_EXPIRE_PASSWORD + DONT_REQ_PREAUTH")
            .AppendLine("    servicePrincipalName = @('MSSQLSvc/sql01.contoso.com:1433', 'MSSQLSvc/sql01.contoso.com')")
            .AppendLine("    'msDS-AllowedToDelegateTo' = @('cifs/fs01.contoso.com')")
            .AppendLine("    adminCount = 1")
            .AppendLine("    lastLogonTimestamp = 134000000000000000   # FileTime")
            .AppendLine("    pwdLastSet = 0                            # 0 = doit changer à la prochaine connexion")
            .AppendLine("    nTSecurityDescriptor = [pscustomobject]@{ Owner = 'CONTOSO\\Helpdesk' }")
            .AppendLine("}")
            .AppendLine("Build-NodeJson -dn 'CN=svc-sql,OU=Services,DC=contoso,DC=com' -name 'svc-sql' -simType 'User' -adObj $user")
            .AppendLine("$group = [pscustomobject]@{ Description = ''; WhenCreated = $null; WhenChanged = $null; MemberOf = @(); groupType = -2147483640 }")
            .AppendLine("Build-NodeJson -dn 'CN=GU-Test,DC=contoso,DC=com' -name 'GU-Test' -simType 'Group' -adObj $group");
        var harnessPath = TempFile(".ps1");
        await File.WriteAllTextAsync(harnessPath, harness.ToString(), new UTF8Encoding(true));

        var lines = (await PowerShellRunner.RunAsync(shell, "-File", harnessPath))
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var options = new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } };
        var user = JsonSerializer.Deserialize<ADObject>(lines[0], options)!;
        var group = JsonSerializer.Deserialize<ADObject>(lines[1], options)!;

        Assert.Equal(ADAccountFlags.PasswordNeverExpires | ADAccountFlags.DontRequirePreauth, user.AccountFlags);
        Assert.Equal(["MSSQLSvc/sql01.contoso.com:1433", "MSSQLSvc/sql01.contoso.com"], user.ServicePrincipalNames);
        Assert.Equal(["cifs/fs01.contoso.com"], user.AllowedToDelegateTo);
        Assert.True(user.AdminCount);
        Assert.Equal("Helpdesk", user.Owner);
        Assert.Equal(DateTime.FromFileTimeUtc(134000000000000000), user.LastLogonTimestamp!.Value.ToUniversalTime());
        Assert.Null(user.PwdLastSet);
        Assert.Null(user.GroupScope);
        // groupType -2147483640 = 0x80000008 : groupe de sécurité universel
        Assert.Equal(ADGroupScope.Universal, group.GroupScope);
        Assert.Equal(ADGroupCategory.Security, group.GroupCategory);
        Assert.False(group.AdminCount);
    }
}
