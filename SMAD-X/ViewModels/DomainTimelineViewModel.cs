using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SMADX.Models;
using SMADX.Services;

namespace SMADX.ViewModels
{
    public partial class DomainTimelineViewModel : ViewModelBase
    {
        // ── Snapshots loaded by the user ────────────────────────────────────
        [ObservableProperty]
        private ObservableCollection<DomainSnapshot> _snapshots = new();

        [ObservableProperty]
        private DomainSnapshot? _selectedBaseline;

        [ObservableProperty]
        private DomainSnapshot? _selectedCurrent;

        // ── Diff results ────────────────────────────────────────────────────
        [ObservableProperty]
        private ObservableCollection<ADChangeItem> _allChanges = new();

        [ObservableProperty]
        private ObservableCollection<ADChangeItem> _filteredChanges = new();

        // ── Filters ─────────────────────────────────────────────────────────
        [ObservableProperty]
        private string _filterText = string.Empty;

        [ObservableProperty]
        private string _filterChangeType = string.Empty;   // "Added" | "Removed" | "Modified" | ""

        [ObservableProperty]
        private string _filterObjectType = string.Empty;

        // ── Status ──────────────────────────────────────────────────────────
        [ObservableProperty]
        private string _statusMessage = string.Empty;

        // ── Commands ────────────────────────────────────────────────────────

        /// <summary>Adds one or more .smad-x.json snapshot files to the timeline.</summary>
        [RelayCommand]
        private async Task AddSnapshotsAsync(IEnumerable<string> filePaths)
        {
            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            foreach (var path in filePaths)
            {
                if (!File.Exists(path)) continue;

                await using var stream = File.OpenRead(path);
                var root = await JsonSerializer.DeserializeAsync<ADObject>(stream, opts);
                if (root is null) continue;

                var snap = new DomainSnapshot
                {
                    FilePath     = path,
                    DomainName   = root.Name,
                    SnapshotDate = root.ModifiedDate,
                    Root         = root
                };

                Snapshots.Add(snap);
            }

            StatusMessage = $"{Snapshots.Count} snapshot(s) loaded.";
        }

        /// <summary>Removes the selected snapshot from the timeline.</summary>
        [RelayCommand]
        private void RemoveSnapshot(DomainSnapshot? snap)
        {
            if (snap is not null)
                Snapshots.Remove(snap);
        }

        /// <summary>Runs the diff between SelectedBaseline and SelectedCurrent.</summary>
        [RelayCommand]
        private void Compare()
        {
            if (SelectedBaseline is null || SelectedCurrent is null)
            {
                StatusMessage = "Please select a baseline and a current snapshot.";
                return;
            }

            var changes = ADDiffService.Compare(SelectedBaseline, SelectedCurrent);
            AllChanges.Clear();
            foreach (var c in changes)
                AllChanges.Add(c);

            ApplyFilters();
            StatusMessage = $"{AllChanges.Count} change(s) detected.";
        }

        /// <summary>Applies text/type filters to AllChanges.</summary>
        [RelayCommand]
        private void ApplyFilters()
        {
            var query = AllChanges.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(FilterChangeType))
                query = query.Where(c => c.ChangeType.ToString() == FilterChangeType);

            if (!string.IsNullOrWhiteSpace(FilterObjectType))
                query = query.Where(c => c.ObjectType.ToString() == FilterObjectType);

            if (!string.IsNullOrWhiteSpace(FilterText))
            {
                var text = FilterText.ToLowerInvariant();
                query = query.Where(c =>
                    c.ObjectName.ToLowerInvariant().Contains(text) ||
                    c.DistinguishedName.ToLowerInvariant().Contains(text));
            }

            FilteredChanges.Clear();
            foreach (var c in query)
                FilteredChanges.Add(c);
        }

        /// <summary>Exports FilteredChanges to a CSV file.</summary>
        [RelayCommand]
        private async Task ExportToCsvAsync(string outputPath)
        {
            var lines = new List<string> { "ChangeType,ObjectName,ObjectType,DistinguishedName,Details" };
            lines.AddRange(FilteredChanges.Select(c =>
                $"{c.ChangeType},{Escape(c.ObjectName)},{c.ObjectType},{Escape(c.DistinguishedName)},{Escape(c.ChangedFieldsSummary)}"));
            await File.WriteAllLinesAsync(outputPath, lines);
            StatusMessage = $"Exported {FilteredChanges.Count} row(s) to {outputPath}";
        }

        private static string Escape(string v) => $"\"{v.Replace("\"", "\"\"")}\"";
    }
}
