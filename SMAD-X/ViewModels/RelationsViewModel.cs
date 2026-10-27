using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SMADX.Models;
using SMADX.Services;

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
    /// Item de groupe sélectionnable par checkbox pour la multi-sélection
    /// </summary>
    public class SelectableGroupItem : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
    {
        public string Name { get; }
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
        public SelectableGroupItem(string name) => Name = name;
    }

    /// <summary>
    /// Nœud dans le TreeView de l'onglet GPO Links.
    /// Peut représenter un ADObject (Domain/OU), un ADSite, ou un en-tête virtuel (dossier).
    /// </summary>
    public class GpoTargetNode
    {
        // ── ADObject target (Domain / OU) ───────────────────────────────────
        public ADObject? AdObjectRef   { get; }

        // ── ADSite target ───────────────────────────────────────────────────
        public ADSite?  SiteRef        { get; }

        // ── Virtual folder (header-only, not selectable as target) ──────────
        public bool     IsVirtualFolder { get; }

        public string DisplayName =>
            AdObjectRef?.Name ?? SiteRef?.Name ?? _folderLabel;

        public string Icon =>
            IsVirtualFolder           ? "🏢" :
            SiteRef      != null      ? "🏢" :
            AdObjectRef?.Type == ADObjectType.Domain ? "🌐" : "📁";

        /// <summary>true when this node is a valid GPO link target (not just a header).</summary>
        public bool IsTarget => !IsVirtualFolder;

        public ObservableCollection<GpoTargetNode> Children { get; } = new();

        private readonly string _folderLabel;

        // ADObject constructor
        public GpoTargetNode(ADObject obj)
        {
            AdObjectRef = obj;
            _folderLabel = string.Empty;
            foreach (var child in obj.Children)
                if (child.Type == ADObjectType.OrganizationalUnit || child.Type == ADObjectType.Domain)
                    Children.Add(new GpoTargetNode(child));
        }

        // ADSite constructor
        public GpoTargetNode(ADSite site)
        {
            SiteRef = site;
            _folderLabel = string.Empty;
        }

        // Virtual folder constructor
        public GpoTargetNode(string folderLabel)
        {
            IsVirtualFolder = true;
            _folderLabel = folderLabel;
        }
    }

    /// <summary>
    /// ViewModel pour la fenêtre de visualisation et configuration des relations
    /// </summary>
    public partial class RelationsViewModel : ViewModelBase
    {
        private readonly ADObject _root;
        private readonly ADSitesTopology? _topology;
        private readonly SMADX.Services.ADValidationService _validation = new();

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

        // Groupes sources avec checkbox pour multi-sélection Group→Group
        public ObservableCollection<SelectableGroupItem> AvailableNestingSources { get; } = new();

        // Groupes cibles avec checkbox pour multi-sélection Group→Group
        public ObservableCollection<SelectableGroupItem> AvailableNestingTargets { get; } = new();

        // --- Filtres de recherche pour les panneaux Group→Group ---
        private string _nestingSourceFilter = string.Empty;
        public string NestingSourceFilter
        {
            get => _nestingSourceFilter;
            set
            {
                SetProperty(ref _nestingSourceFilter, value);
                OnPropertyChanged(nameof(FilteredNestingSources));
            }
        }

        private string _nestingTargetFilter = string.Empty;
        public string NestingTargetFilter
        {
            get => _nestingTargetFilter;
            set
            {
                SetProperty(ref _nestingTargetFilter, value);
                OnPropertyChanged(nameof(FilteredNestingTargets));
            }
        }

        public IEnumerable<SelectableGroupItem> FilteredNestingSources =>
            string.IsNullOrWhiteSpace(NestingSourceFilter)
                ? AvailableNestingSources
                : AvailableNestingSources.Where(g => g.Name.Contains(NestingSourceFilter, System.StringComparison.OrdinalIgnoreCase));

        public IEnumerable<SelectableGroupItem> FilteredNestingTargets =>
            string.IsNullOrWhiteSpace(NestingTargetFilter)
                ? AvailableNestingTargets
                : AvailableNestingTargets.Where(g => g.Name.Contains(NestingTargetFilter, System.StringComparison.OrdinalIgnoreCase));

        // Arbre des OUs/Domaines/Sites pour le TreeView GPO Link
        public ObservableCollection<GpoTargetNode> OuTreeRoots { get; } = new();

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

        private GpoTargetNode? _selectedOuNode;
        public GpoTargetNode? SelectedOuNode
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

        private static LocalizationService Loc => LocalizationService.Instance;

        private string _statusMessage = string.Empty;
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public RelationsViewModel(ADObject root, ADSitesTopology? topology = null)
        {
            _root = root;
            _topology = topology;
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
            AvailableNestingSources.Clear();
            AvailableNestingTargets.Clear();

            foreach (var o in all)
            {
                var s = new ADObjectSuggestion { Name = o.Name, ObjectType = o.Type };
                switch (o.Type)
                {
                    case ADObjectType.User:
                    case ADObjectType.Computer:
                    case ADObjectType.GMSA:
                        AvailableUsers.Add(s);
                        AvailableMembershipTargets.Add(s);
                        break;
                    case ADObjectType.Group:
                        AvailableGroups.Add(s);
                        AvailableNestingSources.Add(new SelectableGroupItem(o.Name));
                        AvailableNestingTargets.Add(new SelectableGroupItem(o.Name));
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
            OnPropertyChanged(nameof(FilteredNestingSources));
            OnPropertyChanged(nameof(FilteredNestingTargets));
        }

        private void BuildOuTree()
        {
            OuTreeRoots.Clear();

            // Domain / OU section
            if (_root.Type == ADObjectType.Domain || _root.Type == ADObjectType.OrganizationalUnit)
                OuTreeRoots.Add(new GpoTargetNode(_root));
            else
                foreach (var child in _root.Children)
                    if (child.Type == ADObjectType.Domain || child.Type == ADObjectType.OrganizationalUnit)
                        OuTreeRoots.Add(new GpoTargetNode(child));

            // Sites section
            if (_topology != null && _topology.Sites.Count > 0)
            {
                var siteFolder = new GpoTargetNode(Loc["Relations.SitesFolder"]);
                foreach (var site in _topology.Sites)
                    siteFolder.Children.Add(new GpoTargetNode(site));
                OuTreeRoots.Add(siteFolder);
            }
        }

        private void LoadRelations()
        {
            Memberships.Clear();
            GroupNestings.Clear();
            GpoLinks.Clear();
            PsoLinks.Clear();

            foreach (var obj in CollectAll(_root))
            {
                // User / Computer / gMSA → Group
                if (obj.Type is ADObjectType.User or ADObjectType.Computer or ADObjectType.GMSA)
                {
                    foreach (var grp in obj.MemberOf)
                        Memberships.Add(new RelationEntry
                        {
                            Source = obj.Name,
                            SourceType = obj.Type.ToString(),
                            Target = grp,
                            RelationType = Loc["Relation.MemberOf"]
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
                            RelationType = Loc["Relation.GroupNesting"]
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
                            RelationType = Loc["Relation.GpoLink"]
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
                            RelationType = Loc["Relation.PsoSubject"]
                        });
                }
            }

            // GPO links from Sites
            if (_topology != null)
            {
                foreach (var site in _topology.Sites)
                    foreach (var gpo in site.LinkedGPOs)
                        GpoLinks.Add(new RelationEntry
                        {
                            Source = site.Name,
                            SourceType = "Site",
                            Target = gpo,
                            RelationType = Loc["Relation.GpoLink"]
                        });
            }
        }

        // --- Commandes User → Group ---

        [RelayCommand]
        private void AddMembership()
        {
            if (string.IsNullOrWhiteSpace(MembershipSource) || string.IsNullOrWhiteSpace(MembershipTarget))
            {
                StatusMessage = Loc["Relations.Msg.SelectMembership"];
                return;
            }
            if (MembershipSource == MembershipTarget)
            {
                StatusMessage = Loc["Relations.Msg.SelfMember"];
                return;
            }

            var source = FindObject(MembershipSource);
            if (source == null) { StatusMessage = string.Format(Loc["Relations.Msg.ObjectNotFound"], MembershipSource); return; }
            if (source.Type is not (ADObjectType.User or ADObjectType.Computer or ADObjectType.GMSA))
            {
                StatusMessage = Loc["Relations.Msg.SourceMustBeAccount"];
                return;
            }
            if (source.MemberOf.Contains(MembershipTarget))
            {
                StatusMessage = Loc["Relations.Msg.AlreadyExists"];
                return;
            }
            if (FindObject(MembershipTarget) is { } targetGroup && !_validation.ValidateMembership(source, targetGroup, out var refused))
            {
                StatusMessage = refused!;
                return;
            }

            source.MemberOf.Add(MembershipTarget);
            Memberships.Add(new RelationEntry
            {
                Source = MembershipSource,
                SourceType = source.Type.ToString(),
                Target = MembershipTarget,
                RelationType = Loc["Relation.MemberOf"]
            });
            StatusMessage = string.Format(Loc["Relations.Msg.MembershipAdded"], MembershipSource, MembershipTarget);
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
            StatusMessage = Loc["Relations.Msg.Removed"];
        }

        // --- Commandes Group → Group ---

        [RelayCommand]
        private void AddGroupNesting()
        {
            var selectedSources = AvailableNestingSources.Where(s => s.IsSelected).ToList();
            var selectedTargets = AvailableNestingTargets.Where(t => t.IsSelected).ToList();

            if (selectedSources.Count == 0 || selectedTargets.Count == 0)
            {
                StatusMessage = Loc["Relations.Msg.SelectNesting"];
                return;
            }

            int added = 0, skipped = 0;
            string? refusal = null;
            foreach (var src in selectedSources)
            {
                var sourceObj = FindObject(src.Name);
                if (sourceObj == null || sourceObj.Type != ADObjectType.Group) { skipped++; continue; }

                foreach (var tgt in selectedTargets)
                {
                    if (tgt.Name == src.Name) { skipped++; continue; }
                    if (sourceObj.MemberOf.Contains(tgt.Name)) { skipped++; continue; }
                    // Portées des groupes (AGDLP) : un global ne contient pas d'universel, etc.
                    if (FindObject(tgt.Name) is { } targetObj && !_validation.ValidateMembership(sourceObj, targetObj, out var error))
                    {
                        refusal ??= error;
                        skipped++;
                        continue;
                    }
                    sourceObj.MemberOf.Add(tgt.Name);
                    GroupNestings.Add(new RelationEntry
                    {
                        Source = src.Name,
                        SourceType = "Group",
                        Target = tgt.Name,
                        RelationType = Loc["Relation.GroupNesting"]
                    });
                    added++;
                }
                src.IsSelected = false;
            }
            foreach (var tgt in selectedTargets) tgt.IsSelected = false;

            StatusMessage = (added > 0
                ? (skipped > 0
                    ? string.Format(Loc["Relations.Msg.NestingAddedSkipped"], added, skipped)
                    : string.Format(Loc["Relations.Msg.NestingAdded"], added))
                : Loc["Relations.Msg.NestingNone"])
                + (refusal != null ? " " + refusal : string.Empty);
        }

        [RelayCommand]
        private void RemoveGroupNesting()
        {
            if (SelectedGroupNesting == null) return;
            var source = FindObject(SelectedGroupNesting.Source);
            source?.MemberOf.Remove(SelectedGroupNesting.Target);
            GroupNestings.Remove(SelectedGroupNesting);
            StatusMessage = Loc["Relations.Msg.NestingRemoved"];
        }

        // --- Commandes GPO Links ---

        [RelayCommand]
        private void AddGpoLink()
        {
            if (SelectedOuNode == null || !SelectedOuNode.IsTarget || string.IsNullOrWhiteSpace(GpoLinkGpo))
            {
                StatusMessage = Loc["Relations.Msg.SelectGpoLink"];
                return;
            }

            if (SelectedOuNode.SiteRef != null)
            {
                // Target is an AD Site
                var site = SelectedOuNode.SiteRef;
                if (site.LinkedGPOs.Contains(GpoLinkGpo))
                {
                    StatusMessage = Loc["Relations.Msg.GpoLinkExists"];
                    return;
                }
                site.LinkedGPOs.Add(GpoLinkGpo);
                GpoLinks.Add(new RelationEntry
                {
                    Source = site.Name,
                    SourceType = "Site",
                    Target = GpoLinkGpo,
                    RelationType = Loc["Relation.GpoLink"]
                });
                StatusMessage = string.Format(Loc["Relations.Msg.GpoLinkedToSite"], GpoLinkGpo, site.Name);
            }
            else if (SelectedOuNode.AdObjectRef != null)
            {
                // Target is a Domain / OU
                var ou = SelectedOuNode.AdObjectRef;
                if (ou.LinkedGPOs.Contains(GpoLinkGpo))
                {
                    StatusMessage = Loc["Relations.Msg.GpoLinkExists"];
                    return;
                }
                ou.LinkedGPOs.Add(GpoLinkGpo);
                GpoLinks.Add(new RelationEntry
                {
                    Source = ou.Name,
                    SourceType = ou.Type == ADObjectType.Domain ? "Domain" : "OU",
                    Target = GpoLinkGpo,
                    RelationType = Loc["Relation.GpoLink"]
                });
                StatusMessage = string.Format(Loc["Relations.Msg.GpoLinked"], GpoLinkGpo, ou.Name);
            }

            GpoLinkGpoObject = null;
            SelectedOuNode = null;
            GpoLinkOu = string.Empty;
            GpoLinkGpo = string.Empty;
        }

        [RelayCommand]
        private void RemoveGpoLink()
        {
            if (SelectedGpoLink == null) return;

            // Try ADSite first
            if (_topology != null)
            {
                var site = _topology.Sites.FirstOrDefault(s => s.Name == SelectedGpoLink.Source);
                if (site != null)
                {
                    site.LinkedGPOs.Remove(SelectedGpoLink.Target);
                    GpoLinks.Remove(SelectedGpoLink);
                    StatusMessage = Loc["Relations.Msg.GpoLinkRemoved"];
                    return;
                }
            }

            // Fall back to ADObject
            var ou = FindObject(SelectedGpoLink.Source);
            ou?.LinkedGPOs.Remove(SelectedGpoLink.Target);
            GpoLinks.Remove(SelectedGpoLink);
            StatusMessage = Loc["Relations.Msg.GpoLinkRemoved"];
        }

        // --- Commandes PSO ---

        [RelayCommand]
        private void AddPsoLink()
        {
            if (string.IsNullOrWhiteSpace(PsoLinkPso) || string.IsNullOrWhiteSpace(PsoLinkTarget))
            {
                StatusMessage = Loc["Relations.Msg.SelectPso"];
                return;
            }

            var pso = FindObject(PsoLinkPso);
            if (pso == null) { StatusMessage = string.Format(Loc["Relations.Msg.PsoNotFound"], PsoLinkPso); return; }
            if (pso.PSOAppliesTo.Contains(PsoLinkTarget))
            {
                StatusMessage = Loc["Relations.Msg.PsoExists"];
                return;
            }

            pso.PSOAppliesTo.Add(PsoLinkTarget);
            PsoLinks.Add(new RelationEntry
            {
                Source = PsoLinkPso,
                SourceType = "PSO",
                Target = PsoLinkTarget,
                RelationType = Loc["Relation.PsoSubject"]
            });
            StatusMessage = string.Format(Loc["Relations.Msg.PsoApplied"], PsoLinkPso, PsoLinkTarget);
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
            StatusMessage = Loc["Relations.Msg.PsoRemoved"];
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
