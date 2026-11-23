using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SMADX.Models;
using SMADX.Services.Labs;

namespace SMADX.Studio.ViewModels
{
    /// <summary>Un exercice du lot déposé, dans l'onglet Copies : un bouton pour passer à ses copies.</summary>
    public sealed record BatchLabItem(string Id, string Title, int Count, bool IsBuiltIn, bool IsOpen)
    {
        /// <summary>« chemin-04-write-members (2) » : l'identifiant, le début du nom des copies, et leur nombre.</summary>
        public string Chip => $"{Id} ({Count})";
        public bool CanOpen => IsBuiltIn && !IsOpen;
        public string Tip => IsBuiltIn ? Title : string.Format(StudioText.Instance["Batch.NotBuiltIn"], Id);
    }

    // Lot de copies de plusieurs exercices (un dossier de devoirs) : rangées par exercice, corrigées l'un après l'autre, bilan
    public sealed partial class StudioViewModel
    {
        /// <summary>Onglet « Copies » de l'exercice SMAD-X ouvert.</summary>
        public const int SubmissionsTab = 6;

        /// <summary>Onglet affiché ; déposer des copies montre l'onglet Copies.</summary>
        [ObservableProperty] private int _selectedTab;

        /// <summary>Le dernier lot déposé (copies de plusieurs exercices), null s'il n'y en a pas.</summary>
        private SubmissionBatch? _batch;

        /// <summary>Dossier déposé d'où vient chaque copie du lot : ses sous-dossiers sont gardés quand elle est rangée.</summary>
        private Dictionary<string, string?> _batchOrigins = new(StringComparer.OrdinalIgnoreCase);

        public ObservableCollection<BatchLabItem> BatchLabs { get; } = new();

        public bool HasBatch => _batch != null;

        [ObservableProperty] private string _batchSummary = "";

        partial void OnLabChanged(LabDefinition? value) => RefreshBatch();

        /// <summary>
        /// Copies de plusieurs exercices déposées ensemble : Studio les range par exercice. L'exercice ouvert reçoit les siennes ;
        /// sans exercice ouvert, le premier exercice fourni du lot s'ouvre avec ses copies. Les autres exercices attendent dans
        /// l'onglet Copies, un clic chacun.
        /// </summary>
        private async Task DropBatchAsync(SubmissionBatch batch, IEnumerable<(string Path, string? From)> dropped)
        {
            _batch = batch;
            _batchOrigins = dropped.GroupBy(d => d.Path, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First().From, StringComparer.OrdinalIgnoreCase);
            if (Lab != null)
                await RunAsync(AddBatchCopies);
            else if (IsEmpty && batch.Labs.FirstOrDefault(l => IsBuiltIn(l.LabId)) is { } first)
                await OpenBuiltInLabAsync(first.LabId);
            RefreshBatch();
            Status = BatchStatus();
            SelectedTab = SubmissionsTab;
        }

        /// <summary>Les copies du lot qui sont de l'exercice ouvert, ajoutées et corrigées (rangées dans 3-copies-rendues s'il en a un).</summary>
        private void AddBatchCopies()
        {
            if (Lab == null || _batch?.Find(Lab.Id) is not { } group) return;
            foreach (var from in group.Paths.GroupBy(p => _batchOrigins.GetValueOrDefault(p), StringComparer.OrdinalIgnoreCase))
                AddSubmissions(from, from.Key);
        }

        private string BatchStatus() =>
            _batch == null ? Status
            : Lab != null && _batch.Find(Lab.Id) is { } shown
                ? string.Format(Text["Batch.Opened"], _batch.Count, _batch.Labs.Count, Lab.Id, shown.Paths.Count)
                : string.Format(Text["Batch.Choose"], _batch.Count, _batch.Labs.Count);

        private bool IsBuiltIn(string id) => LoadBuiltInLabs().Any(l => string.Equals(l.Id, id, StringComparison.OrdinalIgnoreCase));

        private void RefreshBatch()
        {
            BatchLabs.Clear();
            if (_batch != null)
                foreach (var group in _batch.Labs)
                {
                    var builtIn = LoadBuiltInLabs().FirstOrDefault(l => string.Equals(l.Id, group.LabId, StringComparison.OrdinalIgnoreCase));
                    BatchLabs.Add(new BatchLabItem(group.LabId, builtIn?.Title.Resolve(Text.Culture) ?? group.LabId, group.Paths.Count, builtIn != null,
                        string.Equals(Lab?.Id, group.LabId, StringComparison.OrdinalIgnoreCase)));
                }
            BatchSummary = _batch == null ? "" : string.Format(Text["Batch.Summary"], _batch.Count, _batch.Labs.Count);
            OnPropertyChanged(nameof(HasBatch));
            ExportBatchCommand.NotifyCanExecuteChanged();
        }

        /// <summary>Paramètre object, comme pour le menu : CanExecute peut être évalué avec un autre DataContext.</summary>
        [RelayCommand]
        private async Task OpenBatchLab(object? parameter)
        {
            if (parameter is not BatchLabItem item || item.IsOpen) return;
            if (!item.IsBuiltIn)
            {
                Status = string.Format(Text["Batch.NotBuiltIn"], item.Id);
                return;
            }
            if (await OpenBuiltInLabAsync(item.Id)) Status = BatchStatus();
        }

        /// <summary>
        /// Bilan du lot : la note de chaque apprenant à chaque exercice, avec l'exercice ouvert tel qu'il est (vos points, vos
        /// notes à la main) et les autres exercices fournis tels qu'ils sont. Un exercice du lot qui n'est ni ouvert ni fourni
        /// n'y est pas.
        /// </summary>
        public string BatchCsv()
        {
            if (_batch == null) return "";
            var labs = new List<GradedBatchLab>();
            foreach (var group in _batch.Labs)
            {
                if (Lab != null && string.Equals(Lab.Id, group.LabId, StringComparison.OrdinalIgnoreCase))
                    labs.Add(new GradedBatchLab(CurrentLab(), _graded));
                else if (_labs.BuiltInLab(group.LabId) is { } lab)
                    labs.Add(new GradedBatchLab(lab, SubmissionGrader.GradeAll(lab, group.Paths, Text.Culture)));
            }
            return SubmissionBatch.Csv(labs, Text.Culture, Scale);
        }

        [RelayCommand(CanExecute = nameof(HasBatch))]
        private async Task ExportBatchAsync()
        {
            if (_batch == null) return;
            var name = Text.T("bilan-des-exercices.csv", "labs-summary.csv");
            if (Project is { } project)
            {
                await RunAsync(() =>
                {
                    Directory.CreateDirectory(project.ResultsFolder);
                    var inResults = Path.Combine(project.ResultsFolder, name);
                    File.WriteAllText(inResults, BatchCsv(), new System.Text.UTF8Encoding(true));
                    Status = string.Format(Text["Submissions.ResultsSaved"], inResults);
                });
                return;
            }
            if (await _dialogs.SaveFileAsync(Text["Batch.Export"], name, Text["File.Csv"], "*.csv") is not { } path) return;
            await RunAsync(() =>
            {
                // BOM : Excel reconnaît l'UTF-8 et les accents des noms
                File.WriteAllText(path, BatchCsv(), new System.Text.UTF8Encoding(true));
                Status = string.Format(Text["Status.Saved"], path);
            });
        }
    }
}
