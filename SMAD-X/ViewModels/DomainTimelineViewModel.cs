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
        private static LocalizationService Loc => LocalizationService.Instance;

        [ObservableProperty, NotifyPropertyChangedFor(nameof(FileALabel))] private DomainSnapshot? _fileA;
        [ObservableProperty, NotifyPropertyChangedFor(nameof(FileBLabel))] private DomainSnapshot? _fileB;

        /// <summary>Chemin du fichier affiché sous A ou B, ou « aucun fichier sélectionné ».</summary>
        public string FileALabel => FileA?.FilePath ?? Loc["Timeline.NoFile"];
        public string FileBLabel => FileB?.FilePath ?? Loc["Timeline.NoFile"];

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
        [ObservableProperty] private string _statusMessage = Loc["Timeline.Status.Intro"];

        // ── Commands ────────────────────────────────────────────────────────

        private static readonly ADDataService _dataService = new();

        /// <summary>Loads a single .smad-x.json file into slot A or B.</summary>
        public async Task LoadFileAsync(string path, bool isFileA)
        {
            try
            {
                StatusMessage = string.Format(Loc["Timeline.Status.Loading"], System.IO.Path.GetFileName(path));
                var result = await _dataService.LoadDocumentAsync(path);
                var root = result.Document?.Domain;
                if (root is null)
                {
                    StatusMessage = result.Status == DocumentLoadStatus.TooNew
                        ? string.Format(Loc["Status.ErrorLoadTooNew"], result.FileVersion, ADRootDocument.CurrentVersion)
                        : string.Format(Loc["Timeline.Status.LoadFailed"], result.Error ?? Loc["Timeline.Status.InvalidFormat"]);
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

                StatusMessage = string.Format(Loc["Timeline.Status.Loaded"], isFileA ? 'A' : 'B', System.IO.Path.GetFileName(path), snap.DomainName, snap.SnapshotDate);
            }
            catch (Exception ex)
            {
                StatusMessage = string.Format(Loc["Timeline.Status.LoadError"], ex.Message);
            }
        }

        /// <summary>Runs the diff between FileA and FileB.</summary>
        [RelayCommand]
        private void Compare()
        {
            if (FileA is null || FileB is null)
            {
                StatusMessage = Loc["Timeline.Status.NeedBoth"];
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
                StatusMessage = string.Format(Loc["Timeline.Status.Done"], AllChanges.Count, CountAdded, CountRemoved, CountModified);
            }
            catch (Exception ex)
            {
                StatusMessage = string.Format(Loc["Timeline.Status.CompareError"], ex.Message);
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
            StatusMessage = string.Format(Loc["Csv.Exported"], FilteredChanges.Count, outputPath);
        }

        // Les noms viennent des fichiers comparés : pas de formule Excel
        private static string Escape(string v) => SafeText.CsvCell(v, ",", alwaysQuote: true);
    }
}
