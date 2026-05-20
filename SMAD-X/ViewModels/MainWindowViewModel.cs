using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SMADX.Models;
using SMADX.Services;

namespace SMADX.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        private readonly ADDataService _dataService;
        private readonly ADValidationService _validationService;
        private readonly ADPowerShellExportService _powerShellExportService;
        private readonly ADImportPowerShellService _importPowerShellService;

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
        private ADTreeNode? _selectedNode;

        public string SelectedObjectName 
        {
            get => SelectedNode?.Data?.Name ?? string.Empty;
            set
            {
                if (SelectedNode?.Data != null && SelectedNode.Data.Name != value)
                {
                    SelectedNode.Data.Name = value;
                    SelectedNode.Data.ModifiedDate = DateTime.Now;
                    SelectedNode.Data.UpdateDistinguishedName();
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SelectedObjectDistinguishedName));
                }
            }
        }

        public string SelectedObjectType => SelectedNode?.Data?.Type.ToString() ?? string.Empty;

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
            get => SelectedNode?.Data?.Tier ?? string.Empty;
            set
            {
                if (SelectedNode?.Data != null)
                {
                    SelectedNode.Data.Tier = string.IsNullOrWhiteSpace(value) ? null : value;
                    SelectedNode.Data.ModifiedDate = DateTime.Now;
                    OnPropertyChanged();
                }
            }
        }

        [ObservableProperty]
        private bool _isMarkdownEditMode;

        [ObservableProperty]
        private string _objectCountsSummary = string.Empty;

        [ObservableProperty]
        private string _statusMessage = string.Empty;

        // ─── Recherche ────────────────────────────────────────────────────────────

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                    ApplySearch();
            }
        }

        [RelayCommand]
        private void ClearSearch() => SearchText = string.Empty;

        /// <summary>
        /// Applique le filtre de recherche : met à jour IsVisible sur les nœuds,
        /// et expand les parents des nœuds correspondants.
        /// </summary>
        private void ApplySearch()
        {
            var query = _searchText.Trim();
            foreach (var root in RootNodes)
                ApplySearchToNode(root, query);
        }

        private bool ApplySearchToNode(ADTreeNode node, string query)
        {
            bool selfMatch = string.IsNullOrEmpty(query) ||
                             node.Data.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                             node.Data.Type.ToString().Contains(query, StringComparison.OrdinalIgnoreCase) ||
                             (!string.IsNullOrEmpty(node.Data.Description) &&
                              node.Data.Description.Contains(query, StringComparison.OrdinalIgnoreCase));

            bool anyChildMatch = false;
            foreach (var child in node.Children)
                if (ApplySearchToNode(child, query))
                    anyChildMatch = true;

            node.IsVisible = selfMatch || anyChildMatch;
            if (anyChildMatch && !string.IsNullOrEmpty(query))
                node.IsExpanded = true;

            return node.IsVisible;
        }

        // --- Propriétés de relations contextuelles pour le panneau de détails ---

        // ─── Propriétés pour activer/griser le menu contextuel ───────────────────

        private bool CanAdd(ADObjectType childType) =>
            SelectedNode != null &&
            _validationService.ValidateContainerRule(SelectedNode.Data.Type, childType, out _);

        public bool CanAddOU        => CanAdd(ADObjectType.OrganizationalUnit);
        public bool CanAddContainer => CanAdd(ADObjectType.Container);
        public bool CanAddUser      => CanAdd(ADObjectType.User);
        public bool CanAddGroup     => CanAdd(ADObjectType.Group);
        public bool CanAddComputer  => CanAdd(ADObjectType.Computer);
        public bool CanAddGMSA      => CanAdd(ADObjectType.GMSA);
        public bool CanAddPolicy    => CanAdd(ADObjectType.Policy);
        public bool CanAddPSO       => CanAdd(ADObjectType.PasswordSettingsObject);
        public bool CanDeleteSelected  => SelectedNode != null && SelectedNode.Parent != null;
        public bool CanCopySelected    => SelectedNode != null;
        public bool CanManageRelations => SelectedNode != null;
        /// <summary>Vrai si au moins un type enfant peut être ajouté (pour afficher le séparateur)</summary>
        public bool CanAddAnyChild  => CanAddOU || CanAddContainer || CanAddUser || CanAddGroup ||
                                       CanAddComputer || CanAddGMSA || CanAddPolicy || CanAddPSO;
        /// <summary>Vrai si l'objet sélectionné peut être ajouté à un groupe (User, Computer, GMSA)</summary>
        public bool CanAddToGroup =>
            SelectedNode != null &&
            (SelectedNode.Data.Type == ADObjectType.User ||
             SelectedNode.Data.Type == ADObjectType.Computer ||
             SelectedNode.Data.Type == ADObjectType.GMSA);
        /// <summary>Vrai si l'objet sélectionné est un groupe (peut être imbriqué dans un autre groupe)</summary>
        public bool CanAddGroupToGroup =>
            SelectedNode != null &&
            SelectedNode.Data.Type == ADObjectType.Group;
        /// <summary>Vrai si l'objet sélectionné est un groupe (peut recevoir des membres)</summary>
        public bool CanAddMember =>
            SelectedNode != null &&
            SelectedNode.Data.Type == ADObjectType.Group;

        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>Vrai si l'objet sélectionné supporte MemberOf (User/Group)</summary>
        public bool SelectedSupportsMemberOf =>
            SelectedNode?.Data?.Type is ADObjectType.User or ADObjectType.Group;

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

        private ADTreeNode? _clipboardNode;

        public ObservableCollection<string> AvailableTiers { get; }

        public MainWindowViewModel()
        {
            _dataService = new ADDataService();
            _validationService = new ADValidationService();
            _powerShellExportService = new ADPowerShellExportService();
            _importPowerShellService = new ADImportPowerShellService();

            // Initialiser la liste des tiers disponibles
            AvailableTiers = new ObservableCollection<string> { " " };
            foreach (var tier in TierConfigurationService.Instance.Tiers)
            {
                AvailableTiers.Add(tier.Name);
            }

            // S'abonner aux changements de tiers
            TierConfigurationService.Instance.Tiers.CollectionChanged += (s, e) =>
            {
                AvailableTiers.Clear();
                AvailableTiers.Add(" ");
                foreach (var tier in TierConfigurationService.Instance.Tiers)
                {
                    AvailableTiers.Add(tier.Name);
                }
            };

            // S'abonner aux changements de langue pour mettre à jour le statut et les descriptions
            LocalizationService.Instance.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(LocalizationService.CurrentCulture))
                {
                    StatusMessage = LocalizationService.Instance["Status.Ready"];

                    // Mettre à jour les descriptions des objets existants
                    if (RootNodes.Count > 0)
                    {
                        _dataService.UpdateDescriptionsForCurrentLanguage(RootNodes[0].Data);

                        // Forcer le rafraîchissement de l'affichage si un objet est sélectionné
                        if (SelectedNode != null)
                        {
                            OnPropertyChanged(nameof(SelectedObjectDescription));
                        }
                    }
                }
            };

            // Créer une structure d'exemple au démarrage
            InitializeSampleData();
        }

        [RelayCommand]
        private void InitializeSampleData()
        {
            var root = _dataService.CreateSampleStructure();
            var rootNode = new ADTreeNode(root) { IsExpanded = true };
            ExpandDefaultDomainNodes(rootNode);
            RootNodes.Clear();
            RootNodes.Add(rootNode);
            UpdateObjectCounts();
            StatusMessage = LocalizationService.Instance["Status.SampleLoaded"];
        }

        [RelayCommand]
        private async Task CreateNewDomain()
        {
            var mainWindow = GetMainWindow();
            if (mainWindow == null) return;

            var dialog = new Views.NewDomainDialog();
            var result = await dialog.ShowDialog<bool>(mainWindow);

            if (result && !string.IsNullOrEmpty(dialog.DomainName))
            {
                var root = _dataService.CreateFreshDomainStructure(dialog.DomainName, dialog.EnableTiering);
                var rootNode = new ADTreeNode(root) { IsExpanded = true };
                ExpandDefaultDomainNodes(rootNode);
                RootNodes.Clear();
                RootNodes.Add(rootNode);
                UpdateObjectCounts();

                var loc = LocalizationService.Instance;
                var tieringKey = dialog.EnableTiering ? "Status.WithTiering" : "Status.WithoutTiering";
                var tieringStatus = loc[tieringKey];
                StatusMessage = string.Format(loc["Status.DomainCreated"], dialog.DomainName, tieringStatus);
            }
        }

        [RelayCommand]
        private async Task SaveToFile()
        {
            try
            {
                var window = GetMainWindow();
                if (window == null) return;

                var file = await window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = "Sauvegarder la structure AD",
                    FileTypeChoices = new[] 
                    { 
                        new FilePickerFileType("SMAD-X JSON") { Patterns = new[] { "*.smadx.json" } },
                        new FilePickerFileType("Tous les fichiers") { Patterns = new[] { "*" } }
                    },
                    DefaultExtension = "smadx.json",
                    SuggestedFileName = $"smadx_{DateTime.Now:yyyyMMdd_HHmmss}.smadx.json"
                });

                if (file != null && RootNodes.Count > 0)
                {
                    var path = file.Path.LocalPath;
                    var success = await _dataService.SaveToFileAsync(RootNodes[0].Data, path);
                    StatusMessage = success ? $"Sauvegardé dans {path}" : "Erreur lors de la sauvegarde";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur : {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task LoadFromFile()
        {
            try
            {
                var window = GetMainWindow();
                if (window == null) return;

                var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
                {
                    Title = "Charger une structure AD",
                    FileTypeFilter = new[] 
                    { 
                        new FilePickerFileType("SMAD-X JSON") { Patterns = new[] { "*.smadx.json" } },
                        new FilePickerFileType("Tous les fichiers") { Patterns = new[] { "*" } }
                    },
                    AllowMultiple = false
                });

                if (files.Count > 0)
                {
                    var path = files[0].Path.LocalPath;
                    var root = await _dataService.LoadFromFileAsync(path);

                    if (root != null)
                    {
                        var rootNode = new ADTreeNode(root) { IsExpanded = true };
                        RootNodes.Clear();
                        RootNodes.Add(rootNode);
                        UpdateObjectCounts();
                        StatusMessage = $"Chargé depuis {path}";
                    }
                    else
                    {
                        StatusMessage = "Erreur lors du chargement";
                    }
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur : {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task ExportToJson()
        {
            try
            {
                var window = GetMainWindow();
                if (window == null) return;

                var file = await window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = "Exporter la structure en JSON",
                    FileTypeChoices = new[] 
                    { 
                        new FilePickerFileType("JSON") { Patterns = new[] { "*.json" } },
                        new FilePickerFileType("Tous les fichiers") { Patterns = new[] { "*" } }
                    },
                    DefaultExtension = "json",
                    SuggestedFileName = $"ad_structure_{DateTime.Now:yyyyMMdd_HHmmss}.json"
                });

                if (file != null && RootNodes.Count > 0)
                {
                    var path = file.Path.LocalPath;
                    var success = await _dataService.SaveToFileAsync(RootNodes[0].Data, path);
                    StatusMessage = success ? $"Structure exportée vers {path}" : "Erreur lors de l'export";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur : {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task ExportToPowerShell()
        {
            try
            {
                var window = GetMainWindow();
                if (window == null) return;

                var file = await window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = "Exporter en script PowerShell",
                    FileTypeChoices = new[] 
                    { 
                        new FilePickerFileType("PowerShell") { Patterns = new[] { "*.ps1" } },
                        new FilePickerFileType("Tous les fichiers") { Patterns = new[] { "*" } }
                    },
                    DefaultExtension = "ps1",
                    SuggestedFileName = $"Create-ADStructure_{DateTime.Now:yyyyMMdd_HHmmss}.ps1"
                });

                if (file != null && RootNodes.Count > 0)
                {
                    var path = file.Path.LocalPath;
                    var success = await _powerShellExportService.ExportToPowerShellAsync(RootNodes[0].Data, path);
                    StatusMessage = success ? $"Script PowerShell exporté vers {path}" : "Erreur lors de l'export";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur : {ex.Message}";
            }
        }

        [RelayCommand]
        private async Task GenerateImportScript()
        {
            try
            {
                var window = GetMainWindow();
                if (window == null) return;

                var file = await window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = "Générer le script PowerShell d'import depuis AD",
                    FileTypeChoices = new[]
                    {
                        new FilePickerFileType("PowerShell") { Patterns = new[] { "*.ps1" } },
                        new FilePickerFileType("Tous les fichiers") { Patterns = new[] { "*" } }
                    },
                    DefaultExtension = "ps1",
                    SuggestedFileName = "Export-ADToSimulator.ps1"
                });

                if (file != null)
                {
                    var path = file.Path.LocalPath;
                    var success = await _importPowerShellService.GenerateImportScriptAsync(path);
                    StatusMessage = success
                        ? $"Script d'import généré : {path}  —  Exécutez-le sur votre contrôleur de domaine"
                        : "Erreur lors de la génération du script d'import";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Erreur : {ex.Message}";
            }
        }

        [RelayCommand]
        private void AddOrganizationalUnit()
        {
            AddObject(ADObjectType.OrganizationalUnit);
        }

        [RelayCommand]
        private void AddContainer()
        {
            AddObject(ADObjectType.Container);
        }

        [RelayCommand]
        private void AddUser()
        {
            AddObject(ADObjectType.User);
        }

        [RelayCommand]
        private void AddGroup()
        {
            AddObject(ADObjectType.Group);
        }

        [RelayCommand]
        private void AddComputer()
        {
            AddObject(ADObjectType.Computer);
        }

        [RelayCommand]
        private void AddGMSA()
        {
            AddObject(ADObjectType.GMSA);
        }

        [RelayCommand]
        private void AddPolicy()
        {
            if (RootNodes.Count == 0) { StatusMessage = "Aucun domaine chargé."; return; }

            // Chercher le container System\Policies
            var policiesContainer = FindPoliciesContainer(RootNodes[0].Data);
            if (policiesContainer == null)
            {
                // Fallback : créer dans le nœud sélectionné comme avant
                AddObject(ADObjectType.Policy);
                StatusMessage = "Conteneur System\\Policies introuvable — GPO ajoutée dans le nœud sélectionné.";
                return;
            }

            var name = _validationService.SuggestName(ADObjectType.Policy);
            var newObject = new ADObject(name, ADObjectType.Policy)
            {
                Parent = policiesContainer,
                Tier = "Tier 0"
            };
            policiesContainer.Children.Add(newObject);
            newObject.UpdateDistinguishedName();

            // Trouver le nœud TreeView correspondant et y insérer
            var policiesNode = FindTreeNode(RootNodes[0], policiesContainer);
            if (policiesNode != null)
            {
                var newNode = new ADTreeNode(newObject) { Parent = policiesNode };
                policiesNode.Children.Add(newNode);
                policiesNode.IsExpanded = true;
                // Remonter pour s'assurer que System est développé aussi
                if (policiesNode.Parent != null) policiesNode.Parent.IsExpanded = true;
            }

            UpdateObjectCounts();
            StatusMessage = $"GPO '{name}' créée dans System\\Policies.";
        }

        /// <summary>Cherche récursivement le container "Policies" fils de "System"</summary>
        private static ADObject? FindPoliciesContainer(ADObject root)
        {
            foreach (var child in root.Children)
            {
                if (child.Type == ADObjectType.Container && child.Name == "System")
                {
                    foreach (var sys in child.Children)
                    {
                        if (sys.Type == ADObjectType.Container && sys.Name == "Policies")
                            return sys;
                    }
                }
                var found = FindPoliciesContainer(child);
                if (found != null) return found;
            }
            return null;
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

        [RelayCommand]
        private void AddPasswordSettingsObject()
        {
            StatusMessage = "Ajout d'un PSO (doit être créé dans un Container comme 'Password Settings Container')...";
            AddObject(ADObjectType.PasswordSettingsObject);
        }

        private void AddObject(ADObjectType type)
        {
            if (SelectedNode == null)
            {
                StatusMessage = "Veuillez sélectionner un conteneur parent";
                return;
            }

            // Valider si l'objet peut être ajouté
            if (!_validationService.ValidateContainerRule(SelectedNode.Data.Type, type, out var errorMessage))
            {
                StatusMessage = errorMessage ?? "Impossible d'ajouter cet objet ici";
                return;
            }

            var name = _validationService.SuggestName(type);
            var newObject = new ADObject(name, type)
            {
                Parent = SelectedNode.Data,
                Tier = SelectedNode.Data.Tier
            };

            // Ajouter l'objet à la position appropriée
            int insertIndex = GetInsertIndex(SelectedNode.Data.Children, type);
            SelectedNode.Data.Children.Insert(insertIndex, newObject);
            newObject.UpdateDistinguishedName();

            var newNode = new ADTreeNode(newObject)
            {
                Parent = SelectedNode
            };
            SelectedNode.Children.Insert(insertIndex, newNode);
            SelectedNode.IsExpanded = true;

            UpdateObjectCounts();
            StatusMessage = $"{type} ajouté : {name}";
        }

        /// <summary>
        /// Détermine l'index d'insertion pour maintenir un ordre logique dans l'arborescence
        /// </summary>
        private int GetInsertIndex(System.Collections.ObjectModel.ObservableCollection<ADObject> children, ADObjectType newType)
        {
            // Ordre de priorité : Policies, OUs, Groupes, Utilisateurs, Ordinateurs, GMSA
            int priority = GetTypePriority(newType);

            for (int i = 0; i < children.Count; i++)
            {
                if (GetTypePriority(children[i].Type) > priority)
                {
                    return i;
                }
            }

            return children.Count; // Ajouter à la fin si aucun élément n'a une priorité plus basse
        }

        private int GetTypePriority(ADObjectType type)
        {
            return type switch
            {
                ADObjectType.Policy => 1,           // Les policies en premier
                ADObjectType.PasswordSettingsObject => 2, // Puis les PSOs
                ADObjectType.Container => 3,        // Puis les Containers
                ADObjectType.OrganizationalUnit => 4, // Puis les OUs
                ADObjectType.Group => 5,            // Puis les groupes
                ADObjectType.User => 6,             // Puis les utilisateurs
                ADObjectType.Computer => 7,         // Puis les ordinateurs
                ADObjectType.GMSA => 8,             // Puis les GMSA
                _ => 99                             // Autres objets à la fin
            };
        }

        [RelayCommand]
        private void DeleteObject()
        {
            if (SelectedNode == null || SelectedNode.Parent == null)
            {
                StatusMessage = "Impossible de supprimer le nœud racine";
                return;
            }

            // Sauvegarder toutes les références nécessaires AVANT la suppression
            var nodeToDelete = SelectedNode;
            var parentNode = nodeToDelete.Parent;
            var dataToDelete = nodeToDelete.Data;
            var deletedName = dataToDelete.Name;

            // Effectuer la suppression
            parentNode.Children.Remove(nodeToDelete);
            parentNode.Data.Children.Remove(dataToDelete);

            UpdateObjectCounts();
            StatusMessage = $"Objet supprimé : {deletedName}";
        }

        [RelayCommand]
        private void CopyObject()
        {
            if (SelectedNode != null)
            {
                _clipboardNode = SelectedNode;
                StatusMessage = $"Copié : {SelectedNode.Data.Name}";
            }
        }

        [RelayCommand]
        private void PasteObject()
        {
            if (_clipboardNode == null || SelectedNode == null)
            {
                StatusMessage = "Aucun objet à coller ou aucun conteneur sélectionné";
                return;
            }

            // Valider si l'objet peut être collé
            if (!_validationService.ValidateContainerRule(SelectedNode.Data.Type, _clipboardNode.Data.Type, out var errorMessage))
            {
                StatusMessage = errorMessage ?? "Impossible de coller ici";
                return;
            }

            var clone = _clipboardNode.Data.Clone();
            clone.Parent = SelectedNode.Data;
            SelectedNode.Data.Children.Add(clone);
            UpdateDistinguishedNamesRecursive(clone);

            var cloneNode = new ADTreeNode(clone)
            {
                Parent = SelectedNode
            };
            SelectedNode.Children.Add(cloneNode);
            SelectedNode.IsExpanded = true;

            UpdateObjectCounts();
            StatusMessage = $"Collé : {clone.Name}";
        }

        [RelayCommand]
        private void ToggleMarkdownEdit()
        {
            IsMarkdownEditMode = !IsMarkdownEditMode;
        }

        [RelayCommand]
        private void RenameObject()
        {
            if (SelectedNode != null)
            {
                SelectedNode.IsEditing = true;
            }
        }

        [RelayCommand]
        private void ConfirmRename(string newName)
        {
            if (SelectedNode != null && !string.IsNullOrWhiteSpace(newName))
            {
                if (_validationService.ValidateName(newName, out var errorMessage))
                {
                    SelectedNode.Data.Name = newName;
                    SelectedNode.Data.ModifiedDate = DateTime.Now;
                    UpdateDistinguishedNamesRecursive(SelectedNode.Data);
                    SelectedNode.IsEditing = false;
                    OnPropertyChanged(nameof(SelectedObjectName));
                    StatusMessage = $"Renommé en : {newName}";
                }
                else
                {
                    StatusMessage = errorMessage ?? "Nom invalide";
                }
            }
        }

        private void UpdateDistinguishedNamesRecursive(ADObject obj)
        {
            obj.UpdateDistinguishedName();
            foreach (var child in obj.Children)
            {
                UpdateDistinguishedNamesRecursive(child);
            }
        }

        private void UpdateObjectCounts()
        {
            if (RootNodes.Count == 0) return;

            var counts = RootNodes[0].Data.CountObjectsByType();
            var summary = string.Join(" | ", counts.Select(kvp => $"{kvp.Key}: {kvp.Value}"));
            ObjectCountsSummary = summary;
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

        private Window? GetMainWindow()
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                return desktop.MainWindow;
            }
            return null;
        }

        [RelayCommand]
        private async Task ManageRelations()
        {
            var mainWindow = GetMainWindow();
            if (mainWindow == null || RootNodes.Count == 0) return;

            var vm = new RelationsViewModel(RootNodes[0].Data);
            var dialog = new Views.RelationsWindow { DataContext = vm };
            await dialog.ShowDialog(mainWindow);

            // Rafraîchir les propriétés de relations après fermeture
            OnPropertyChanged(nameof(SelectedMemberOf));
            OnPropertyChanged(nameof(SelectedLinkedGPOs));
            OnPropertyChanged(nameof(SelectedPSOAppliesTo));
            StatusMessage = "Relations mises à jour.";
        }

        [RelayCommand]
        private async Task AddToGroup()
        {
            if (SelectedNode == null) return;
            var mainWindow = GetMainWindow();
            if (mainWindow == null || RootNodes.Count == 0) return;

            var vm = new RelationsViewModel(RootNodes[0].Data);
            vm.PreselectSource(SelectedNode.Data);
            var dialog = new Views.RelationsWindow { DataContext = vm };
            await dialog.ShowDialog(mainWindow);

            OnPropertyChanged(nameof(SelectedMemberOf));
            StatusMessage = "Relations mises à jour.";
        }

        [RelayCommand]
        private async Task AddMember()
        {
            if (SelectedNode == null) return;
            var mainWindow = GetMainWindow();
            if (mainWindow == null || RootNodes.Count == 0) return;

            var vm = new RelationsViewModel(RootNodes[0].Data);
            vm.PreselectTarget(SelectedNode.Data);
            var dialog = new Views.RelationsWindow { DataContext = vm };
            await dialog.ShowDialog(mainWindow);

            OnPropertyChanged(nameof(SelectedMemberOf));
            StatusMessage = "Relations mises à jour.";
        }

        [RelayCommand]
        private async Task AddGroupToGroup()
        {
            if (SelectedNode == null) return;
            var mainWindow = GetMainWindow();
            if (mainWindow == null || RootNodes.Count == 0) return;

            var vm = new RelationsViewModel(RootNodes[0].Data);
            // Pré-sélectionner le groupe courant comme source dans l'onglet Group-in-Group
            vm.PreselectNestingSource(SelectedNode.Data);
            var dialog = new Views.RelationsWindow { DataContext = vm };
            await dialog.ShowDialog(mainWindow);

            StatusMessage = "Relations mises à jour.";
        }

        [RelayCommand]
        private void ShowGraphView()
        {
            var mainWindow = GetMainWindow();
            if (mainWindow == null || RootNodes.Count == 0) return;

            var vm = new GraphViewModel(RootNodes[0].Data);
            var win = new Views.GraphWindow(vm);
            win.Show(mainWindow);
        }

        [RelayCommand]
        private void ExpandAll()
        {
            foreach (var root in RootNodes)
                ExpandAllRecursive(root);
        }

        [RelayCommand]
        private void CollapseAll()
        {
            foreach (var root in RootNodes)
                CollapseAllRecursive(root);
        }

        private void ExpandAllRecursive(ADTreeNode node)
        {
            node.IsExpanded = true;
            foreach (var child in node.Children)
            {
                ExpandAllRecursive(child);
            }
        }

        private void CollapseAllRecursive(ADTreeNode node)
        {
            node.IsExpanded = false;
            foreach (var child in node.Children)
            {
                CollapseAllRecursive(child);
            }
        }
    }
}
