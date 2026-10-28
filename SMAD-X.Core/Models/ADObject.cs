using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SMADX.Models
{
    /// <summary>
    /// Représente un objet Active Directory (Domain, OU, User, Group, Computer, etc.)
    /// </summary>
    public partial class ADObject : ObservableObject
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        [ObservableProperty]
        private string _name = string.Empty;

        public ADObjectType Type { get; set; }

        [ObservableProperty]
        private string _description = string.Empty;

        [ObservableProperty]
        private string _distinguishedName = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(TierColor))]
        private string? _tier = null; // For tiering model (Tier 0, Tier 1, Tier 2)

        [JsonIgnore]
        public string TierColor
        {
            get
            {
                if (string.IsNullOrEmpty(Tier))
                    return "#808080"; // Gris par défaut

                return TierConfigurationService.Instance.GetTierColor(Tier);
            }
        }

        public ObservableCollection<ADObject> Children { get; set; } = new ObservableCollection<ADObject>();

        [JsonIgnore]
        public ADObject? Parent { get; set; }

        // --- Relations inter-objets ---

        /// <summary>
        /// Pour User/Group : liste des SAMAccountNames des groupes dont cet objet est membre
        /// </summary>
        public ObservableCollection<string> MemberOf { get; set; } = new();

        /// <summary>
        /// Pour OU : liste des noms de GPO liées à cette OU
        /// </summary>
        public ObservableCollection<string> LinkedGPOs { get; set; } = new();

        /// <summary>
        /// Pour les contrôleurs de domaine (Computer) : nom du site AD auquel l'objet appartient.
        /// </summary>
        public string? SiteName { get; set; }

        /// <summary>
        /// Pour PSO : liste des SAMAccountNames (users/groupes) auxquels ce PSO s'applique
        /// </summary>
        public ObservableCollection<string> PSOAppliesTo { get; set; } = new();

        /// <summary>
        /// ACL-based delegations found on this OU/Container.
        /// </summary>
        public ObservableCollection<ADDelegation> Delegations { get; set; } = new();

        /// <summary>Number of direct (non-inherited) delegations on this object.</summary>
        [JsonIgnore]
        public int DirectDelegationCount => Delegations.Count(d => !d.IsInherited);

        /// <summary>True if this object carries at least one direct delegation.</summary>
        [JsonIgnore]
        public bool HasDelegations => DirectDelegationCount > 0;

        /// <summary>Emoji icon matching the object type (same as main TreeView).</summary>
        [JsonIgnore]
        public string Icon => Type switch
        {
            ADObjectType.Domain               => "🌐",
            ADObjectType.OrganizationalUnit   => "📁",
            ADObjectType.Container            => "📦",
            ADObjectType.User                 => "👤",
            ADObjectType.Group                => "👥",
            ADObjectType.Computer             => "💻",
            ADObjectType.GMSA                 => "🔐",
            ADObjectType.Policy               => "📋",
            ADObjectType.PasswordSettingsObject => "🔑",
            _                                 => "❓"
        };

        // --- Attributs de sécurité (comptes et groupes) ---

        /// <summary>Pour User / Computer / GMSA : bits de userAccountControl liés à la sécurité.</summary>
        public ADAccountFlags AccountFlags { get; set; }

        /// <summary>Pour User / Computer / GMSA : SPN (servicePrincipalName), ex. MSSQLSvc/sql01.contoso.com:1433.</summary>
        public ObservableCollection<string> ServicePrincipalNames { get; set; } = new();

        /// <summary>Pour User / Computer / GMSA : services cibles de la délégation contrainte (msDS-AllowedToDelegateTo).</summary>
        public ObservableCollection<string> AllowedToDelegateTo { get; set; } = new();

        /// <summary>
        /// adminCount = 1 : l'objet est (ou a été) membre d'un groupe protégé. SDProp lui applique
        /// alors toutes les heures les ACL d'AdminSDHolder, et ne remet pas l'attribut à 0 s'il en sort.
        /// </summary>
        public bool AdminCount { get; set; }

        /// <summary>Pour Group : portée (null pour les autres types).</summary>
        public ADGroupScope? GroupScope { get; set; }

        /// <summary>Pour Group : sécurité ou distribution (null pour les autres types).</summary>
        public ADGroupCategory? GroupCategory { get; set; }

        /// <summary>Propriétaire de l'objet (SAMAccountName). Il peut modifier les ACL de l'objet.</summary>
        public string? Owner { get; set; }

        /// <summary>
        /// Pour User / Computer / GMSA : dernière ouverture de session (lastLogonTimestamp, répliqué
        /// avec un retard de 9 à 14 jours). Null = jamais connecté ou inconnu.
        /// </summary>
        public DateTime? LastLogonTimestamp { get; set; }

        /// <summary>Pour User / Computer / GMSA : dernier changement de mot de passe (pwdLastSet). Null = inconnu.</summary>
        public DateTime? PwdLastSet { get; set; }

        /// <summary>Vrai si le type porte des attributs de compte (userAccountControl, SPN, délégation).</summary>
        [JsonIgnore]
        public bool IsAccount => Type is ADObjectType.User or ADObjectType.Computer or ADObjectType.GMSA;

        // --- Propriétés spécifiques PSO (Fine-Grained Password Policy) ---

        public int? PSOPrecedence { get; set; }
        public int? PSOMinPasswordLength { get; set; }
        public int? PSOPasswordHistoryCount { get; set; }
        public bool? PSOComplexityEnabled { get; set; }
        public int? PSOMaxPasswordAgeDays { get; set; }
        public int? PSOMinPasswordAgeDays { get; set; }
        public int? PSOLockoutThreshold { get; set; }
        public int? PSOLockoutDurationMinutes { get; set; }
        public int? PSOLockoutObservationWindowMinutes { get; set; }

        /// <summary>
        /// Pour le domaine : ms-DS-MachineAccountQuota, nombre d'ordinateurs que tout utilisateur authentifié peut
        /// joindre au domaine (10 par défaut). Null = inconnu (document d'avant la 0.9.1).
        /// </summary>
        public int? MachineAccountQuota { get; set; }

        // --- Ordinateurs : LAPS, administrateurs locaux, sessions ---

        /// <summary>
        /// Pour Computer : expiration du mot de passe de l'administrateur local géré par LAPS (Windows LAPS
        /// msLAPS-PasswordExpirationTime, ou LAPS historique ms-Mcs-AdmPwdExpirationTime). Null = pas de LAPS.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DateTime? LapsPasswordExpiration { get; set; }

        /// <summary>Pour Computer : comptes et groupes du groupe local Administrateurs de la machine.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ObservableCollection<string>? LocalAdmins { get; set; }

        /// <summary>
        /// Pour Computer : comptes qui ont ouvert une session sur la machine. Leurs identifiants restent en mémoire,
        /// et tout administrateur de la machine peut les y voler.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ObservableCollection<string>? Sessions { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;

        public DateTime ModifiedDate { get; set; } = DateTime.Now;

        public ADObject()
        {
        }

        public ADObject(string name, ADObjectType type)
        {
            Name = name;
            Type = type;
        }

        /// <summary>
        /// Calcule le Distinguished Name basé sur la hiérarchie
        /// </summary>
        public void UpdateDistinguishedName()
        {
            var parts = new System.Collections.Generic.List<string>();
            var current = this;

            while (current != null)
            {
                if (current.Type == ADObjectType.Domain)
                {
                    // Pour les domaines, séparer par les points : contoso.com → DC=contoso,DC=com
                    var domainParts = current.Name.Split('.');
                    foreach (var domainPart in domainParts)
                    {
                        parts.Add("DC=" + domainPart);
                    }
                }
                else
                {
                    var prefix = current.Type switch
                    {
                        ADObjectType.OrganizationalUnit => "OU=",
                        ADObjectType.Container => "CN=",
                        ADObjectType.User => "CN=",
                        ADObjectType.Group => "CN=",
                        ADObjectType.Computer => "CN=",
                        ADObjectType.GMSA => "CN=",
                        ADObjectType.Policy => "CN=",
                        ADObjectType.PasswordSettingsObject => "CN=",
                        _ => "CN="
                    };
                    parts.Add(prefix + current.Name);
                }
                current = current.Parent;
            }

            // Ne pas inverser - le DN va de l'objet (CN) vers le domaine (DC)
            DistinguishedName = string.Join(",", parts);
        }

        /// <summary>
        /// Recalcule le DN de l'objet et de tous ses descendants (après renommage, collage ou
        /// création). Les délégations posées sur un objet suivent son nouveau DN.
        /// </summary>
        public void UpdateDistinguishedNameTree()
        {
            var oldDn = DistinguishedName;
            UpdateDistinguishedName();

            foreach (var d in Delegations.Where(d => string.Equals(d.TargetDN, oldDn, StringComparison.OrdinalIgnoreCase)))
                d.TargetDN = DistinguishedName;

            foreach (var child in Children)
                child.UpdateDistinguishedNameTree();
        }

        /// <summary>
        /// Clone l'objet et tous ses enfants
        /// </summary>
        public ADObject Clone()
        {
            var clone = new ADObject
            {
                Id = Guid.NewGuid(),
                Name = Name + " (Copy)",
                Type = Type,
                Description = Description,
                // Conservé pour que le recalcul du DN après collage puisse recibler les délégations
                DistinguishedName = DistinguishedName,
                Tier = Tier,
                SiteName = SiteName,
                MemberOf = new ObservableCollection<string>(MemberOf),
                LinkedGPOs = new ObservableCollection<string>(LinkedGPOs),
                PSOAppliesTo = new ObservableCollection<string>(PSOAppliesTo),
                Delegations = new ObservableCollection<ADDelegation>(Delegations.Select(d => new ADDelegation
                {
                    TrusteeName = d.TrusteeName,
                    TrusteeType = d.TrusteeType,
                    TargetDN = d.TargetDN,
                    Right = d.Right,
                    RightCategory = d.RightCategory,
                    IsInherited = d.IsInherited,
                    Tier = d.Tier
                })),
                // Comme la copie dans « Utilisateurs et ordinateurs AD » : les options du compte et la
                // délégation sont reprises, mais pas les SPN (ils doivent rester uniques dans la forêt),
                // ni adminCount (posé par SDProp), ni le propriétaire (le créateur de la copie)
                AccountFlags = AccountFlags,
                AllowedToDelegateTo = new ObservableCollection<string>(AllowedToDelegateTo),
                // Les administrateurs locaux suivent la copie ; ni les sessions ni le mot de passe LAPS
                LocalAdmins = LocalAdmins is { } admins ? new ObservableCollection<string>(admins) : null,
                GroupScope = GroupScope,
                GroupCategory = GroupCategory,
                PSOPrecedence = PSOPrecedence,
                PSOMinPasswordLength = PSOMinPasswordLength,
                PSOPasswordHistoryCount = PSOPasswordHistoryCount,
                PSOComplexityEnabled = PSOComplexityEnabled,
                PSOMaxPasswordAgeDays = PSOMaxPasswordAgeDays,
                PSOMinPasswordAgeDays = PSOMinPasswordAgeDays,
                PSOLockoutThreshold = PSOLockoutThreshold,
                PSOLockoutDurationMinutes = PSOLockoutDurationMinutes,
                PSOLockoutObservationWindowMinutes = PSOLockoutObservationWindowMinutes,
                CreatedDate = DateTime.Now,
                ModifiedDate = DateTime.Now
            };

            foreach (var child in Children)
            {
                var childClone = child.Clone();
                childClone.Parent = clone;
                clone.Children.Add(childClone);
            }

            return clone;
        }

        /// <summary>
        /// Compte récursivement les objets par type
        /// </summary>
        public System.Collections.Generic.Dictionary<ADObjectType, int> CountObjectsByType()
        {
            var counts = new System.Collections.Generic.Dictionary<ADObjectType, int>();
            CountRecursive(this, counts);
            return counts;
        }

        private static void CountRecursive(ADObject obj, System.Collections.Generic.Dictionary<ADObjectType, int> counts)
        {
            if (!counts.ContainsKey(obj.Type))
                counts[obj.Type] = 0;
            counts[obj.Type]++;

            foreach (var child in obj.Children)
            {
                CountRecursive(child, counts);
            }
        }
    }
}
