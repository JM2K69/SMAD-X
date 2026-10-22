using SMADX.Models;
using SMADX.Services;
using SMADX.Services.Audit;
using SMADX.ViewModels;
using static SMADX.Tests.TestHelpers;

namespace SMADX.Tests;

/// <summary>Le catalogue lui-même : identifiants, textes, références.</summary>
public class AuditCatalogueTests
{
    /// <summary>
    /// Points de contrôle ANSSI cités par les règles, avec leurs niveaux officiels relevés dans la
    /// checklist du CERT-FR (cert.cyber.gouv.fr/uploads/ad_checklist.html, octobre 2026).
    /// Les points info_* n'ont pas de niveau.
    /// </summary>
    private static readonly Dictionary<string, int[]> OfficialAnssiPoints = new()
    {
        ["vuln_spn_priv"] = [1],
        ["info_spn_present"] = [],
        ["vuln_kerberos_properties_preauth_priv"] = [1],
        ["vuln_kerberos_properties_preauth"] = [2],
        ["vuln_krbtgt"] = [2],
        ["vuln_delegation_t4d"] = [1],
        ["vuln_delegation_t2a4d"] = [1],
        ["vuln_delegation_a2d2"] = [1],
        ["vuln_protected_users"] = [3],
        ["vuln_dont_expire_priv"] = [1],
        ["vuln_dont_expire"] = [2],
        ["vuln_password_change_priv"] = [1],
        ["vuln_password_change_dc_no_change"] = [1],
        ["vuln_password_change_server_no_change_90"] = [2],
        ["vuln_password_change_msa_no_change_90"] = [2],
        ["vuln_guest"] = [2],
        ["info_user_accounts_dormant"] = [],
        ["vuln_privileged_members_no_admincount"] = [2],
        ["warning_admincount"] = [1, 3],
        ["warning_privileged_members_not_empty"] = [3],
        ["vuln_dnsadmins"] = [1],
        ["vuln_owner"] = [3],
        ["vuln_permissions_gpo_priv"] = [1],
        ["vuln_privileged_members_perm"] = [1, 2],
        ["vuln_group_loop"] = [4]
    };

    public static TheoryData<string> RuleIds()
    {
        var data = new TheoryData<string>();
        foreach (var rule in AuditRules.All)
            data.Add(rule.Id);
        return data;
    }

    private static AuditRule Rule(string id) => AuditRules.All.Single(r => r.Id == id);

    [Fact]
    public void RuleIds_AreUnique()
    {
        Assert.Equal(AuditRules.All.Count, AuditRules.All.Select(r => r.Id).Distinct().Count());
    }

    [Theory]
    [MemberData(nameof(RuleIds))]
    public void Rule_CitesOnlyOfficialAnssiPoints(string id)
    {
        foreach (var reference in Rule(id).References.Where(r => r.Framework == AuditFramework.Anssi && r.Id != "ANSSI-PA-099"))
            Assert.True(OfficialAnssiPoints.ContainsKey(reference.Id), $"{id} cite un point ANSSI inconnu : {reference.Id}");
    }

    [Theory]
    [MemberData(nameof(RuleIds))]
    public void Rule_AnssiLevel_MatchesOfficialLevelAndSeverity(string id)
    {
        var rule = Rule(id);
        if (rule.AnssiLevel is not { } level) return;

        var point = rule.References.First(r => r.Framework == AuditFramework.Anssi).Id;
        Assert.Contains(level, OfficialAnssiPoints[point]);
        // Niveau 1 → Critique … niveau 4 → Faible
        Assert.Equal((AuditSeverity)(level - 1), rule.Severity);
    }

    [Theory]
    [MemberData(nameof(RuleIds))]
    public void Rule_HasSecureReferencesFromSeveralFrameworks(string id)
    {
        var references = Rule(id).References;

        Assert.All(references, r => Assert.StartsWith("https://", r.Url));
        Assert.True(references.Select(r => r.Framework).Distinct().Count() >= 2, $"{id} doit croiser au moins deux référentiels");
    }

    [Theory]
    [MemberData(nameof(RuleIds))]
    public void Rule_HasDocumentationAndDetailInEveryLanguage(string id)
    {
        var loc = LocalizationService.Instance;
        var previous = loc.CurrentCulture.Name;
        try
        {
            foreach (var culture in new[] { "fr-FR", "en-US" })
            {
                loc.SetLanguage(culture);
                var rule = Rule(id);
                Assert.StartsWith("# ", rule.Documentation);
                Assert.False(string.IsNullOrWhiteSpace(rule.Title));
                Assert.DoesNotContain("[Audit.", rule.FormatDetail(["a", "b", "c", "d"]));
            }
        }
        finally
        {
            loc.SetLanguage(previous);
        }
    }

    [Fact]
    public void Mitre_Urls_FollowAttackLayout()
    {
        var kerberoasting = AuditRules.All.SelectMany(r => r.References).First(r => r.Id == "T1558.003");

        Assert.Equal("https://attack.mitre.org/techniques/T1558/003/", kerberoasting.Url);
    }
}

/// <summary>Chaque règle, sur un domaine construit pour la déclencher, et sur sa version corrigée.</summary>
public class AuditRuleBehaviourTests
{
    private static readonly DateTime Now = new(2026, 10, 22);

    public AuditRuleBehaviourTests() => TierConfigurationService.Instance.ResetToDefaults();

    /// <summary>Domaine minimal : Users (groupes protégés + DnsAdmins + Protected Users), une OU Corp, un DC.</summary>
    private static ADObject Domain()
    {
        var domain = new ADObject("contoso.com", ADObjectType.Domain) { Tier = "Tier 0" };
        var users = Add(domain, new ADObject("Users", ADObjectType.Container));
        foreach (var g in new[] { "Domain Admins", "Schema Admins", "Enterprise Admins", "Account Operators", "DnsAdmins", "Protected Users" })
            Add(users, new ADObject(g, ADObjectType.Group) { Tier = "Tier 0", AdminCount = BuiltInSecurityDefaults.ProtectedGroups.Contains(g), Owner = "Domain Admins" });
        var dcOu = Add(domain, new ADObject("Domain Controllers", ADObjectType.OrganizationalUnit) { Tier = "Tier 0" });
        Add(dcOu, new ADObject("DC01", ADObjectType.Computer)
        {
            Tier = "Tier 0", AccountFlags = ADAccountFlags.TrustedForDelegation, PwdLastSet = Now.AddDays(-10), LastLogonTimestamp = Now
        });
        var corp = Add(domain, new ADObject("Corp", ADObjectType.OrganizationalUnit) { Tier = "Tier 2" });
        Add(corp, new ADObject("jdoe", ADObjectType.User) { Tier = "Tier 2", PwdLastSet = Now.AddDays(-30), LastLogonTimestamp = Now.AddDays(-1) });
        return domain;
    }

    private static ADObject Admin(ADObject domain)
    {
        var admin = Add(Find(domain, "Corp"), new ADObject("adm-alice", ADObjectType.User)
        {
            Tier = "Tier 0", AdminCount = true, AccountFlags = ADAccountFlags.NotDelegated,
            PwdLastSet = Now.AddDays(-30), LastLogonTimestamp = Now, Owner = "Domain Admins"
        });
        admin.MemberOf.Add("Domain Admins");
        return admin;
    }

    private static IReadOnlyList<AuditFinding> Audit(ADObject domain) => new ADSecurityAuditService().Run(domain, Now).Findings;

    private static void AssertFires(ADObject domain, string ruleId, string objectName) =>
        Assert.Contains(Audit(domain), f => f.Rule.Id == ruleId && f.Object.Name == objectName);

    private static void AssertSilent(ADObject domain, string ruleId) =>
        Assert.DoesNotContain(Audit(domain), f => f.Rule.Id == ruleId);

    [Fact]
    public void CleanDomain_HasNoFinding()
    {
        var domain = Domain();
        Admin(domain);

        Assert.Empty(Audit(domain));
    }

    [Fact]
    public void Kerberoasting_PrivilegedThroughNestedGroup_IsCritical()
    {
        var domain = Domain();
        var nested = Add(Find(domain, "Corp"), new ADObject("GG-SQL-Admins", ADObjectType.Group) { Tier = "Tier 0" });
        nested.MemberOf.Add("Domain Admins");
        var svc = Add(Find(domain, "Corp"), new ADObject("svc-sql", ADObjectType.User) { Tier = "Tier 0", AdminCount = true, AccountFlags = ADAccountFlags.NotDelegated, LastLogonTimestamp = Now });
        svc.MemberOf.Add("GG-SQL-Admins");
        svc.ServicePrincipalNames.Add("MSSQLSvc/sql01.contoso.com:1433");

        AssertFires(domain, "KRB-01", "svc-sql");
        AssertSilent(domain, "KRB-02");
    }

    [Fact]
    public void Kerberoasting_StandardUser_IsMedium_ButNotKrbtgtNorGmsa()
    {
        var domain = Domain();
        Find(domain, "jdoe").ServicePrincipalNames.Add("HTTP/intranet");
        var krbtgt = Add(Find(domain, "Users"), new ADObject("krbtgt", ADObjectType.User) { AccountFlags = ADAccountFlags.AccountDisabled, PwdLastSet = Now });
        krbtgt.ServicePrincipalNames.Add("kadmin/changepw");
        var gmsa = Add(Find(domain, "Corp"), new ADObject("gmsa-web", ADObjectType.GMSA) { PwdLastSet = Now });
        gmsa.ServicePrincipalNames.Add("HTTP/web");

        var findings = Audit(domain).Where(f => f.Rule.Id is "KRB-01" or "KRB-02").ToList();

        Assert.Equal("jdoe", Assert.Single(findings).Object.Name);
        Assert.Equal(AuditSeverity.Medium, findings[0].Severity);
    }

    [Fact]
    public void AsRepRoasting_SplitsPrivilegedAndStandard()
    {
        var domain = Domain();
        Find(domain, "jdoe").AccountFlags = ADAccountFlags.DontRequirePreauth;
        Admin(domain).AccountFlags |= ADAccountFlags.DontRequirePreauth;

        AssertFires(domain, "KRB-04", "jdoe");
        AssertFires(domain, "KRB-03", "adm-alice");
    }

    [Fact]
    public void AsRepRoasting_IgnoresDisabledAccounts()
    {
        var domain = Domain();
        Find(domain, "jdoe").AccountFlags = ADAccountFlags.DontRequirePreauth | ADAccountFlags.AccountDisabled;

        AssertSilent(domain, "KRB-04");
    }

    [Fact]
    public void Krbtgt_OlderThanOneYear_Fires()
    {
        var domain = Domain();
        var krbtgt = Add(Find(domain, "Users"), new ADObject("krbtgt", ADObjectType.User) { AccountFlags = ADAccountFlags.AccountDisabled, PwdLastSet = Now.AddDays(-400) });

        AssertFires(domain, "KRB-05", "krbtgt");
        krbtgt.PwdLastSet = Now.AddDays(-100);
        AssertSilent(domain, "KRB-05");
    }

    [Fact]
    public void UnconstrainedDelegation_IsNormalOnDcOnly()
    {
        var domain = Domain();
        Add(Find(domain, "Corp"), new ADObject("SRV-APP", ADObjectType.Computer) { Tier = "Tier 1", AccountFlags = ADAccountFlags.TrustedForDelegation, PwdLastSet = Now });

        var findings = Audit(domain).Where(f => f.Rule.Id == "DLG-01").ToList();

        Assert.Equal("SRV-APP", Assert.Single(findings).Object.Name);
    }

    [Fact]
    public void ConstrainedDelegationToDc_DistinguishesProtocolTransition()
    {
        var domain = Domain();
        var withPt = Add(Find(domain, "Corp"), new ADObject("svc-pt", ADObjectType.User) { AccountFlags = ADAccountFlags.TrustedToAuthForDelegation, LastLogonTimestamp = Now });
        withPt.AllowedToDelegateTo.Add("ldap/DC01.contoso.com");
        var withoutPt = Add(Find(domain, "Corp"), new ADObject("svc-kcd", ADObjectType.User) { LastLogonTimestamp = Now });
        withoutPt.AllowedToDelegateTo.Add("cifs/dc01");
        var toFileServer = Add(Find(domain, "Corp"), new ADObject("svc-fs", ADObjectType.User) { LastLogonTimestamp = Now });
        toFileServer.AllowedToDelegateTo.Add("cifs/fs01.contoso.com");

        AssertFires(domain, "DLG-02", "svc-pt");
        AssertFires(domain, "DLG-03", "svc-kcd");
        Assert.DoesNotContain(Audit(domain), f => f.Object.Name == "svc-fs" && f.Rule.Id.StartsWith("DLG"));
    }

    [Fact]
    public void PrivilegedAccount_WithoutProtection_IsReported_UntilInProtectedUsers()
    {
        var domain = Domain();
        var admin = Admin(domain);
        admin.AccountFlags = ADAccountFlags.None;

        AssertFires(domain, "DLG-04", "adm-alice");
        admin.MemberOf.Add("Protected Users");
        AssertSilent(domain, "DLG-04");
    }

    [Fact]
    public void PasswordNeverExpires_SeverityDependsOnPrivilege()
    {
        var domain = Domain();
        Find(domain, "jdoe").AccountFlags = ADAccountFlags.PasswordNeverExpires;
        Admin(domain).AccountFlags |= ADAccountFlags.PasswordNeverExpires;

        AssertFires(domain, "PWD-02", "jdoe");
        AssertFires(domain, "PWD-01", "adm-alice");
    }

    [Fact]
    public void PasswordAgeRules_UseTheirThresholds()
    {
        var domain = Domain();
        Admin(domain).PwdLastSet = Now.AddDays(-(3 * 365 + 1));
        Find(domain, "DC01").PwdLastSet = Now.AddDays(-46);
        Add(Find(domain, "Corp"), new ADObject("WKS-OLD", ADObjectType.Computer) { PwdLastSet = Now.AddDays(-91) });
        Add(Find(domain, "Corp"), new ADObject("WKS-OK", ADObjectType.Computer) { PwdLastSet = Now.AddDays(-89) });
        Add(Find(domain, "Corp"), new ADObject("gmsa-old", ADObjectType.GMSA) { PwdLastSet = Now.AddDays(-91) });

        AssertFires(domain, "PWD-04", "adm-alice");
        AssertFires(domain, "PWD-05", "DC01");
        AssertFires(domain, "PWD-06", "WKS-OLD");
        AssertFires(domain, "PWD-07", "gmsa-old");
        Assert.DoesNotContain(Audit(domain), f => f.Object.Name == "WKS-OK");
    }

    [Fact]
    public void PasswordNotRequired_AndGuestEnabled_AreReported()
    {
        var domain = Domain();
        Find(domain, "jdoe").AccountFlags = ADAccountFlags.PasswordNotRequired;
        Add(Find(domain, "Users"), new ADObject("Guest", ADObjectType.User) { LastLogonTimestamp = Now });

        AssertFires(domain, "PWD-03", "jdoe");
        AssertFires(domain, "ACC-01", "Guest");
    }

    [Fact]
    public void DormantAccount_UsesCisThreshold_AndCreationDateWhenNeverLoggedOn()
    {
        var domain = Domain();
        Find(domain, "jdoe").LastLogonTimestamp = Now.AddDays(-46);
        Add(Find(domain, "Corp"), new ADObject("never", ADObjectType.User) { CreatedDate = Now.AddDays(-60) });
        Add(Find(domain, "Corp"), new ADObject("newcomer", ADObjectType.User) { CreatedDate = Now.AddDays(-5) });

        AssertFires(domain, "ACC-02", "jdoe");
        AssertFires(domain, "ACC-02", "never");
        Assert.DoesNotContain(Audit(domain), f => f.Rule.Id == "ACC-02" && f.Object.Name == "newcomer");
    }

    [Fact]
    public void AdminCount_MissingOnMember_AndOrphanOnFormerAdmin()
    {
        var domain = Domain();
        Admin(domain).AdminCount = false;
        Find(domain, "jdoe").AdminCount = true;

        AssertFires(domain, "PRIV-01", "adm-alice");
        AssertFires(domain, "PRIV-02", "jdoe");
    }

    [Fact]
    public void GroupsThatShouldBeEmpty_AndDnsAdmins()
    {
        var domain = Domain();
        Admin(domain).MemberOf.Add("Schema Admins");
        Find(domain, "jdoe").MemberOf.Add("DnsAdmins");

        AssertFires(domain, "PRIV-03", "Schema Admins");
        AssertFires(domain, "PRIV-04", "jdoe");
        Assert.DoesNotContain(Audit(domain), f => f.Rule.Id == "PRIV-04" && f.Object.Name == "adm-alice");
    }

    [Fact]
    public void ProtectedGroup_OutsideTier0_IsReported()
    {
        var domain = Domain();
        Find(domain, "Account Operators").Tier = "Tier 1";

        AssertFires(domain, "PRIV-05", "Account Operators");
    }

    [Fact]
    public void NonAdministrativeOwner_OnPrivilegedAccount_IsReported()
    {
        var domain = Domain();
        Admin(domain).Owner = "Helpdesk";
        Find(domain, "jdoe").Owner = "Helpdesk"; // compte standard : pas un objet critique

        AssertFires(domain, "ACL-01", "adm-alice");
        Assert.DoesNotContain(Audit(domain), f => f.Rule.Id == "ACL-01" && f.Object.Name == "jdoe");
    }

    [Fact]
    public void Tiering_MembershipGpoAndDelegationViolations()
    {
        var domain = Domain();
        Admin(domain);
        var helpdesk = Add(Find(domain, "Corp"), new ADObject("GG-Helpdesk", ADObjectType.Group) { Tier = "Tier 2" });
        Find(domain, "jdoe").MemberOf.Add("Domain Admins");                       // Tier 2 → groupe Tier 0
        Add(domain, new ADObject("GPO-Workstations", ADObjectType.Policy) { Tier = "Tier 2" });
        Find(domain, "Domain Controllers").LinkedGPOs.Add("GPO-Workstations");    // GPO Tier 2 → OU Tier 0
        Find(domain, "Domain Controllers").Delegations.Add(new ADDelegation       // Tier 2 → droits sur Tier 0
        {
            TrusteeName = helpdesk.Name, TargetDN = Find(domain, "Domain Controllers").DistinguishedName,
            Right = "GenericAll", RightCategory = RightCategory.FullControl
        });

        AssertFires(domain, "TIER-01", "jdoe");
        AssertFires(domain, "TIER-02", "GPO-Workstations");
        AssertFires(domain, "TIER-03", "Domain Controllers");
    }

    [Fact]
    public void GroupLoop_IsDetected_WithoutInfiniteRecursion()
    {
        var domain = Domain();
        var a = Add(Find(domain, "Corp"), new ADObject("GG-A", ADObjectType.Group));
        var b = Add(Find(domain, "Corp"), new ADObject("GG-B", ADObjectType.Group));
        a.MemberOf.Add("GG-B");
        b.MemberOf.Add("GG-A");

        AssertFires(domain, "GRP-01", "GG-A");
        AssertFires(domain, "GRP-01", "GG-B");
    }
}

public class AuditReportTests
{
    private static readonly DateTime Now = new(2026, 10, 22);

    public AuditReportTests() => TierConfigurationService.Instance.ResetToDefaults();

    [Fact]
    public void FreshDomain_IsNotCompliantOutOfTheBox()
    {
        // Constat pédagogique : un AD tout juste promu ne passe pas les points ANSSI de niveau 1
        var report = new ADSecurityAuditService().Run(new ADDataService().CreateFreshDomainStructure("corp.local"), Now);

        Assert.Equal(["DLG-04", "PRIV-03", "PWD-01"], report.FailedRules.Select(r => r.Id).Order());
        Assert.All(report.Findings.Where(f => f.Rule.Id != "PRIV-03"), f => Assert.Equal("Administrator", f.Object.Name));
        Assert.Equal(1, report.EstimatedAnssiLevel);
    }

    [Fact]
    public void SampleDomain_ShowsItsTeachingScenarios()
    {
        var report = new ADSecurityAuditService().Run(new ADDataService().CreateSampleStructure(), DateTime.Now);

        var failed = report.FailedRules.Select(r => r.Id).ToHashSet();
        Assert.Contains("KRB-05", failed);   // krbtgt jamais renouvelé
        Assert.Contains("ACC-02", failed);   // asmith dormant
        Assert.Contains("PWD-06", failed);   // WKS-001 sorti du parc
        Assert.Contains("PRIV-05", failed);  // Read-only Domain Controllers classé Tier 1
        Assert.DoesNotContain("PWD-05", failed); // les DC renouvellent leur mot de passe
    }

    [Fact]
    public void Score_CountsEachFailedRuleOnce()
    {
        var domain = new ADObject("contoso.com", ADObjectType.Domain);
        for (var i = 0; i < 10; i++)
            Add(domain, new ADObject($"user{i}", ADObjectType.User) { AccountFlags = ADAccountFlags.DontRequirePreauth, LastLogonTimestamp = Now });

        var report = new ADSecurityAuditService().Run(domain, Now);

        Assert.Equal(10, report.Findings.Count);
        Assert.Equal(100 - AuditReport.Penalty(AuditSeverity.High), report.Score);
        Assert.Equal(2, report.EstimatedAnssiLevel);
    }

    [Fact]
    public void EmptyReport_HasMaximalScoreAndLevel()
    {
        var report = new ADSecurityAuditService().Run(new ADObject("empty.local", ADObjectType.Domain), Now);

        Assert.Equal(100, report.Score);
        Assert.Equal(5, report.EstimatedAnssiLevel);
    }
}

public class AuditViewModelTests
{
    [Fact]
    public void MainViewModel_OpensAudit_AndBadgesTheTree()
    {
        var dialogs = new FakeDialogService();
        var main = new MainWindowViewModel(dialogs);

        main.ShowAuditCommand.Execute(null);

        var audit = Assert.IsType<AuditViewModel>(dialogs.LastAudit);
        Assert.True(audit.HasFindings);
        var krbtgt = main.RootNodes[0].Children.First(c => c.Data.Name == "Users").Children.First(c => c.Data.Name == "krbtgt");
        Assert.True(krbtgt.HasAuditFindings);
        Assert.Contains("KRB-05", krbtgt.AuditTooltip);
    }

    [Fact]
    public void Filter_ShowInTree_AndRerunAfterFix()
    {
        var dialogs = new FakeDialogService();
        var main = new MainWindowViewModel(dialogs);
        main.ShowAuditCommand.Execute(null);
        var audit = dialogs.LastAudit!;

        audit.SelectedFilter = audit.FilterOptions.First(o => o.Severity == AuditSeverity.High);
        Assert.All(audit.Findings, f => Assert.Equal(AuditSeverity.High, f.Severity));

        audit.SelectedFinding = audit.Findings.First(f => f.RuleId == "KRB-05");
        audit.ShowInTreeCommand.Execute(null);
        Assert.Equal("krbtgt", main.SelectedNode!.Data.Name);
        Assert.True(main.SelectedNode.Parent!.IsExpanded);

        // L'apprenant corrige (renouvelle krbtgt) puis relance : le constat et le badge disparaissent
        main.SelectedNode.Data.PwdLastSet = DateTime.Now;
        audit.RunCommand.Execute(null);
        Assert.DoesNotContain(audit.Findings, f => f.RuleId == "KRB-05");
        Assert.False(main.SelectedNode.HasAuditFindings);
    }
}
