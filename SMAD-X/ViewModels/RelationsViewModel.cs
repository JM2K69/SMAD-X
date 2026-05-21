using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SMADX.Models;

namespace SMADX.ViewModels
{
    /// <summary>
    /// Représente une ligne dans la vue des relations
    /// </summary>
    public class RelationEntry
    {
        public string Source { get; set; } = string.Empty;
        public string SourceType { get; set; } = string.Empty;
        public string Target { get; set; } = string.Empty;
        public string RelationType { get; set; } = string.Empty;
    }

    /// <summary>
    /// Objet de suggestion dans les ComboBox (nom + type + icône)
    /// </summary>
    public class ADObjectSuggestion
    {
        public string Name { get; set; } = string.Empty;
        public ADObjectType ObjectType { get; set; }
        public string Icon => ObjectType switch
        {
            ADObjectType.Computer              => "🖥️",
            ADObjectType.User                  => "👤",
            ADObjectType.Group                 => "👥",
            ADObjectType.Policy                => "📋",
            ADObjectType.PasswordSettingsObject=> "🔑",
            ADObjectType.OrganizationalUnit    => "📁",
            ADObjectType.Domain                => "🌐",
            _                                  => "📄"
        };
        public override string ToString() => Name;
    }

    /// <summary>
    /// Nœud dans le TreeView des OUs/Domaines pour la sélection GPO Link
    /// </summary>
    public class OuTreeNode
    {
        public ADObject AdObjectRef { get; }
        public string DisplayName => AdObjectRef.Name;
        public string Icon => AdObjectRef.Type == ADObjectType.Domain ? "🌐" : "📁";
        public ObservableCollection<OuTreeNode> Children { get; } = new();

        public OuTreeNode(ADObject obj)
        {
            AdObjectRef = obj;
            foreach (var child in obj.Children)
            {
                if (child.Type == ADObjectType.OrganizationalUnit || child.Type == ADObjectType.Domain)
                    Children.Add(new OuTreeNode(child));
            }
        }
    }

    /// <summary>
    /// ViewModel pour la fenêtre de visualisation et configuration des relations
    /// </summary>
    public partial class RelationsViewModel : ViewModelBase
    {
        private readonly ADObject _root;

        // --- Onglet User → Group ---
        public ObservableCollection<RelationEntry> Memberships { get; } = new();
        private RelationEntry? _selectedMembership;
        public RelationEntry? SelectedMembership
        {
            get => _selectedMembership;
            set => SetProperty(ref _selectedMembership, value);
        }
        private string _membershipSource = string.Empty;
        public string MembershipSource
        {
            get => _membershipSource;
            set => SetProperty(ref _membershipSource, value);
        }
        private string _membershipTarget = string.Empty;
        public string MembershipTarget
        {
            get => _membershipTarget;
            set => SetProperty(ref _membershipTarget, value);
        }

        // --- Onglet Group → Group (imbrication) ---
        public ObservableCollection<RelationEntry> GroupNestings { get; } = new();
        private RelationEntry? _selectedGroupNesting;
        public RelationEntry? SelectedGroupNesting
        {
            get => _selectedGroupNesting;
            set => SetProperty(ref _selectedGroupNesting, value);
        }
        private string _nestingSource = string.Empty;
        public string NestingSource
        {
            get => _nestingSource;
            set => SetProperty(ref _nestingSource, value);
        }
        private string _nestingTarget = string.Empty;
        public string NestingTarget
        {
            get => _nestingTarget;
            set => SetProperty(ref _nestingTarget, value);
        }

        // --- Onglet GPO Links ---
        public ObservableCollection<RelationEntry> GpoLinks { get; } = new();
        private RelationEntry? _selectedGpoLink;
        public RelationEntry? SelectedGpoLink
        {
            get => _selectedGpoLink;
            set => SetProperty(ref _selectedGpoLink, value);
        }
        private string _gpoLinkOu = string.Empty;
        public string GpoLinkOu
        {
            get => _gpoLinkOu;
            set => SetProperty(ref _gpoLinkOu, value);
        }
        private string _gpoLinkGpo = string.Empty;
        public string GpoLinkGpo
        {
            get => _gpoLinkGpo;
            set => SetProperty(ref _gpoLinkGpo, value);
        }

        // --- Onglet PSO ---
        public ObservableCollection<RelationEntry> PsoLinks { get; } = new();
        private RelationEntry? _selectedPsoLink;
        public RelationEntry? SelectedPsoLink
        {
            get => _selectedPsoLink;
            set => SetProperty(ref _selectedPsoLink, value);
        }
        private string _psoLinkPso = string.Empty;
        public string PsoLinkPso
        {
            get => _psoLinkPso;
            set => SetProperty(ref _psoLinkPso, value);
        }
        private string _psoLinkTarget = string.Empty;
        public string PsoLinkTarget
        {
            get => _psoLinkTarget;
            set => SetProperty(ref _psoLinkTarget, value);
        }

        // --- Listes de suggestions (toutes typées) ---
        public ObservableCollection<ADObjectSuggestion> AvailableUsers { get; } = new();
        public ObservableCollection<ADObjectSuggestion> AvailableGroups { get; } = new();
        public ObservableCollection<ADObjectSuggestion> AvailableOUs { get; } = new();
        public ObservableCollection<ADObjectSuggestion> AvailableGPOs { get; } = new();
        public ObservableCollection<ADObjectSuggestion> AvailablePSOs { get; } = new();
        public ObservableCollection<ADObjectSuggestion> AvailableMembershipTargets { get; } = new();

        // Arbre des OUs/Domaines pour le TreeView GPO Link
        public ObservableCollection<OuTreeNode> OuTreeRoots { get; } = new();

        // Propriétés objet liées aux ComboBox (mettent à jour la string correspondante)
        private ADObjectSuggestion? _membershipSourceObject;
        public ADObjectSuggestion? MembershipSourceObject
        {
            get => _membershipSourceObject;
            set { SetProperty(ref _membershipSourceObject, value); MembershipSource = value?.Name ?? string.Empty; }
        }

        private ADObjectSuggestion? _membershipTargetObject;
        public ADObjectSuggestion? MembershipTargetObject
        {
            get => _membershipTargetObject;
            set { SetProperty(ref _membershipTargetObject, value); MembershipTarget = value?.Name ?? string.Empty; }
        }

        private ADObjectSuggestion? _nestingSourceObject;
        public ADObjectSuggestion? NestingSourceObject
        {
            get => _nestingSourceObject;
            set { SetProperty(ref _nestingSourceObject, value); NestingSource = value?.Name ?? string.Empty; }
        }

        private ADObjectSuggestion? _nestingTargetObject;
        public ADObjectSuggestion? NestingTargetObject
        {
            get => _nestingTargetObject;
            set { SetProperty(ref _nestingTargetObject, value); NestingTarget = value?.Name ?? string.Empty; }
        }

        private ADObjectSuggestion? _gpoLinkGpoObject;
        public ADObjectSuggestion? GpoLinkGpoObject
        {
            get => _gpoLinkGpoObject;
            set { SetProperty(ref _gpoLinkGpoObject, value); GpoLinkGpo = value?.Name ?? string.Empty; }
        }

        private OuTreeNode? _selectedOuNode;
        public OuTreeNode? SelectedOuNode
        {
            get => _selectedOuNode;
            set { SetProperty(ref _selectedOuNode, value); GpoLinkOu = value?.DisplayName ?? string.Empty; }
        }

        private ADObjectSuggestion? _psoLinkPsoObject;
        public ADObjectSuggestion? PsoLinkPsoObject
        {
            get => _psoLinkPsoObject;
            set { SetProperty(ref _psoLinkPsoObject, value); PsoLinkPso = value?.Name ?? string.Empty; }
        }

        private ADObjectSuggestion? _psoLinkTargetObject;
        public ADObjectSuggestion? PsoLinkTargetObject
        {
            get => _psoLinkTargetObject;
            set { SetProperty(ref _psoLinkTargetObject, value); PsoLinkTarget = value?.Name ?? string.Empty; }
        }

        private string _statusMessage = string.Empty;
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public RelationsViewModel(ADObject root)
        {
            _root = root;
            BuildSuggestions();
            LoadRelations();
        }

        private int _selectedTabIndex = 0;
        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set => SetProperty(ref _selectedTabIndex, value);
        }

        /// <summary>Pré-sélectionne l'objet source dans l'onglet Memberships</summary>
        public void PreselectSource(ADObject obj)
        {
            MembershipSource = obj.Name;
        }

        /// <summary>Pré-sélectionne le groupe cible dans l'onglet Memberships</summary>
        public void PreselectTarget(ADObject obj)
        {
            MembershipTarget = obj.Name;
        }

        /// <summary>Pré-sélectionne le groupe source dans l'onglet Group-in-Group</summary>
        public void PreselectNestingSource(ADObject obj)
        {
            NestingSource = obj.Name;
            SelectedTabIndex = 1;
        }

        private void BuildSuggestions()
        {
            var all = CollectAll(_root).ToList();

            AvailableUsers.Clear();
            AvailableGroups.Clear();
            AvailableOUs.Clear();
            AvailableGPOs.Clear();
            AvailablePSOs.Clear();
            AvailableMembershipTargets.Clear();

            foreach (var o in all)
            {
                var s = new ADObjectSuggestion { Name = o.Name, ObjectType = o.Type };
                switch (o.Type)
                {
                    case ADObjectType.User:
                    case ADObjectType.Computer:
                        AvailableUsers.Add(s);
                        AvailableMembershipTargets.Add(s);
                        break;
                    case ADObjectType.Group:
                        AvailableGroups.Add(s);
                        AvailableMembershipTargets.Add(s);
                        break;
                    case ADObjectType.Domain:
                    case ADObjectType.OrganizationalUnit:
                        AvailableOUs.Add(s);
                        break;
                    case ADObjectType.Policy:
                        AvailableGPOs.Add(s);
                        break;
                    case ADObjectType.PasswordSettingsObject:
                        AvailablePSOs.Add(s);
                        break;
                }
            }

            BuildOuTree();
        }

        private void BuildOuTree()
        {
            OuTreeRoots.Clear();
            if (_root.Type == ADObjectType.Domain || _root.Type == ADObjectType.OrganizationalUnit)
                OuTreeRoots.Add(new OuTreeNode(_root));
            else
                foreach (var child in _root.Children)
                    if (child.Type == ADObjectType.Domain || child.Type == ADObjectType.OrganizationalUnit)
                        OuTreeRoots.Add(new OuTreeNode(child));
        }

        private void LoadRelations()
        {
            Memberships.Clear();
            GroupNestings.Clear();
            GpoLinks.Clear();
            PsoLinks.Clear();

            foreach (var obj in CollectAll(_root))
            {
                // User / Computer → Group
                if (obj.Type == ADObjectType.User || obj.Type == ADObjectType.Computer)
                {
                    foreach (var grp in obj.MemberOf)
                        Memberships.Add(new RelationEntry
                        {
                            Source = obj.Name,
                            SourceType = obj.Type == ADObjectType.Computer ? "Computer" : "User",
                            Target = grp,
                            RelationType = "MemberOf"
                        });
                }
                // Group → Group
                if (obj.Type == ADObjectType.Group)
                {
                    foreach (var grp in obj.MemberOf)
                        GroupNestings.Add(new RelationEntry
                        {
                            Source = obj.Name,
                            SourceType = "Group",
                            Target = grp,
                            RelationType = "Group in Group"
                        });
                }
                if (obj.Type == ADObjectType.OrganizationalUnit || obj.Type == ADObjectType.Domain)
                {
                    foreach (var gpo in obj.LinkedGPOs)
                        GpoLinks.Add(new RelationEntry
                        {
                            Source = obj.Name,
                            SourceType = obj.Type == ADObjectType.Domain ? "Domain" : "OU",
                            Target = gpo,
                            RelationType = "GPO Link"
                        });
                }
                if (obj.Type == ADObjectType.PasswordSettingsObject)
                {
                    foreach (var t in obj.PSOAppliesTo)
                        PsoLinks.Add(new RelationEntry
                        {
                            Source = obj.Name,
                            SourceType = "PSO",
                            Target = t,
                            RelationType = "PSO Subject"
                        });
                }
            }
        }

        // --- Commandes User → Group ---

        [RelayCommand]
        private void AddMembership()
        {
            if (string.IsNullOrWhiteSpace(MembershipSource) || string.IsNullOrWhiteSpace(MembershipTarget))
            {
                StatusMessage = "Sélectionnez un utilisateur source et un groupe cible.";
                return;
            }
            if (MembershipSource == MembershipTarget)
            {
                StatusMessage = "Un objet ne peut pas être membre de lui-même.";
                return;
            }

            var source = FindObject(MembershipSource);
            if (source == null) { StatusMessage = $"Objet '{MembershipSource}' introuvable."; return; }
            if (source.Type != ADObjectType.User && source.Type != ADObjectType.Computer)
            {
                StatusMessage = "La source doit être un utilisateur ou un ordinateur. Pour Group→Group, utilisez l'onglet dédié.";
                return;
            }
            if (source.MemberOf.Contains(MembershipTarget))
            {
                StatusMessage = "Cette relation existe déjà.";
                return;
            }

            source.MemberOf.Add(MembershipTarget);
            Memberships.Add(new RelationEntry
            {
                Source = MembershipSource,
                SourceType = source.Type == ADObjectType.Computer ? "Computer" : "User",
                Target = MembershipTarget,
                RelationType = "MemberOf"
            });
            StatusMessage = $"✔ {MembershipSource} → {MembershipTarget} ajouté.";
            MembershipSourceObject = null;
            MembershipTargetObject = null;
            MembershipSource = string.Empty;
            MembershipTarget = string.Empty;
        }

        [RelayCommand]
        private void RemoveMembership()
        {
            if (SelectedMembership == null) return;
            var source = FindObject(SelectedMembership.Source);
            source?.MemberOf.Remove(SelectedMembership.Target);
            Memberships.Remove(SelectedMembership);
            StatusMessage = "✔ Relation supprimée.";
        }

        // --- Commandes Group → Group ---

        [RelayCommand]
        private void AddGroupNesting()
        {
            if (string.IsNullOrWhiteSpace(NestingSource) || string.IsNullOrWhiteSpace(NestingTarget))
            {
                StatusMessage = "Sélectionnez le groupe source et le groupe cible (parent).";
                return;
            }
            if (NestingSource == NestingTarget)
            {
                StatusMessage = "Un groupe ne peut pas être membre de lui-même.";
                return;
            }

            var source = FindObject(NestingSource);
            if (source == null) { StatusMessage = $"Groupe '{NestingSource}' introuvable."; return; }
            if (source.Type != ADObjectType.Group)
            {
                StatusMessage = "La source doit être un groupe.";
                return;
            }
            if (source.MemberOf.Contains(NestingTarget))
            {
                StatusMessage = "Cette imbrication existe déjà.";
                return;
            }

            source.MemberOf.Add(NestingTarget);
            GroupNestings.Add(new RelationEntry
            {
                Source = NestingSource,
                SourceType = "Group",
                Target = NestingTarget,
                RelationType = "Group in Group"
            });
            StatusMessage = $"✔ {NestingSource} ⊂ {NestingTarget} ajouté.";
            NestingSourceObject = null;
            NestingTargetObject = null;
            NestingSource = string.Empty;
            NestingTarget = string.Empty;
        }

        [RelayCommand]
        private void RemoveGroupNesting()
        {
            if (SelectedGroupNesting == null) return;
            var source = FindObject(SelectedGroupNesting.Source);
            source?.MemberOf.Remove(SelectedGroupNesting.Target);
            GroupNestings.Remove(SelectedGroupNesting);
            StatusMessage = "✔ Imbrication supprimée.";
        }

        // --- Commandes GPO Links ---

        [RelayCommand]
        private void AddGpoLink()
        {
            if (SelectedOuNode == null || string.IsNullOrWhiteSpace(GpoLinkGpo))
            {
                StatusMessage = "Sélectionnez un domaine/OU cible et une GPO.";
                return;
            }

            var ou = SelectedOuNode.AdObjectRef;
            if (ou.LinkedGPOs.Contains(GpoLinkGpo))
            {
                StatusMessage = "Ce lien GPO existe déjà.";
                return;
            }

            ou.LinkedGPOs.Add(GpoLinkGpo);
            GpoLinks.Add(new RelationEntry
            {
                Source = ou.Name,
                SourceType = ou.Type == ADObjectType.Domain ? "Domain" : "OU",
                Target = GpoLinkGpo,
                RelationType = "GPO Link"
            });
            StatusMessage = $"✔ GPO '{GpoLinkGpo}' liée à '{ou.Name}'.";
            GpoLinkGpoObject = null;
            SelectedOuNode = null;
            GpoLinkOu = string.Empty;
            GpoLinkGpo = string.Empty;
        }

        [RelayCommand]
        private void RemoveGpoLink()
        {
            if (SelectedGpoLink == null) return;
            var ou = FindObject(SelectedGpoLink.Source);
            ou?.LinkedGPOs.Remove(SelectedGpoLink.Target);
            GpoLinks.Remove(SelectedGpoLink);
            StatusMessage = "✔ Lien GPO supprimé.";
        }

        // --- Commandes PSO ---

        [RelayCommand]
        private void AddPsoLink()
        {
            if (string.IsNullOrWhiteSpace(PsoLinkPso) || string.IsNullOrWhiteSpace(PsoLinkTarget))
            {
                StatusMessage = "Sélectionnez un PSO et un utilisateur/groupe cible.";
                return;
            }

            var pso = FindObject(PsoLinkPso);
            if (pso == null) { StatusMessage = $"PSO '{PsoLinkPso}' introuvable."; return; }
            if (pso.PSOAppliesTo.Contains(PsoLinkTarget))
            {
                StatusMessage = "Cette application PSO existe déjà.";
                return;
            }

            pso.PSOAppliesTo.Add(PsoLinkTarget);
            PsoLinks.Add(new RelationEntry
            {
                Source = PsoLinkPso,
                SourceType = "PSO",
                Target = PsoLinkTarget,
                RelationType = "PSO Subject"
            });
            StatusMessage = $"✔ PSO '{PsoLinkPso}' appliqué à '{PsoLinkTarget}'.";
            PsoLinkPsoObject = null;
            PsoLinkTargetObject = null;
            PsoLinkPso = string.Empty;
            PsoLinkTarget = string.Empty;
        }

        [RelayCommand]
        private void RemovePsoLink()
        {
            if (SelectedPsoLink == null) return;
            var pso = FindObject(SelectedPsoLink.Source);
            pso?.PSOAppliesTo.Remove(SelectedPsoLink.Target);
            PsoLinks.Remove(SelectedPsoLink);
            StatusMessage = "✔ Application PSO supprimée.";
        }

        private ADObject? FindObject(string name)
        {
            return CollectAll(_root).FirstOrDefault(o => o.Name == name);
        }

        private static IEnumerable<ADObject> CollectAll(ADObject obj)
        {
            yield return obj;
            foreach (var child in obj.Children)
                foreach (var desc in CollectAll(child))
                    yield return desc;
        }
    }
}
