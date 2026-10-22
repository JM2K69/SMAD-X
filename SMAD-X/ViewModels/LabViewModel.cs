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
        public LabObjectiveItem(LabObjective objective, string culture)
        {
            Objective = objective;
            Title = objective.Title.Resolve(culture);
            Hint = objective.Hint.Resolve(culture);
        }

        public LabObjective Objective { get; }
        public string Title { get; }
        public string Hint { get; }
        public int Points => Objective.Points;

        [ObservableProperty]
        private bool _isHintVisible;

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
        private void ToggleHint() => IsHintVisible = !IsHintVisible;
    }

    /// <summary>
    /// Fenêtre « Exercice » : énoncé, objectifs, vérification par le correcteur, indices, recommencer,
    /// et export d'un compte rendu Markdown à remettre à l'enseignant.
    /// </summary>
    public sealed partial class LabViewModel : ViewModelBase
    {
        private readonly LabDefinition _lab;
        private readonly ADObject _start;
        private readonly Func<ADObject?> _getCurrent;
        private readonly Action _restart;
        private readonly IDialogService _dialogs;
        private readonly LabGrader _grader = new();
        private LabResult? _lastResult;

        private static LocalizationService Loc => LocalizationService.Instance;

        /// <param name="start">Domaine de départ préparé, référence de la comparaison « non modifié ».</param>
        /// <param name="getCurrent">Domaine de l'apprenant dans son état actuel.</param>
        /// <param name="restart">Recharge le domaine de départ dans la fenêtre principale.</param>
        public LabViewModel(LabDefinition lab, ADObject start, Func<ADObject?> getCurrent, Action restart, IDialogService dialogs)
        {
            _lab = lab;
            _start = start;
            _getCurrent = getCurrent;
            _restart = restart;
            _dialogs = dialogs;
            var culture = Loc.CurrentCulture.Name;
            Title = lab.Title.Resolve(culture);
            Statement = lab.Statement.Resolve(culture);
            foreach (var objective in lab.Objectives)
                Objectives.Add(new LabObjectiveItem(objective, culture));
        }

        public string Title { get; }
        public string Statement { get; }
        public string LevelLabel => Loc[$"Lab.Level.{_lab.Level}"];
        public string DurationLabel => string.Format(Loc["Lab.Duration"], _lab.DurationMinutes);
        public ObservableCollection<LabObjectiveItem> Objectives { get; }  = new();

        [ObservableProperty]
        private string _studentName = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ScoreLabel))]
        private int _points;

        [ObservableProperty]
        private bool _isCompleted;

        [ObservableProperty]
        private bool _hasBeenChecked;

        public int MaxPoints => _lab.MaxPoints;
        public string ScoreLabel => string.Format(Loc["Lab.Score"], Points, MaxPoints);
        public int HintsUsed => Objectives.Count(o => o.IsHintVisible);

        [RelayCommand]
        private void Check()
        {
            var current = _getCurrent();
            if (current == null) return;

            _lastResult = _grader.Grade(_lab, _start, current, DateTime.Now);
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

        [RelayCommand]
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

        [RelayCommand]
        private async Task ExportResult()
        {
            Check();
            if (_lastResult == null) return;

            var name = string.IsNullOrWhiteSpace(StudentName) ? "apprenant" : StudentName.Trim();
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            var path = await _dialogs.PickSaveFileAsync(Loc["Lab.Export.Title"], $"{_lab.Id}_{name}_{DateTime.Now:yyyyMMdd_HHmm}.md", "md",
                new FileFilter(Loc["FileType.Markdown"], "*.md"));
            if (path == null) return;

            await File.WriteAllTextAsync(path, BuildReport(_lastResult), new UTF8Encoding(false));
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
                var hint = item.IsHintVisible ? $" ({Loc["Lab.Report.HintUsed"]})" : string.Empty;
                sb.AppendLine($"| {(r.Passed ? "✔" : "✘")} | {Escape(item.Title)}{hint} | {r.EarnedPoints} / {item.Points} | {Escape(r.Feedback)} |");
            }
            return sb.ToString();
        }

        private static string Escape(string text) => text.Replace("|", "\\|").Replace("\n", " ");
    }
}
