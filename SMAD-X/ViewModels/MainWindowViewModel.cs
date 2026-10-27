using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using SMADX.Models;
using SMADX.Services;

namespace SMADX.ViewModels
{
    /// <summary>
    /// ViewModel de la fenêtre principale. Réparti en fichiers partiels par domaine :
    /// <list type="bullet">
    /// <item>MainWindowViewModel.cs : état, sélection et panneau de détails ;</item>
    /// <item>MainWindowViewModel.Files.cs : nouveau domaine, ouverture, sauvegarde, exports ;</item>
    /// <item>MainWindowViewModel.Edit.cs : ajout, suppression, copier/coller, renommage ;</item>
    /// <item>MainWindowViewModel.Tree.cs : recherche, déplier/replier, panneau des sites ;</item>
    /// <item>MainWindowViewModel.Windows.cs : fenêtres secondaires, thème et langue.</item>
    /// </list>
    /// Les fenêtres et sélecteurs de fichiers passent par <see cref="IDialogService"/>.
    /// </summary>
    public partial class MainWindowViewModel : ViewModelBase
    {
        private readonly IDialogService _dialogs;
        private readonly ADDataService _dataService;
        private readonly ADValidationService _validationService;
        private readonly ADImportPowerShellService _importPowerShellService;
        private readonly ADDocumentReportService _reportService;

        [ObservableProperty]
        private ObservableCollection<ADTreeNode> _rootNodes = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SelectedObjectName))]
        [NotifyPropertyChangedFor(nameof(SelectedObjectType))]
        [NotifyPropertyChangedFor(nameof(SelectedObjectDescription))]
        [NotifyPropertyChangedFor(nameof(SelectedObjectDistinguishedName))]
        [NotifyPropertyChangedFor(nameof(SelectedObjectTier))]
        [NotifyPropertyChangedFor(nameof(SelectedSupportsMemberOf))]
        [NotifyPropertyChangedFor(nameof(SelectedSupportsGpoLinks))]
        [NotifyPropertyChangedFor(nameof(SelectedIsPso))]
        [NotifyPropertyChangedFor(nameof(SelectedMemberOf))]
        [NotifyPropertyChangedFor(nameof(SelectedLinkedGPOs))]
        [NotifyPropertyChangedFor(nameof(SelectedPSOAppliesTo))]
        [NotifyPropertyChangedFor(nameof(CanAddOU))]
        [NotifyPropertyChangedFor(nameof(CanAddContainer))]
        [NotifyPropertyChangedFor(nameof(CanAddUser))]
        [NotifyPropertyChangedFor(nameof(CanAddGroup))]
        [NotifyPropertyChangedFor(nameof(CanAddComputer))]
        [NotifyPropertyChangedFor(nameof(CanAddGMSA))]
        [NotifyPropertyChangedFor(nameof(CanAddPolicy))]
        [NotifyPropertyChangedFor(nameof(CanAddPSO))]
        [NotifyPropertyChangedFor(nameof(CanDeleteSelected))]
        [NotifyPropertyChangedFor(nameof(CanCopySelected))]
        [NotifyPropertyChangedFor(nameof(CanManageRelations))]
        [NotifyPropertyChangedFor(nameof(CanAddToGroup))]
        [NotifyPropertyChangedFor(nameof(CanAddAnyChild))]
        [NotifyPropertyChangedFor(nameof(CanAddMember))]
        [NotifyPropertyChangedFor(nameof(CanAddGroupToGroup))]
        [NotifyPropertyChangedFor(nameof(SelectedObjectTierIndex))]
        private ADTreeNode? _selectedNode;

        /// <summary>Root ADObject of the currently loaded domain tree (null when no domain loaded).</summary>
        public ADObject? RootObject => RootNodes.Count > 0 ? RootNodes[0].Data : null;

        /// <summary>Sites topology loaded from the v2 document (null for v1 files or if not yet imported).</summary>
        [ObservableProperty]
        private ADSitesTopology? _sitesTopology;

        /// <summary>Full v2 document — kept in memory for save-round-trip preservation of SitesTopology.</summary>
        private ADRootDocument? _currentDocument;

        [ObservableProperty]
        private bool _isMarkdownEditMode;

        [ObservableProperty]
        private string _objectCountsSummary = string.Empty;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        public ObservableCollection<string> AvailableTiers { get; }

        /// <summary>Constructeur du concepteur XAML (Design.DataContext) : aucune fenêtre ne peut s'ouvrir.</summary>
        public MainWindowViewModel() : this(new NullDialogService())
        {
        }

        public MainWindowViewModel(IDialogService dialogs)
        {
            _dialogs = dialogs;
            _dataService = new ADDataService();
            _validationService = new ADValidationService();
            _importPowerShellService = new ADImportPowerShellService();
            _reportService = new ADDocumentReportService();

            // Initialiser la liste des tiers disponibles
            AvailableTiers = new ObservableCollection<string>();
            RefreshAvailableTiers();
            TierConfigurationService.Instance.Tiers.CollectionChanged += (s, e) => RefreshAvailableTiers();

            // S'abonner aux changements de langue pour mettre à jour le statut et les descriptions
            LocalizationService.Instance.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(LocalizationService.CurrentCulture))
                {
                    StatusMessage = LocalizationService.Instance["Status.Ready"];
                    RefreshLabMenus();

                    // Textes composés ici, et non liés au proxy de traduction : type affiché, comptes, panneau des sites
                    OnPropertyChanged(nameof(SelectedObjectType));
                    UpdateObjectCounts();
                    foreach (var site in SiteRootNodes)
                        site.Relocalize();

                    // Mettre à jour les descriptions des objets existants
                    if (RootObject != null)
                    {
                        _dataService.UpdateDescriptionsForCurrentLanguage(RootObject);

                        // Forcer le rafraîchissement de l'affichage si un objet est sélectionné
                        if (SelectedNode != null)
                            OnPropertyChanged(nameof(SelectedObjectDescription));
                    }
                }
            };

            // Créer une structure d'exemple au démarrage
            InitializeSampleData();
        }

        private void RefreshAvailableTiers()
        {
            AvailableTiers.Clear();
            AvailableTiers.Add(" ");
            foreach (var tier in TierConfigurationService.Instance.Tiers)
                AvailableTiers.Add(tier.Name);
        }

        // ─── Panneau de détails de l'objet sélectionné ───────────────────────────

        /// <summary>
        /// Nom de l'objet sélectionné. Le champ du panneau (et F2) ne le transmet qu'à la validation
        /// (Entrée ou sortie du champ) : un renommage refusé laisse l'ancien nom.
        /// </summary>
        public string SelectedObjectName
        {
            get => SelectedNode?.Data?.Name ?? string.Empty;
            set
            {
                if (SelectedNode?.Data != null && SelectedNode.Data.Name != value)
                    TryRename(value);
            }
        }

        public string SelectedObjectType => SelectedNode?.Data is { } data ? Loc[$"ObjectType.{data.Type}"] : string.Empty;

        public string SelectedObjectDescription
        {
            get => SelectedNode?.Data?.Description ?? string.Empty;
            set
            {
                if (SelectedNode?.Data != null)
                {
                    SelectedNode.Data.Description = value;
                    SelectedNode.Data.ModifiedDate = DateTime.Now;
                    OnPropertyChanged();
                }
            }
        }

        public string SelectedObjectDistinguishedName => SelectedNode?.Data?.DistinguishedName ?? string.Empty;

        public string SelectedObjectTier
        {
            get => string.IsNullOrWhiteSpace(SelectedNode?.Data?.Tier) ? " " : SelectedNode!.Data!.Tier!;
            set
            {
                if (SelectedNode?.Data != null)
                {
                    SelectedNode.Data.Tier = string.IsNullOrWhiteSpace(value) ? null : value;
                    SelectedNode.Data.ModifiedDate = DateTime.Now;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SelectedObjectTierIndex));
                }
            }
        }

        public int SelectedObjectTierIndex
        {
            get
            {
                var tier = SelectedNode?.Data?.Tier;
                if (string.IsNullOrWhiteSpace(tier)) return 0;
                var idx = AvailableTiers.IndexOf(tier);
                return idx < 0 ? 0 : idx;
            }
            set
            {
                if (SelectedNode?.Data != null)
                {
                    var newTier = value <= 0 || value >= AvailableTiers.Count
                        ? null
                        : AvailableTiers[value];
                    SelectedNode.Data.Tier = string.IsNullOrWhiteSpace(newTier) ? null : newTier;
                    SelectedNode.Data.ModifiedDate = DateTime.Now;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SelectedObjectTier));
                }
            }
        }

        /// <summary>Vrai si l'objet sélectionné supporte MemberOf (User/Group/Computer)</summary>
        public bool SelectedSupportsMemberOf =>
            SelectedNode?.Data?.Type is ADObjectType.User or ADObjectType.Group or ADObjectType.Computer;

        /// <summary>Vrai si l'objet sélectionné est une OU ou un Domain (supporte les GPO liées)</summary>
        public bool SelectedSupportsGpoLinks =>
            SelectedNode?.Data?.Type is ADObjectType.OrganizationalUnit or ADObjectType.Domain;

        /// <summary>Vrai si l'objet sélectionné est un PSO</summary>
        public bool SelectedIsPso =>
            SelectedNode?.Data?.Type == ADObjectType.PasswordSettingsObject;

        /// <summary>MemberOf de l'objet sélectionné</summary>
        public ObservableCollection<string>? SelectedMemberOf => SelectedNode?.Data?.MemberOf;

        /// <summary>GPO liées à l'OU sélectionnée</summary>
        public ObservableCollection<string>? SelectedLinkedGPOs => SelectedNode?.Data?.LinkedGPOs;

        /// <summary>Sujets du PSO sélectionné</summary>
        public ObservableCollection<string>? SelectedPSOAppliesTo => SelectedNode?.Data?.PSOAppliesTo;

        /// <summary>Attributs de sécurité de l'objet sélectionné (section « Sécurité » du panneau).</summary>
        [ObservableProperty]
        private ObjectSecurityViewModel? _selectedSecurity;

        /// <summary>Politique de mot de passe du domaine ou du PSO sélectionné (section du panneau de détails), sinon null.</summary>
        [ObservableProperty]
        private PasswordPolicyViewModel? _selectedPasswordPolicy;

        partial void OnSelectedNodeChanged(ADTreeNode? value)
        {
            SelectedSecurity = value?.Data is { } data ? new ObjectSecurityViewModel(data) : null;
            SelectedPasswordPolicy = value?.Data is { Type: ADObjectType.Domain or ADObjectType.PasswordSettingsObject } policyHolder
                ? new PasswordPolicyViewModel(policyHolder)
                : null;
            // Les modifications faites dans le panneau sur l'objet précédent deviennent un état annulable
            RecordHistory();
        }

        // ─── Chargement d'un domaine et barre d'état ─────────────────────────────

        /// <summary>
        /// Remplace le domaine affiché (exemple, nouveau domaine ou fichier ouvert) et
        /// conserve le document complet pour que la sauvegarde garde la topologie des sites.
        /// </summary>
        private void ShowDomain(ADRootDocument document, bool expandDefaultNodes)
        {
            _currentDocument = document;
            SitesTopology = document.SitesTopology;

            var rootNode = new ADTreeNode(document.Domain) { IsExpanded = true };
            if (expandDefaultNodes)
                ExpandDefaultDomainNodes(rootNode);

            SelectedNode = null;
            RootNodes.Clear();
            RootNodes.Add(rootNode);
            UpdateObjectCounts();
            ResetHistory();
            ResetBaseline();
            MarkSaved();
        }

        private void UpdateObjectCounts()
        {
            if (RootObject == null) return;

            var counts = RootObject.CountObjectsByType()
                .Select(kvp => string.Format(Loc["Status.ObjectCount"], Loc[$"ObjectCount.{kvp.Key}"], kvp.Value))
                .ToList();

            if (SitesTopology != null && SitesTopology.Sites.Count > 0)
                counts.Add(string.Format(Loc["Status.SiteCount"], SitesTopology.Sites.Count));

            ObjectCountsSummary = string.Join(" | ", counts);
        }

        /// <summary>
        /// Développe automatiquement les nœuds importants d'un nouveau domaine :
        /// System → Policies (pour que les GPOs par défaut soient visibles immédiatement).
        /// </summary>
        private static void ExpandDefaultDomainNodes(ADTreeNode root)
        {
            foreach (var child in root.Children)
            {
                // Développer System et Domain Controllers au premier niveau
                if (child.Data.Name is "System" or "Domain Controllers")
                {
                    child.IsExpanded = true;
                    foreach (var grandChild in child.Children)
                    {
                        // Développer Policies sous System
                        if (grandChild.Data.Name == "Policies")
                            grandChild.IsExpanded = true;
                    }
                }
            }
        }

        /// <summary>Cherche récursivement le ADTreeNode correspondant à un ADObject</summary>
        private static ADTreeNode? FindTreeNode(ADTreeNode treeNode, ADObject target)
        {
            if (treeNode.Data == target) return treeNode;
            foreach (var child in treeNode.Children)
            {
                var found = FindTreeNode(child, target);
                if (found != null) return found;
            }
            return null;
        }
    }
}
