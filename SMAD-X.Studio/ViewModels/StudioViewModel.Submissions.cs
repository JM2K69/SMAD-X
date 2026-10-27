using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SMADX.Models;
using SMADX.Services.Labs;

namespace SMADX.Studio.ViewModels
{
    /// <summary>Une copie dans l'onglet « Copies » : note, indices, objectifs atteints et remarques.</summary>
    public sealed record SubmissionRow(GradedSubmission Graded, LabDefinition Lab, string Culture)
    {
        public string Student => Graded.Student;
        public string FileName => Path.GetFileName(Graded.Path);
        public string SavedAt => Graded.Submission?.SavedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? "";
        public string Grade => Graded.Graded ? $"{Graded.Points} / {Lab.MaxPoints}" : "—";
        public string Percent => Graded.Graded && Lab.MaxPoints > 0 ? $"{100.0 * Graded.Points / Lab.MaxPoints:0} %" : "";
        public int Hints => Graded.HintsUsed;
        /// <summary>Une pastille par objectif : verte s'il est atteint, rouge sinon ; aucune pour une copie non corrigée.</summary>
        public IReadOnlyList<ObjectiveMark> Marks => Graded.Graded
            ? Lab.Objectives.Select(o => new ObjectiveMark(Graded.Passed(o), $"{o.Id} · {o.Title.Resolve(Culture)} ({o.Points} pt)")).ToList()
            : [];
        public string Remarks => string.Join(" ", Graded.Issues.Select(i => i.Message));
        public string RemarksColor => Graded.Issues.Any(i => i.Level == LabIssueLevel.Error) ? "#E74C3C"
            : Graded.Issues.Any(i => i.Level == LabIssueLevel.Warning) ? "#F39C12" : "#27AE60";
    }

    /// <summary>Pastille d'un objectif dans la ligne d'une copie.</summary>
    public sealed record ObjectiveMark(bool Passed, string Tip)
    {
        public string Color => Passed ? "#27AE60" : "#C0392B";
    }

    /// <summary>Réussite d'un objectif dans la classe.</summary>
    public sealed record ObjectiveRate(string Label, int Points, string Rate, double Percent);

    // Onglet « Copies » : corriger les copies d'une classe avec l'exercice ouvert
    public sealed partial class StudioViewModel
    {
        private readonly List<string> _submissionPaths = new();
        private IReadOnlyList<GradedSubmission> _graded = [];

        public ObservableCollection<SubmissionRow> Submissions { get; } = new();
        public ObservableCollection<ObjectiveRate> ObjectiveRates { get; } = new();

        [ObservableProperty] private string _classSummary = "";

        public bool HasSubmissions => Submissions.Count > 0;

        [RelayCommand(CanExecute = nameof(HasLab))]
        private async Task AddSubmissionsAsync()
        {
            var paths = await _dialogs.OpenFilesAsync(Text["Submissions.Add"], Text["File.Submission"], "*" + LabSubmissionService.FileExtension);
            if (paths.Count > 0) AddSubmissions(paths);
        }

        [RelayCommand(CanExecute = nameof(HasLab))]
        private async Task AddSubmissionFolderAsync()
        {
            if (await _dialogs.PickFolderAsync(Text["Submissions.AddFolder"]) is not { } folder) return;
            AddSubmissions(Directory.GetFiles(folder, "*" + LabSubmissionService.FileExtension, SearchOption.AllDirectories));
        }

        /// <summary>Ajoute des copies à la liste (sans doublon de fichier) et corrige tout.</summary>
        public void AddSubmissions(IEnumerable<string> paths)
        {
            foreach (var path in paths.Select(Path.GetFullPath))
                if (!_submissionPaths.Contains(path, StringComparer.OrdinalIgnoreCase))
                    _submissionPaths.Add(path);
            RegradeSubmissions();
        }

        /// <summary>Corrige toutes les copies avec l'exercice tel qu'il est ouvert (objectifs et points compris).</summary>
        [RelayCommand(CanExecute = nameof(HasLab))]
        private void RegradeSubmissions()
        {
            if (Lab == null) return;
            var lab = CurrentLab();
            _graded = SubmissionGrader.GradeAll(lab, _submissionPaths, EditCulture);
            Submissions.Clear();
            foreach (var row in _graded)
                Submissions.Add(new SubmissionRow(row, lab, EditCulture));

            ObjectiveRates.Clear();
            var graded = _graded.Count(r => r.Graded);
            foreach (var o in lab.Objectives)
            {
                var rate = SubmissionGrader.SuccessRate(o, _graded);
                ObjectiveRates.Add(new ObjectiveRate($"{o.Id} · {o.Title.Resolve(EditCulture)}", o.Points,
                    rate is { } r ? $"{_graded.Count(g => g.Passed(o))} / {graded} ({100 * r:0} %)" : "—", 100 * (rate ?? 0)));
            }

            ClassSummary = SubmissionGrader.Average(_graded) is { } average && SubmissionGrader.Median(_graded) is { } median
                ? string.Format(Text["Submissions.Summary"], graded, _graded.Count, average.ToString("0.#", Numbers), lab.MaxPoints,
                    lab.MaxPoints > 0 ? $"{100 * average / lab.MaxPoints:0} %" : "—", median.ToString("0.#", Numbers))
                : _graded.Count > 0 ? string.Format(Text["Submissions.NoneGraded"], _graded.Count) : Text["Submissions.None"];
            OnPropertyChanged(nameof(HasSubmissions));
            ExportGradesCommand.NotifyCanExecuteChanged();
            ExportClassReportCommand.NotifyCanExecuteChanged();
            if (_graded.Count > 0) Status = string.Format(Text["Submissions.Graded"], graded);
        }

        [RelayCommand]
        private void ClearSubmissions() => ResetSubmissions();

        [RelayCommand(CanExecute = nameof(HasSubmissions))]
        private async Task ExportGradesAsync()
        {
            if (await _dialogs.SaveFileAsync(Text["Submissions.ExportCsv"], $"{LabId}{Text.T(".notes", ".grades")}.csv", Text["File.Csv"], "*.csv") is not { } path) return;
            await RunAsync(() =>
            {
                // BOM : Excel reconnaît l'UTF-8 et les accents des noms
                File.WriteAllText(path, SubmissionGrader.Csv(CurrentLab(), _graded, EditCulture), new System.Text.UTF8Encoding(true));
                Status = string.Format(Text["Status.Saved"], path);
            });
        }

        [RelayCommand(CanExecute = nameof(HasSubmissions))]
        private async Task ExportClassReportAsync()
        {
            if (await _dialogs.SaveFileAsync(Text["Submissions.ExportReport"], $"{LabId}{Text.T(".bilan", ".results")}.md", Text["File.Markdown"], "*.md") is not { } path) return;
            await RunAsync(() =>
            {
                File.WriteAllText(path, SubmissionGrader.ClassReport(CurrentLab(), _graded, EditCulture));
                Status = string.Format(Text["Status.Saved"], path);
            });
        }

        /// <summary>Les copies appartiennent à un exercice : en ouvrir un autre vide la liste.</summary>
        private void ResetSubmissions()
        {
            _submissionPaths.Clear();
            _graded = [];
            Submissions.Clear();
            ObjectiveRates.Clear();
            ClassSummary = Text["Submissions.None"];
            OnPropertyChanged(nameof(HasSubmissions));
            ExportGradesCommand.NotifyCanExecuteChanged();
            ExportClassReportCommand.NotifyCanExecuteChanged();
        }

        private CultureInfo Numbers => CultureInfo.GetCultureInfo(EditCulture);
    }
}
