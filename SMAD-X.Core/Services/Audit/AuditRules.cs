using System;
using System.Collections.Generic;
using System.Linq;
using SMADX.Models;
using static SMADX.Services.Audit.AuditContext;

namespace SMADX.Services.Audit
{
    /// <summary>
    /// Catalogue des règles d'audit. Chaque règle cite les référentiels qui la fondent ; les identifiants
    /// ANSSI sont ceux des points de contrôle Active Directory publiés par le CERT-FR (niveau 1 = le plus
    /// critique). Les textes pédagogiques sont dans Content/{culture}/Audit.{Id}.md.
    /// </summary>
    public static class AuditRules
    {
        // ─── Références ───────────────────────────────────────────────────────────

        private const string AnssiChecklistUrl = "https://cert.cyber.gouv.fr/uploads/ad_checklist.html";
        private const string AnssiPa099Url = "https://messervices.cyber.gouv.fr/guides/recommandations-pour-ladministration-securisee-des-si-reposant-sur-ad";
        private const string Nist80053Url = "https://csrc.nist.gov/pubs/sp/800/53/r5/upd1/final";
        private const string Nist80063BUrl = "https://pages.nist.gov/800-63-4/sp800-63b.html";
        private const string CisControl5Url = "https://cas.docs.cisecurity.org/en/latest/source/Controls5/";
        private const string CisControl6Url = "https://cas.docs.cisecurity.org/en/latest/source/Controls6/";

        private static AuditReference Anssi(string id, string title) => new(AuditFramework.Anssi, id, title, AnssiChecklistUrl);
        private static readonly AuditReference AnssiPa099 = new(AuditFramework.Anssi, "ANSSI-PA-099",
            "Recommandations pour l'administration sécurisée des SI reposant sur AD (cloisonnement en tiers)", AnssiPa099Url);
        private static AuditReference Nist(string id, string title) => new(AuditFramework.Nist, $"SP 800-53 Rev. 5 {id}", title, Nist80053Url);
        private static readonly AuditReference Nist80063B = new(AuditFramework.Nist, "SP 800-63B",
            "Digital Identity Guidelines: Authentication and Authenticator Management", Nist80063BUrl);
        private static AuditReference Cis(string id, string title) =>
            new(AuditFramework.Cis, $"CIS Controls v8 {id}", title, id.StartsWith("6.") ? CisControl6Url : CisControl5Url);
        private static AuditReference Mitre(string id, string title) =>
            new(AuditFramework.MitreAttack, id, title, $"https://attack.mitre.org/techniques/{id.Replace('.', '/')}/");
        private static AuditReference Microsoft(string title, string url) => new(AuditFramework.Microsoft, "Microsoft Learn", title, url);

        private static readonly AuditReference MsProtectedAccounts = Microsoft("Appendix C - Protected Accounts and Groups in Active Directory",
            "https://learn.microsoft.com/en-us/windows-server/identity/ad-ds/plan/security-best-practices/appendix-c--protected-accounts-and-groups-in-active-directory");
        private static readonly AuditReference MsPrivilegedGroups = Microsoft("Active Directory Privileged Accounts and Groups Guide",
            "https://learn.microsoft.com/en-us/windows-server/identity/ad-ds/plan/security-best-practices/appendix-b--privileged-accounts-and-groups-in-active-directory");
        private static readonly AuditReference MsProtectedUsers = Microsoft("Protected Users Security Group",
            "https://learn.microsoft.com/en-us/windows-server/security/credentials-protection-and-management/protected-users-security-group");
        private static readonly AuditReference MsAccessModel = Microsoft("Securing privileged access: Enterprise access model",
            "https://learn.microsoft.com/en-us/security/privileged-access-workstations/privileged-access-access-model");
        private static readonly AuditReference MsConstrainedDelegation = Microsoft("Kerberos Constrained Delegation Overview",
            "https://learn.microsoft.com/en-us/windows-server/security/kerberos/kerberos-constrained-delegation-overview");
        private static readonly AuditReference MsKrbtgtReset = Microsoft("AD Forest Recovery - Reset the krbtgt password",
            "https://learn.microsoft.com/en-us/windows-server/identity/ad-ds/manage/forest-recovery-guide/ad-forest-recovery-reset-the-krbtgt-password");
        private static readonly AuditReference MsLeastPrivilege = Microsoft("Implementing Least-Privilege Administrative Models",
            "https://learn.microsoft.com/en-us/windows-server/identity/ad-ds/plan/security-best-practices/implementing-least-privilege-administrative-models");
        private static readonly AuditReference MsMachineAccountQuota = Microsoft("ms-DS-MachineAccountQuota attribute",
            "https://learn.microsoft.com/en-us/windows/win32/adschema/a-ms-ds-machineaccountquota");
        private static readonly AuditReference MsGmsa = Microsoft("Group Managed Service Accounts overview",
            "https://learn.microsoft.com/en-us/windows-server/security/group-managed-service-accounts/group-managed-service-accounts-overview");

        private static readonly AuditReference MitreKerberoasting = Mitre("T1558.003", "Steal or Forge Kerberos Tickets: Kerberoasting");
        private static readonly AuditReference MitreAsRepRoasting = Mitre("T1558.004", "Steal or Forge Kerberos Tickets: AS-REP Roasting");
        private static readonly AuditReference MitreGoldenTicket = Mitre("T1558.001", "Steal or Forge Kerberos Tickets: Golden Ticket");
        private static readonly AuditReference MitreKerberosTickets = Mitre("T1558", "Steal or Forge Kerberos Tickets");
        private static readonly AuditReference MitrePassTheTicket = Mitre("T1550.003", "Use Alternate Authentication Material: Pass the Ticket");
        private static readonly AuditReference MitreDomainAccounts = Mitre("T1078.002", "Valid Accounts: Domain Accounts");
        private static readonly AuditReference MitreAccountManipulation = Mitre("T1098", "Account Manipulation");
        private static readonly AuditReference MitreWindowsPermissions = Mitre("T1222.001", "File and Directory Permissions Modification: Windows Permissions");
        private static readonly AuditReference MitreGpoModification = Mitre("T1484.001", "Domain or Tenant Policy Modification: Group Policy Modification");

        private static readonly AuditReference NistAccountManagement = Nist("AC-2", "Account Management");
        private static readonly AuditReference NistSeparationOfDuties = Nist("AC-5", "Separation of Duties");
        private static readonly AuditReference NistLeastPrivilege = Nist("AC-6", "Least Privilege");
        private static readonly AuditReference NistPrivilegedAccounts = Nist("AC-6(5)", "Least Privilege | Privileged Accounts");
        private static readonly AuditReference NistAuthenticators = Nist("IA-5", "Authenticator Management");

        private static readonly AuditReference CisAdminAccounts = Cis("5.4", "Restrict Administrator Privileges to Dedicated Administrator Accounts");
        private static readonly AuditReference CisServiceAccounts = Cis("5.5", "Establish and Maintain an Inventory of Service Accounts");
        private static readonly AuditReference CisDormant = Cis("5.3", "Disable Dormant Accounts");
        private static readonly AuditReference CisUniquePasswords = Cis("5.2", "Use Unique Passwords");
        private static readonly AuditReference CisRbac = Cis("6.8", "Define and Maintain Role-Based Access Control");

        // ─── Seuils (jours) ───────────────────────────────────────────────────────

        /// <summary>CIS 5.3 : compte inactif depuis plus de 45 jours.</summary>
        public const int DormantDays = 45;
        /// <summary>ANSSI vuln_krbtgt : mot de passe krbtgt inchangé depuis plus d'un an.</summary>
        public const int KrbtgtMaxDays = 365;
        /// <summary>ANSSI vuln_password_change_priv : compte privilégié, mot de passe inchangé depuis plus de 3 ans.</summary>
        public const int PrivilegedPasswordMaxDays = 3 * 365;
        /// <summary>ANSSI vuln_password_change_dc_no_change : mot de passe machine d'un DC inchangé depuis plus de 45 jours.</summary>
        public const int DcPasswordMaxDays = 45;
        /// <summary>ANSSI vuln_password_change_server_no_change_90 / msa_no_change_90 : plus de 90 jours.</summary>
        public const int MachinePasswordMaxDays = 90;

        /// <summary>
        /// Longueur minimale d'un mot de passe : 8 caractères, plancher commun à NIST SP 800-63B, CIS Controls 5.2 et
        /// à l'ANSSI pour les comptes privilégiés (vuln_privileged_members_password). AD propose 7 par défaut.
        /// </summary>
        public const int MinPasswordLength = 8;

        /// <summary>ANSSI vuln_privileged_members_password : renouvellement au maximum tous les 3 ans.</summary>
        public const int PrivilegedPasswordRenewalMaxDays = 3 * 365;

        /// <summary>Politique trop faible pour un compte privilégié (ANSSI) ; une valeur inconnue n'est pas jugée.</summary>
        private static bool WeakForPrivileged(ADObject policy) =>
            policy.PSOMinPasswordLength is { } length &&
            (length < MinPasswordLength || policy.PSOMaxPasswordAgeDays is 0 || policy.PSOMaxPasswordAgeDays > PrivilegedPasswordRenewalMaxDays);

        /// <summary>Groupes qui doivent rester vides hors opérations ponctuelles (ANSSI, Microsoft annexe B).</summary>
        private static readonly string[] GroupsThatShouldBeEmpty =
            { "Schema Admins", "Enterprise Admins", "Account Operators", "Server Operators", "Print Operators", "Backup Operators" };

        /// <summary>Propriétaires légitimes d'un objet privilégié.</summary>
        private static readonly HashSet<string> AdministrativeOwners = new(StringComparer.OrdinalIgnoreCase)
            { "Domain Admins", "Enterprise Admins", "Administrators", "SYSTEM" };

        // ─── Helpers ──────────────────────────────────────────────────────────────

        private static IEnumerable<ADObject> EnabledUsers(AuditContext c) =>
            c.Objects.Where(o => o.Type == ADObjectType.User && IsEnabled(o));

        private static bool HasFlag(ADObject o, ADAccountFlags flag) => o.AccountFlags.HasFlag(flag);

        private static (ADObject, object[]) Finding(ADObject o, params object[] args) => (o, args);

        /// <summary>Hôte visé par un SPN « service/hôte[:port][/nom] ».</summary>
        private static string SpnHost(string spn)
        {
            var host = spn.Contains('/') ? spn[(spn.IndexOf('/') + 1)..] : spn;
            var end = host.IndexOfAny(new[] { ':', '/' });
            return end >= 0 ? host[..end] : host;
        }

        /// <summary>Contrôleur de domaine visé par un SPN de délégation, ou null.</summary>
        private static ADObject? TargetedDc(AuditContext c, string spn)
        {
            var host = SpnHost(spn);
            return c.DomainControllers.FirstOrDefault(dc =>
                string.Equals(host, dc.Name, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(host, $"{dc.Name}.{c.Domain.Name}", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Objets dont la compromission donne le contrôle du domaine (cible du propriétaire inadapté).</summary>
        private static IEnumerable<ADObject> CriticalObjects(AuditContext c) =>
            c.Objects.Where(o => o == c.Domain || c.ProtectedGroups.Contains(o) || c.IsPrivileged(o) || IsDomainController(o));

        // ─── Catalogue ────────────────────────────────────────────────────────────

        public static IReadOnlyList<AuditRule> All { get; } = new List<AuditRule>
        {
            // Kerberos
            new()
            {
                Id = "KRB-01", Severity = AuditSeverity.Critical, AnssiLevel = 1,
                References = new[] { Anssi("vuln_spn_priv", "Comptes privilégiés avec SPN"), MitreKerberoasting, NistPrivilegedAccounts, CisAdminAccounts, MsGmsa },
                Evaluate = c => EnabledUsers(c)
                    .Where(o => o.Name != "krbtgt" && o.ServicePrincipalNames.Count > 0 && c.IsPrivileged(o))
                    .Select(o => Finding(o, string.Join(", ", o.ServicePrincipalNames)))
            },
            new()
            {
                Id = "KRB-02", Severity = AuditSeverity.Medium,
                References = new[] { Anssi("info_spn_present", "Comptes utilisateurs avec SPN"), MitreKerberoasting, NistAuthenticators, CisServiceAccounts, MsGmsa },
                Evaluate = c => EnabledUsers(c)
                    .Where(o => o.Name != "krbtgt" && o.ServicePrincipalNames.Count > 0 && !c.IsPrivileged(o))
                    .Select(o => Finding(o, string.Join(", ", o.ServicePrincipalNames)))
            },
            new()
            {
                Id = "KRB-03", Severity = AuditSeverity.Critical, AnssiLevel = 1,
                References = new[] { Anssi("vuln_kerberos_properties_preauth_priv", "Comptes privilégiés sans préauthentification Kerberos"), MitreAsRepRoasting, NistPrivilegedAccounts },
                Evaluate = c => c.Objects
                    .Where(o => o.IsAccount && IsEnabled(o) && HasFlag(o, ADAccountFlags.DontRequirePreauth) && c.IsPrivileged(o))
                    .Select(o => Finding(o))
            },
            new()
            {
                Id = "KRB-04", Severity = AuditSeverity.High, AnssiLevel = 2,
                References = new[] { Anssi("vuln_kerberos_properties_preauth", "Comptes sans préauthentification Kerberos"), MitreAsRepRoasting, NistAuthenticators },
                Evaluate = c => c.Objects
                    .Where(o => o.IsAccount && IsEnabled(o) && HasFlag(o, ADAccountFlags.DontRequirePreauth) && !c.IsPrivileged(o))
                    .Select(o => Finding(o))
            },
            new()
            {
                Id = "KRB-05", Severity = AuditSeverity.High, AnssiLevel = 2,
                References = new[] { Anssi("vuln_krbtgt", "Mot de passe du compte krbtgt inchangé depuis plus d'un an"), MitreGoldenTicket, NistAuthenticators, MsKrbtgtReset },
                Evaluate = c => c.Objects
                    .Where(o => o.Type == ADObjectType.User && o.Name == "krbtgt" && c.AgeInDays(o.PwdLastSet) > KrbtgtMaxDays)
                    .Select(o => Finding(o, c.AgeInDays(o.PwdLastSet)!))
            },

            // Délégation
            new()
            {
                Id = "DLG-01", Severity = AuditSeverity.Critical, AnssiLevel = 1,
                References = new[] { Anssi("vuln_delegation_t4d", "Délégation d'authentification non contrainte"), MitreKerberosTickets, MitrePassTheTicket, NistLeastPrivilege, MsProtectedUsers },
                Evaluate = c => c.Objects
                    .Where(o => o.IsAccount && HasFlag(o, ADAccountFlags.TrustedForDelegation) && !IsDomainController(o))
                    .Select(o => Finding(o))
            },
            new()
            {
                Id = "DLG-02", Severity = AuditSeverity.Critical, AnssiLevel = 1,
                References = new[] { Anssi("vuln_delegation_t2a4d", "Délégation d'authentification contrainte avec transition de protocole vers un service privilégié"), MitreKerberosTickets, NistLeastPrivilege, MsConstrainedDelegation },
                Evaluate = c => c.Objects
                    .Where(o => o.IsAccount && HasFlag(o, ADAccountFlags.TrustedToAuthForDelegation))
                    .SelectMany(o => o.AllowedToDelegateTo.Select(spn => (o, spn, dc: TargetedDc(c, spn))))
                    .Where(x => x.dc != null)
                    .Select(x => Finding(x.o, x.spn, x.dc!.Name))
            },
            new()
            {
                Id = "DLG-03", Severity = AuditSeverity.Critical, AnssiLevel = 1,
                References = new[] { Anssi("vuln_delegation_a2d2", "Délégation d'authentification contrainte vers un service privilégié"), MitreKerberosTickets, NistLeastPrivilege, MsConstrainedDelegation },
                Evaluate = c => c.Objects
                    .Where(o => o.IsAccount && !HasFlag(o, ADAccountFlags.TrustedToAuthForDelegation) && !IsDomainController(o))
                    .SelectMany(o => o.AllowedToDelegateTo.Select(spn => (o, spn, dc: TargetedDc(c, spn))))
                    .Where(x => x.dc != null)
                    .Select(x => Finding(x.o, x.spn, x.dc!.Name))
            },
            new()
            {
                Id = "DLG-04", Severity = AuditSeverity.Medium, AnssiLevel = 3,
                References = new[] { Anssi("vuln_protected_users", "Comptes privilégiés non membres du groupe Protected Users"), MitrePassTheTicket, NistPrivilegedAccounts, MsProtectedUsers },
                Evaluate = c => EnabledUsers(c)
                    .Where(o => o.Name != "krbtgt" && c.IsPrivileged(o) && !HasFlag(o, ADAccountFlags.NotDelegated)
                                && !(c.Group("Protected Users") is { } pu && c.EffectiveGroups(o).Contains(pu)))
                    .Select(o => Finding(o))
            },

            // Mots de passe
            new()
            {
                Id = "PWD-01", Severity = AuditSeverity.Critical, AnssiLevel = 1,
                References = new[] { Anssi("vuln_dont_expire_priv", "Comptes privilégiés dont le mot de passe n'expire jamais"), NistPrivilegedAccounts, NistAuthenticators, Nist80063B },
                Evaluate = c => EnabledUsers(c)
                    .Where(o => HasFlag(o, ADAccountFlags.PasswordNeverExpires) && c.IsPrivileged(o))
                    .Select(o => Finding(o))
            },
            new()
            {
                Id = "PWD-02", Severity = AuditSeverity.High, AnssiLevel = 2,
                References = new[] { Anssi("vuln_dont_expire", "Comptes dont le mot de passe n'expire jamais"), NistAuthenticators, Nist80063B },
                Evaluate = c => EnabledUsers(c)
                    .Where(o => HasFlag(o, ADAccountFlags.PasswordNeverExpires) && !c.IsPrivileged(o))
                    .Select(o => Finding(o))
            },
            new()
            {
                Id = "PWD-03", Severity = AuditSeverity.High,
                References = new[] { MitreDomainAccounts, NistAuthenticators, CisUniquePasswords },
                Evaluate = c => c.Objects
                    .Where(o => o.IsAccount && IsEnabled(o) && HasFlag(o, ADAccountFlags.PasswordNotRequired))
                    .Select(o => Finding(o))
            },
            new()
            {
                Id = "PWD-04", Severity = AuditSeverity.Critical, AnssiLevel = 1,
                References = new[] { Anssi("vuln_password_change_priv", "Comptes privilégiés dont le mot de passe est inchangé depuis plus de 3 ans"), NistPrivilegedAccounts, NistAuthenticators },
                Evaluate = c => EnabledUsers(c)
                    .Where(o => c.IsPrivileged(o) && c.AgeInDays(o.PwdLastSet) > PrivilegedPasswordMaxDays)
                    .Select(o => Finding(o, c.AgeInDays(o.PwdLastSet)!))
            },
            new()
            {
                Id = "PWD-05", Severity = AuditSeverity.Critical, AnssiLevel = 1,
                References = new[] { Anssi("vuln_password_change_dc_no_change", "Contrôleurs de domaine dont le mot de passe de compte d'ordinateur est inchangé depuis plus de 45 jours"), NistAuthenticators },
                Evaluate = c => c.DomainControllers
                    .Where(o => c.AgeInDays(o.PwdLastSet) > DcPasswordMaxDays)
                    .Select(o => Finding(o, c.AgeInDays(o.PwdLastSet)!))
            },
            new()
            {
                Id = "PWD-06", Severity = AuditSeverity.High, AnssiLevel = 2,
                References = new[] { Anssi("vuln_password_change_server_no_change_90", "Serveurs dont le mot de passe de compte d'ordinateur est inchangé depuis plus de 90 jours"), NistAccountManagement, CisDormant },
                Evaluate = c => c.Objects
                    .Where(o => o.Type == ADObjectType.Computer && !IsDomainController(o) && IsEnabled(o) && c.AgeInDays(o.PwdLastSet) > MachinePasswordMaxDays)
                    .Select(o => Finding(o, c.AgeInDays(o.PwdLastSet)!))
            },
            new()
            {
                Id = "PWD-07", Severity = AuditSeverity.High, AnssiLevel = 2,
                References = new[] { Anssi("vuln_password_change_msa_no_change_90", "Comptes de service managés dont le mot de passe de compte est inchangé depuis plus de 90 jours"), NistAuthenticators, MsGmsa },
                Evaluate = c => c.Objects
                    .Where(o => o.Type == ADObjectType.GMSA && c.AgeInDays(o.PwdLastSet) > MachinePasswordMaxDays)
                    .Select(o => Finding(o, c.AgeInDays(o.PwdLastSet)!))
            },

            // Politique de mot de passe
            new()
            {
                Id = "PWD-08", Severity = AuditSeverity.Medium,
                References = new[] { Nist80063B, CisUniquePasswords, NistAuthenticators },
                Evaluate = c => c.Domain.PSOMinPasswordLength is { } length && length < MinPasswordLength
                    ? new[] { Finding(c.Domain, length) }
                    : Array.Empty<(ADObject, object[])>()
            },
            new()
            {
                Id = "PWD-09", Severity = AuditSeverity.High, AnssiLevel = 2,
                References = new[] { Anssi("vuln_privileged_members_password", "Comptes membres de groupes privilégiés avec une mauvaise politique de mot de passe"), Nist80063B, NistPrivilegedAccounts },
                Evaluate = c => c.PrivilegedAccounts
                    .Where(o => o.Type == ADObjectType.User && IsEnabled(o))
                    .Select(o => (o, policy: c.EffectivePasswordPolicy(o)))
                    .Where(x => WeakForPrivileged(x.policy))
                    .Select(x => Finding(x.o, x.policy.Name, x.policy.PSOMinPasswordLength!, x.policy.PSOMaxPasswordAgeDays ?? 0))
            },

            // Comptes
            new()
            {
                Id = "ACC-03", Severity = AuditSeverity.Low, AnssiLevel = 4,
                References = new[] { Anssi("vuln_user_accounts_machineaccountquota", "Ajout de machines au domaine non limité"), MsMachineAccountQuota,
                                     Mitre("T1136.002", "Create Account: Domain Account"), NistAccountManagement },
                Evaluate = c => c.Domain.MachineAccountQuota is > 0 and var quota
                    ? new[] { Finding(c.Domain, quota) }
                    : Array.Empty<(ADObject, object[])>()
            },
            new()
            {
                Id = "ACC-01", Severity = AuditSeverity.High, AnssiLevel = 2,
                References = new[] { Anssi("vuln_guest", "Compte Invité actif"), NistAccountManagement, MitreDomainAccounts },
                Evaluate = c => EnabledUsers(c).Where(o => o.Name == "Guest").Select(o => Finding(o))
            },
            new()
            {
                Id = "ACC-02", Severity = AuditSeverity.Medium,
                References = new[] { Anssi("info_user_accounts_dormant", "Comptes actifs inutilisés"), CisDormant, NistAccountManagement, MitreDomainAccounts },
                Evaluate = c => EnabledUsers(c)
                    .Where(o => o.Name != "krbtgt")
                    // Jamais connecté : on compte depuis la création du compte
                    .Select(o => (o, days: c.AgeInDays(o.LastLogonTimestamp ?? o.CreatedDate)))
                    .Where(x => x.days > DormantDays)
                    .Select(x => Finding(x.o, x.days!, x.o.LastLogonTimestamp?.ToString("yyyy-MM-dd") ?? "—"))
            },

            // Privilèges
            new()
            {
                Id = "PRIV-01", Severity = AuditSeverity.High, AnssiLevel = 2,
                References = new[] { Anssi("vuln_privileged_members_no_admincount", "Comptes membres de groupes privilégiés ayant un attribut adminCount vide ou valant 0"), MsProtectedAccounts, NistPrivilegedAccounts },
                Evaluate = c => c.PrivilegedAccounts.Where(o => !o.AdminCount).Select(o => Finding(o))
            },
            new()
            {
                Id = "PRIV-02", Severity = AuditSeverity.Low,
                References = new[] { Anssi("warning_admincount", "Comptes ayant la propriété adminCount"), MsProtectedAccounts, NistAccountManagement },
                Evaluate = c => c.Objects
                    .Where(o => o.AdminCount && o.IsAccount && !c.IsPrivileged(o) && !IsDomainController(o) && o.Name != "krbtgt")
                    .Select(o => Finding(o))
            },
            new()
            {
                Id = "PRIV-03", Severity = AuditSeverity.Medium, AnssiLevel = 3,
                References = new[] { Anssi("warning_privileged_members_not_empty", "Groupes privilégiés non vides"), MsPrivilegedGroups, NistPrivilegedAccounts, CisAdminAccounts },
                Evaluate = c => GroupsThatShouldBeEmpty
                    .Select(c.Group).OfType<ADObject>()
                    .Select(g => (g, members: c.DirectMembers(g).Select(m => m.Name).ToList()))
                    .Where(x => x.members.Count > 0)
                    .Select(x => Finding(x.g, string.Join(", ", x.members)))
            },
            new()
            {
                Id = "PRIV-04", Severity = AuditSeverity.Critical, AnssiLevel = 1,
                References = new[] { Anssi("vuln_dnsadmins", "Permissions dangereuses sur le groupe DnsAdmins"), MsPrivilegedGroups, NistLeastPrivilege },
                Evaluate = c => c.Group("DnsAdmins") is not { } dnsAdmins
                    ? Enumerable.Empty<(ADObject, object[])>()
                    : c.DirectMembers(dnsAdmins).Where(m => !c.IsPrivileged(m)).Select(m => Finding(m))
            },
            new()
            {
                Id = "PRIV-05", Severity = AuditSeverity.Medium,
                References = new[] { MsPrivilegedGroups, AnssiPa099, MsAccessModel },
                Evaluate = c => c.ProtectedGroups
                    .Where(g => TierLevel(g) is > 0)
                    .Select(g => Finding(g, g.Tier!))
            },

            // Propriétaires et cloisonnement
            new()
            {
                Id = "ACL-01", Severity = AuditSeverity.Medium, AnssiLevel = 3,
                References = new[] { Anssi("vuln_owner", "Objets ayant un propriétaire inadapté"), MitreWindowsPermissions, MitreAccountManipulation, NistLeastPrivilege },
                Evaluate = c => CriticalObjects(c)
                    .Where(o => !string.IsNullOrWhiteSpace(o.Owner) && !AdministrativeOwners.Contains(o.Owner!))
                    .Select(o => Finding(o, o.Owner!))
            },
            new()
            {
                Id = "TIER-01", Severity = AuditSeverity.High,
                References = new[] { AnssiPa099, MsAccessModel, NistSeparationOfDuties, NistLeastPrivilege, CisRbac },
                Evaluate = c => c.Objects
                    .Where(o => o.IsAccount || o.Type == ADObjectType.Group)
                    .SelectMany(o => c.EffectiveGroups(o).Select(g => (o, g)))
                    .Where(x => TierLevel(x.o) is { } member && TierLevel(x.g) is { } group && group < member)
                    .Select(x => Finding(x.o, x.o.Tier!, x.g.Name, x.g.Tier!))
            },
            new()
            {
                Id = "TIER-02", Severity = AuditSeverity.High,
                References = new[] { Anssi("vuln_permissions_gpo_priv", "Permissions dangereuses sur les objets de GPO s'appliquant aux membres des groupes privilégiés"), AnssiPa099, MitreGpoModification, MsAccessModel },
                Evaluate = c => c.Objects
                    .Where(o => o.Type is ADObjectType.OrganizationalUnit or ADObjectType.Domain)
                    .SelectMany(ou => ou.LinkedGPOs.Select(name => (ou, gpo: c.Objects.FirstOrDefault(p => p.Type == ADObjectType.Policy && p.Name == name))))
                    .Where(x => x.gpo != null && TierLevel(x.ou) is { } ouTier && TierLevel(x.gpo) is { } gpoTier && gpoTier > ouTier)
                    .Select(x => Finding(x.gpo!, x.gpo!.Tier!, x.ou.Name, x.ou.Tier!))
            },
            new()
            {
                Id = "TIER-03", Severity = AuditSeverity.High,
                References = new[] { Anssi("vuln_privileged_members_perm", "Permissions dangereuses sur des membres de groupes privilégiés"), AnssiPa099, MitreAccountManipulation, NistLeastPrivilege, MsLeastPrivilege },
                Evaluate = c => c.Objects
                    .SelectMany(o => o.Delegations.Where(d => !d.IsInherited).Select(d => (o, d)))
                    .Where(x => x.d.RightCategory is RightCategory.FullControl or RightCategory.AttributeWrite or RightCategory.PasswordReset)
                    .Select(x => (x.o, x.d, trustee: c.Principal(x.d.TrusteeName)))
                    .Where(x => x.trustee != null && TierLevel(x.o) is { } target && TierLevel(x.trustee) is { } trustee && trustee > target)
                    .Select(x => Finding(x.o, x.d.TrusteeName, x.trustee!.Tier!, x.d.Right, x.o.Tier!))
            },

            // Groupes
            new()
            {
                Id = "GRP-01", Severity = AuditSeverity.Low, AnssiLevel = 4,
                References = new[] { Anssi("vuln_group_loop", "Présences de boucles d'imbrication de groupes"), NistAccountManagement },
                Evaluate = c => c.Objects
                    .Where(o => o.Type == ADObjectType.Group && c.EffectiveGroups(o).Any(g => c.EffectiveGroups(g).Contains(o)))
                    .Select(o => Finding(o))
            }
        };
    }
}
