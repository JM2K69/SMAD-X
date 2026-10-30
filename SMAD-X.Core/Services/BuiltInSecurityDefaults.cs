using System;
using System.Collections.Generic;
using System.Linq;
using SMADX.Models;

namespace SMADX.Services
{
    /// <summary>
    /// Attributs de sécurité qu'Active Directory pose lui-même sur un domaine fraîchement promu :
    /// options des comptes intégrés, SPN des ordinateurs, portée des groupes, adminCount des
    /// objets protégés par AdminSDHolder, ACE par défaut qui donnent un contrôle, administrateurs
    /// locaux des ordinateurs joints. Appliqué aux structures générées par ADDataService, pour que
    /// l'apprenant parte d'un état réaliste : l'audit y trouve ce qu'il trouverait sur un vrai AD neuf.
    /// </summary>
    public static class BuiltInSecurityDefaults
    {
        /// <summary>Groupes protégés par AdminSDHolder (SDProp leur pose adminCount = 1).</summary>
        public static readonly IReadOnlySet<string> ProtectedGroups = new HashSet<string>
        {
            "Account Operators", "Administrators", "Backup Operators", "Domain Admins",
            "Domain Controllers", "Enterprise Admins", "Enterprise Key Admins", "Key Admins",
            "Print Operators", "Read-only Domain Controllers", "Replicator", "Schema Admins",
            "Server Operators"
        };

        private static readonly IReadOnlyDictionary<string, ADGroupScope> NonGlobalGroups = new Dictionary<string, ADGroupScope>
        {
            ["Enterprise Admins"] = ADGroupScope.Universal,
            ["Schema Admins"] = ADGroupScope.Universal,
            ["Enterprise Key Admins"] = ADGroupScope.Universal,
            ["Enterprise Read-only Domain Controllers"] = ADGroupScope.Universal,
            ["Cert Publishers"] = ADGroupScope.DomainLocal,
            ["RAS and IAS Servers"] = ADGroupScope.DomainLocal,
            ["Allowed RODC Password Replication Group"] = ADGroupScope.DomainLocal,
            ["Denied RODC Password Replication Group"] = ADGroupScope.DomainLocal,
            ["DnsAdmins"] = ADGroupScope.DomainLocal
        };

        private static readonly IReadOnlyDictionary<string, ADAccountFlags> BuiltInUsers = new Dictionary<string, ADAccountFlags>
        {
            // userAccountControl réel : 66048, 66082, 514, 66082, 66050
            ["Administrator"] = ADAccountFlags.PasswordNeverExpires,
            ["Guest"] = ADAccountFlags.AccountDisabled | ADAccountFlags.PasswordNotRequired | ADAccountFlags.PasswordNeverExpires,
            ["krbtgt"] = ADAccountFlags.AccountDisabled
        };

        /// <summary>Comptes intégrés membres de groupes protégés dès la promotion.</summary>
        private static readonly IReadOnlySet<string> ProtectedUsers = new HashSet<string> { "Administrator", "krbtgt" };

        /// <summary>
        /// Appartenances créées par la promotion du domaine (objet → groupes). Les comptes et groupes les plus sensibles
        /// sont membres de Denied RODC Password Replication Group : un RODC ne met jamais leurs secrets en cache.
        /// </summary>
        private static readonly IReadOnlyDictionary<string, string[]> BuiltInMemberships = new Dictionary<string, string[]>
        {
            ["Administrator"] = new[] { "Administrators", "Domain Admins", "Enterprise Admins", "Schema Admins", "Group Policy Creator Owners" },
            ["Domain Admins"] = new[] { "Administrators", DeniedRodc },
            ["Enterprise Admins"] = new[] { "Administrators", DeniedRodc },
            ["Domain Users"] = new[] { "Users" },
            ["Guest"] = new[] { "Guests", "Domain Guests" },
            ["Domain Guests"] = new[] { "Guests" },
            ["krbtgt"] = new[] { DeniedRodc },
            ["Schema Admins"] = new[] { DeniedRodc },
            ["Domain Controllers"] = new[] { DeniedRodc },
            ["Read-only Domain Controllers"] = new[] { DeniedRodc },
            ["Cert Publishers"] = new[] { DeniedRodc },
            ["Group Policy Creator Owners"] = new[] { DeniedRodc }
        };

        private const string DeniedRodc = "Denied RODC Password Replication Group";

        /// <summary>
        /// Groupes de Builtin propres aux contrôleurs de domaine, créés par la promotion (propriétaire : Domain Admins). Les
        /// autres viennent des groupes locaux du serveur promu et gardent pour propriétaire le groupe Administrators.
        /// </summary>
        private static readonly IReadOnlySet<string> DomainOnlyBuiltinGroups = new HashSet<string>
        {
            "Account Operators", "Server Operators", "Incoming Forest Trust Builders", "Pre-Windows 2000 Compatible Access",
            "Terminal Server License Servers", "Windows Authorization Access Group"
        };

        /// <summary>Comptes locaux du serveur promu devenus comptes du domaine : leur propriétaire reste Administrators.</summary>
        private static readonly IReadOnlySet<string> LocalAccounts = new HashSet<string> { "Administrator", "Guest" };

        /// <summary>
        /// Propriétaire d'un objet créé par l'installation, comme dans un vrai domaine neuf : Administrators pour ce qui vient
        /// des comptes et groupes locaux du serveur (et le conteneur Builtin), SYSTEM pour ce que crée le rôle DNS, Enterprise
        /// Admins pour SOA-Policies, Domain Admins pour le reste. SDProp donne ensuite Domain Admins, le propriétaire
        /// d'AdminSDHolder, aux comptes et groupes protégés (voir <see cref="Apply"/>).
        /// </summary>
        private static string DefaultOwner(ADObject o) => o switch
        {
            { Type: ADObjectType.Container, Name: "Builtin" } => "Administrators",
            { Type: ADObjectType.Group, Parent: { Type: ADObjectType.Container, Name: "Builtin" } } when !DomainOnlyBuiltinGroups.Contains(o.Name) => "Administrators",
            { Type: ADObjectType.User, Parent: { Type: ADObjectType.Container, Name: "Users" } } when LocalAccounts.Contains(o.Name) => "Administrators",
            { Name: "DnsAdmins" or "DnsUpdateProxy", Type: ADObjectType.Group } or { Name: "MicrosoftDNS", Type: ADObjectType.Container } => "SYSTEM",
            { Name: "SOA-Policies", Type: ADObjectType.Container } => "Enterprise Admins",
            _ => "Domain Admins"
        };

        public static void Apply(ADObject domain)
        {
            var fqdn = domain.Name;
            var promotion = domain.CreatedDate;
            ApplyBuiltInMemberships(domain);
            foreach (var obj in Flatten(domain).Where(o => o != domain))
            {
                obj.Owner = DefaultOwner(obj);

                // Tous les mots de passe datent de la promotion ; seuls l'administrateur et les DC se sont connectés
                if (obj.IsAccount)
                {
                    obj.PwdLastSet = promotion;
                    var isDc = obj.Parent is { Type: ADObjectType.OrganizationalUnit, Name: "Domain Controllers" };
                    if (isDc || (obj.Type == ADObjectType.User && obj.Name == "Administrator"))
                        obj.LastLogonTimestamp = promotion;
                }

                switch (obj.Type)
                {
                    case ADObjectType.Group:
                        obj.GroupCategory = ADGroupCategory.Security;
                        obj.GroupScope = obj.Parent is { Type: ADObjectType.Container, Name: "Builtin" }
                            ? ADGroupScope.DomainLocal
                            : NonGlobalGroups.GetValueOrDefault(obj.Name, ADGroupScope.Global);
                        obj.AdminCount = ProtectedGroups.Contains(obj.Name);
                        break;

                    case ADObjectType.User:
                        if (BuiltInUsers.TryGetValue(obj.Name, out var flags))
                            obj.AccountFlags = flags;
                        obj.AdminCount = ProtectedUsers.Contains(obj.Name);
                        if (obj.Name == "krbtgt")
                            obj.ServicePrincipalNames = new() { "kadmin/changepw" };
                        break;

                    case ADObjectType.Computer:
                        var isDc = obj.Parent is { Type: ADObjectType.OrganizationalUnit, Name: "Domain Controllers" };
                        obj.ServicePrincipalNames = new(ComputerSpns(obj.Name, fqdn, isDc));
                        // Joindre une machine au domaine ajoute Domain Admins à son groupe local Administrateurs ;
                        // un DC n'a pas de groupe local : c'est le groupe Builtin Administrators qui l'administre
                        obj.LocalAdmins = new() { isDc ? "Administrators" : "Domain Admins" };
                        // Un DC est en délégation non contrainte : c'est normal pour lui, pas ailleurs. Il n'est pas protégé
                        // par AdminSDHolder : SDProp ignore l'appartenance par le groupe principal (Domain Controllers)
                        if (isDc)
                            obj.AccountFlags = ADAccountFlags.TrustedForDelegation;
                        break;

                    case ADObjectType.GMSA:
                        // Un gMSA porte souvent un SPN, sans risque de Kerberoasting : son mot de passe
                        // fait 120 caractères aléatoires et change automatiquement
                        obj.ServicePrincipalNames = new() { $"HTTP/{obj.Name}.{fqdn}" };
                        break;
                }

                // SDProp a tourné (dans l'heure qui suit la promotion) : un compte ou un groupe protégé a le propriétaire
                // et les permissions d'AdminSDHolder, sans héritage
                if (obj.AdminCount)
                    obj.Owner = "Domain Admins";
            }
            ApplyDefaultAcl(domain);
        }

        /// <summary>
        /// ACE explicites d'un domaine neuf, sur sa racine et ses conteneurs, telles que l'import les lit dans un vrai
        /// domaine (il écarte, comme ici, SYSTEM, Administrators, CREATOR OWNER, SELF et Enterprise Domain Controllers, et
        /// les droits par défaut des comptes, groupes et ordinateurs, les mêmes dans tout domaine). Relevées sur un
        /// Windows Server 2025 tout juste promu :
        /// <list type="bullet">
        /// <item>la racine (et Builtin, qui en reprend les droits) : Enterprise Admins contrôle total, Domain Admins
        /// modification des permissions, réplication complète des Domain Controllers (le DCSync qui est leur rôle) et sans les
        /// secrets des RODC de l'entreprise, droits étendus d'Authenticated Users sur les mots de passe, d'Incoming Forest
        /// Trust Builders sur les approbations, de Cloneable Domain Controllers ; Key Admins et Enterprise Key Admins écrivent
        /// msDS-KeyCredentialLink (Windows Hello Entreprise) ;</item>
        /// <item>AdminSDHolder, modèle des comptes protégés : Domain Admins et Enterprise Admins modifient les permissions,
        /// Everyone change son mot de passe, Cert Publishers et Terminal Server License Servers écrivent leurs attributs ;</item>
        /// <item>les conteneurs : Account Operators y crée comptes, groupes et comptes de service, Print Operators des files
        /// d'impression, Group Policy Creator Owners des GPO et des filtres WMI, DnsAdmins gère MicrosoftDNS, les
        /// ordinateurs sauvegardent leur TPM ;</item>
        /// <item>les deux GPO par défaut, que Domain Admins et Enterprise Admins modifient ;</item>
        /// <item>les comptes et groupes protégés, une fois SDProp passé : les droits d'AdminSDHolder.</item>
        /// </list>
        /// Ce qui donne un contrôle va à des groupes privilégiés : sur un domaine neuf, ni le graphe ni l'audit n'y voient de
        /// faiblesse, comme sur un vrai.
        /// </summary>
        private static void ApplyDefaultAcl(ADObject domain)
        {
            var all = Flatten(domain).ToList();
            var groups = all.Where(o => o.Type == ADObjectType.Group).Select(o => o.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var wellKnown = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Authenticated Users", "Everyone" };
            void Allow(ADObject? target, string trustee, string right, RightCategory category)
            {
                if (target == null || !groups.Contains(trustee) && !wellKnown.Contains(trustee)
                    || target.Delegations.Any(d => d.TrusteeName == trustee && d.Right == right)) return;
                target.Delegations.Add(new ADDelegation
                {
                    TrusteeName = trustee, TrusteeType = TrusteeType.Group, TargetDN = target.DistinguishedName,
                    Right = right, RightCategory = category, Tier = target.Tier
                });
            }
            // Conteneur au chemin donné depuis la racine (null s'il n'existe pas dans ce domaine)
            ADObject? At(params string[] path)
            {
                var current = domain;
                foreach (var name in path)
                    if ((current = current.Children.FirstOrDefault(c => c.Name == name && c.Type is ADObjectType.Container or ADObjectType.OrganizationalUnit)) == null)
                        return null;
                return current;
            }

            // La racine du domaine, et Builtin qui en reprend les droits
            foreach (var root in new[] { domain, At("Builtin") })
            {
                Allow(root, "Enterprise Admins", "GenericAll", RightCategory.FullControl);
                Allow(root, "Domain Admins", "WriteDacl", RightCategory.WriteDacl);
                Allow(root, "Domain Controllers", "DS-Replication-Get-Changes-All", RightCategory.Replication);
                Allow(root, "Enterprise Read-only Domain Controllers", "DS-Replication-Get-Changes", RightCategory.Other);
                foreach (var right in new[] { "Update-Password-Not-Required-Bit", "Unexpire-Password", "Enable-Per-User-Reversibly-Encrypted-Password" })
                    Allow(root, "Authenticated Users", right, RightCategory.Other);
                Allow(root, "Incoming Forest Trust Builders", "Create-Inbound-Forest-Trust", RightCategory.Other);
            }
            Allow(domain, "Cloneable Domain Controllers", "DS-Clone-Domain-Controller", RightCategory.Other);
            Allow(domain, "Key Admins", "WriteProperty:msDS-KeyCredentialLink", RightCategory.WriteKeyCredentialLink);
            Allow(domain, "Enterprise Key Admins", "WriteProperty:msDS-KeyCredentialLink", RightCategory.WriteKeyCredentialLink);

            // Conteneurs des comptes : Account Operators y crée et supprime comptes et groupes, Print Operators des files d'impression
            foreach (var container in new[] { At("Users"), At("Computers") })
            {
                Allow(container, "Domain Admins", "WriteDacl", RightCategory.WriteDacl);
                foreach (var cls in new[] { "user", "group", "inetOrgPerson" })
                    Allow(container, "Account Operators", $"CreateChild:{cls}", RightCategory.Other);
                Allow(container, "Print Operators", "CreateChild:printQueue", RightCategory.Other);
            }
            Allow(At("Computers"), "Account Operators", "CreateChild:computer", RightCategory.ComputerManagement);
            Allow(At("Managed Service Accounts"), "Domain Admins", "WriteDacl", RightCategory.WriteDacl);
            foreach (var cls in new[] { "user", "group", "msDS-ManagedServiceAccount" })
                Allow(At("Managed Service Accounts"), "Account Operators", $"CreateChild:{cls}", RightCategory.Other);
            Allow(At("Domain Controllers"), "Domain Admins", "WriteDacl", RightCategory.WriteDacl);
            foreach (var container in new[] { At("ForeignSecurityPrincipals"), At("Program Data"), At("Program Data", "Microsoft") })
                Allow(container, "Domain Admins", "GenericAll", RightCategory.FullControl);
            foreach (var trustee in new[] { "Domain Admins", "Domain Controllers", "Enterprise Admins", "Key Admins", "Enterprise Key Admins" })
                Allow(At("Keys"), trustee, "GenericAll", RightCategory.FullControl);

            // System et ses conteneurs
            foreach (var path in new[]
            {
                new[] { "System" }, ["System", "ComPartitions"], ["System", "ComPartitionSets"], ["System", "DomainUpdates"],
                ["System", "DomainUpdates", "ActiveDirectoryUpdate"], ["System", "DomainUpdates", "Windows2003Update"],
                ["System", "IP Security"], ["System", "Meetings"], ["System", "Policies"], ["System", "RpcServices"],
                ["System", "WinsockServices"], ["System", "WMIPolicy", "PolicyTemplate"], ["System", "WMIPolicy", "PolicyType"],
                ["System", "WMIPolicy", "WMIGPO"]
            })
                Allow(At(path), "Domain Admins", "WriteDacl", RightCategory.WriteDacl);
            Allow(At("System", "Policies"), "Group Policy Creator Owners", "CreateChild", RightCategory.Other);
            // Les deux GPO par défaut : Domain Admins et Enterprise Admins en modifient les paramètres et les permissions
            foreach (var gpo in At("System", "Policies")?.Children.Where(o => o.Type == ADObjectType.Policy) ?? [])
            {
                Allow(gpo, "Domain Admins", "WriteDacl", RightCategory.WriteDacl);
                Allow(gpo, "Enterprise Admins", "WriteDacl", RightCategory.WriteDacl);
            }
            foreach (var name in new[] { "PolicyTemplate", "PolicyType", "WMIGPO" })
                Allow(At("System", "WMIPolicy", name), "Group Policy Creator Owners", "CreateChild", RightCategory.Other);
            foreach (var path in new[] { new[] { "System", "WMIPolicy" }, ["System", "WMIPolicy", "SOM"] })
            {
                Allow(At(path), "Domain Admins", "GenericAll", RightCategory.FullControl);
                Allow(At(path), "Enterprise Admins", "GenericAll", RightCategory.FullControl);
                Allow(At(path), "Group Policy Creator Owners", "GenericWrite", RightCategory.GenericWrite);
            }
            Allow(At("System", "PSPs"), "Domain Admins", "GenericAll", RightCategory.FullControl);
            Allow(At("System", "Password Settings Container"), "Domain Admins", "WriteDacl", RightCategory.WriteDacl);
            foreach (var container in new[] { At("Infrastructure"), At("LostAndFound") })
                Allow(container, "Domain Admins", "WriteDacl", RightCategory.WriteDacl);
            // NTDS Quotas : chacun peut lire son quota d'objets ; TPM Devices : chaque ordinateur y sauvegarde son TPM
            Allow(At("NTDS Quotas"), "Domain Admins", "GenericAll", RightCategory.FullControl);
            Allow(At("NTDS Quotas"), "Everyone", "DS-Query-Self-Quota", RightCategory.Other);
            Allow(At("TPM Devices"), "Domain Admins", "GenericAll", RightCategory.FullControl);
            Allow(At("TPM Devices"), "Domain Computers", "CreateChild:msTPM-InformationObject", RightCategory.Other);
            Allow(At("System", "MicrosoftDNS"), "Domain Admins", "GenericAll", RightCategory.FullControl);
            Allow(At("System", "MicrosoftDNS"), "DnsAdmins", "WriteDacl", RightCategory.WriteDacl);
            Allow(At("System", "RAS and IAS Servers Access Check"), "RAS and IAS Servers", "WriteDacl", RightCategory.WriteDacl);
            Allow(At("System", "SOA-Policies"), "Domain Admins", "WriteDacl", RightCategory.WriteDacl);
            Allow(At("System", "SOA-Policies"), "Enterprise Admins", "WriteDacl", RightCategory.WriteDacl);

            // AdminSDHolder : SDProp recopie ces droits sur tous les comptes et groupes protégés
            var holder = At("System", "AdminSDHolder");
            Allow(holder, "Domain Admins", "WriteDacl", RightCategory.WriteDacl);
            Allow(holder, "Enterprise Admins", "WriteDacl", RightCategory.WriteDacl);
            Allow(holder, "Everyone", "User-Change-Password", RightCategory.Other);
            Allow(holder, "Cert Publishers", "WriteProperty:userCertificate", RightCategory.AttributeWrite);
            Allow(holder, "Terminal Server License Servers", "WriteProperty:terminalServer", RightCategory.AttributeWrite);
            Allow(holder, "Terminal Server License Servers", "WriteProperty:5805bc62-bdc9-4428-a5e2-856a0f4c185e", RightCategory.AttributeWrite);

            // Les comptes et groupes protégés, après SDProp : les droits d'AdminSDHolder, moins ceux que leur classe donne déjà
            // (un compte laisse déjà Everyone changer son mot de passe, Cert Publishers et Terminal Server License Servers
            // écrire leurs attributs)
            foreach (var o in all.Where(o => o.AdminCount && o.Type is ADObjectType.User or ADObjectType.Group))
            {
                Allow(o, "Domain Admins", "WriteDacl", RightCategory.WriteDacl);
                Allow(o, "Enterprise Admins", "WriteDacl", RightCategory.WriteDacl);
                if (o.Type == ADObjectType.Group)
                    Allow(o, "Everyone", "User-Change-Password", RightCategory.Other);
            }
        }

        /// <summary>
        /// Appartenances posées par AD à la promotion, pour les objets intégrés (conteneurs Users et Builtin)
        /// et les contrôleurs de domaine (groupe Domain Controllers, leur groupe principal).
        /// Seuls les groupes présents dans le domaine sont ajoutés, sans doublon.
        /// </summary>
        private static void ApplyBuiltInMemberships(ADObject domain)
        {
            var all = Flatten(domain).ToList();
            var groups = all.Where(o => o.Type == ADObjectType.Group).Select(o => o.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

            void AddMembership(ADObject obj, string group)
            {
                if (groups.Contains(group) && !obj.MemberOf.Contains(group, StringComparer.OrdinalIgnoreCase))
                    obj.MemberOf.Add(group);
            }

            foreach (var obj in all)
            {
                var builtIn = obj.Parent is { Type: ADObjectType.Container, Name: "Users" or "Builtin" };
                if (builtIn && BuiltInMemberships.TryGetValue(obj.Name, out var memberships))
                    foreach (var group in memberships)
                        AddMembership(obj, group);

                if (obj.Type == ADObjectType.Computer && obj.Parent is { Type: ADObjectType.OrganizationalUnit, Name: "Domain Controllers" })
                    AddMembership(obj, "Domain Controllers");
            }
        }

        /// <summary>
        /// Historique du domaine d'exemple : un domaine en production depuis 2 ans, avec les écarts
        /// les plus courants en audit réel, pour que l'audit ait matière à enseigner dès le démarrage.
        /// </summary>
        public static void ApplySampleHistory(ADObject domain, DateTime now)
        {
            var created = now.AddYears(-2);
            foreach (var obj in Flatten(domain))
            {
                obj.CreatedDate = created;
                if (!obj.IsAccount) continue;

                // Comptes actifs : connexion récente ; mot de passe utilisateur changé il y a 60 jours,
                // mot de passe machine il y a 20 jours (un ordinateur le renouvelle tous les 30 jours)
                obj.PwdLastSet = now.AddDays(obj.Type == ADObjectType.User ? -60 : -20);
                obj.LastLogonTimestamp = obj.AccountFlags.HasFlag(ADAccountFlags.AccountDisabled) ? null : now.AddDays(-3);

                switch (obj.Name)
                {
                    case "krbtgt":
                        // Jamais renouvelé depuis la promotion : un Golden Ticket forgé il y a 2 ans reste valide
                        obj.PwdLastSet = created;
                        break;
                    case "asmith":
                        obj.LastLogonTimestamp = now.AddDays(-200);
                        break;
                    case "WKS-001":
                        // Poste éteint ou sorti du parc : il ne renouvelle plus son mot de passe (tous les 30 jours)
                        obj.PwdLastSet = now.AddDays(-120);
                        obj.LastLogonTimestamp = now.AddDays(-120);
                        break;
                }
            }
        }

        /// <summary>
        /// SPN d'un ordinateur joint, et ceux qu'enregistre un contrôleur de domaine : annuaire (ldap, sous ses noms DNS et
        /// NetBIOS, et pour les partitions DNS), catalogue global, DNS, réplication (DRS, RPC, DFSR, sous l'identifiant de
        /// son agent de l'annuaire), Bureau à distance.
        /// </summary>
        private static IEnumerable<string> ComputerSpns(string name, string fqdn, bool isDc)
        {
            var host = $"{name}.{fqdn}";
            yield return $"HOST/{name}";
            yield return $"HOST/{host}";
            yield return $"RestrictedKrbHost/{name}";
            yield return $"RestrictedKrbHost/{host}";
            if (!isDc) yield break;
            var netbios = NetBiosName(fqdn);
            // Identifiant de l'agent de l'annuaire (objectGUID de son objet NTDS Settings) : stable pour un même DC
            var dsa = new Guid(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes($"{fqdn}|{name}|NTDS Settings"))[..16]);
            yield return $"HOST/{name}/{netbios}";
            yield return $"HOST/{host}/{netbios}";
            yield return $"HOST/{host}/{fqdn}";
            yield return $"ldap/{name}";
            yield return $"ldap/{name}/{netbios}";
            yield return $"ldap/{host}";
            yield return $"ldap/{host}/{netbios}";
            yield return $"ldap/{host}/{fqdn}";
            yield return $"ldap/{host}/ForestDnsZones.{fqdn}";
            yield return $"ldap/{host}/DomainDnsZones.{fqdn}";
            yield return $"ldap/{dsa}._msdcs.{fqdn}";
            yield return $"GC/{host}/{fqdn}";
            yield return $"DNS/{host}";
            yield return $"E3514235-4B06-11D1-AB04-00C04FC2DCD2/{dsa}/{fqdn}";
            yield return $"RPC/{dsa}._msdcs.{fqdn}";
            yield return $"Dfsr-12F9A27C-BF97-4787-9364-D31B6C55EB04/{host}";
            yield return $"TERMSRV/{name}";
            yield return $"TERMSRV/{host}";
        }

        /// <summary>Nom NetBIOS du domaine : le premier libellé du nom DNS, en majuscules, 15 caractères au plus.</summary>
        public static string NetBiosName(string fqdn)
        {
            var label = fqdn.Split('.')[0].ToUpperInvariant();
            return label.Length > 15 ? label[..15] : label;
        }

        private static IEnumerable<ADObject> Flatten(ADObject root) =>
            new[] { root }.Concat(root.Children.SelectMany(Flatten));
    }
}
