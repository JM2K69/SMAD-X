using System;
using System.Collections.Generic;
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

        /// <summary>Une garde ne rapporte pas de point : elle est marquée 🛡.</summary>
        public bool IsGuard => Objective.Guard;
        public bool IsScored => !Objective.Guard;

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

        /// <summary>Non atteint, mais une part des points est acquise (crédit partiel).</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(StatusIcon))]
        [NotifyPropertyChangedFor(nameof(StatusColor))]
        private bool _isPartial;

        [ObservableProperty]
        private string _feedback = string.Empty;

        public string StatusIcon => Status.Icon(Passed, IsPartial);
        public string StatusColor => Status.Color(Passed, IsPartial);

        [RelayCommand]
        private void ToggleHint()
        {
            if (CanShowHint) IsHintVisible = !IsHintVisible;
        }
    }

    /// <summary>Pictogrammes et couleurs d'un objectif ou d'une question corrigés.</summary>
    internal static class Status
    {
        public static string Icon(bool? passed, bool partial) => passed switch { true => "✔", false when partial => "◐", false => "✘", _ => "○" };
        public static string Color(bool? passed, bool partial) => passed switch { true => "#27AE60", false when partial => "#E67E22", false => "#C0392B", _ => "#7F8C8D" };
    }

    /// <summary>Un choix d'une question : coché ou non. Les boutons radio d'une question partagent son identifiant de groupe.</summary>
    public sealed partial class LabChoiceItem : ObservableObject
    {
        private readonly LabQuestionItem _question;

        public LabChoiceItem(LabQuestionItem question, LabChoice choice, string culture)
        {
            _question = question;
            Id = choice.Id;
            Text = choice.Text.Resolve(culture);
        }

        public string Id { get; }
        public string Text { get; }
        public string GroupName => "question-" + _question.Id;

        [ObservableProperty]
        private bool _isSelected;

        partial void OnIsSelectedChanged(bool value) => _question.OnChoiceChanged(this, value);
    }

    /// <summary>Une proposition d'association, dans la liste déroulante de chaque élément.</summary>
    public sealed record LabOption(string Id, string Text)
    {
        public override string ToString() => Text;
    }

    /// <summary>Un élément à associer : la proposition choisie par l'apprenant.</summary>
    public sealed partial class LabPromptItem : ObservableObject
    {
        public LabPromptItem(LabPrompt prompt, IReadOnlyList<LabOption> options, string culture, string? chosen)
        {
            Id = prompt.Id;
            Text = prompt.Text.Resolve(culture);
            Options = options;
            _selected = options.FirstOrDefault(o => string.Equals(o.Id, chosen, StringComparison.OrdinalIgnoreCase));
        }

        public string Id { get; }
        public string Text { get; }
        public IReadOnlyList<LabOption> Options { get; }

        [ObservableProperty]
        private LabOption? _selected;
    }

    /// <summary>
    /// Une question de l'exercice dans la fenêtre : la réponse de l'apprenant et, hors examen, sa correction. En
    /// examen, le sujet n'a pas le corrigé : la réponse est enregistrée dans la copie et corrigée par le formateur.
    /// </summary>
    public sealed partial class LabQuestionItem : ObservableObject
    {
        public LabQuestionItem(LabQuestion question, string culture, bool canShowHint, LabAnswer? answer)
        {
            Question = question;
            Title = question.Title.Resolve(culture);
            Hint = question.Hint.Resolve(culture);
            CanShowHint = canShowHint && !string.IsNullOrWhiteSpace(Hint);
            Choices = question.Choices.Select(c => new LabChoiceItem(this, c, culture)).ToList();
            var options = question.Choices.Select(c => new LabOption(c.Id, c.Text.Resolve(culture))).ToList();
            Prompts = (question.Prompts ?? []).Select(p => new LabPromptItem(p, options, culture, answer?.Matches?.GetValueOrDefault(p.Id))).ToList();
            _text = answer?.Text ?? string.Empty;
            foreach (var choice in Choices.Where(c => answer?.Choices?.Contains(c.Id, StringComparer.OrdinalIgnoreCase) == true))
                choice.IsSelected = true;
        }

        public LabQuestion Question { get; }
        public string Id => Question.Id;
        public string Title { get; }
        public string Hint { get; }
        public int Points => Question.Points;
        /// <summary>Choix unique ou oui / non : des boutons radio.</summary>
        public bool IsSingleChoice => Question.Kind is LabQuestionKind.SingleChoice or LabQuestionKind.TrueFalse;
        public bool IsMultipleChoice => Question.Kind == LabQuestionKind.MultipleChoice;
        public bool IsMatching => Question.Kind == LabQuestionKind.Matching;
        public bool IsText => Question.Kind == LabQuestionKind.Text;
        public IReadOnlyList<LabChoiceItem> Choices { get; }
        public IReadOnlyList<LabPromptItem> Prompts { get; }
        public bool CanShowHint { get; }

        [ObservableProperty]
        private string _text;

        [ObservableProperty]
        private bool _isHintVisible;

        public bool HintSeen { get; set; }

        partial void OnIsHintVisibleChanged(bool value)
        {
            if (value) HintSeen = true;
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(StatusIcon))]
        [NotifyPropertyChangedFor(nameof(StatusColor))]
        private bool? _passed;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(StatusIcon))]
        [NotifyPropertyChangedFor(nameof(StatusColor))]
        private bool _isPartial;

        [ObservableProperty]
        private string _feedback = string.Empty;

        public string StatusIcon => Status.Icon(Passed, IsPartial);
        public string StatusColor => Status.Color(Passed, IsPartial);

        /// <summary>La réponse telle qu'elle s'enregistre dans la copie.</summary>
        public LabAnswer Answer => Question.Kind switch
        {
            LabQuestionKind.Text => new LabAnswer { Text = string.IsNullOrWhiteSpace(Text) ? null : Text.Trim() },
            LabQuestionKind.Matching => new LabAnswer { Matches = Prompts.Where(p => p.Selected != null).ToDictionary(p => p.Id, p => p.Selected!.Id) },
            _ => new LabAnswer { Choices = Choices.Where(c => c.IsSelected).Select(c => c.Id).ToList() }
        };

        /// <summary>Choix unique : cocher un choix décoche les autres (le groupe des boutons radio le fait aussi).</summary>
        internal void OnChoiceChanged(LabChoiceItem choice, bool selected)
        {
            if (!selected || !IsSingleChoice) return;
            foreach (var other in Choices.Where(c => c != choice && c.IsSelected))
                other.IsSelected = false;
        }

        [RelayCommand]
        private void ToggleHint()
        {
            if (CanShowHint) IsHintVisible = !IsHintVisible;
        }
    }

    /// <summary>
    /// Fenêtre « Exercice » : énoncé, objectifs, questions, vérification par le correcteur, indices, recommencer,
    /// compte rendu Markdown et copie (.smadx-submission.json) à rendre au formateur. En mode examen,
    /// il n'y a ni indice, ni score, ni « Recommencer » : la copie est enregistrée, et « Vérifier », si le
    /// formateur l'a permis, montre seulement les objectifs atteints. Le temps passé est mesuré.
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
        /// <param name="resumed">Copie reprise : nom de l'apprenant, indices déjà consultés et réponses.</param>
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
            foreach (var question in _lab.Questions)
                Questions.Add(new LabQuestionItem(question, culture, canShowHint: !_lab.Exam, resumed?.Answers.GetValueOrDefault(question.Id))
                {
                    HintSeen = resumed?.HintsUsed.Contains(question.Id) == true
                });
            _studentName = resumed?.Student ?? string.Empty;
            _studentId = resumed?.StudentId ?? string.Empty;
        }

        public string Title { get; }
        public string Statement { get; }
        public string LevelLabel => Loc[$"Lab.Level.{_lab.Level}"];
        public string DurationLabel => string.Format(Loc["Lab.Duration"], _lab.DurationMinutes);
        public ObservableCollection<LabObjectiveItem> Objectives { get; } = new();
        public ObservableCollection<LabQuestionItem> Questions { get; } = new();
        public bool HasQuestions => Questions.Count > 0;

        public LabDefinition Lab => _lab;

        public Guid SessionId => _session.SessionId;

        /// <summary>Après chaque enregistrement réussi de la copie.</summary>
        public event Action? Saved;

        /// <summary>Réponses actuelles, par question.</summary>
        public IReadOnlyDictionary<string, LabAnswer> Answers() => Questions.ToDictionary(q => q.Id, q => q.Answer);

        /// <summary>Réponses et identité en une ligne : sert à savoir si la copie a des modifications non enregistrées.</summary>
        public string AnswersState() =>
            string.Join("\u001f", Questions.Select(q => $"{q.Id}={q.Answer.Signature()}")) + "\u001f" + StudentName + "\u001f" + StudentId;

        private IEnumerable<string> HintsSeen() =>
            Objectives.Where(o => o.HintSeen).Select(o => o.Objective.Id).Concat(Questions.Where(q => q.HintSeen).Select(q => q.Id));

        /// <summary>La copie dans son état actuel (copie de secours), sans l'écrire dans le fichier de l'apprenant.</summary>
        public LabSubmission Snapshot(ADRootDocument work, DateTime now) =>
            _session.ToSubmission(StudentName, work, HintsSeen(), now, Answers(), StudentId);

        public bool IsExam => _lab.Exam;

        /// <summary>Score, « Recommencer » et compte rendu : tout sauf en examen.</summary>
        public bool ShowGrading => !IsExam;

        /// <summary>« Vérifier » : hors examen, ou en examen si le formateur l'a permis (objectifs atteints, sans score ni explication).</summary>
        public bool CanCheck => !IsExam || _lab.ExamCheck;

        /// <summary>Temps passé, séances reprises comprises ; rafraîchi par la fenêtre.</summary>
        public int ElapsedMinutes => _session.ElapsedMinutes(_clock());

        public bool IsOverTime => ElapsedMinutes > _lab.DurationMinutes;

        public string ElapsedLabel => string.Format(Loc["Lab.Elapsed"], ElapsedMinutes, _lab.DurationMinutes);

        /// <summary>Appelé périodiquement par la fenêtre : temps passé.</summary>
        public void Tick()
        {
            OnPropertyChanged(nameof(ElapsedMinutes));
            OnPropertyChanged(nameof(ElapsedLabel));
            OnPropertyChanged(nameof(IsOverTime));
        }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasStudentName))]
        [NotifyCanExecuteChangedFor(nameof(SaveSubmissionCommand))]
        private string _studentName = string.Empty;

        /// <summary>Identifiant de l'apprenant (numéro, adresse), facultatif : il sert à l'export des notes vers Moodle ou l'ENT.</summary>
        [ObservableProperty]
        private string _studentId = string.Empty;

        public bool HasStudentName => !string.IsNullOrWhiteSpace(StudentName);

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ScoreLabel))]
        private double _points;

        [ObservableProperty]
        private bool _isCompleted;

        [ObservableProperty]
        private bool _hasBeenChecked;

        /// <summary>Dernier enregistrement de la copie (ou erreur), vide avant le premier.</summary>
        [ObservableProperty]
        private string _submissionStatus = string.Empty;

        public int MaxPoints => _lab.MaxPoints;
        public string ScoreLabel => string.Format(Loc["Lab.Score"], SubmissionGrader.Format(Points, Loc.CurrentCulture), MaxPoints);
        public int HintsUsed => HintsSeen().Count();

        [RelayCommand(CanExecute = nameof(CanCheck))]
        private void Check()
        {
            if (_getCurrent()?.Domain is not { } current) return;

            _lastResult = _grader.Grade(_lab, _session.Start.Domain, current, _clock(), Answers());
            foreach (var result in _lastResult.Objectives)
            {
                var item = Objectives.First(o => o.Objective == result.Objective);
                item.Passed = result.Passed;
                // En examen, ni explication (ce serait un indice) ni crédit partiel (ce serait un score)
                item.IsPartial = !IsExam && result.EarnedPoints > 0;
                item.Feedback = IsExam ? string.Empty : result.Feedback;
            }
            // En examen, le sujet n'a pas le corrigé : les questions ne se vérifient pas
            foreach (var result in _lastResult.Questions)
            {
                var item = Questions.First(q => q.Question == result.Question);
                item.Passed = IsExam || result.Fraction is null ? null : result.Passed;
                item.IsPartial = !IsExam && result.EarnedPoints > 0;
                item.Feedback = IsExam ? string.Empty : result.Feedback;
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
                item.IsPartial = false;
                item.Feedback = string.Empty;
                item.IsHintVisible = false;
            }
            foreach (var item in Questions)
            {
                item.Passed = null;
                item.IsPartial = false;
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

            var name = string.IsNullOrWhiteSpace(StudentName) ? Loc["Lab.Export.DefaultStudent"] : StudentName.Trim();
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            var path = await _dialogs.PickSaveFileAsync(Loc["Lab.Export.Title"], $"{_lab.Id}_{name}_{_clock():yyyyMMdd_HHmm}.md", "md",
                new FileFilter(Loc["FileType.Markdown"], "*.md"));
            if (path == null) return;

            await File.WriteAllTextAsync(path, BuildReport(_lastResult), new UTF8Encoding(false));
        }

        /// <summary>
        /// Enregistre la copie : l'exercice reçu, le domaine actuel, les réponses et les indices consultés. Le premier
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
                var submission = _session.ToSubmission(StudentName, work, HintsSeen(), now, Answers(), StudentId);
                await File.WriteAllTextAsync(path, new LabSubmissionService().Serialize(submission), new UTF8Encoding(false));
                _submissionPath = path;
                SubmissionStatus = string.Format(Loc["Lab.Submission.Saved"], path, now.ToString("HH:mm"));
                Saved?.Invoke();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                SubmissionStatus = ex.Message;
            }
        }

        /// <summary>Compte rendu Markdown : identité, score, détail par objectif et par question, indices utilisés.</summary>
        public string BuildReport(LabResult result)
        {
            var culture = Loc.CurrentCulture.Name;
            var numbers = Loc.CurrentCulture;
            var sb = new StringBuilder();
            sb.AppendLine($"# {Title}");
            sb.AppendLine();
            sb.AppendLine($"- **{Loc["Lab.Report.Student"]}** : {(string.IsNullOrWhiteSpace(StudentName) ? "—" : StudentName.Trim())}");
            sb.AppendLine($"- **{Loc["Lab.Report.Lab"]}** : `{_lab.Id}` ({LevelLabel})");
            sb.AppendLine($"- **{Loc["Lab.Report.Date"]}** : {result.GradedAt:yyyy-MM-dd HH:mm}");
            sb.AppendLine($"- **{Loc["Lab.Report.Score"]}** : {SubmissionGrader.Format(result.Points, numbers)} / {result.MaxPoints}");
            sb.AppendLine($"- **{Loc["Lab.Report.Hints"]}** : {HintsUsed}");
            sb.AppendLine($"- **{Loc["Lab.Report.Audit"]}** : {result.Audit.Score} / 100");
            sb.AppendLine();
            sb.AppendLine($"| | {Loc["Lab.Report.Objective"]} | {Loc["Lab.Report.Points"]} | {Loc["Lab.Report.Feedback"]} |");
            sb.AppendLine("|---|---|---|---|");
            foreach (var item in Objectives)
            {
                var r = result.Objectives.First(o => o.Objective == item.Objective);
                var hint = item.HintSeen ? $" ({Loc["Lab.Report.HintUsed"]})" : string.Empty;
                sb.AppendLine($"| {Status.Icon(r.Passed, r.EarnedPoints > 0)} | {Escape(item.Title)}{hint} | {SubmissionGrader.Format(r.EarnedPoints, numbers)} / {item.Points} | {Escape(r.Feedback)} |");
            }
            if (Questions.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"| | {Loc["Lab.Report.Question"]} | {Loc["Lab.Report.Answer"]} | {Loc["Lab.Report.Points"]} | {Loc["Lab.Report.Feedback"]} |");
                sb.AppendLine("|---|---|---|---|---|");
                foreach (var item in Questions)
                {
                    var r = result.Questions.First(q => q.Question == item.Question);
                    var points = r.Fraction is null ? "—" : $"{SubmissionGrader.Format(r.EarnedPoints, numbers)} / {item.Points}";
                    sb.AppendLine($"| {Status.Icon(r.Fraction is null ? null : r.Passed, r.EarnedPoints > 0)} | {Escape(item.Title)} | {Escape(SubmissionGrader.AnswerText(item.Question, r.Answer, culture))} | {points} | {Escape(r.Feedback)} |");
                }
            }
            return sb.ToString();
        }

        private static string Escape(string text) => text.Replace("|", "\\|").Replace("\n", " ");
    }
}
