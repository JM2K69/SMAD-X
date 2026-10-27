using System;
using System.Collections.Generic;
using System.Linq;
using SMADX.Models;

namespace SMADX.Services
{
    /// <summary>
    /// Attributs de sécurité qu'Active Directory pose lui-même sur un domaine fraîchement promu :
    /// options des comptes intégrés, SPN des ordinateurs, portée des groupes, adminCount des
    /// objets protégés par AdminSDHolder. Appliqué aux structures générées par ADDataService,
    /// pour que l'apprenant parte d'un état réaliste.
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
            ["krbtgt"] = ADAccountFlags.AccountDisabled,
            ["DefaultAccount"] = ADAccountFlags.AccountDisabled | ADAccountFlags.PasswordNotRequired | ADAccountFlags.PasswordNeverExpires,
            ["WDAGUtilityAccount"] = ADAccountFlags.AccountDisabled | ADAccountFlags.PasswordNeverExpires
        };

        /// <summary>Comptes intégrés membres de groupes protégés dès la promotion.</summary>
        private static readonly IReadOnlySet<string> ProtectedUsers = new HashSet<string> { "Administrator", "krbtgt" };

        /// <summary>Appartenances créées par la promotion du domaine (objet → groupes).</summary>
        private static readonly IReadOnlyDictionary<string, string[]> BuiltInMemberships = new Dictionary<string, string[]>
        {
            ["Administrator"] = new[] { "Administrators", "Domain Admins", "Enterprise Admins", "Schema Admins", "Group Policy Creator Owners" },
            ["Domain Admins"] = new[] { "Administrators" },
            ["Enterprise Admins"] = new[] { "Administrators" },
            ["Domain Users"] = new[] { "Users" },
            ["Guest"] = new[] { "Guests", "Domain Guests" },
            ["Domain Guests"] = new[] { "Guests" }
        };

        public static void Apply(ADObject domain)
        {
            var fqdn = domain.Name;
            var promotion = domain.CreatedDate;
            ApplyBuiltInMemberships(domain);
            foreach (var obj in Flatten(domain).Where(o => o != domain))
            {
                // Les objets créés par un administrateur du domaine appartiennent au groupe Domain Admins
                obj.Owner = "Domain Admins";

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
                        if (isDc)
                        {
                            // Un DC est en délégation non contrainte : c'est normal pour lui, pas ailleurs
                            obj.AccountFlags = ADAccountFlags.TrustedForDelegation;
                            obj.AdminCount = true;
                        }
                        break;

                    case ADObjectType.GMSA:
                        // Un gMSA porte souvent un SPN, sans risque de Kerberoasting : son mot de passe
                        // fait 120 caractères aléatoires et change automatiquement
                        obj.ServicePrincipalNames = new() { $"HTTP/{obj.Name}.{fqdn}" };
                        break;
                }
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

        private static IEnumerable<string> ComputerSpns(string name, string fqdn, bool isDc)
        {
            var host = $"{name}.{fqdn}";
            yield return $"HOST/{name}";
            yield return $"HOST/{host}";
            yield return $"RestrictedKrbHost/{name}";
            yield return $"RestrictedKrbHost/{host}";
            if (!isDc) yield break;
            yield return $"ldap/{host}";
            yield return $"ldap/{host}/{fqdn}";
            yield return $"GC/{host}/{fqdn}";
            yield return $"DNS/{host}";
        }

        private static IEnumerable<ADObject> Flatten(ADObject root) =>
            new[] { root }.Concat(root.Children.SelectMany(Flatten));
    }
}
