using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
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
        /// <summary>Options JSON des fichiers SMAD-X (énumérations par leur nom), partagées avec les exercices.</summary>
        public static JsonSerializerOptions SerializerOptions => JsonOptions;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };

        // ── Format v2 (ADRootDocument) ────────────────────────────────────────

        /// <summary>
        /// Sauvegarde le document racine v2 (domaine + topologie de sites).
        /// </summary>
        public async Task<bool> SaveDocumentAsync(ADRootDocument document, string filePath)
        {
            try
            {
                // Persist current tier configuration (including custom tiers)
                document.TierConfigurations = TierConfigurationService.Instance.Tiers
                    .Select(t => new TierConfiguration(t.Name, t.Color, t.Description, t.Level))
                    .ToList();
                document.Version = ADRootDocument.CurrentVersion;

                var json = JsonSerializer.Serialize(document, JsonOptions);
                await File.WriteAllTextAsync(filePath, json);
                return true;
            }
            catch (Exception ex)
            {
                ErrorReporter.Log("Sauvegarde", ex);
                return false;
            }
        }

        /// <summary>
        /// Charge un fichier .smad-x.json de n'importe quelle version connue : le JSON est d'abord
        /// migré vers <see cref="ADRootDocument.CurrentVersion"/> (voir <see cref="ADDocumentMigrator"/>),
        /// puis désérialisé. Un fichier écrit par une version plus récente de SMAD-X est refusé.
        /// Ne modifie pas les tiers de l'application : voir <see cref="ApplyTierConfiguration"/>.
        /// </summary>
        public async Task<DocumentLoadResult> LoadDocumentAsync(string filePath)
        {
            if (!File.Exists(filePath))
                return DocumentLoadResult.Failed(DocumentLoadStatus.NotFound);

            int fileVersion = 0;
            try
            {
                var json = await File.ReadAllTextAsync(filePath);
                if (JsonNode.Parse(json) is not JsonObject root)
                    return DocumentLoadResult.Failed(DocumentLoadStatus.InvalidFormat, error: "La racine du fichier n'est pas un objet JSON.");

                var migrated = ADDocumentMigrator.MigrateToCurrent(root, out fileVersion);
                var document = migrated.Deserialize<ADRootDocument>(JsonOptions);
                if (document?.Domain == null)
                    return DocumentLoadResult.Failed(DocumentLoadStatus.InvalidFormat, fileVersion, "Aucun domaine dans le fichier.");

                RestoreParentReferences(document.Domain, null);
                return new DocumentLoadResult(DocumentLoadStatus.Loaded, document, fileVersion);
            }
            catch (DocumentTooNewException ex)
            {
                return DocumentLoadResult.Failed(DocumentLoadStatus.TooNew, ex.FileVersion);
            }
            catch (Exception ex) when (ex is JsonException or FormatException or InvalidOperationException or NotSupportedException)
            {
                return DocumentLoadResult.Failed(DocumentLoadStatus.InvalidFormat, fileVersion, ex.Message);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return DocumentLoadResult.Failed(DocumentLoadStatus.Unreadable, error: ex.Message);
            }
        }

        /// <summary>
        /// Rend actifs les tiers enregistrés dans le document, ou les tiers par défaut s'il n'en a pas
        /// (sinon ceux du document précédent resteraient affichés). À appeler pour le document ouvert
        /// dans la fenêtre principale, pas pour un fichier seulement comparé.
        /// </summary>
        public void ApplyTierConfiguration(ADRootDocument document)
        {
            if (document.TierConfigurations is { Count: > 0 } tiers)
                TierConfigurationService.Instance.RestoreFromList(tiers);
            else
                TierConfigurationService.Instance.ResetToDefaults();
        }

        // ── Format v1 (rétro-compatibilité) — conservé pour l'export JSON simple ──

        /// <summary>
        /// Sauvegarde uniquement l'arbre AD (format v1 / JSON simple).
        /// Utilisé par ExportToJson pour un export "plat" sans sites.
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
                ErrorReporter.Log("Export JSON", ex);
                return false;
            }
        }

        /// <summary>
        /// Charge uniquement l'arbre AD (format v1 — conservé pour compatibilité).
        /// Préférez LoadDocumentAsync qui gère les deux formats.
        /// </summary>
        public async Task<ADObject?> LoadFromFileAsync(string filePath)
        {
            var result = await LoadDocumentAsync(filePath);
            return result.Document?.Domain;
        }

        /// <summary>Rétablit les liens Parent (non sérialisés) d'un arbre désérialisé.</summary>
        public static void RestoreParentReferences(ADObject current, ADObject? parent)
        {
            current.Parent = parent;

            foreach (var child in current.Children)
            {
                RestoreParentReferences(child, current);
            }
        }

        /// <summary>
        /// Domaine tel qu'il sort d'une installation d'Active Directory : conteneurs et groupes intégrés,
        /// premier contrôleur de domaine, deux GPO par défaut et la politique de mot de passe par défaut.
        /// </summary>
        /// <param name="domainName">Nom du domaine (FQDN)</param>
        /// <param name="enableTiering">Si true, affecte automatiquement les tiers aux objets</param>
        public ADObject CreateFreshDomainStructure(string domainName, bool enableTiering = true) =>
            BuildDomainStructure(domainName, enableTiering, labBase: false);

        /// <summary>
        /// Base des exercices de SMAD-X Studio : le domaine neuf, plus un poste, trois GPO métier, les groupes de
        /// délégation GG-* et leurs délégations. Les recettes et les générateurs d'exercices partent de ce domaine.
        /// </summary>
        public ADObject CreateLabBaseStructure(string domainName, bool enableTiering = true) =>
            BuildDomainStructure(domainName, enableTiering, labBase: true);

        /// <summary>Politique de mot de passe par défaut d'un domaine neuf (Default Domain Policy).</summary>
        public static void ApplyDefaultPasswordPolicy(ADObject domain)
        {
            domain.PSOMinPasswordLength = 7;
            domain.PSOPasswordHistoryCount = 24;
            domain.PSOComplexityEnabled = true;
            domain.PSOMaxPasswordAgeDays = 42;
            domain.PSOMinPasswordAgeDays = 1;
            domain.PSOLockoutThreshold = 0;
            // Tout utilisateur authentifié peut joindre 10 ordinateurs au domaine
            domain.MachineAccountQuota = 10;
        }

        private ADObject BuildDomainStructure(string domainName, bool enableTiering, bool labBase)
        {
            var loc = LocalizationService.Instance;

            var domain = new ADObject(domainName, ADObjectType.Domain)
            {
                Description = string.Format(loc["Desc.Domain"], domainName)
            };
            ApplyDefaultPasswordPolicy(domain);

            // Helper pour affecter un tier seulement si le tiering est activé
            string? GetTier(string tier) => enableTiering ? tier : null;

            // 1. Container Builtin (groupes intégrés système)
            AddBuiltinContainer(domain, GetTier, loc);

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
                Tier = GetTier("Tier 0"),
                Parent = usersContainer
            };
            usersContainer.Children.Add(readOnlyDCs);
            AddUsersContainerExtras(usersContainer, GetTier, loc);

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

            // Un poste dans Computers (base des exercices ; un domaine neuf n'en a pas)
            if (labBase)
                computersContainer.Children.Add(new ADObject("WORKSTATION-01", ADObjectType.Computer)
                {
                    Description = loc["Desc.Computers.Workstation"],
                    Tier = GetTier("Tier 2"),
                    Parent = computersContainer
                });

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
                SiteName = "Default-First-Site-Name",
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
            AddAdminSdHolder(systemContainer, GetTier, loc);

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

            if (labBase)
                AddLabBaseObjects(domain, usersContainer, computersContainer, domainControllersOU, policiesContainer, GetTier, loc);
            else
                domain.UpdateDistinguishedNameTree();

            // 6. Container ForeignSecurityPrincipals
            var fspContainer = new ADObject("ForeignSecurityPrincipals", ADObjectType.Container)
            {
                Description = loc["Desc.ForeignSecurityPrincipals"],
                Parent = domain
            };
            domain.Children.Add(fspContainer);

            AddRootContainers(domain, loc);

            // Mettre à jour les DN des conteneurs ajoutés après le premier calcul
            domain.UpdateDistinguishedNameTree();

            BuiltInSecurityDefaults.Apply(domain);
            return domain;
        }

        /// <summary>Objets de la base des exercices : GPO métier, groupes de délégation GG-* et leurs délégations.</summary>
        private static void AddLabBaseObjects(ADObject domain, ADObject usersContainer, ADObject computersContainer, ADObject domainControllersOU,
            ADObject policiesContainer, Func<string, string?> GetTier, LocalizationService loc)
        {
            // GPO 3 : GPO-Baseline-DC — Baseline sécurité site par défaut
            var gpoBaselineDC = new ADObject("GPO-Baseline-DC", ADObjectType.Policy)
            {
                Description = loc["Desc.Sample.GPO.DefaultSite"],
                Tier = GetTier("Tier 0"),
                Parent = policiesContainer
            };
            policiesContainer.Children.Add(gpoBaselineDC);

            // GPO 4 : GPO-Paris-Workstations — Postes de travail Site-Paris
            var gpoParis = new ADObject("GPO-Paris-Workstations", ADObjectType.Policy)
            {
                Description = loc["Desc.Sample.GPO.Paris"],
                Tier = GetTier("Tier 1"),
                Parent = policiesContainer
            };
            policiesContainer.Children.Add(gpoParis);

            // GPO 5 : GPO-Lyon-Workstations — Postes de travail Site-Lyon
            var gpoLyon = new ADObject("GPO-Lyon-Workstations", ADObjectType.Policy)
            {
                Description = loc["Desc.Sample.GPO.Lyon"],
                Tier = GetTier("Tier 1"),
                Parent = policiesContainer
            };
            policiesContainer.Children.Add(gpoLyon);

            // ── Groupes de délégation ────────────────────────────────────────
            var ggHelpDeskFresh = new ADObject("GG-HelpDesk", ADObjectType.Group)
            {
                Description = loc["Desc.Delegation.GGHelpDesk"],
                Tier = GetTier("Tier 2"),
                Parent = usersContainer
            };
            usersContainer.Children.Add(ggHelpDeskFresh);

            var ggITWorkstationsFresh = new ADObject("GG-IT-Workstations", ADObjectType.Group)
            {
                Description = loc["Desc.Delegation.GGITWorkstations"],
                Tier = GetTier("Tier 2"),
                Parent = usersContainer
            };
            usersContainer.Children.Add(ggITWorkstationsFresh);

            var ggITServersFresh = new ADObject("GG-IT-Servers", ADObjectType.Group)
            {
                Description = loc["Desc.Delegation.GGITServers"],
                Tier = GetTier("Tier 1"),
                Parent = usersContainer
            };
            usersContainer.Children.Add(ggITServersFresh);

            var ggTier1OperatorsFresh = new ADObject("GG-Tier1-Operators", ADObjectType.Group)
            {
                Description = loc["Desc.Delegation.GGTier1Operators"],
                Tier = GetTier("Tier 0"),
                Parent = usersContainer
            };
            usersContainer.Children.Add(ggTier1OperatorsFresh);

            // Mettre à jour tous les DN avant d'assigner les TargetDN des délégations
            domain.UpdateDistinguishedNameTree();

            // ── Délégations ──────────────────────────────────────────────────
            // Delegation de réinitialisation de mot de passe sur CN=Users
            usersContainer.Delegations.Add(new ADDelegation
            {
                TrusteeName = "GG-HelpDesk",
                TrusteeType = TrusteeType.Group,
                TargetDN = usersContainer.DistinguishedName,
                Right = "ResetPassword",
                RightCategory = RightCategory.PasswordReset,
                Tier = "Tier 2"
            });
            usersContainer.Delegations.Add(new ADDelegation
            {
                TrusteeName = "GG-HelpDesk",
                TrusteeType = TrusteeType.Group,
                TargetDN = usersContainer.DistinguishedName,
                Right = "UnlockAccount",
                RightCategory = RightCategory.AccountUnlock,
                Tier = "Tier 2"
            });

            // IT-Workstations → CN=Computers
            computersContainer.Delegations.Add(new ADDelegation
            {
                TrusteeName = "GG-IT-Workstations",
                TrusteeType = TrusteeType.Group,
                TargetDN = computersContainer.DistinguishedName,
                Right = "CreateChild:computer",
                RightCategory = RightCategory.ComputerManagement,
                Tier = "Tier 2"
            });

            // Tier1-Operators → OU Domain Controllers
            domainControllersOU.Delegations.Add(new ADDelegation
            {
                TrusteeName = "GG-Tier1-Operators",
                TrusteeType = TrusteeType.Group,
                TargetDN = domainControllersOU.DistinguishedName,
                Right = "WriteAttribute",
                RightCategory = RightCategory.AttributeWrite,
                Tier = "Tier 0"
            });
        }

        /// <summary>
        /// Container Builtin et ses groupes intégrés (domaine local), tels que les crée la promotion du domaine.
        /// </summary>
        private static void AddBuiltinContainer(ADObject domain, Func<string, string?> tier, LocalizationService loc)
        {
            var builtin = new ADObject("Builtin", ADObjectType.Container) { Description = loc["Desc.Builtin"], Parent = domain };
            domain.Children.Add(builtin);
            // Les groupes de Builtin donnent leurs droits sur les contrôleurs de domaine : Tier 0, sauf Users et Guests
            foreach (var (name, key, groupTier) in new[]
            {
                ("Access Control Assistance Operators", "Desc.Builtin.AccessControlAssistance", "Tier 0"),
                ("Account Operators", "Desc.Builtin.AccountOperators", "Tier 0"),
                ("Administrators", "Desc.Builtin.Administrators", "Tier 0"),
                ("Backup Operators", "Desc.Builtin.BackupOperators", "Tier 0"),
                ("Certificate Service DCOM Access", "Desc.Builtin.CertificateServiceDcom", "Tier 0"),
                ("Cryptographic Operators", "Desc.Builtin.CryptographicOperators", "Tier 0"),
                ("Distributed COM Users", "Desc.Builtin.DistributedComUsers", "Tier 0"),
                ("Event Log Readers", "Desc.Builtin.EventLogReaders", "Tier 0"),
                ("Guests", "Desc.Builtin.Guests", "Tier 2"),
                ("Hyper-V Administrators", "Desc.Builtin.HyperVAdministrators", "Tier 0"),
                ("IIS_IUSRS", "Desc.Builtin.IisIusrs", "Tier 0"),
                ("Incoming Forest Trust Builders", "Desc.Builtin.IncomingForestTrustBuilders", "Tier 0"),
                ("Network Configuration Operators", "Desc.Builtin.NetworkConfigurationOperators", "Tier 0"),
                ("Performance Log Users", "Desc.Builtin.PerformanceLogUsers", "Tier 0"),
                ("Performance Monitor Users", "Desc.Builtin.PerformanceMonitorUsers", "Tier 0"),
                ("Pre-Windows 2000 Compatible Access", "Desc.Builtin.PreWindows2000", "Tier 0"),
                ("Print Operators", "Desc.Builtin.PrintOperators", "Tier 0"),
                ("RDS Endpoint Servers", "Desc.Builtin.RdsEndpointServers", "Tier 0"),
                ("RDS Management Servers", "Desc.Builtin.RdsManagementServers", "Tier 0"),
                ("RDS Remote Access Servers", "Desc.Builtin.RdsRemoteAccessServers", "Tier 0"),
                ("Remote Desktop Users", "Desc.Builtin.RemoteDesktopUsers", "Tier 0"),
                ("Remote Management Users", "Desc.Builtin.RemoteManagementUsers", "Tier 0"),
                ("Replicator", "Desc.Builtin.Replicator", "Tier 0"),
                ("Server Operators", "Desc.Builtin.ServerOperators", "Tier 0"),
                ("Storage Replica Administrators", "Desc.Builtin.StorageReplicaAdministrators", "Tier 0"),
                ("Terminal Server License Servers", "Desc.Builtin.TerminalServerLicenseServers", "Tier 0"),
                ("Users", "Desc.Builtin.Users", "Tier 2"),
                ("Windows Authorization Access Group", "Desc.Builtin.WindowsAuthorizationAccess", "Tier 0")
            })
                builtin.Children.Add(new ADObject(name, ADObjectType.Group) { Description = loc[key], Tier = tier(groupTier), Parent = builtin });
        }

        /// <summary>Groupes du conteneur Users absents des premières versions de SMAD-X : Domain Guests, Enterprise Read-only Domain Controllers.</summary>
        private static void AddUsersContainerExtras(ADObject usersContainer, Func<string, string?> tier, LocalizationService loc)
        {
            usersContainer.Children.Add(new ADObject("Domain Guests", ADObjectType.Group)
            {
                Description = loc["Desc.Users.DomainGuests"], Tier = tier("Tier 2"), Parent = usersContainer
            });
            usersContainer.Children.Add(new ADObject("Enterprise Read-only Domain Controllers", ADObjectType.Group)
            {
                Description = loc["Desc.Users.EnterpriseReadOnlyDCs"], Tier = tier("Tier 0"), Parent = usersContainer
            });
        }

        /// <summary>
        /// Conteneurs créés à la racine par l'installation, en plus de Builtin, Users, Computers, System et
        /// ForeignSecurityPrincipals (visibles dans « Utilisateurs et ordinateurs AD » en mode avancé).
        /// </summary>
        private static void AddRootContainers(ADObject domain, LocalizationService loc)
        {
            foreach (var (name, key) in new[]
            {
                ("Infrastructure", "Desc.Container.Infrastructure"),
                ("Keys", "Desc.Container.Keys"),
                ("LostAndFound", "Desc.Container.LostAndFound"),
                ("Managed Service Accounts", "Desc.Container.ManagedServiceAccounts"),
                ("NTDS Quotas", "Desc.Container.NtdsQuotas"),
                ("Program Data", "Desc.Container.ProgramData"),
                ("TPM Devices", "Desc.Container.TpmDevices")
            })
                domain.Children.Add(new ADObject(name, ADObjectType.Container) { Description = loc[key], Parent = domain });
        }

        /// <summary>
        /// CN=AdminSDHolder,CN=System : modèle des permissions que SDProp réapplique toutes les heures aux comptes et
        /// groupes protégés. Le contrôler, c'est contrôler tous les administrateurs.
        /// </summary>
        private static void AddAdminSdHolder(ADObject systemContainer, Func<string, string?> tier, LocalizationService loc) =>
            systemContainer.Children.Add(new ADObject("AdminSDHolder", ADObjectType.Container)
            {
                Description = loc["Desc.System.AdminSDHolder"],
                Tier = tier("Tier 0"),
                Parent = systemContainer
            });

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
            // En production depuis deux ans, la politique par défaut n'a jamais été durcie
            ApplyDefaultPasswordPolicy(domain);

            // Containers par défaut (comme dans un vrai AD)
            AddBuiltinContainer(domain, tier => tier, loc);

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

            // Écart voulu, que l'audit enseigne (PRIV-05) : un DC en lecture seule reste un contrôleur de domaine, donc Tier 0
            var readOnlyDCs = new ADObject("Read-only Domain Controllers", ADObjectType.Group)
            {
                Description = loc["Desc.Users.ReadOnlyDCs"],
                Tier = "Tier 1",
                Parent = usersContainer
            };
            usersContainer.Children.Add(readOnlyDCs);
            AddUsersContainerExtras(usersContainer, tier => tier, loc);

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

            // Compte nominatif et groupe des administrateurs Tier 0 : un nom de compte est unique dans le
            // domaine, ils ne peuvent pas s'appeler « Administrator » ni « Domain Admins »
            var adminUser = new ADObject("adm-lmartin", ADObjectType.User)
            {
                Description = loc["Desc.Sample.AdminUser"],
                Tier = "Tier 0",
                Parent = adminOU
            };
            adminOU.Children.Add(adminUser);

            var tier0Admins = new ADObject("GG-Tier0-Admins", ADObjectType.Group)
            {
                Description = loc["Desc.Sample.AdminDomainAdmins"],
                Tier = "Tier 0",
                Parent = adminOU
            };
            adminOU.Children.Add(tier0Admins);

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
                SiteName = "Default-First-Site-Name",
                Parent = domainControllersOU
            };
            domainControllersOU.Children.Add(dc01);

            var dc02 = new ADObject("DC02", ADObjectType.Computer)
            {
                Description = loc["Desc.Sample.DC02"],
                Tier = "Tier 0",
                SiteName = "Site-Paris",
                Parent = domainControllersOU
            };
            domainControllersOU.Children.Add(dc02);

            var dc03 = new ADObject("DC03", ADObjectType.Computer)
            {
                Description = loc["Desc.Sample.DC03"],
                Tier = "Tier 0",
                SiteName = "Site-Lyon",
                Parent = domainControllersOU
            };
            domainControllersOU.Children.Add(dc03);

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
            AddAdminSdHolder(systemContainer, tier => tier, loc);

            // PSOs : les administrateurs ont une politique renforcée, les utilisateurs une politique standard
            var psoAdmin = new ADObject("PSO-Tier0-Admins", ADObjectType.PasswordSettingsObject)
            {
                Description = loc["Desc.Sample.PSOAdmin"],
                Tier = "Tier 0",
                Parent = psoContainer,
                PSOPrecedence = 10, PSOMinPasswordLength = 16, PSOPasswordHistoryCount = 24, PSOComplexityEnabled = true,
                PSOMaxPasswordAgeDays = 365, PSOMinPasswordAgeDays = 1, PSOLockoutThreshold = 5, PSOLockoutDurationMinutes = 30,
                PSOLockoutObservationWindowMinutes = 30
            };
            psoContainer.Children.Add(psoAdmin);

            var psoUsers = new ADObject("PSO-Standard-Users", ADObjectType.PasswordSettingsObject)
            {
                Description = loc["Desc.Sample.PSOUsers"],
                Tier = "Tier 2",
                Parent = psoContainer,
                PSOPrecedence = 20, PSOMinPasswordLength = 12, PSOPasswordHistoryCount = 24, PSOComplexityEnabled = true,
                PSOMaxPasswordAgeDays = 0, PSOMinPasswordAgeDays = 1, PSOLockoutThreshold = 10, PSOLockoutDurationMinutes = 15,
                PSOLockoutObservationWindowMinutes = 15
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

            var gpoBaselineDCSample = new ADObject("GPO-Baseline-DC", ADObjectType.Policy)
            {
                Description = loc["Desc.Sample.GPO.DefaultSite"],
                Tier = "Tier 0",
                Parent = policiesContainer
            };
            policiesContainer.Children.Add(gpoBaselineDCSample);

            var gpoParisSample = new ADObject("GPO-Paris-Workstations", ADObjectType.Policy)
            {
                Description = loc["Desc.Sample.GPO.Paris"],
                Tier = "Tier 1",
                Parent = policiesContainer
            };
            policiesContainer.Children.Add(gpoParisSample);

            var gpoLyonSample = new ADObject("GPO-Lyon-Workstations", ADObjectType.Policy)
            {
                Description = loc["Desc.Sample.GPO.Lyon"],
                Tier = "Tier 1",
                Parent = policiesContainer
            };
            policiesContainer.Children.Add(gpoLyonSample);

            // ── Relations GPO ────────────────────────────────────────────────
            // Default Domain Policy → domaine entier
            domain.LinkedGPOs.Add("Default Domain Policy");
            // Default Domain Controllers Policy → OU Domain Controllers
            domainControllersOU.LinkedGPOs.Add("Default Domain Controllers Policy");
            // ── Relations PSO ────────────────────────────────────────────────
            psoAdmin.PSOAppliesTo.Add("Domain Admins");
            psoAdmin.PSOAppliesTo.Add("GG-Tier0-Admins");
            psoUsers.PSOAppliesTo.Add("jdoe");
            psoUsers.PSOAppliesTo.Add("asmith");

            // ── Relations MemberOf ───────────────────────────────────────────
            // AGDLP : le compte nominatif est dans le groupe global des administrateurs Tier 0, lui-même dans Domain Admins
            adminUser.MemberOf.Add("GG-Tier0-Admins");
            tier0Admins.MemberOf.Add("Domain Admins");
            user1.MemberOf.Add("Domain Users");
            user2.MemberOf.Add("Domain Users");

            // ── Groupes de délégation ────────────────────────────────────────
            var ggHelpDesk = new ADObject("GG-HelpDesk", ADObjectType.Group)
            {
                Description = loc["Desc.Delegation.GGHelpDesk"],
                Tier = "Tier 2",
                Parent = adminOU
            };
            adminOU.Children.Add(ggHelpDesk);

            var ggITWorkstations = new ADObject("GG-IT-Workstations", ADObjectType.Group)
            {
                Description = loc["Desc.Delegation.GGITWorkstations"],
                Tier = "Tier 2",
                Parent = adminOU
            };
            adminOU.Children.Add(ggITWorkstations);

            var ggITServers = new ADObject("GG-IT-Servers", ADObjectType.Group)
            {
                Description = loc["Desc.Delegation.GGITServers"],
                Tier = "Tier 1",
                Parent = adminOU
            };
            adminOU.Children.Add(ggITServers);

            var ggTier1Operators = new ADObject("GG-Tier1-Operators", ADObjectType.Group)
            {
                Description = loc["Desc.Delegation.GGTier1Operators"],
                Tier = "Tier 1",
                Parent = adminOU
            };
            adminOU.Children.Add(ggTier1Operators);

            // Mettre à jour tous les DN avant d'assigner les TargetDN des délégations
            domain.UpdateDistinguishedNameTree();

            // ── Délégations ──────────────────────────────────────────────────
            // HelpDesk → OU Users : Reset password + Unlock account
            usersOU.Delegations.Add(new ADDelegation
            {
                TrusteeName = "GG-HelpDesk",
                TrusteeType = TrusteeType.Group,
                TargetDN = usersOU.DistinguishedName,
                Right = "ResetPassword",
                RightCategory = RightCategory.PasswordReset,
                Tier = "Tier 2"
            });
            usersOU.Delegations.Add(new ADDelegation
            {
                TrusteeName = "GG-HelpDesk",
                TrusteeType = TrusteeType.Group,
                TargetDN = usersOU.DistinguishedName,
                Right = "UnlockAccount",
                RightCategory = RightCategory.AccountUnlock,
                Tier = "Tier 2"
            });

            // IT-Workstations → OU Workstations : CreateChild:computer
            workstationsOU.Delegations.Add(new ADDelegation
            {
                TrusteeName = "GG-IT-Workstations",
                TrusteeType = TrusteeType.Group,
                TargetDN = workstationsOU.DistinguishedName,
                Right = "CreateChild:computer",
                RightCategory = RightCategory.ComputerManagement,
                Tier = "Tier 2"
            });

            // IT-Servers → OU Servers : CreateChild:computer + FullControl
            serversOU.Delegations.Add(new ADDelegation
            {
                TrusteeName = "GG-IT-Servers",
                TrusteeType = TrusteeType.Group,
                TargetDN = serversOU.DistinguishedName,
                Right = "CreateChild:computer",
                RightCategory = RightCategory.ComputerManagement,
                Tier = "Tier 1"
            });
            serversOU.Delegations.Add(new ADDelegation
            {
                TrusteeName = "GG-IT-Servers",
                TrusteeType = TrusteeType.Group,
                TargetDN = serversOU.DistinguishedName,
                Right = "FullControl",
                RightCategory = RightCategory.FullControl,
                Tier = "Tier 1"
            });

            // Tier1-Operators → OU Domain Controllers : WriteAttribute
            domainControllersOU.Delegations.Add(new ADDelegation
            {
                TrusteeName = "GG-Tier1-Operators",
                TrusteeType = TrusteeType.Group,
                TargetDN = domainControllersOU.DistinguishedName,
                Right = "WriteAttribute",
                RightCategory = RightCategory.AttributeWrite,
                Tier = "Tier 0"
            });

            AddRootContainers(domain, loc);
            domain.UpdateDistinguishedNameTree();
            BuiltInSecurityDefaults.Apply(domain);
            BuiltInSecurityDefaults.ApplySampleHistory(domain, DateTime.Now);
            return domain;
        }

        /// <summary>
        /// Crée la topologie de sites AD de démonstration :
        ///   - Site Paris  (10.0.1.0/24) — DC01, DC02 (PDC Emulator + RID Master)
        ///   - Site Lyon   (10.0.2.0/24) — DC03 (site secondaire)
        ///   - Lien Paris-Lyon (coût 100, intervalle 15 min)
        /// </summary>
        public ADSitesTopology CreateSampleTopology()
        {
            var loc = LocalizationService.Instance;

            var topology = new ADSitesTopology();

            // ── Default-First-Site-Name ───────────────────────────────────────
            var siteDefault = new ADSite
            {
                Name        = "Default-First-Site-Name",
                Description = loc["Desc.Sample.DefaultSite"],
                Location    = "Site par défaut Active Directory",
            };
            siteDefault.Subnets.Add(new ADSubnet
            {
                Cidr        = "192.168.0.0/24",
                SiteName    = "Default-First-Site-Name",
                Description = loc["Desc.Sample.SubnetDefault"],
                Location    = "Default DC"
            });
            siteDefault.DomainControllers.Add("DC01.contoso.com");
            siteDefault.LinkedGPOs.Add("GPO-Baseline-DC");

            // ── Site Paris ───────────────────────────────────────────────────
            var siteParis = new ADSite
            {
                Name        = "Site-Paris",
                Description = loc["Desc.Sample.SiteParis"],
                Location    = "Paris, France — Datacenter Principal",
            };
            siteParis.Subnets.Add(new ADSubnet
            {
                Cidr        = "10.0.1.0/24",
                SiteName    = "Site-Paris",
                Description = loc["Desc.Sample.SubnetParis"],
                Location    = "Paris DC"
            });
            siteParis.DomainControllers.Add("DC02.contoso.com");
            siteParis.LinkedGPOs.Add("GPO-Paris-Workstations");

            // ── Site Lyon ────────────────────────────────────────────────────
            var siteLyon = new ADSite
            {
                Name        = "Site-Lyon",
                Description = loc["Desc.Sample.SiteLyon"],
                Location    = "Lyon, France — Site Secondaire",
            };
            siteLyon.Subnets.Add(new ADSubnet
            {
                Cidr        = "10.0.2.0/24",
                SiteName    = "Site-Lyon",
                Description = loc["Desc.Sample.SubnetLyon"],
                Location    = "Lyon DC"
            });
            siteLyon.DomainControllers.Add("DC03.contoso.com");
            siteLyon.LinkedGPOs.Add("GPO-Lyon-Workstations");

            topology.Sites.Add(siteDefault);
            topology.Sites.Add(siteParis);
            topology.Sites.Add(siteLyon);

            // ── Lien Default ↔ Paris ─────────────────────────────────────────
            topology.SiteLinks.Add(new ADSiteLink
            {
                Name                       = "SiteLink-Default-Paris",
                Transport                  = "IP",
                Cost                       = 100,
                ReplicationIntervalMinutes = 15,
                ReplicationSchedule        = "Always",
                BridgeheadAuto             = true,
                Description                = loc["Desc.Sample.SiteLinkDP"],
                SiteNames                  = { "Default-First-Site-Name", "Site-Paris" }
            });

            // ── Lien Default ↔ Lyon ──────────────────────────────────────────
            topology.SiteLinks.Add(new ADSiteLink
            {
                Name                       = "SiteLink-Default-Lyon",
                Transport                  = "IP",
                Cost                       = 150,
                ReplicationIntervalMinutes = 30,
                ReplicationSchedule        = "Always",
                BridgeheadAuto             = true,
                Description                = loc["Desc.Sample.SiteLinkDL"],
                SiteNames                  = { "Default-First-Site-Name", "Site-Lyon" }
            });

            // ── Lien Paris ↔ Lyon ────────────────────────────────────────────
            topology.SiteLinks.Add(new ADSiteLink
            {
                Name                       = "DEFAULTIPSITELINK",
                Transport                  = "IP",
                Cost                       = 100,
                ReplicationIntervalMinutes = 15,
                ReplicationSchedule        = "Always",
                BridgeheadAuto             = true,
                Description                = loc["Desc.Sample.SiteLinkPL"],
                SiteNames                  = { "Site-Paris", "Site-Lyon" }
            });

            return topology;
        }

        /// <summary>
        /// Topologie d'un domaine fraîchement promu : un seul site par défaut contenant DC01,
        /// et le lien DEFAULTIPSITELINK créé automatiquement par Windows Server
        /// </summary>
        public ADSitesTopology CreateFreshTopology(string domainName)
        {
            var loc = LocalizationService.Instance;
            var topology = new ADSitesTopology();

            var siteDefault = new ADSite
            {
                Name        = "Default-First-Site-Name",
                Description = loc["Desc.Sample.DefaultSite"]
            };
            siteDefault.DomainControllers.Add($"DC01.{domainName}");
            topology.Sites.Add(siteDefault);

            topology.SiteLinks.Add(new ADSiteLink
            {
                Name                       = "DEFAULTIPSITELINK",
                Transport                  = "IP",
                Cost                       = 100,
                ReplicationIntervalMinutes = 180,
                SiteNames                  = { "Default-First-Site-Name" }
            });

            return topology;
        }

        /// <summary>
        /// Met à jour les descriptions des objets AD dans la langue actuelle. Seule une description par défaut
        /// (le texte d'une clé « Desc.* » dans l'une des langues) est traduite : celle qu'ont écrite l'apprenant,
        /// le formateur ou l'annuaire importé reste telle quelle.
        /// </summary>
        public void UpdateDescriptionsForCurrentLanguage(ADObject root)
        {
            var loc = LocalizationService.Instance;
            UpdateDescriptionRecursive(root, loc);
        }

        private static void UpdateDescriptionRecursive(ADObject obj, LocalizationService loc)
        {
            // La description d'un nouveau domaine contient son nom
            if (obj.Type == ADObjectType.Domain
                && loc.Cultures.Any(c => LocalizationService.SameText(obj.Description, string.Format(loc.Translate("Desc.Domain", c), obj.Name))))
            {
                obj.Description = string.Format(loc["Desc.Domain"], obj.Name);
            }
            else if (loc.FindDescriptionKey(obj.Description) is { } key)
            {
                obj.Description = loc[key];
            }

            foreach (var child in obj.Children)
                UpdateDescriptionRecursive(child, loc);
        }
    }
}
