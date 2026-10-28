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
        ["vuln_group_loop"] = [4],
        ["vuln_privileged_members_password"] = [2],
        ["vuln_user_accounts_machineaccountquota"] = [4],
        ["vuln_permissions_naming_context"] = [1, 2],
        ["vuln_permissions_adminsdholder"] = [1, 2],
        ["vuln_permissions_dc"] = [1, 2],
        ["vuln_permissions_gpo_container_priv"] = [2],
        ["vuln_silo_priv"] = [4]
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
        Add(Find(domain, "Corp"), new ADObject("WKS-OK", ADObjectType.Computer) { PwdLastSet = Now.AddDays(-89), LapsPasswordExpiration = Now.AddDays(10) });
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

    private static void Grant(ADObject holder, string trustee, string right, RightCategory category) =>
        holder.Delegations.Add(new ADDelegation { TrusteeName = trustee, Right = right, RightCategory = category, TargetDN = holder.DistinguishedName });

    [Fact]
    public void DangerousRights_OnTheDomainAdminSdHolderAndDcs_AreCritical_UnlessHeldByPrivilegedGroups()
    {
        var domain = Domain();
        var holder = Add(Add(domain, new ADObject("System", ADObjectType.Container)), new ADObject("AdminSDHolder", ADObjectType.Container));
        Grant(domain, "jdoe", "DS-Replication-Get-Changes-All", RightCategory.Replication);
        Grant(holder, "Everyone", "WriteDacl", RightCategory.WriteDacl);
        Grant(Find(domain, "DC01"), "jdoe", "WriteProperty:msDS-KeyCredentialLink", RightCategory.WriteKeyCredentialLink);

        var acl03 = Audit(domain).Where(f => f.Rule.Id == "ACL-03").ToList();
        Assert.Equal(["AdminSDHolder", "contoso.com", "DC01"], acl03.Select(f => f.Object.Name).Order(StringComparer.OrdinalIgnoreCase));
        Assert.All(acl03, f => Assert.Equal(AuditSeverity.Critical, f.Severity));
        // Déjà signalé par ACL-03 : ACL-02 ne le répète pas
        AssertSilent(domain, "ACL-02");

        // Les mêmes droits pour des groupes privilégiés : ce sont ceux d'un AD neuf
        foreach (var o in new[] { domain, holder, Find(domain, "DC01") }) o.Delegations.Clear();
        Grant(domain, "Domain Admins", "DS-Replication-Get-Changes-All", RightCategory.Replication);
        Grant(holder, "Domain Admins", "WriteDacl", RightCategory.WriteDacl);
        AssertSilent(domain, "ACL-03");

        // Répliquer sans les secrets, ou sur une OU, n'est pas un DCSync
        Grant(domain, "jdoe", "DS-Replication-Get-Changes", RightCategory.Other);
        Grant(Find(domain, "Corp"), "jdoe", "DS-Replication-Get-Changes-All", RightCategory.Replication);
        AssertSilent(domain, "ACL-03");
    }

    [Fact]
    public void PermissionPath_FromAnUntieredGroup_IsReportedOnTheObjectThatHoldsTheAce()
    {
        var domain = Domain();
        Admin(domain);
        var gpo = Add(domain, new ADObject("GPO-DC", ADObjectType.Policy) { Tier = "Tier 0", Owner = "Domain Admins" });
        Find(domain, "Domain Controllers").LinkedGPOs.Add(gpo.Name);
        Add(Find(domain, "Corp"), new ADObject("GG-Migration", ADObjectType.Group) { Owner = "Domain Admins" });
        Find(domain, "jdoe").MemberOf.Add("GG-Migration");
        Grant(gpo, "GG-Migration", "GenericWrite", RightCategory.GenericWrite);

        var finding = Assert.Single(Audit(domain), f => f.Rule.Id == "ACL-02");

        Assert.Same(gpo, finding.Object);
        Assert.Contains("GG-Migration", finding.Detail);
        Assert.Contains("2", finding.Detail); // le groupe et son membre
        // Le groupe n'a pas de tier : le cloisonnement (TIER-03) ne voit rien
        AssertSilent(domain, "TIER-03");

        // Classé en Tier 2, c'est TIER-03 qui le signale, et ACL-02 se tait
        Find(domain, "GG-Migration").Tier = "Tier 2";
        AssertFires(domain, "TIER-03", "GPO-DC");
        AssertSilent(domain, "ACL-02");
    }

    [Fact]
    public void PermissionPath_ThroughAnUnprotectedAdmin_AndFromAnyUser()
    {
        var domain = Domain();
        var admins = Add(domain, new ADObject("Admins", ADObjectType.OrganizationalUnit) { Owner = "Domain Admins" });
        var admin = Add(admins, new ADObject("adm-bob", ADObjectType.User) { Tier = "Tier 0", AdminCount = true, AccountFlags = ADAccountFlags.NotDelegated,
                                                                             PwdLastSet = Now.AddDays(-30), LastLogonTimestamp = Now, Owner = "Domain Admins" });
        admin.MemberOf.Add("Domain Admins");
        Grant(admins, "Domain Users", "GenericAll", RightCategory.FullControl);

        // Un compte protégé : l'ACE héritée ne l'atteint pas, mais une GPO liée à l'OU, si
        var finding = Assert.Single(Audit(domain), f => f.Rule.Id == "ACL-02");
        Assert.Same(admins, finding.Object);
        Assert.Contains(LocalizationService.Instance["AttackPath.AnyUser"], finding.Detail);
    }

    [Fact]
    public void AdminSession_OnALessSensitiveMachine_IsExposed()
    {
        var domain = Domain();
        var admin = Admin(domain);
        var ws = Add(Find(domain, "Corp"), new ADObject("WS01", ADObjectType.Computer) { Tier = "Tier 2", LapsPasswordExpiration = Now.AddDays(10), Sessions = ["adm-alice", "jdoe"] });
        var server = Add(domain, new ADObject("SRV-T0", ADObjectType.Computer) { Tier = "Tier 0", LapsPasswordExpiration = Now.AddDays(10), Sessions = ["adm-alice"] });
        Find(domain, "DC01").Sessions = ["adm-alice"];

        var tier04 = Audit(domain).Where(f => f.Rule.Id == "TIER-04").ToList();

        // Sur le poste du Tier 2 seulement : un serveur du Tier 0 et un DC sont à leur place
        Assert.Same(admin, Assert.Single(tier04).Object);
        Assert.Contains("WS01", tier04[0].Detail);

        // Un compte du Tier 1 sur un poste du Tier 2 aussi
        var ops = Add(Find(domain, "Corp"), new ADObject("ops-srv", ADObjectType.User) { Tier = "Tier 1", PwdLastSet = Now.AddDays(-30), LastLogonTimestamp = Now });
        ws.Sessions!.Add(ops.Name);
        AssertFires(domain, "TIER-04", "ops-srv");
        server.Sessions!.Clear();
    }

    [Fact]
    public void ComputerWithoutLaps_IsReported_ButNotADcNorADisabledOne()
    {
        var domain = Domain();
        Add(Find(domain, "Corp"), new ADObject("WS01", ADObjectType.Computer) { PwdLastSet = Now.AddDays(-10) });
        Add(Find(domain, "Corp"), new ADObject("WS02", ADObjectType.Computer) { PwdLastSet = Now.AddDays(-10), LapsPasswordExpiration = Now.AddDays(5) });
        Add(Find(domain, "Corp"), new ADObject("WS03", ADObjectType.Computer) { PwdLastSet = Now.AddDays(-10), AccountFlags = ADAccountFlags.AccountDisabled });

        Assert.Equal(["WS01"], Audit(domain).Where(f => f.Rule.Id == "LAPS-01").Select(f => f.Object.Name));
    }

    [Fact]
    public void Owner_OfAGpoLinkedToTheDcs_MustBePrivileged()
    {
        var domain = Domain();
        Admin(domain);
        var gpo = Add(domain, new ADObject("GPO-DC", ADObjectType.Policy) { Tier = "Tier 0", Owner = "jdoe" });
        Find(domain, "Domain Controllers").LinkedGPOs.Add(gpo.Name);
        AssertFires(domain, "ACL-01", "GPO-DC");

        // Un compte d'administration du domaine est un propriétaire légitime
        gpo.Owner = "adm-alice";
        AssertSilent(domain, "ACL-01");
    }

    [Fact]
    public void LocalAdmin_FromALessSensitiveTier_IsATieringViolation()
    {
        var domain = Domain();
        Add(Find(domain, "Corp"), new ADObject("GG-Support", ADObjectType.Group) { Tier = "Tier 2" });
        Add(domain, new ADObject("SRV-T0", ADObjectType.Computer) { Tier = "Tier 0", LapsPasswordExpiration = Now.AddDays(10), LocalAdmins = ["Domain Admins", "GG-Support"] });

        var finding = Assert.Single(Audit(domain), f => f.Rule.Id == "TIER-03");
        Assert.Equal("SRV-T0", finding.Object.Name);
        Assert.Contains("GG-Support", finding.Detail);
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

    private static ADObject Pso(ADObject domain, string name, int precedence, int length, int maxAge, params string[] appliesTo)
    {
        var container = Flatten(domain).First(ADValidationService.IsPasswordSettingsContainer);
        var pso = Add(container, new ADObject(name, ADObjectType.PasswordSettingsObject)
        {
            PSOPrecedence = precedence, PSOMinPasswordLength = length, PSOMaxPasswordAgeDays = maxAge
        });
        foreach (var subject in appliesTo) pso.PSOAppliesTo.Add(subject);
        return pso;
    }

    [Fact]
    public void EffectivePasswordPolicy_FollowsAdPrecedence()
    {
        var domain = new ADDataService().CreateFreshDomainStructure("corp.local");
        var weak = Pso(domain, "PSO-Faible", 5, 6, 0, "Domain Admins");
        var strong = Pso(domain, "PSO-Fort", 1, 16, 365, "Domain Admins");
        var direct = Pso(domain, "PSO-Direct", 50, 20, 365, "Administrator");
        var context = new AuditContext(domain, Now);
        var admin = context.Objects.Single(o => o is { Name: "Administrator", Type: ADObjectType.User });

        Assert.Same(direct, context.EffectivePasswordPolicy(admin));           // un PSO sur le compte l'emporte sur ceux des groupes
        direct.PSOAppliesTo.Clear();
        Assert.Same(strong, new AuditContext(domain, Now).EffectivePasswordPolicy(admin)); // puis la plus petite priorité
        strong.PSOAppliesTo.Clear();
        Assert.Same(weak, new AuditContext(domain, Now).EffectivePasswordPolicy(admin));
        weak.PSOAppliesTo.Clear();
        Assert.Same(domain, new AuditContext(domain, Now).EffectivePasswordPolicy(admin)); // sinon la politique du domaine
    }

    [Theory]
    [InlineData(16, 365, false)]
    [InlineData(7, 365, true)]       // moins de 8 caractères
    [InlineData(16, 0, true)]        // jamais renouvelé
    [InlineData(16, 1200, true)]     // renouvelé tous les plus de 3 ans
    public void PrivilegedAccounts_NeedAStrongEffectivePolicy(int length, int maxAge, bool weak)
    {
        var domain = new ADDataService().CreateFreshDomainStructure("corp.local");
        domain.PSOMinPasswordLength = 12; // la politique du domaine n'est pas en cause
        Pso(domain, "PSO-Admins", 10, length, maxAge, "Domain Admins");

        var failed = new ADSecurityAuditService().Run(domain, Now).FailedRules.Select(r => r.Id).ToList();

        Assert.Equal(weak, failed.Contains("PWD-09"));
        Assert.DoesNotContain("PWD-08", failed);
    }

    [Fact]
    public void UnknownPasswordPolicy_IsNotJudged()
    {
        // Un document d'avant la 0.9.1 n'a pas de politique de mot de passe : les exercices fournis restent inchangés
        var domain = new ADDataService().CreateFreshDomainStructure("corp.local");
        domain.PSOMinPasswordLength = null;

        var failed = new ADSecurityAuditService().Run(domain, Now).FailedRules.Select(r => r.Id).ToList();

        Assert.DoesNotContain("PWD-08", failed);
        Assert.DoesNotContain("PWD-09", failed);
    }

    [Theory]
    [InlineData(10, true)]
    [InlineData(0, false)]
    [InlineData(null, false)]   // inconnu : document d'avant la 0.9.1
    public void MachineAccountQuota_AboveZero_IsReported(int? quota, bool reported)
    {
        var domain = new ADDataService().CreateFreshDomainStructure("corp.local");
        domain.MachineAccountQuota = quota;

        Assert.Equal(reported, new ADSecurityAuditService().Run(domain, Now).FailedRules.Any(r => r.Id == "ACC-03"));
    }

    [Fact]
    public void FreshDomain_IsNotCompliantOutOfTheBox()
    {
        // Constat pédagogique : un AD tout juste promu ne passe pas les points ANSSI de niveau 1, et sa politique
        // de mot de passe par défaut (7 caractères) est trop courte, pour tous comme pour l'administrateur
        var report = new ADSecurityAuditService().Run(new ADDataService().CreateFreshDomainStructure("corp.local"), Now);

        Assert.Equal(["ACC-03", "DLG-04", "PRIV-03", "PWD-01", "PWD-08", "PWD-09"], report.FailedRules.Select(r => r.Id).Order());
        Assert.Equal("corp.local", Assert.Single(report.Findings, f => f.Rule.Id == "PWD-08").Object.Name);
        Assert.Equal("corp.local", Assert.Single(report.Findings, f => f.Rule.Id == "ACC-03").Object.Name); // quota de 10 par défaut
        Assert.All(report.Findings.Where(f => f.Rule.Id is not ("PRIV-03" or "PWD-08" or "ACC-03")), f => Assert.Equal("Administrator", f.Object.Name));
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
        Assert.Contains("PWD-08", failed);   // politique du domaine jamais durcie
        Assert.Contains("ACC-03", failed);   // quota de jonction de machines par défaut
        Assert.DoesNotContain("PWD-09", failed); // les administrateurs ont leur PSO
        Assert.DoesNotContain("PWD-05", failed); // les DC renouvellent leur mot de passe
        // LAPS déployé sur le seul serveur d'applications ; adm-lmartin connecté au serveur de fichiers
        Assert.Contains(report.Findings, f => f.Rule.Id == "LAPS-01" && f.Object.Name == "SRV-FILE-01");
        Assert.DoesNotContain(report.Findings, f => f.Rule.Id == "LAPS-01" && f.Object.Name == "SRV-APP-01");
        Assert.Contains(report.Findings, f => f.Rule.Id == "TIER-04" && f.Object.Name == "adm-lmartin");
        Assert.DoesNotContain("ACL-03", failed);
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
