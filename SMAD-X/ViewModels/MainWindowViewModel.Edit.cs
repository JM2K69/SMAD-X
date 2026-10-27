using System;
using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using SMADX.Models;
using SMADX.Services;

namespace SMADX.ViewModels
{
    // Ajout, suppression, copier/coller et renommage des objets de l'arbre
    public partial class MainWindowViewModel
    {
        private ADTreeNode? _clipboardNode;

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

        // ─── Ajout ────────────────────────────────────────────────────────────────

        [RelayCommand]
        private void AddOrganizationalUnit() => AddObject(ADObjectType.OrganizationalUnit);

        [RelayCommand]
        private void AddContainer() => AddObject(ADObjectType.Container);

        [RelayCommand]
        private void AddUser() => AddObject(ADObjectType.User);

        [RelayCommand]
        private void AddGroup() => AddObject(ADObjectType.Group);

        [RelayCommand]
        private void AddComputer() => AddObject(ADObjectType.Computer);

        [RelayCommand]
        private void AddGMSA() => AddObject(ADObjectType.GMSA);

        [RelayCommand]
        private void AddPolicy()
        {
            if (RootObject == null) { StatusMessage = Loc["Status.NoDomain"]; return; }

            // Chercher le container System\Policies
            var policiesContainer = FindPoliciesContainer(RootObject);
            if (policiesContainer == null)
            {
                // Fallback : créer dans le nœud sélectionné comme avant
                AddObject(ADObjectType.Policy);
                StatusMessage = Loc["Status.PoliciesMissing"];
                return;
            }

            var name = ADDomainEditor.UniqueChildName(policiesContainer, ADObjectType.Policy, _validationService.SuggestName(ADObjectType.Policy));
            var newObject = new ADObject(name, ADObjectType.Policy)
            {
                Parent = policiesContainer,
                Tier = null
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

                SelectForRenameLater(newNode);
            }

            UpdateObjectCounts();
            RecordHistory();
            StatusMessage = string.Format(Loc["Status.GpoCreated"], name);
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

        [RelayCommand]
        private void AddPasswordSettingsObject()
        {
            StatusMessage = Loc["Status.AddingPso"];
            AddObject(ADObjectType.PasswordSettingsObject);
        }

        private void AddObject(ADObjectType type)
        {
            if (SelectedNode == null)
            {
                StatusMessage = Loc["Status.SelectParent"];
                return;
            }

            // Valider si l'objet peut être ajouté
            if (!_validationService.ValidateContainerRule(SelectedNode.Data.Type, type, out var errorMessage))
            {
                StatusMessage = errorMessage ?? Loc["Status.CannotAddHere"];
                return;
            }

            var name = ADDomainEditor.UniqueChildName(SelectedNode.Data, type, _validationService.SuggestName(type));
            var newObject = new ADObject(name, type)
            {
                Parent = SelectedNode.Data,
                Tier = type == ADObjectType.Policy || type == ADObjectType.PasswordSettingsObject
                    ? null
                    : SelectedNode.Data.Tier
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
            RecordHistory();
            StatusMessage = string.Format(Loc["Status.ObjectAdded"], Loc[$"ObjectType.{type}"], name);

            SelectForRenameLater(newNode);
        }

        /// <summary>
        /// Sélectionne le nœud créé et passe en renommage, après que Avalonia a créé son TreeViewItem.
        /// </summary>
        private void SelectForRenameLater(ADTreeNode node)
        {
            Dispatcher.UIThread.Post(() =>
            {
                SelectedNode = node;
                node.IsEditing = true;
            }, DispatcherPriority.Background);
        }

        /// <summary>
        /// Détermine l'index d'insertion pour maintenir un ordre logique dans l'arborescence
        /// </summary>
        private static int GetInsertIndex(ObservableCollection<ADObject> children, ADObjectType newType)
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

        private static int GetTypePriority(ADObjectType type)
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

        // ─── Suppression, copier/coller, renommage ───────────────────────────────

        [RelayCommand]
        private void DeleteObject()
        {
            if (SelectedNode == null || SelectedNode.Parent == null)
            {
                StatusMessage = Loc["Status.CannotDeleteRoot"];
                return;
            }

            // Sauvegarder toutes les références nécessaires AVANT la suppression
            var nodeToDelete = SelectedNode;
            var parentNode = nodeToDelete.Parent;
            var dataToDelete = nodeToDelete.Data;
            var deletedName = dataToDelete.Name;

            // Effectuer la suppression, puis retirer les appartenances, délégations… qui la nommaient
            parentNode.Children.Remove(nodeToDelete);
            parentNode.Data.Children.Remove(dataToDelete);
            var references = RootObject is { } root ? ADDomainEditor.RemoveReferences(root, dataToDelete, SitesTopology) : 0;

            UpdateObjectCounts();
            RecordHistory();
            StatusMessage = string.Format(Loc["Status.Deleted"], deletedName)
                            + (references > 0 ? string.Format(Loc["Status.ReferencesRemoved"], references) : string.Empty);
        }

        [RelayCommand]
        private void CopyObject()
        {
            if (SelectedNode != null)
            {
                _clipboardNode = SelectedNode;
                StatusMessage = string.Format(Loc["Status.Copied"], SelectedNode.Data.Name);
            }
        }

        [RelayCommand]
        private void PasteObject()
        {
            if (_clipboardNode == null || SelectedNode == null)
            {
                StatusMessage = Loc["Status.NothingToPaste"];
                return;
            }

            // Valider si l'objet peut être collé
            if (!_validationService.ValidateContainerRule(SelectedNode.Data.Type, _clipboardNode.Data.Type, out var errorMessage))
            {
                StatusMessage = errorMessage ?? Loc["Status.CannotPasteHere"];
                return;
            }

            var clone = _clipboardNode.Data.Clone();
            clone.Name = ADDomainEditor.UniqueChildName(SelectedNode.Data, clone.Type, clone.Name);
            clone.Parent = SelectedNode.Data;
            SelectedNode.Data.Children.Add(clone);
            clone.UpdateDistinguishedNameTree();

            var cloneNode = new ADTreeNode(clone)
            {
                Parent = SelectedNode
            };
            SelectedNode.Children.Add(cloneNode);
            SelectedNode.IsExpanded = true;

            // Sélectionner le nœud collé et activer le mode édition pour renommage rapide
            SelectedNode = cloneNode;
            cloneNode.IsEditing = true;

            UpdateObjectCounts();
            RecordHistory();
            StatusMessage = string.Format(Loc["Status.Pasted"], clone.Name);
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
        private void ConfirmRename(string newName) => TryRename(newName);

        /// <summary>
        /// Seul chemin de renommage (panneau, F2, commande) : nom validé et libre parmi les frères,
        /// DN du sous-arbre recalculés, références par nom réécrites, état annulable.
        /// </summary>
        private void TryRename(string newName)
        {
            if (SelectedNode?.Data is not { } data || RootObject is not { } root) return;

            var error = ADDomainEditor.Rename(root, data, newName, SitesTopology, out var references);
            if (error != null)
            {
                StatusMessage = error;
                return;
            }

            SelectedNode.IsEditing = false;
            OnPropertyChanged(nameof(SelectedObjectName));
            OnPropertyChanged(nameof(SelectedObjectDistinguishedName));
            RecordHistory();
            StatusMessage = string.Format(Loc["Status.Renamed"], data.Name)
                            + (references > 0 ? string.Format(Loc["Status.ReferencesUpdated"], references) : string.Empty);
        }
    }
}
