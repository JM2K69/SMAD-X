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

        partial void OnSelectedOUNodeChanged(ADObject? value) => ApplyFilters();

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

        public void LoadFromTree(ADObject root)
        {
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
                $"{Q(d.TrusteeName)},{d.TrusteeType},{Q(d.TargetDN)},{Q(d.Right)},{d.RightCategory},{d.IsInherited},{Q(d.Tier ?? string.Empty)}"));
            await File.WriteAllLinesAsync(outputPath, lines);
            StatusMessage = $"Exported {FilteredDelegations.Count} row(s) to {outputPath}";
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static IEnumerable<ADDelegation> CollectDelegations(ADObject node)
        {
            foreach (var d in node.Delegations)
                yield return d;
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

        private static string Q(string v) => "\"" + v.Replace("\"", "\"\"") + "\"";
    }
}
