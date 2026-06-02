using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SMADX.Models;
using SMADX.Services;

namespace SMADX.ViewModels
{
    public partial class DomainTimelineViewModel : ViewModelBase
    {
        // ── The two files being compared ────────────────────────────────────
        [ObservableProperty] private DomainSnapshot? _fileA;
        [ObservableProperty] private DomainSnapshot? _fileB;

        // ── Diff results ────────────────────────────────────────────────────
        [ObservableProperty] private ObservableCollection<ADChangeItem> _allChanges    = new();
        [ObservableProperty] private ObservableCollection<ADChangeItem> _filteredChanges = new();

        // ── Stats ───────────────────────────────────────────────────────────
        [ObservableProperty] private int _countAdded;
        [ObservableProperty] private int _countRemoved;
        [ObservableProperty] private int _countModified;

        // ── Filters ─────────────────────────────────────────────────────────
        [ObservableProperty] private string _filterText       = string.Empty;
        [ObservableProperty] private string _filterChangeType = string.Empty;
        [ObservableProperty] private string _filterCategory   = string.Empty;

        public IReadOnlyList<string> ChangeTypeOptions { get; } =
            new[] { "", "Added", "Removed", "Modified" };

        public IReadOnlyList<string> CategoryOptions { get; } =
            new[] { "", "Structure", "MemberOf", "GPO", "PSO", "Delegation" };

        // ── Status ──────────────────────────────────────────────────────────
        [ObservableProperty] private string _statusMessage = "Open two .smad-x.json files to compare.";

        // ── Commands ────────────────────────────────────────────────────────

        private static readonly ADDataService _dataService = new();

        /// <summary>Loads a single .smad-x.json file into slot A or B.</summary>
        public async Task LoadFileAsync(string path, bool isFileA)
        {
            try
            {
                StatusMessage = $"Loading {System.IO.Path.GetFileName(path)}…";
                var root = await _dataService.LoadFromFileAsync(path);
                if (root is null)
                {
                    StatusMessage = "Failed to load file — invalid format.";
                    return;
                }

                var snap = new DomainSnapshot
                {
                    FilePath     = path,
                    DomainName   = root.Name,
                    SnapshotDate = root.ModifiedDate,
                    Root         = root
                };

                if (isFileA) FileA = snap;
                else         FileB = snap;

                StatusMessage = $"File {(isFileA ? "A" : "B")} loaded: {System.IO.Path.GetFileName(path)}  ({snap.DomainName}  {snap.SnapshotDate:yyyy-MM-dd HH:mm})";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading file: {ex.Message}";
            }
        }

        /// <summary>Runs the diff between FileA and FileB.</summary>
        [RelayCommand]
        private void Compare()
        {
            if (FileA is null || FileB is null)
            {
                StatusMessage = "Please open both File A and File B before comparing.";
                return;
            }

            try
            {
                var changes = ADDiffService.Compare(FileA, FileB);
                AllChanges.Clear();
                foreach (var c in changes)
                    AllChanges.Add(c);

                CountAdded    = AllChanges.Count(c => c.ChangeType == ChangeType.Added);
                CountRemoved  = AllChanges.Count(c => c.ChangeType == ChangeType.Removed);
                CountModified = AllChanges.Count(c => c.ChangeType == ChangeType.Modified);

                ApplyFilters();
                StatusMessage = $"Comparison complete — {AllChanges.Count} change(s)  ·  +{CountAdded}  -{CountRemoved}  ~{CountModified}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Comparison error: {ex.Message}";
            }
        }

        /// <summary>Applies text / type / category filters to AllChanges → FilteredChanges.</summary>
        [RelayCommand]
        private void ApplyFilters()
        {
            var query = AllChanges.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(FilterChangeType) &&
                System.Enum.TryParse<ChangeType>(FilterChangeType, out var ct))
                query = query.Where(c => c.ChangeType == ct);

            if (!string.IsNullOrWhiteSpace(FilterCategory) &&
                System.Enum.TryParse<ChangeCategory>(FilterCategory, out var cat))
                query = query.Where(c => c.ChangeCategory == cat);

            if (!string.IsNullOrWhiteSpace(FilterText))
            {
                var text = FilterText.ToLowerInvariant();
                query = query.Where(c =>
                    c.ObjectName.ToLowerInvariant().Contains(text) ||
                    c.DistinguishedName.ToLowerInvariant().Contains(text) ||
                    c.ChangedFieldsSummary.ToLowerInvariant().Contains(text));
            }

            FilteredChanges.Clear();
            foreach (var c in query)
                FilteredChanges.Add(c);
        }

        /// <summary>Exports FilteredChanges to a CSV file.</summary>
        [RelayCommand]
        private async Task ExportToCsvAsync(string outputPath)
        {
            var lines = new List<string> { "ChangeType,Category,ObjectName,ObjectType,DistinguishedName,Details" };
            lines.AddRange(FilteredChanges.Select(c =>
                $"{c.ChangeType},{c.ChangeCategory},{Escape(c.ObjectName)},{c.ObjectType},{Escape(c.DistinguishedName)},{Escape(c.ChangedFieldsSummary)}"));
            await File.WriteAllLinesAsync(outputPath, lines);
            StatusMessage = $"Exported {FilteredChanges.Count} row(s) to {outputPath}";
        }

        private static string Escape(string v) => $"\"{v.Replace("\"", "\"\"")}\"";
    }
}
