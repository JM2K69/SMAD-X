using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SMADX.Models;

namespace SMADX.ViewModels
{
    public partial class DelegationsViewModel : ViewModelBase
    {
        // ── Source data ────────────────────────────────────────────────────────
        [ObservableProperty]
        private ObservableCollection<ADDelegation> _allDelegations = new();

        [ObservableProperty]
        private ObservableCollection<ADDelegation> _filteredDelegations = new();

        [ObservableProperty]
        private ADDelegation? _selectedDelegation;

        // ── OU tree ────────────────────────────────────────────────────────────
        /// <summary>Root items for the left TreeView (wraps the domain root).</summary>
        [ObservableProperty]
        private ObservableCollection<ADObject> _treeRootItems = new();

        /// <summary>Currently selected OU/Container in the left tree.</summary>
        [ObservableProperty]
        private ADObject? _selectedOUNode;

        partial void OnSelectedOUNodeChanged(ADObject? value)
        {
            ApplyFilters();
            AddDelegationCommand.NotifyCanExecuteChanged();
        }

        // ── Filter category list ───────────────────────────────────────────────
        public static List<string> CategoryItems { get; } =
            new() { "", "PasswordReset", "ComputerManagement", "AccountUnlock", "AttributeWrite", "FullControl", "Other" };

        // ── Filters ────────────────────────────────────────────────────────────
        [ObservableProperty]
        private string _filterTrustee = string.Empty;

        [ObservableProperty]
        private string _filterTargetDN = string.Empty;

        [ObservableProperty]
        private string _filterCategory = string.Empty;

        [ObservableProperty]
        private bool _hideInherited = false;

        // ── Statistics ─────────────────────────────────────────────────────────
        [ObservableProperty] private int _countPasswordReset;
        [ObservableProperty] private int _countComputerManagement;
        [ObservableProperty] private int _countAccountUnlock;
        [ObservableProperty] private int _countAttributeWrite;
        [ObservableProperty] private int _countFullControl;
        [ObservableProperty] private int _countOther;

        // ── Status ─────────────────────────────────────────────────────────────
        [ObservableProperty]
        private string _statusMessage = string.Empty;

        // ── Public API ─────────────────────────────────────────────────────────

        // ── Édition ────────────────────────────────────────────────────────────

        private ADObject? _root;

        /// <summary>Groupes et comptes du domaine, candidats mandataires d'une nouvelle délégation.</summary>
        public ObservableCollection<string> Principals { get; } = new();

        public static RightCategory[] RightCategories { get; } = System.Enum.GetValues<RightCategory>();

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(AddDelegationCommand))]
        private string? _newTrustee;

        [ObservableProperty]
        private RightCategory _newCategory = RightCategory.PasswordReset;

        /// <summary>Droit AD enregistré pour chaque catégorie (mêmes libellés que le script d'import).</summary>
        public static string DefaultRight(RightCategory category) => category switch
        {
            RightCategory.PasswordReset      => "ResetPassword",
            RightCategory.AccountUnlock      => "WriteProperty:lockoutTime",
            RightCategory.ComputerManagement => "CreateChild:computer",
            RightCategory.AttributeWrite     => "WriteProperty:member",
            RightCategory.FullControl        => "GenericAll",
            _                                => "ReadProperty"
        };

        private bool CanAddDelegation() =>
            SelectedOUNode is { Type: ADObjectType.OrganizationalUnit or ADObjectType.Container or ADObjectType.Domain }
            && !string.IsNullOrWhiteSpace(NewTrustee);

        /// <summary>Délègue la catégorie choisie au mandataire choisi, sur l'OU sélectionnée dans l'arbre.</summary>
        [RelayCommand(CanExecute = nameof(CanAddDelegation))]
        private void AddDelegation()
        {
            var target = SelectedOUNode!;
            var trustee = FindPrincipal(NewTrustee!);
            var delegation = new ADDelegation
            {
                TrusteeName = NewTrustee!.Trim(),
                TrusteeType = trustee?.Type switch
                {
                    ADObjectType.User => TrusteeType.User,
                    ADObjectType.Computer => TrusteeType.Computer,
                    _ => TrusteeType.Group
                },
                TargetDN = target.DistinguishedName,
                Right = DefaultRight(NewCategory),
                RightCategory = NewCategory,
                IsInherited = false,
                Tier = target.Tier
            };
            target.Delegations.Add(delegation);
            target.ModifiedDate = System.DateTime.Now;
            AllDelegations.Add(delegation);
            ApplyFilters();
            RefreshStats();
        }

        /// <summary>Retire la délégation sélectionnée (les délégations héritées se retirent sur l'objet parent).</summary>
        [RelayCommand]
        private void RemoveDelegation()
        {
            if (SelectedDelegation is not { IsInherited: false } delegation || _root is null) return;

            var owner = Flatten(_root).FirstOrDefault(o => o.Delegations.Contains(delegation));
            if (owner is null) return;
            owner.Delegations.Remove(delegation);
            owner.ModifiedDate = System.DateTime.Now;
            AllDelegations.Remove(delegation);
            ApplyFilters();
            RefreshStats();
        }

        private ADObject? FindPrincipal(string name) => _root is null ? null : Flatten(_root).FirstOrDefault(o =>
            o.Type is ADObjectType.Group or ADObjectType.User or ADObjectType.Computer or ADObjectType.GMSA &&
            string.Equals(o.Name, name.Trim(), System.StringComparison.OrdinalIgnoreCase));

        private static IEnumerable<ADObject> Flatten(ADObject node) =>
            new[] { node }.Concat(node.Children.SelectMany(Flatten));

        public void LoadFromTree(ADObject root)
        {
            _root = root;
            Principals.Clear();
            foreach (var name in Flatten(root).Where(o => o.Type is ADObjectType.Group or ADObjectType.User)
                         .Select(o => o.Name).Distinct().OrderBy(n => n, System.StringComparer.CurrentCultureIgnoreCase))
                Principals.Add(name);

            TreeRootItems.Clear();
            TreeRootItems.Add(root);

            AllDelegations.Clear();
            foreach (var d in CollectDelegations(root))
                AllDelegations.Add(d);

            SelectedOUNode = null;
            ApplyFilters();
            RefreshStats();
        }

        [RelayCommand]
        public void ClearOUFilter()
        {
            SelectedOUNode = null;
        }

        [RelayCommand]
        private void ApplyFilters()
        {
            var query = AllDelegations.AsEnumerable();

            // Filter by selected OU (exact TargetDN match)
            if (SelectedOUNode is not null)
                query = query.Where(d => string.Equals(
                    d.TargetDN, SelectedOUNode.DistinguishedName,
                    System.StringComparison.OrdinalIgnoreCase));

            if (HideInherited)
                query = query.Where(d => !d.IsInherited);

            if (!string.IsNullOrWhiteSpace(FilterTrustee))
            {
                var t = FilterTrustee.ToLowerInvariant();
                query = query.Where(d => d.TrusteeName.ToLowerInvariant().Contains(t));
            }

            if (!string.IsNullOrWhiteSpace(FilterTargetDN))
            {
                var t = FilterTargetDN.ToLowerInvariant();
                query = query.Where(d => d.TargetDN.ToLowerInvariant().Contains(t));
            }

            if (!string.IsNullOrWhiteSpace(FilterCategory))
                query = query.Where(d => d.RightCategory.ToString() == FilterCategory);

            FilteredDelegations.Clear();
            foreach (var d in query)
                FilteredDelegations.Add(d);

            var ouLabel = SelectedOUNode is not null ? $" \u2014 {SelectedOUNode.Name}" : string.Empty;
            StatusMessage = $"{FilteredDelegations.Count} / {AllDelegations.Count} delegation(s){ouLabel}.";
        }

        [RelayCommand]
        private async Task ExportToCsvAsync(string outputPath)
        {
            var lines = new List<string> { "TrusteeName,TrusteeType,TargetDN,Right,RightCategory,IsInherited,Tier" };
            lines.AddRange(FilteredDelegations.Select(d =>
                $"{Q(d.TrusteeName)},{d.TrusteeType},{Q(d.TargetDN)},{Q(d.Right)},{d.RightCategory},{d.IsInherited},{Q(d.Tier)}"));
            await File.WriteAllLinesAsync(outputPath, lines);
            StatusMessage = $"Exported {FilteredDelegations.Count} row(s) to {outputPath}";
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static IEnumerable<ADDelegation> CollectDelegations(ADObject node)
        {
            foreach (var d in node.Delegations)
            {
                // Propagate the OU's tier to the delegation if not already set
                if (string.IsNullOrWhiteSpace(d.Tier) && !string.IsNullOrWhiteSpace(node.Tier))
                    d.Tier = node.Tier;
                yield return d;
            }
            foreach (var child in node.Children)
                foreach (var d in CollectDelegations(child))
                    yield return d;
        }

        private void RefreshStats()
        {
            CountPasswordReset      = AllDelegations.Count(d => d.RightCategory == RightCategory.PasswordReset);
            CountComputerManagement = AllDelegations.Count(d => d.RightCategory == RightCategory.ComputerManagement);
            CountAccountUnlock      = AllDelegations.Count(d => d.RightCategory == RightCategory.AccountUnlock);
            CountAttributeWrite     = AllDelegations.Count(d => d.RightCategory == RightCategory.AttributeWrite);
            CountFullControl        = AllDelegations.Count(d => d.RightCategory == RightCategory.FullControl);
            CountOther              = AllDelegations.Count(d => d.RightCategory == RightCategory.Other);
        }

        // Les mandataires viennent du domaine, parfois importé : pas de formule Excel
        private static string Q(string? v) => SMADX.Services.SafeText.CsvCell(v, ",", alwaysQuote: true);
    }
}
