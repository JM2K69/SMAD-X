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
using SMADX.Services;
using SMADX.Services.Labs;

namespace SMADX.Studio.ViewModels
{
    /// <summary>Une copie dans l'onglet « Copies » : note, indices, objectifs et questions, réponses à noter et remarques.</summary>
    public sealed record SubmissionRow(GradedSubmission Graded, LabDefinition Lab, string Culture)
    {
        private CultureInfo Numbers => CultureInfo.GetCultureInfo(Culture);
        public string Student => Graded.Student;
        public string FileName => Path.GetFileName(Graded.Path);
        public string SavedAt => Graded.Submission?.SavedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? "";
        public string Grade => Graded.Graded ? $"{SubmissionGrader.Format(Graded.Points, Numbers)} / {Lab.MaxPoints}{(Graded.ToReview > 0 ? " *" : "")}" : "—";
        public string Percent => Graded.Graded && Lab.MaxPoints > 0 ? $"{100.0 * Graded.Points / Lab.MaxPoints:0} %" : "";
        public int Hints => Graded.HintsUsed;
        public int ToReview => Graded.ToReview;

        /// <summary>
        /// Une pastille par objectif puis par question : verte atteint, orange en partie, rouge non atteint, grise à noter ;
        /// aucune pour une copie non corrigée.
        /// </summary>
        public IReadOnlyList<ObjectiveMark> Marks => Graded.Result is not { } result ? []
            : result.Objectives.Select(o => new ObjectiveMark(o.Passed ? MarkState.Met : o.EarnedPoints > 0 ? MarkState.Partial : MarkState.Missed,
                    $"{o.Objective.Id} · {o.Objective.Title.Resolve(Culture)} ({SubmissionGrader.Format(o.EarnedPoints, Numbers)} / {o.Objective.Points} pt)"))
                .Concat(result.Questions.Select(q => new ObjectiveMark(q.NeedsReview ? MarkState.ToReview : q.Passed ? MarkState.Met : q.EarnedPoints > 0 ? MarkState.Partial : MarkState.Missed,
                    $"{q.Question.Id} · {q.Question.Title.Resolve(Culture)} ({(q.NeedsReview ? StudioText.Instance["Submissions.ToGrade"] : SubmissionGrader.Format(q.EarnedPoints, Numbers))} / {q.Question.Points} pt)")))
                .ToList();
        public string Remarks => string.Join(" ", Graded.Issues.Select(i => i.Message));
        public string RemarksColor => Graded.Issues.Any(i => i.Level == LabIssueLevel.Error) ? "#E74C3C"
            : Graded.Issues.Any(i => i.Level == LabIssueLevel.Warning) ? "#F39C12" : "#27AE60";
    }

    public enum MarkState { Met, Partial, Missed, ToReview }

    /// <summary>Pastille d'un objectif ou d'une question dans la ligne d'une copie.</summary>
    public sealed record ObjectiveMark(MarkState State, string Tip)
    {
        public ObjectiveMark(bool passed, string tip) : this(passed ? MarkState.Met : MarkState.Missed, tip)
        {
        }

        public bool Passed => State == MarkState.Met;
        public string Color => State switch { MarkState.Met => "#27AE60", MarkState.Partial => "#E67E22", MarkState.ToReview => "#95A5A6", _ => "#C0392B" };
    }

    /// <summary>Réussite d'un objectif ou d'une question dans la classe.</summary>
    public sealed record ObjectiveRate(string Label, int Points, string Rate, double Percent);

    /// <summary>
    /// Une question de la copie sélectionnée : la réponse de l'apprenant, la correction automatique et les points que
    /// le formateur donne à la main (vide : la correction automatique compte).
    /// </summary>
    public sealed partial class QuestionMarkItem : ObservableObject
    {
        public QuestionMarkItem(LabQuestionResult result, string culture, double? manual)
        {
            Result = result;
            var numbers = CultureInfo.GetCultureInfo(culture);
            Label = $"{result.Question.Id} · {result.Question.Title.Resolve(culture)}";
            Answer = SubmissionGrader.AnswerText(result.Question, result.Answer, culture);
            Automatic = result.NeedsReview && !result.Manual ? StudioText.Instance["Submissions.ToGrade"]
                : result.Manual ? StudioText.Instance["Submissions.MarkedByHand"]
                : $"{SubmissionGrader.Format(result.EarnedPoints, numbers)} / {result.Question.Points}";
            _points = manual;
        }

        public LabQuestionResult Result { get; }
        public string Label { get; }
        public string Answer { get; }
        public string Automatic { get; }
        public int Max => Result.Question.Points;
        public bool NeedsReview => Result.NeedsReview;

        /// <summary>Points donnés à la main ; null : la correction automatique compte.</summary>
        [ObservableProperty]
        private double? _points;
    }

    // Onglet « Copies » : corriger les copies d'une classe avec l'exercice ouvert
    public sealed partial class StudioViewModel
    {
        private readonly List<string> _submissionPaths = new();
        private IReadOnlyList<GradedSubmission> _graded = [];

        public ObservableCollection<SubmissionRow> Submissions { get; } = new();
        public ObservableCollection<ObjectiveRate> ObjectiveRates { get; } = new();

        [ObservableProperty] private string _classSummary = "";

        public bool HasSubmissions => Submissions.Count > 0;

        /// <summary>Barème des notes exportées (CSV, Moodle, comptes rendus) : sur 20 par défaut.</summary>
        [ObservableProperty] private int _scale = SubmissionGrader.DefaultScale;

        partial void OnScaleChanged(int value)
        {
            if (value < 1) Scale = 1;
        }

        [RelayCommand(CanExecute = nameof(HasLab))]
        private async Task AddSubmissionsAsync()
        {
            var paths = await _dialogs.OpenFilesAsync(Text["Submissions.Add"], Text["File.Submission"], "*" + LabSubmissionService.FileExtension);
            if (paths.Count > 0) await RunAsync(() => AddSubmissions(paths));
        }

        [RelayCommand(CanExecute = nameof(HasLab))]
        private async Task AddSubmissionFolderAsync()
        {
            if (await _dialogs.PickFolderAsync(Text["Submissions.AddFolder"]) is not { } folder) return;
            await RunAsync(() => AddSubmissions(Directory.GetFiles(folder, "*" + LabSubmissionService.FileExtension, SearchOption.AllDirectories), folder));
        }

        /// <summary>
        /// Ajoute des copies à la liste (sans doublon de fichier) et corrige tout. Pour un exercice rangé dans son dossier,
        /// les copies reçues d'ailleurs sont d'abord rangées dans 3-copies-rendues : les notes à la main s'enregistrent à côté,
        /// et l'exercice les retrouve à sa prochaine ouverture.
        /// </summary>
        /// <param name="fromFolder">Dossier d'où viennent les copies : ses sous-dossiers sont gardés.</param>
        public void AddSubmissions(IEnumerable<string> paths, string? fromFolder = null)
        {
            var project = Project;
            var filed = 0;
            foreach (var original in paths.Select(Path.GetFullPath))
            {
                var path = project?.ImportSubmission(original, fromFolder != null ? Path.GetRelativePath(fromFolder, original) : null) ?? original;
                if (path != original) filed++;
                if (!_submissionPaths.Contains(path, StringComparer.OrdinalIgnoreCase))
                    _submissionPaths.Add(path);
            }
            RegradeSubmissions();
            if (filed > 0)
                Status = string.Format(Text["Submissions.Filed"], filed, Path.GetFileName(project!.SubmissionsFolder)) + " " + Status;
        }

        /// <summary>Le dossier de l'exercice a été renommé : les copies de la liste y sont toujours, sous le nouveau nom.</summary>
        private void MoveSubmissions(string oldRoot, string newRoot)
        {
            if (_submissionPaths.Count == 0) return;
            for (var i = 0; i < _submissionPaths.Count; i++)
                if (_submissionPaths[i].StartsWith(oldRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    _submissionPaths[i] = newRoot + _submissionPaths[i][oldRoot.Length..];
            RegradeSubmissions();
        }

        /// <summary>
        /// Corrige toutes les copies avec l'exercice tel qu'il est ouvert (objectifs, questions et points compris). Pour un
        /// exercice rangé dans son dossier, les copies déposées depuis dans 3-copies-rendues sont ajoutées.
        /// </summary>
        [RelayCommand(CanExecute = nameof(HasLab))]
        private void RegradeSubmissions()
        {
            if (Lab == null) return;
            foreach (var path in Project?.SubmissionFiles() ?? [])
                if (!_submissionPaths.Contains(path, StringComparer.OrdinalIgnoreCase))
                    _submissionPaths.Add(path);
            var selected = SelectedSubmission?.Graded.Path;
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
            foreach (var q in lab.Questions)
            {
                var rate = SubmissionGrader.SuccessRate(q, _graded);
                ObjectiveRates.Add(new ObjectiveRate($"{q.Id} · {q.Title.Resolve(EditCulture)}", q.Points,
                    rate is { } r ? $"{_graded.Count(g => g.Passed(q))} / {graded} ({100 * r:0} %)" : "—", 100 * (rate ?? 0)));
            }

            var toReview = _graded.Sum(r => r.ToReview);
            ClassSummary = SubmissionGrader.Average(_graded) is { } average && SubmissionGrader.Median(_graded) is { } median
                ? string.Format(Text["Submissions.Summary"], graded, _graded.Count, average.ToString("0.#", Numbers), lab.MaxPoints,
                    lab.MaxPoints > 0 ? $"{100 * average / lab.MaxPoints:0} %" : "—", median.ToString("0.#", Numbers))
                  + (toReview > 0 ? " " + string.Format(Text["Submissions.ToReview"], toReview) : "")
                : _graded.Count > 0 ? string.Format(Text["Submissions.NoneGraded"], _graded.Count) : Text["Submissions.None"];
            OnPropertyChanged(nameof(HasSubmissions));
            ExportGradesCommand.NotifyCanExecuteChanged();
            ExportMoodleCommand.NotifyCanExecuteChanged();
            ExportReportsCommand.NotifyCanExecuteChanged();
            ExportClassReportCommand.NotifyCanExecuteChanged();
            SelectedSubmission = Submissions.FirstOrDefault(s => string.Equals(s.Graded.Path, selected, StringComparison.OrdinalIgnoreCase));
            if (_graded.Count > 0) Status = string.Format(Text["Submissions.Graded"], graded);
        }

        [RelayCommand]
        private void ClearSubmissions() => ResetSubmissions();

        /// <summary>Pour un exercice rangé dans son dossier, les exports vont dans 4-resultats, sans rien demander.</summary>
        private bool SaveInResults(Func<LabProject, string> file, Action<string> write)
        {
            if (Project is not { } project) return false;
            Directory.CreateDirectory(project.ResultsFolder);
            var path = file(project);
            write(path);
            Status = string.Format(Text["Submissions.ResultsSaved"], path);
            return true;
        }

        [RelayCommand(CanExecute = nameof(HasSubmissions))]
        private async Task ExportGradesAsync()
        {
            if (SaveInResults(p => p.GradesPath, path => File.WriteAllText(path, SubmissionGrader.Csv(CurrentLab(), _graded, EditCulture, Scale), new System.Text.UTF8Encoding(true)))) return;
            if (await _dialogs.SaveFileAsync(Text["Submissions.ExportCsv"], $"{LabId}{Text.T(".notes", ".grades")}.csv", Text["File.Csv"], "*.csv") is not { } path) return;
            await RunAsync(() =>
            {
                // BOM : Excel reconnaît l'UTF-8 et les accents des noms
                File.WriteAllText(path, SubmissionGrader.Csv(CurrentLab(), _graded, EditCulture, Scale), new System.Text.UTF8Encoding(true));
                Status = string.Format(Text["Status.Saved"], path);
            });
        }

        [RelayCommand(CanExecute = nameof(HasSubmissions))]
        private async Task ExportMoodleAsync()
        {
            if (SaveInResults(p => p.MoodlePath, path => File.WriteAllText(path, SubmissionGrader.MoodleCsv(CurrentLab(), _graded, EditCulture, Scale), new System.Text.UTF8Encoding(false)))) return;
            if (await _dialogs.SaveFileAsync(Text["Submissions.ExportMoodle"], $"{LabId}.moodle.csv", Text["File.Csv"], "*.csv") is not { } path) return;
            await RunAsync(() =>
            {
                File.WriteAllText(path, SubmissionGrader.MoodleCsv(CurrentLab(), _graded, EditCulture, Scale), new System.Text.UTF8Encoding(false));
                Status = string.Format(Text["Status.Saved"], path);
            });
        }

        /// <summary>Un compte rendu PDF par copie corrigée, dans le dossier choisi, à remettre à chaque apprenant.</summary>
        [RelayCommand(CanExecute = nameof(HasSubmissions))]
        private async Task ExportReportsAsync()
        {
            if (SaveInResults(p => p.ReportsFolder, folder => ExportReports(folder))) return;
            if (await _dialogs.PickFolderAsync(Text["Submissions.ExportReports"]) is not { } folder) return;
            await RunAsync(() => Status = string.Format(Text["Submissions.ReportsSaved"], ExportReports(folder), folder));
        }

        /// <summary>Écrit les comptes rendus PDF ; renvoie leur nombre.</summary>
        public int ExportReports(string folder)
        {
            Directory.CreateDirectory(folder);
            var lab = CurrentLab();
            var count = 0;
            foreach (var row in _graded.Where(r => r.Graded))
            {
                ADDocumentReportService.MarkdownToPdf(SubmissionGrader.StudentReport(lab, row, EditCulture, Scale),
                    Path.Combine(folder, SubmissionGrader.StudentReportFileName(lab, row, ".pdf")));
                count++;
            }
            return count;
        }

        [RelayCommand(CanExecute = nameof(HasSubmissions))]
        private async Task ExportClassReportAsync()
        {
            if (SaveInResults(p => p.ClassReportPath, path => File.WriteAllText(path, SubmissionGrader.ClassReport(CurrentLab(), _graded, EditCulture)))) return;
            if (await _dialogs.SaveFileAsync(Text["Submissions.ExportReport"], $"{LabId}{Text.T(".bilan", ".results")}.md", Text["File.Markdown"], "*.md") is not { } path) return;
            await RunAsync(() =>
            {
                File.WriteAllText(path, SubmissionGrader.ClassReport(CurrentLab(), _graded, EditCulture));
                Status = string.Format(Text["Status.Saved"], path);
            });
        }

        // ─── Copie sélectionnée : notes à la main et appréciation ────────────────

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasSelectedSubmission))]
        [NotifyCanExecuteChangedFor(nameof(SaveMarksCommand))]
        private SubmissionRow? _selectedSubmission;

        public bool HasSelectedSubmission => SelectedSubmission is { Graded.Graded: true };

        public ObservableCollection<QuestionMarkItem> SelectedAnswers { get; } = new();

        /// <summary>Appréciation du formateur sur la copie sélectionnée, reprise dans son compte rendu.</summary>
        [ObservableProperty] private string _comment = "";

        partial void OnSelectedSubmissionChanged(SubmissionRow? value)
        {
            SelectedAnswers.Clear();
            Comment = value?.Graded.Marks?.Comment ?? "";
            if (value?.Graded.Result is not { } result) return;
            foreach (var q in result.Questions)
                SelectedAnswers.Add(new QuestionMarkItem(q, EditCulture,
                    q.Manual && value.Graded.Marks?.Questions.TryGetValue(q.Question.Id, out var mark) == true ? mark.Points : null));
        }

        /// <summary>
        /// Enregistre, à côté de la copie (.smadx-marks.json), les points donnés à la main et l'appréciation, puis
        /// recorrige : la copie elle-même, scellée, n'est pas modifiée.
        /// </summary>
        [RelayCommand(CanExecute = nameof(HasSelectedSubmission))]
        private void SaveMarks()
        {
            if (SelectedSubmission?.Graded is not { Submission: { } submission } row) return;
            var marks = new SubmissionMarks
            {
                LabId = submission.LabId, SessionId = submission.SessionId,
                Comment = string.IsNullOrWhiteSpace(Comment) ? null : Comment.Trim()
            };
            foreach (var item in SelectedAnswers.Where(a => a.Points is not null))
                marks.Questions[item.Result.Question.Id] = new QuestionMark
                {
                    Points = Math.Clamp(item.Points!.Value, 0, item.Max),
                    Answer = item.Result.Answer?.Signature() ?? string.Empty
                };
            try
            {
                SubmissionMarksService.Save(marks, row.Path);
                RegradeSubmissions();
                Status = string.Format(Text["Submissions.MarksSaved"], row.Student);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Status = string.Format(Text["Status.Error"], ex.Message);
            }
        }

        /// <summary>Les copies appartiennent à un exercice : en ouvrir un autre vide la liste.</summary>
        private void ResetSubmissions()
        {
            _submissionPaths.Clear();
            _graded = [];
            Submissions.Clear();
            ObjectiveRates.Clear();
            SelectedSubmission = null;
            ClassSummary = Text["Submissions.None"];
            OnPropertyChanged(nameof(HasSubmissions));
            ExportGradesCommand.NotifyCanExecuteChanged();
            ExportMoodleCommand.NotifyCanExecuteChanged();
            ExportReportsCommand.NotifyCanExecuteChanged();
            ExportClassReportCommand.NotifyCanExecuteChanged();
        }

        private CultureInfo Numbers => CultureInfo.GetCultureInfo(EditCulture);
    }
}
