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

        public static void Apply(ADObject domain)
        {
            var fqdn = domain.Name;
            foreach (var obj in Flatten(domain).Where(o => o != domain))
            {
                // Les objets créés par un administrateur du domaine appartiennent au groupe Domain Admins
                obj.Owner = "Domain Admins";

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
