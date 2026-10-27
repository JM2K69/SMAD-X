using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SMADX.Models;
using SMADX.Services;
using SMADX.Services.Labs;

namespace SMADX.ViewModels
{
    /// <summary>Un objectif de l'exercice dans la fenêtre : texte, indice, statut après vérification.</summary>
    public sealed partial class LabObjectiveItem : ObservableObject
    {
        public LabObjectiveItem(LabObjective objective, string culture, bool canShowHint = true)
        {
            Objective = objective;
            Title = objective.Title.Resolve(culture);
            Hint = objective.Hint.Resolve(culture);
            CanShowHint = canShowHint;
        }

        public LabObjective Objective { get; }
        public string Title { get; }
        public string Hint { get; }
        public int Points => Objective.Points;

        /// <summary>Faux en mode examen : pas d'indice.</summary>
        public bool CanShowHint { get; }

        [ObservableProperty]
        private bool _isHintVisible;

        /// <summary>Indice consulté au moins une fois, même s'il a été masqué ensuite.</summary>
        public bool HintSeen { get; set; }

        partial void OnIsHintVisibleChanged(bool value)
        {
            if (value) HintSeen = true;
        }

        /// <summary>null = pas encore vérifié.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(StatusIcon))]
        [NotifyPropertyChangedFor(nameof(StatusColor))]
        private bool? _passed;

        [ObservableProperty]
        private string _feedback = string.Empty;

        public string StatusIcon => Passed switch { true => "✔", false => "✘", _ => "○" };
        public string StatusColor => Passed switch { true => "#27AE60", false => "#C0392B", _ => "#7F8C8D" };

        [RelayCommand]
        private void ToggleHint()
        {
            if (CanShowHint) IsHintVisible = !IsHintVisible;
        }
    }

    /// <summary>
    /// Fenêtre « Exercice » : énoncé, objectifs, vérification par le correcteur, indices, recommencer,
    /// compte rendu Markdown et copie (.smadx-submission.json) à rendre au formateur. En mode examen,
    /// il n'y a ni indice, ni vérification, ni « Recommencer » : seule la copie est enregistrée.
    /// </summary>
    public sealed partial class LabViewModel : ViewModelBase
    {
        private readonly LabSession _session;
        private readonly LabDefinition _lab;
        private readonly Func<ADRootDocument?> _getCurrent;
        private readonly Action _restart;
        private readonly IDialogService _dialogs;
        private readonly Func<DateTime> _clock;
        private readonly LabGrader _grader = new();
        private LabResult? _lastResult;
        private string? _submissionPath;

        private static LocalizationService Loc => LocalizationService.Instance;

        /// <param name="session">Exercice en cours et son domaine de départ préparé, référence de la comparaison.</param>
        /// <param name="getCurrent">Document de l'apprenant dans son état actuel.</param>
        /// <param name="restart">Recharge le domaine de départ dans la fenêtre principale.</param>
        /// <param name="resumed">Copie reprise : nom de l'apprenant et indices déjà consultés.</param>
        /// <param name="submissionPath">Fichier de la copie reprise, réécrit par « Enregistrer ma copie ».</param>
        public LabViewModel(LabSession session, Func<ADRootDocument?> getCurrent, Action restart, IDialogService dialogs,
                            LabSubmission? resumed = null, string? submissionPath = null, Func<DateTime>? clock = null)
        {
            _session = session;
            _lab = session.Lab;
            _getCurrent = getCurrent;
            _restart = restart;
            _dialogs = dialogs;
            _clock = clock ?? (() => DateTime.Now);
            _submissionPath = submissionPath;
            var culture = Loc.CurrentCulture.Name;
            Title = _lab.Title.Resolve(culture);
            Statement = _lab.Statement.Resolve(culture);
            foreach (var objective in _lab.Objectives)
                Objectives.Add(new LabObjectiveItem(objective, culture, canShowHint: !_lab.Exam)
                {
                    HintSeen = resumed?.HintsUsed.Contains(objective.Id) == true
                });
            _studentName = resumed?.Student ?? string.Empty;
        }

        public string Title { get; }
        public string Statement { get; }
        public string LevelLabel => Loc[$"Lab.Level.{_lab.Level}"];
        public string DurationLabel => string.Format(Loc["Lab.Duration"], _lab.DurationMinutes);
        public ObservableCollection<LabObjectiveItem> Objectives { get; }  = new();

        public bool IsExam => _lab.Exam;

        /// <summary>Vérification, score, « Recommencer » et compte rendu : tout sauf en examen.</summary>
        public bool ShowGrading => !IsExam;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasStudentName))]
        [NotifyCanExecuteChangedFor(nameof(SaveSubmissionCommand))]
        private string _studentName = string.Empty;

        public bool HasStudentName => !string.IsNullOrWhiteSpace(StudentName);

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ScoreLabel))]
        private int _points;

        [ObservableProperty]
        private bool _isCompleted;

        [ObservableProperty]
        private bool _hasBeenChecked;

        /// <summary>Dernier enregistrement de la copie (ou erreur), vide avant le premier.</summary>
        [ObservableProperty]
        private string _submissionStatus = string.Empty;

        public int MaxPoints => _lab.MaxPoints;
        public string ScoreLabel => string.Format(Loc["Lab.Score"], Points, MaxPoints);
        public int HintsUsed => Objectives.Count(o => o.HintSeen);

        [RelayCommand(CanExecute = nameof(ShowGrading))]
        private void Check()
        {
            if (_getCurrent()?.Domain is not { } current) return;

            _lastResult = _grader.Grade(_lab, _session.Start.Domain, current, _clock());
            foreach (var result in _lastResult.Objectives)
            {
                var item = Objectives.First(o => o.Objective == result.Objective);
                item.Passed = result.Passed;
                item.Feedback = result.Feedback;
            }
            Points = _lastResult.Points;
            IsCompleted = _lastResult.Completed;
            HasBeenChecked = true;
        }

        [RelayCommand(CanExecute = nameof(ShowGrading))]
        private void Restart()
        {
            _restart();
            foreach (var item in Objectives)
            {
                item.Passed = null;
                item.Feedback = string.Empty;
                item.IsHintVisible = false;
            }
            Points = 0;
            IsCompleted = false;
            HasBeenChecked = false;
            _lastResult = null;
        }

        [RelayCommand(CanExecute = nameof(ShowGrading))]
        private async Task ExportResult()
        {
            Check();
            if (_lastResult == null) return;

            var name = string.IsNullOrWhiteSpace(StudentName) ? "apprenant" : StudentName.Trim();
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            var path = await _dialogs.PickSaveFileAsync(Loc["Lab.Export.Title"], $"{_lab.Id}_{name}_{_clock():yyyyMMdd_HHmm}.md", "md",
                new FileFilter(Loc["FileType.Markdown"], "*.md"));
            if (path == null) return;

            await File.WriteAllTextAsync(path, BuildReport(_lastResult), new UTF8Encoding(false));
        }

        /// <summary>
        /// Enregistre la copie : l'exercice reçu, le domaine actuel et les indices consultés. Le premier
        /// enregistrement demande le fichier, les suivants le réécrivent.
        /// </summary>
        [RelayCommand(CanExecute = nameof(HasStudentName))]
        private async Task SaveSubmission()
        {
            if (_getCurrent() is not { } work) return;
            var path = _submissionPath ?? await _dialogs.PickSaveFileAsync(Loc["Lab.Submission.Title"],
                LabSubmissionService.SuggestedFileName(_lab.Id, StudentName), "smadx-submission.json",
                new FileFilter(Loc["FileType.Submission"], "*" + LabSubmissionService.FileExtension));
            if (path == null) return;

            var now = _clock();
            try
            {
                var submission = _session.ToSubmission(StudentName, work, Objectives.Where(o => o.HintSeen).Select(o => o.Objective.Id), now);
                await File.WriteAllTextAsync(path, new LabSubmissionService().Serialize(submission), new UTF8Encoding(false));
                _submissionPath = path;
                SubmissionStatus = string.Format(Loc["Lab.Submission.Saved"], path, now.ToString("HH:mm"));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                SubmissionStatus = ex.Message;
            }
        }

        /// <summary>Compte rendu Markdown : identité, score, détail par objectif, indices utilisés.</summary>
        public string BuildReport(LabResult result)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# {Title}");
            sb.AppendLine();
            sb.AppendLine($"- **{Loc["Lab.Report.Student"]}** : {(string.IsNullOrWhiteSpace(StudentName) ? "—" : StudentName.Trim())}");
            sb.AppendLine($"- **{Loc["Lab.Report.Lab"]}** : `{_lab.Id}` ({LevelLabel})");
            sb.AppendLine($"- **{Loc["Lab.Report.Date"]}** : {result.GradedAt:yyyy-MM-dd HH:mm}");
            sb.AppendLine($"- **{Loc["Lab.Report.Score"]}** : {result.Points} / {result.MaxPoints}");
            sb.AppendLine($"- **{Loc["Lab.Report.Hints"]}** : {HintsUsed}");
            sb.AppendLine($"- **{Loc["Lab.Report.Audit"]}** : {result.Audit.Score} / 100");
            sb.AppendLine();
            sb.AppendLine($"| | {Loc["Lab.Report.Objective"]} | {Loc["Lab.Report.Points"]} | {Loc["Lab.Report.Feedback"]} |");
            sb.AppendLine("|---|---|---|---|");
            foreach (var item in Objectives)
            {
                var r = result.Objectives.First(o => o.Objective == item.Objective);
                var hint = item.HintSeen ? $" ({Loc["Lab.Report.HintUsed"]})" : string.Empty;
                sb.AppendLine($"| {(r.Passed ? "✔" : "✘")} | {Escape(item.Title)}{hint} | {r.EarnedPoints} / {item.Points} | {Escape(r.Feedback)} |");
            }
            return sb.ToString();
        }

        private static string Escape(string text) => text.Replace("|", "\\|").Replace("\n", " ");
    }
}
