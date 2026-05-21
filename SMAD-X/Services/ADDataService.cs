using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using SMADX.Models;

namespace SMADX.Services
{
    /// <summary>
    /// Service pour sauvegarder et charger les structures AD en JSON
    /// </summary>
    public class ADDataService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };

        /// <summary>
        /// Sauvegarde la structure AD dans un fichier JSON
        /// </summary>
        public async Task<bool> SaveToFileAsync(ADObject root, string filePath)
        {
            try
            {
                var json = JsonSerializer.Serialize(root, JsonOptions);
                await File.WriteAllTextAsync(filePath, json);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors de la sauvegarde : {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Charge la structure AD depuis un fichier JSON
        /// </summary>
        public async Task<ADObject?> LoadFromFileAsync(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    return null;

                var json = await File.ReadAllTextAsync(filePath);
                var root = JsonSerializer.Deserialize<ADObject>(json, JsonOptions);

                if (root != null)
                {
                    RestoreParentReferences(root, null);
                }

                return root;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors du chargement : {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Restaure les références parent après désérialisation
        /// </summary>
        private void RestoreParentReferences(ADObject current, ADObject? parent)
        {
            current.Parent = parent;

            foreach (var child in current.Children)
            {
                RestoreParentReferences(child, current);
            }
        }

        /// <summary>
        /// Crée une structure AD vierge conforme à une installation fraîche d'Active Directory
        /// </summary>
        /// <param name="domainName">Nom du domaine (FQDN)</param>
        /// <param name="enableTiering">Si true, affecte automatiquement les tiers aux objets</param>
        public ADObject CreateFreshDomainStructure(string domainName, bool enableTiering = true)
        {
            var loc = LocalizationService.Instance;

            var domain = new ADObject(domainName, ADObjectType.Domain)
            {
                Description = string.Format(loc["Desc.Domain"], domainName)
            };

            // Helper pour affecter un tier seulement si le tiering est activé
            string? GetTier(string tier) => enableTiering ? tier : null;

            // 1. Container Builtin (groupes intégrés système)
            var builtinContainer = new ADObject("Builtin", ADObjectType.Container)
            {
                Description = loc["Desc.Builtin"],
                Parent = domain
            };
            domain.Children.Add(builtinContainer);

            // Groupes Builtin
            var builtinAdmins = new ADObject("Administrators", ADObjectType.Group)
            {
                Description = loc["Desc.Builtin.Administrators"],
                Tier = GetTier("Tier 0"),
                Parent = builtinContainer
            };
            builtinContainer.Children.Add(builtinAdmins);

            var builtinUsers = new ADObject("Users", ADObjectType.Group)
            {
                Description = loc["Desc.Builtin.Users"],
                Tier = GetTier("Tier 2"),
                Parent = builtinContainer
            };
            builtinContainer.Children.Add(builtinUsers);

            var builtinGuests = new ADObject("Guests", ADObjectType.Group)
            {
                Description = loc["Desc.Builtin.Guests"],
                Tier = GetTier("Tier 2"),
                Parent = builtinContainer
            };
            builtinContainer.Children.Add(builtinGuests);

            var serverOps = new ADObject("Server Operators", ADObjectType.Group)
            {
                Description = loc["Desc.Builtin.ServerOperators"],
                Tier = GetTier("Tier 1"),
                Parent = builtinContainer
            };
            builtinContainer.Children.Add(serverOps);

            var accountOps = new ADObject("Account Operators", ADObjectType.Group)
            {
                Description = loc["Desc.Builtin.AccountOperators"],
                Tier = GetTier("Tier 1"),
                Parent = builtinContainer
            };
            builtinContainer.Children.Add(accountOps);

            var backupOps = new ADObject("Backup Operators", ADObjectType.Group)
            {
                Description = loc["Desc.Builtin.BackupOperators"],
                Tier = GetTier("Tier 0"),
                Parent = builtinContainer
            };
            builtinContainer.Children.Add(backupOps);

            // 2. Container Users (utilisateurs et groupes par défaut)
            var usersContainer = new ADObject("Users", ADObjectType.Container)
            {
                Description = loc["Desc.Users"],
                Tier = GetTier("Tier 2"),
                Parent = domain
            };
            domain.Children.Add(usersContainer);

            // Compte Administrator
            var administrator = new ADObject("Administrator", ADObjectType.User)
            {
                Description = loc["Desc.Users.Administrator"],
                Tier = GetTier("Tier 0"),
                Parent = usersContainer
            };
            usersContainer.Children.Add(administrator);

            // Compte Guest
            var guest = new ADObject("Guest", ADObjectType.User)
            {
                Description = loc["Desc.Users.Guest"],
                Tier = GetTier("Tier 2"),
                Parent = usersContainer
            };
            usersContainer.Children.Add(guest);

            // Compte Krbtgt
            var krbtgt = new ADObject("krbtgt", ADObjectType.User)
            {
                Description = loc["Desc.Users.Krbtgt"],
                Tier = GetTier("Tier 0"),
                Parent = usersContainer
            };
            usersContainer.Children.Add(krbtgt);

            // Groupes du domaine
            var domainAdmins = new ADObject("Domain Admins", ADObjectType.Group)
            {
                Description = loc["Desc.Users.DomainAdmins"],
                Tier = GetTier("Tier 0"),
                Parent = usersContainer
            };
            usersContainer.Children.Add(domainAdmins);

            var domainUsers = new ADObject("Domain Users", ADObjectType.Group)
            {
                Description = loc["Desc.Users.DomainUsers"],
                Tier = GetTier("Tier 2"),
                Parent = usersContainer
            };
            usersContainer.Children.Add(domainUsers);

            var domainComputers = new ADObject("Domain Computers", ADObjectType.Group)
            {
                Description = loc["Desc.Users.DomainComputers"],
                Tier = GetTier("Tier 2"),
                Parent = usersContainer
            };
            usersContainer.Children.Add(domainComputers);

            var domainControllers = new ADObject("Domain Controllers", ADObjectType.Group)
            {
                Description = loc["Desc.Users.DomainControllers"],
                Tier = GetTier("Tier 0"),
                Parent = usersContainer
            };
            usersContainer.Children.Add(domainControllers);

            var schemaAdmins = new ADObject("Schema Admins", ADObjectType.Group)
            {
                Description = loc["Desc.Users.SchemaAdmins"],
                Tier = GetTier("Tier 0"),
                Parent = usersContainer
            };
            usersContainer.Children.Add(schemaAdmins);

            var enterpriseAdmins = new ADObject("Enterprise Admins", ADObjectType.Group)
            {
                Description = loc["Desc.Users.EnterpriseAdmins"],
                Tier = GetTier("Tier 0"),
                Parent = usersContainer
            };
            usersContainer.Children.Add(enterpriseAdmins);

            var groupPolicyCreatorOwners = new ADObject("Group Policy Creator Owners", ADObjectType.Group)
            {
                Description = loc["Desc.Users.GroupPolicyCreatorOwners"],
                Tier = GetTier("Tier 0"),
                Parent = usersContainer
            };
            usersContainer.Children.Add(groupPolicyCreatorOwners);

            var readOnlyDCs = new ADObject("Read-only Domain Controllers", ADObjectType.Group)
            {
                Description = loc["Desc.Users.ReadOnlyDCs"],
                Tier = GetTier("Tier 1"),
                Parent = usersContainer
            };
            usersContainer.Children.Add(readOnlyDCs);

            var dnsAdmins = new ADObject("DnsAdmins", ADObjectType.Group)
            {
                Description = loc["Desc.Users.DnsAdmins"],
                Tier = GetTier("Tier 0"),
                Parent = usersContainer
            };
            usersContainer.Children.Add(dnsAdmins);

            var certPublishers = new ADObject("Cert Publishers", ADObjectType.Group)
            {
                Description = loc["Desc.Users.CertPublishers"],
                Tier = GetTier("Tier 0"),
                Parent = usersContainer
            };
            usersContainer.Children.Add(certPublishers);

            var rasAndIas = new ADObject("RAS and IAS Servers", ADObjectType.Group)
            {
                Description = loc["Desc.Users.RASandIAS"],
                Tier = GetTier("Tier 1"),
                Parent = usersContainer
            };
            usersContainer.Children.Add(rasAndIas);

            var allowedRODC = new ADObject("Allowed RODC Password Replication Group", ADObjectType.Group)
            {
                Description = loc["Desc.Users.AllowedRODCReplication"],
                Tier = GetTier("Tier 1"),
                Parent = usersContainer
            };
            usersContainer.Children.Add(allowedRODC);

            var deniedRODC = new ADObject("Denied RODC Password Replication Group", ADObjectType.Group)
            {
                Description = loc["Desc.Users.DeniedRODCReplication"],
                Tier = GetTier("Tier 0"),
                Parent = usersContainer
            };
            usersContainer.Children.Add(deniedRODC);

            var cloneableDCs = new ADObject("Cloneable Domain Controllers", ADObjectType.Group)
            {
                Description = loc["Desc.Users.CloneableDCs"],
                Tier = GetTier("Tier 0"),
                Parent = usersContainer
            };
            usersContainer.Children.Add(cloneableDCs);

            var protectedUsers = new ADObject("Protected Users", ADObjectType.Group)
            {
                Description = loc["Desc.Users.ProtectedUsers"],
                Tier = GetTier("Tier 0"),
                Parent = usersContainer
            };
            usersContainer.Children.Add(protectedUsers);

            var keyAdmins = new ADObject("Key Admins", ADObjectType.Group)
            {
                Description = loc["Desc.Users.KeyAdmins"],
                Tier = GetTier("Tier 0"),
                Parent = usersContainer
            };
            usersContainer.Children.Add(keyAdmins);

            var enterpriseKeyAdmins = new ADObject("Enterprise Key Admins", ADObjectType.Group)
            {
                Description = loc["Desc.Users.EnterpriseKeyAdmins"],
                Tier = GetTier("Tier 0"),
                Parent = usersContainer
            };
            usersContainer.Children.Add(enterpriseKeyAdmins);

            var dnsUpdateProxy = new ADObject("DnsUpdateProxy", ADObjectType.Group)
            {
                Description = loc["Desc.Users.DnsUpdateProxy"],
                Tier = GetTier("Tier 1"),
                Parent = usersContainer
            };
            usersContainer.Children.Add(dnsUpdateProxy);

            var defaultAccount = new ADObject("DefaultAccount", ADObjectType.User)
            {
                Description = loc["Desc.Users.DefaultAccount"],
                Tier = GetTier("Tier 2"),
                Parent = usersContainer
            };
            usersContainer.Children.Add(defaultAccount);

            var wdagUtility = new ADObject("WDAGUtilityAccount", ADObjectType.User)
            {
                Description = loc["Desc.Users.WDAGUtilityAccount"],
                Tier = GetTier("Tier 2"),
                Parent = usersContainer
            };
            usersContainer.Children.Add(wdagUtility);

            // 3. Container Computers (ordinateurs par défaut)
            var computersContainer = new ADObject("Computers", ADObjectType.Container)
            {
                Description = loc["Desc.Computers"],
                Tier = GetTier("Tier 2"),
                Parent = domain
            };
            domain.Children.Add(computersContainer);

            // Ordinateur par défaut dans Computers
            var defaultComputer = new ADObject("WORKSTATION-01", ADObjectType.Computer)
            {
                Description = loc["Desc.Computers.Workstation"],
                Tier = GetTier("Tier 2"),
                Parent = computersContainer
            };
            computersContainer.Children.Add(defaultComputer);

            // 4. OU Domain Controllers
            var domainControllersOU = new ADObject("Domain Controllers", ADObjectType.OrganizationalUnit)
            {
                Description = loc["Desc.DomainControllersOU"],
                Tier = GetTier("Tier 0"),
                Parent = domain
            };
            domain.Children.Add(domainControllersOU);

            // Contrôleur de domaine principal dans l'OU Domain Controllers
            var primaryDC = new ADObject("DC01", ADObjectType.Computer)
            {
                Description = loc["Desc.DomainControllers.DC"],
                Tier = GetTier("Tier 0"),
                Parent = domainControllersOU
            };
            domainControllersOU.Children.Add(primaryDC);

            // 5. Container System (objets système)
            var systemContainer = new ADObject("System", ADObjectType.Container)
            {
                Description = loc["Desc.System"],
                Parent = domain
            };
            domain.Children.Add(systemContainer);

            // Password Settings Container
            var psoContainer = new ADObject("Password Settings Container", ADObjectType.Container)
            {
                Description = loc["Desc.System.PSO"],
                Parent = systemContainer
            };
            systemContainer.Children.Add(psoContainer);

            // Container Policies (SYSVOL) — contient les 2 GPOs présentes dans tout domaine neuf
            var policiesContainer = new ADObject("Policies", ADObjectType.Container)
            {
                Description = loc["Desc.GPOPoliciesContainer"],
                Tier = GetTier("Tier 0"),
                Parent = systemContainer
            };
            systemContainer.Children.Add(policiesContainer);

            // GPO 1 : Default Domain Policy — GUID fixe Microsoft
            var defaultDomainPolicy = new ADObject("Default Domain Policy", ADObjectType.Policy)
            {
                Description = loc["Desc.DefaultDomainPolicy"],
                Tier = GetTier("Tier 0"),
                Parent = policiesContainer
            };
            policiesContainer.Children.Add(defaultDomainPolicy);

            // GPO 2 : Default Domain Controllers Policy — GUID fixe Microsoft
            var defaultDCPolicy = new ADObject("Default Domain Controllers Policy", ADObjectType.Policy)
            {
                Description = loc["Desc.DefaultDomainControllersPolicy"],
                Tier = GetTier("Tier 0"),
                Parent = policiesContainer
            };
            policiesContainer.Children.Add(defaultDCPolicy);

            // Lier les GPOs aux OUs correspondantes
            domain.LinkedGPOs.Add("Default Domain Policy");
            domainControllersOU.LinkedGPOs.Add("Default Domain Controllers Policy");

            // 6. Container ForeignSecurityPrincipals
            var fspContainer = new ADObject("ForeignSecurityPrincipals", ADObjectType.Container)
            {
                Description = loc["Desc.ForeignSecurityPrincipals"],
                Parent = domain
            };
            domain.Children.Add(fspContainer);

            // Mettre à jour tous les DN
            UpdateDistinguishedNamesRecursive(domain);

            return domain;
        }

        /// <summary>
        /// Crée une structure AD d'exemple
        /// </summary>
        public ADObject CreateSampleStructure()
        {
            var loc = LocalizationService.Instance;

            var domain = new ADObject("contoso.com", ADObjectType.Domain)
            {
                Description = loc["Desc.Sample.Domain"],
                //Tier = "Tier 0"
            };

            // Containers par défaut (comme dans un vrai AD)

            var usersContainer = new ADObject("Users", ADObjectType.Container)
            {
                Description = loc["Desc.Users"],
                Tier = "Tier 2",
                Parent = domain
            };
            domain.Children.Add(usersContainer);

            // Comptes par défaut
            var administrator = new ADObject("Administrator", ADObjectType.User)
            {
                Description = loc["Desc.Users.Administrator"],
                Tier = "Tier 0",
                Parent = usersContainer
            };
            usersContainer.Children.Add(administrator);

            var guest = new ADObject("Guest", ADObjectType.User)
            {
                Description = loc["Desc.Users.Guest"],
                Tier = "Tier 2",
                Parent = usersContainer
            };
            usersContainer.Children.Add(guest);

            var krbtgt = new ADObject("krbtgt", ADObjectType.User)
            {
                Description = loc["Desc.Users.Krbtgt"],
                Tier = "Tier 0",
                Parent = usersContainer
            };
            usersContainer.Children.Add(krbtgt);

            var defaultAccount = new ADObject("DefaultAccount", ADObjectType.User)
            {
                Description = loc["Desc.Users.DefaultAccount"],
                Tier = "Tier 2",
                Parent = usersContainer
            };
            usersContainer.Children.Add(defaultAccount);

            var wdagUtility = new ADObject("WDAGUtilityAccount", ADObjectType.User)
            {
                Description = loc["Desc.Users.WDAGUtilityAccount"],
                Tier = "Tier 2",
                Parent = usersContainer
            };
            usersContainer.Children.Add(wdagUtility);

            // Groupes de domaine par défaut
            var domainAdminsUsers = new ADObject("Domain Admins", ADObjectType.Group)
            {
                Description = loc["Desc.Users.DomainAdmins"],
                Tier = "Tier 0",
                Parent = usersContainer
            };
            usersContainer.Children.Add(domainAdminsUsers);

            var domainUsers = new ADObject("Domain Users", ADObjectType.Group)
            {
                Description = loc["Desc.Users.DomainUsers"],
                Tier = "Tier 2",
                Parent = usersContainer
            };
            usersContainer.Children.Add(domainUsers);

            var domainComputers = new ADObject("Domain Computers", ADObjectType.Group)
            {
                Description = loc["Desc.Users.DomainComputers"],
                Tier = "Tier 2",
                Parent = usersContainer
            };
            usersContainer.Children.Add(domainComputers);

            var domainControllers = new ADObject("Domain Controllers", ADObjectType.Group)
            {
                Description = loc["Desc.Users.DomainControllers"],
                Tier = "Tier 0",
                Parent = usersContainer
            };
            usersContainer.Children.Add(domainControllers);

            var schemaAdmins = new ADObject("Schema Admins", ADObjectType.Group)
            {
                Description = loc["Desc.Users.SchemaAdmins"],
                Tier = "Tier 0",
                Parent = usersContainer
            };
            usersContainer.Children.Add(schemaAdmins);

            var enterpriseAdmins = new ADObject("Enterprise Admins", ADObjectType.Group)
            {
                Description = loc["Desc.Users.EnterpriseAdmins"],
                Tier = "Tier 0",
                Parent = usersContainer
            };
            usersContainer.Children.Add(enterpriseAdmins);

            var groupPolicyCreatorOwners = new ADObject("Group Policy Creator Owners", ADObjectType.Group)
            {
                Description = loc["Desc.Users.GroupPolicyCreatorOwners"],
                Tier = "Tier 0",
                Parent = usersContainer
            };
            usersContainer.Children.Add(groupPolicyCreatorOwners);

            var readOnlyDCs = new ADObject("Read-only Domain Controllers", ADObjectType.Group)
            {
                Description = loc["Desc.Users.ReadOnlyDCs"],
                Tier = "Tier 1",
                Parent = usersContainer
            };
            usersContainer.Children.Add(readOnlyDCs);

            var dnsAdmins = new ADObject("DnsAdmins", ADObjectType.Group)
            {
                Description = loc["Desc.Users.DnsAdmins"],
                Tier = "Tier 0",
                Parent = usersContainer
            };
            usersContainer.Children.Add(dnsAdmins);

            var certPublishers = new ADObject("Cert Publishers", ADObjectType.Group)
            {
                Description = loc["Desc.Users.CertPublishers"],
                Tier = "Tier 0",
                Parent = usersContainer
            };
            usersContainer.Children.Add(certPublishers);

            var rasAndIas = new ADObject("RAS and IAS Servers", ADObjectType.Group)
            {
                Description = loc["Desc.Users.RASandIAS"],
                Tier = "Tier 1",
                Parent = usersContainer
            };
            usersContainer.Children.Add(rasAndIas);

            var allowedRODC = new ADObject("Allowed RODC Password Replication Group", ADObjectType.Group)
            {
                Description = loc["Desc.Users.AllowedRODCReplication"],
                Tier = "Tier 1",
                Parent = usersContainer
            };
            usersContainer.Children.Add(allowedRODC);

            var deniedRODC = new ADObject("Denied RODC Password Replication Group", ADObjectType.Group)
            {
                Description = loc["Desc.Users.DeniedRODCReplication"],
                Tier = "Tier 0",
                Parent = usersContainer
            };
            usersContainer.Children.Add(deniedRODC);

            var cloneableDCs = new ADObject("Cloneable Domain Controllers", ADObjectType.Group)
            {
                Description = loc["Desc.Users.CloneableDCs"],
                Tier = "Tier 0",
                Parent = usersContainer
            };
            usersContainer.Children.Add(cloneableDCs);

            var protectedUsers = new ADObject("Protected Users", ADObjectType.Group)
            {
                Description = loc["Desc.Users.ProtectedUsers"],
                Tier = "Tier 0",
                Parent = usersContainer
            };
            usersContainer.Children.Add(protectedUsers);

            var keyAdmins = new ADObject("Key Admins", ADObjectType.Group)
            {
                Description = loc["Desc.Users.KeyAdmins"],
                Tier = "Tier 0",
                Parent = usersContainer
            };
            usersContainer.Children.Add(keyAdmins);

            var enterpriseKeyAdmins = new ADObject("Enterprise Key Admins", ADObjectType.Group)
            {
                Description = loc["Desc.Users.EnterpriseKeyAdmins"],
                Tier = "Tier 0",
                Parent = usersContainer
            };
            usersContainer.Children.Add(enterpriseKeyAdmins);

            var dnsUpdateProxy = new ADObject("DnsUpdateProxy", ADObjectType.Group)
            {
                Description = loc["Desc.Users.DnsUpdateProxy"],
                Tier = "Tier 1",
                Parent = usersContainer
            };
            usersContainer.Children.Add(dnsUpdateProxy);

            var computersContainer = new ADObject("Computers", ADObjectType.Container)
            {
                Description = loc["Desc.Computers"],
                Tier = "Tier 2",
                Parent = domain
            };
            domain.Children.Add(computersContainer);

            // OU Admin
            var adminOU = new ADObject("Admin", ADObjectType.OrganizationalUnit)
            {
                Description = loc["Desc.Sample.AdminOU"],
                Tier = "Tier 0",
                Parent = domain
            };
            domain.Children.Add(adminOU);

            var adminUser = new ADObject("Administrator", ADObjectType.User)
            {
                Description = loc["Desc.Sample.AdminUser"],
                Tier = "Tier 0",
                Parent = adminOU
            };
            adminOU.Children.Add(adminUser);

            var adminOUDomainAdmins = new ADObject("Domain Admins", ADObjectType.Group)
            {
                Description = loc["Desc.Sample.AdminDomainAdmins"],
                Tier = "Tier 0",
                Parent = adminOU
            };
            adminOU.Children.Add(adminOUDomainAdmins);

            // OU Servers
            var serversOU = new ADObject("Servers", ADObjectType.OrganizationalUnit)
            {
                Description = loc["Desc.Sample.ServersOU"],
                Tier = "Tier 1",
                Parent = domain
            };
            domain.Children.Add(serversOU);

            var fileServer = new ADObject("SRV-FILE-01", ADObjectType.Computer)
            {
                Description = loc["Desc.Sample.FileServer"],
                Tier = "Tier 1",
                Parent = serversOU
            };
            serversOU.Children.Add(fileServer);

            var appServer = new ADObject("SRV-APP-01", ADObjectType.Computer)
            {
                Description = loc["Desc.Sample.AppServer"],
                Tier = "Tier 1",
                Parent = serversOU
            };
            serversOU.Children.Add(appServer);

            // OU Users
            var usersOU = new ADObject("Users", ADObjectType.OrganizationalUnit)
            {
                Description = loc["Desc.Sample.UsersOU"],
                Tier = "Tier 2",
                Parent = domain
            };
            domain.Children.Add(usersOU);

            var user1 = new ADObject("jdoe", ADObjectType.User)
            {
                Description = loc["Desc.Sample.UserJdoe"],
                Tier = "Tier 2",
                Parent = usersOU
            };
            usersOU.Children.Add(user1);

            var user2 = new ADObject("asmith", ADObjectType.User)
            {
                Description = loc["Desc.Sample.UserAsmith"],
                Tier = "Tier 2",
                Parent = usersOU
            };
            usersOU.Children.Add(user2);

            // OU Workstations
            var workstationsOU = new ADObject("Workstations", ADObjectType.OrganizationalUnit)
            {
                Description = loc["Desc.Sample.WorkstationsOU"],
                Tier = "Tier 2",
                Parent = domain
            };
            domain.Children.Add(workstationsOU);

            var workstation1 = new ADObject("WKS-001", ADObjectType.Computer)
            {
                Description = loc["Desc.Sample.Workstation1"],
                Tier = "Tier 2",
                Parent = workstationsOU
            };
            workstationsOU.Children.Add(workstation1);

            // OU Domain Controllers
            var domainControllersOU = new ADObject("Domain Controllers", ADObjectType.OrganizationalUnit)
            {
                Description = loc["Desc.Sample.DomainControllersOU"],
                Tier = "Tier 0",
                Parent = domain
            };
            domain.Children.Add(domainControllersOU);

            var dc01 = new ADObject("DC01", ADObjectType.Computer)
            {
                Description = loc["Desc.Sample.DC01"],
                Tier = "Tier 0",
                Parent = domainControllersOU
            };
            domainControllersOU.Children.Add(dc01);

            // GMSA
            var gmsa = new ADObject("svc-webapp", ADObjectType.GMSA)
            {
                Description = loc["Desc.Sample.GMSA"],
                Tier = "Tier 1",
                Parent = serversOU
            };
            serversOU.Children.Add(gmsa);

            // Container System
            var systemContainer = new ADObject("System", ADObjectType.Container)
            {
                Description = loc["Desc.Sample.SystemContainer"],
                Parent = domain
            };
            domain.Children.Add(systemContainer);

            // Password Settings Container
            var psoContainer = new ADObject("Password Settings Container", ADObjectType.Container)
            {
                Description = loc["Desc.Sample.PSOContainer"],
                Parent = systemContainer
            };
            systemContainer.Children.Add(psoContainer);

            // PSOs
            var psoAdmin = new ADObject("PSO-Tier0-Admins", ADObjectType.PasswordSettingsObject)
            {
                Description = loc["Desc.Sample.PSOAdmin"],
                Tier = "Tier 0",
                Parent = psoContainer
            };
            psoContainer.Children.Add(psoAdmin);

            var psoUsers = new ADObject("PSO-Standard-Users", ADObjectType.PasswordSettingsObject)
            {
                Description = loc["Desc.Sample.PSOUsers"],
                Tier = "Tier 2",
                Parent = psoContainer
            };
            psoContainer.Children.Add(psoUsers);

            // Container Policies (SYSVOL) — 2 GPOs par défaut Microsoft
            var policiesContainer = new ADObject("Policies", ADObjectType.Container)
            {
                Description = loc["Desc.GPOPoliciesContainer"],
                Tier = "Tier 0",
                Parent = systemContainer
            };
            systemContainer.Children.Add(policiesContainer);

            var defaultDomainPolicy = new ADObject("Default Domain Policy", ADObjectType.Policy)
            {
                Description = loc["Desc.DefaultDomainPolicy"],
                Tier = "Tier 0",
                Parent = policiesContainer
            };
            policiesContainer.Children.Add(defaultDomainPolicy);

            var defaultDCPolicy = new ADObject("Default Domain Controllers Policy", ADObjectType.Policy)
            {
                Description = loc["Desc.DefaultDomainControllersPolicy"],
                Tier = "Tier 0",
                Parent = policiesContainer
            };
            policiesContainer.Children.Add(defaultDCPolicy);

            // ── Relations GPO ────────────────────────────────────────────────
            // Default Domain Policy → domaine entier
            domain.LinkedGPOs.Add("Default Domain Policy");
            // Default Domain Controllers Policy → OU Domain Controllers
            domainControllersOU.LinkedGPOs.Add("Default Domain Controllers Policy");
            // GPO Password Policy → OU Admin (exemple)
            adminOU.LinkedGPOs.Add("Password Policy");

            // ── Relations PSO ────────────────────────────────────────────────
            psoAdmin.PSOAppliesTo.Add("Domain Admins");
            psoAdmin.PSOAppliesTo.Add("Administrator");
            psoUsers.PSOAppliesTo.Add("jdoe");
            psoUsers.PSOAppliesTo.Add("asmith");

            // ── Relations MemberOf ───────────────────────────────────────────
            adminUser.MemberOf.Add("Domain Admins");
            user1.MemberOf.Add("Domain Users");
            user2.MemberOf.Add("Domain Users");

            // Mettre à jour tous les DN
            UpdateDistinguishedNamesRecursive(domain);

            return domain;
        }

        private void UpdateDistinguishedNamesRecursive(ADObject obj)
        {
            obj.UpdateDistinguishedName();
            foreach (var child in obj.Children)
            {
                UpdateDistinguishedNamesRecursive(child);
            }
        }

        /// <summary>
        /// Met à jour les descriptions des objets AD dans la langue actuelle
        /// </summary>
        public void UpdateDescriptionsForCurrentLanguage(ADObject root)
        {
            var loc = LocalizationService.Instance;

            // Mettre à jour récursivement
            UpdateDescriptionRecursive(root, loc);
        }

        private void UpdateDescriptionRecursive(ADObject obj, LocalizationService loc)
        {
            // Mettre à jour la description selon le type et le nom de l'objet
            if (obj.Type == ADObjectType.Domain)
            {
                obj.Description = string.Format(loc["Desc.Domain"], obj.Name);
            }
            else if (obj.Name == "Builtin" && obj.Type == ADObjectType.Container)
            {
                obj.Description = loc["Desc.Builtin"];
            }
            else if (obj.Name == "Administrators" && obj.Parent?.Name == "Builtin")
            {
                obj.Description = loc["Desc.Builtin.Administrators"];
            }
            else if (obj.Name == "Users" && obj.Parent?.Name == "Builtin")
            {
                obj.Description = loc["Desc.Builtin.Users"];
            }
            else if (obj.Name == "Guests" && obj.Parent?.Name == "Builtin")
            {
                obj.Description = loc["Desc.Builtin.Guests"];
            }
            else if (obj.Name == "Server Operators" && obj.Parent?.Name == "Builtin")
            {
                obj.Description = loc["Desc.Builtin.ServerOperators"];
            }
            else if (obj.Name == "Account Operators" && obj.Parent?.Name == "Builtin")
            {
                obj.Description = loc["Desc.Builtin.AccountOperators"];
            }
            else if (obj.Name == "Backup Operators" && obj.Parent?.Name == "Builtin")
            {
                obj.Description = loc["Desc.Builtin.BackupOperators"];
            }
            else if (obj.Name == "Users" && obj.Type == ADObjectType.Container && obj.Parent?.Type == ADObjectType.Domain)
            {
                obj.Description = loc["Desc.Users"];
            }
            else if (obj.Name == "Administrator" && obj.Parent?.Name == "Users" && obj.Parent?.Type == ADObjectType.Container)
            {
                obj.Description = loc["Desc.Users.Administrator"];
            }
            else if (obj.Name == "Guest" && obj.Parent?.Name == "Users")
            {
                obj.Description = loc["Desc.Users.Guest"];
            }
            else if (obj.Name == "krbtgt")
            {
                obj.Description = loc["Desc.Users.Krbtgt"];
            }
            else if (obj.Name == "Domain Admins")
            {
                obj.Description = loc["Desc.Users.DomainAdmins"];
            }
            else if (obj.Name == "Domain Users")
            {
                obj.Description = loc["Desc.Users.DomainUsers"];
            }
            else if (obj.Name == "Domain Computers")
            {
                obj.Description = loc["Desc.Users.DomainComputers"];
            }
            else if (obj.Name == "Domain Controllers" && obj.Type == ADObjectType.Group)
            {
                obj.Description = loc["Desc.Users.DomainControllers"];
            }
            else if (obj.Name == "Schema Admins")
            {
                obj.Description = loc["Desc.Users.SchemaAdmins"];
            }
            else if (obj.Name == "Enterprise Admins")
            {
                obj.Description = loc["Desc.Users.EnterpriseAdmins"];
            }
            else if (obj.Name == "Group Policy Creator Owners")
            {
                obj.Description = loc["Desc.Users.GroupPolicyCreatorOwners"];
            }
            else if (obj.Name == "Read-only Domain Controllers")
            {
                obj.Description = loc["Desc.Users.ReadOnlyDCs"];
            }
            else if (obj.Name == "DnsAdmins")
            {
                obj.Description = loc["Desc.Users.DnsAdmins"];
            }
            else if (obj.Name == "Cert Publishers")
            {
                obj.Description = loc["Desc.Users.CertPublishers"];
            }
            else if (obj.Name == "RAS and IAS Servers")
            {
                obj.Description = loc["Desc.Users.RASandIAS"];
            }
            else if (obj.Name == "Allowed RODC Password Replication Group")
            {
                obj.Description = loc["Desc.Users.AllowedRODCReplication"];
            }
            else if (obj.Name == "Denied RODC Password Replication Group")
            {
                obj.Description = loc["Desc.Users.DeniedRODCReplication"];
            }
            else if (obj.Name == "Cloneable Domain Controllers")
            {
                obj.Description = loc["Desc.Users.CloneableDCs"];
            }
            else if (obj.Name == "Protected Users")
            {
                obj.Description = loc["Desc.Users.ProtectedUsers"];
            }
            else if (obj.Name == "Key Admins")
            {
                obj.Description = loc["Desc.Users.KeyAdmins"];
            }
            else if (obj.Name == "Enterprise Key Admins")
            {
                obj.Description = loc["Desc.Users.EnterpriseKeyAdmins"];
            }
            else if (obj.Name == "DnsUpdateProxy")
            {
                obj.Description = loc["Desc.Users.DnsUpdateProxy"];
            }
            else if (obj.Name == "DefaultAccount")
            {
                obj.Description = loc["Desc.Users.DefaultAccount"];
            }
            else if (obj.Name == "WDAGUtilityAccount")
            {
                obj.Description = loc["Desc.Users.WDAGUtilityAccount"];
            }
            else if (obj.Name == "Computers" && obj.Type == ADObjectType.Container)
            {
                obj.Description = loc["Desc.Computers"];
            }
            else if (obj.Name == "WORKSTATION-01")
            {
                obj.Description = loc["Desc.Computers.Workstation"];
            }
            // ── Sample structure OUs / objects ──────────────────────────────
            else if (obj.Name == "Admin" && obj.Type == ADObjectType.OrganizationalUnit)
            {
                obj.Description = loc["Desc.Sample.AdminOU"];
            }
            else if (obj.Name == "Administrator" && obj.Parent?.Name == "Admin" && obj.Type == ADObjectType.User)
            {
                obj.Description = loc["Desc.Sample.AdminUser"];
            }
            else if (obj.Name == "Domain Admins" && obj.Parent?.Name == "Admin" && obj.Type == ADObjectType.Group)
            {
                obj.Description = loc["Desc.Sample.AdminDomainAdmins"];
            }
            else if (obj.Name == "Servers" && obj.Type == ADObjectType.OrganizationalUnit)
            {
                obj.Description = loc["Desc.Sample.ServersOU"];
            }
            else if (obj.Name == "SRV-FILE-01")
            {
                obj.Description = loc["Desc.Sample.FileServer"];
            }
            else if (obj.Name == "SRV-APP-01")
            {
                obj.Description = loc["Desc.Sample.AppServer"];
            }
            else if (obj.Name == "Users" && obj.Type == ADObjectType.OrganizationalUnit)
            {
                obj.Description = loc["Desc.Sample.UsersOU"];
            }
            else if (obj.Name == "jdoe")
            {
                obj.Description = loc["Desc.Sample.UserJdoe"];
            }
            else if (obj.Name == "asmith")
            {
                obj.Description = loc["Desc.Sample.UserAsmith"];
            }
            else if (obj.Name == "Workstations" && obj.Type == ADObjectType.OrganizationalUnit)
            {
                obj.Description = loc["Desc.Sample.WorkstationsOU"];
            }
            else if (obj.Name == "WKS-001")
            {
                obj.Description = loc["Desc.Sample.Workstation1"];
            }
            else if (obj.Name == "svc-webapp" && obj.Type == ADObjectType.GMSA)
            {
                obj.Description = loc["Desc.Sample.GMSA"];
            }
            else if (obj.Name == "PSO-Tier0-Admins")
            {
                obj.Description = loc["Desc.Sample.PSOAdmin"];
            }
            else if (obj.Name == "PSO-Standard-Users")
            {
                obj.Description = loc["Desc.Sample.PSOUsers"];
            }
            // ────────────────────────────────────────────────────────────────
            else if (obj.Name == "Domain Controllers" && obj.Type == ADObjectType.OrganizationalUnit)
            {
                obj.Description = loc["Desc.Sample.DomainControllersOU"];
            }
            else if (obj.Name == "DC01")
            {
                obj.Description = loc["Desc.Sample.DC01"];
            }
            else if (obj.Name == "System" && obj.Type == ADObjectType.Container)
            {
                obj.Description = loc["Desc.Sample.SystemContainer"];
            }
            else if (obj.Name == "Password Settings Container")
            {
                obj.Description = loc["Desc.Sample.PSOContainer"];
            }
            else if (obj.Name == "ForeignSecurityPrincipals")
            {
                obj.Description = loc["Desc.ForeignSecurityPrincipals"];
            }
            else if (obj.Name == "Policies" && obj.Type == ADObjectType.Container && obj.Parent?.Name == "System")
            {
                obj.Description = loc["Desc.GPOPoliciesContainer"];
            }
            else if (obj.Name == "Default Domain Policy" && obj.Type == ADObjectType.Policy)
            {
                obj.Description = loc["Desc.DefaultDomainPolicy"];
            }
            else if (obj.Name == "Default Domain Controllers Policy" && obj.Type == ADObjectType.Policy)
            {
                obj.Description = loc["Desc.DefaultDomainControllersPolicy"];
            }

            // Parcourir récursivement les enfants
            foreach (var child in obj.Children)
            {
                UpdateDescriptionRecursive(child, loc);
            }
        }
    }
}
