using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SMADX.Models;
using SMADX.ViewModels;
using SMARX.Models;
using SMARX.Services.Labs;

namespace SMARX.ViewModels
{
    /// <summary>Un objectif de l'exercice dans la fenêtre : texte, indice, statut après vérification.</summary>
    public sealed partial class SmarxObjectiveItem : ObservableObject
    {
        public SmarxObjectiveItem(SmarxObjective objective, string culture, bool canShowHint)
        {
            Objective = objective;
            Title = objective.Title.Resolve(culture);
            Hint = objective.Hint.Resolve(culture);
            CanShowHint = canShowHint && !string.IsNullOrWhiteSpace(Hint);
        }

        public SmarxObjective Objective { get; }
        public string Id => Objective.Id;
        public string Title { get; }
        public string Hint { get; }
        public int Points => Objective.Points;
        public bool IsGuard => Objective.Guard;
        public bool IsScored => !Objective.Guard;
        public bool CanShowHint { get; }

        [ObservableProperty] private bool _isHintVisible;
        public bool HintSeen { get; set; }
        partial void OnIsHintVisibleChanged(bool value) { if (value) HintSeen = true; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(StatusIcon), nameof(StatusColor))]
        private bool? _passed;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(StatusIcon), nameof(StatusColor))]
        private bool _isPartial;

        [ObservableProperty] private string _feedback = string.Empty;

        public string StatusIcon => Status.Icon(Passed, IsPartial);
        public string StatusColor => Status.Color(Passed, IsPartial);

        [RelayCommand]
        private void ToggleHint() { if (CanShowHint) IsHintVisible = !IsHintVisible; }
    }

    /// <summary>
    /// Fenêtre « Exercice » de SMAR-X : énoncé, objectifs, questions, « Vérifier » (le correcteur sur les serveurs de
    /// l'apprenant), indices, « Recommencer » et copie scellée à rendre au formateur. En examen : ni indice, ni
    /// score, ni « Recommencer » ; « Vérifier », si le formateur l'a permis, dit seulement ce qui est atteint.
    /// </summary>
    public sealed partial class SmarxLabViewModel : ObservableObject
    {
        private static SmarxText Text => SmarxText.Instance;
        private readonly Func<SmarxDocument> _work;
        private readonly Action _restart;

        public SmarxLabViewModel(SmarxLab lab, Func<SmarxDocument> work, Action restart)
        {
            Lab = lab;
            _work = work;
            _restart = restart;
            Build(null);
        }

        public SmarxLab Lab { get; }
        public Guid SessionId { get; } = Guid.NewGuid();
        public DateTime StartedOn { get; private set; } = DateTime.Now;

        public bool IsExam => Lab.Exam;
        /// <summary>Score, explications et « Recommencer » : hors examen.</summary>
        public bool ShowGrading => !Lab.Exam;
        public bool CanCheck => !Lab.Exam || Lab.ExamCheck;

        public string Title => Lab.Title.Resolve(Text.Culture);
        public string Statement => Lab.Statement.Resolve(Text.Culture);
        public string LevelLabel => Lab.Level switch
        {
            LabLevel.Beginner => Text.T("Débutant", "Beginner"),
            LabLevel.Intermediate => Text.T("Intermédiaire", "Intermediate"),
            _ => Text.T("Avancé", "Advanced")
        };
        public string DurationLabel => Text.T($"Durée indicative : {Lab.DurationMinutes} min", $"Suggested time: {Lab.DurationMinutes} min");
        public string ExamNotice => Text.T("Mode examen : pas d'indice ni de score. Enregistrez votre copie, puis rendez le fichier à votre formateur.",
                                           "Exam mode: no hints and no score. Save your copy, then hand the file to your trainer.");

        public IReadOnlyList<SmarxObjectiveItem> Objectives { get; private set; } = [];
        public IReadOnlyList<LabQuestionItem> Questions { get; private set; } = [];
        public bool HasQuestions => Questions.Count > 0;

        [ObservableProperty] private string _scoreLabel = string.Empty;
        [ObservableProperty] private bool _isCompleted;
        [ObservableProperty] private string _studentName = string.Empty;
        [ObservableProperty] private string _studentId = string.Empty;
        [ObservableProperty] private string _submissionStatus = string.Empty;

        public bool HasStudentName => !string.IsNullOrWhiteSpace(StudentName);
        partial void OnStudentNameChanged(string value) => OnPropertyChanged(nameof(HasStudentName));

        /// <summary>Les réponses telles qu'elles s'enregistrent dans la copie.</summary>
        public Dictionary<string, LabAnswer> Answers => Questions.ToDictionary(q => q.Id, q => q.Answer);

        public List<string> HintsUsed =>
            Objectives.Where(o => o.HintSeen).Select(o => o.Id).Concat(Questions.Where(q => q.HintSeen).Select(q => q.Id)).ToList();

        /// <summary>Construit les objectifs et les questions dans la langue courante, en gardant les réponses données.</summary>
        private void Build(IReadOnlyDictionary<string, LabAnswer>? answers)
        {
            var culture = Text.Culture;
            var seen = Objectives.Where(o => o.HintSeen).Select(o => o.Id).ToHashSet();
            Objectives = Lab.Objectives.Select(o => new SmarxObjectiveItem(o, culture, !Lab.Exam) { HintSeen = seen.Contains(o.Id) }).ToList();
            Questions = Lab.Questions.Select(q => new LabQuestionItem(q, culture, !Lab.Exam, answers?.GetValueOrDefault(q.Id))).ToList();
            ScoreLabel = Text.T($"0 / {Lab.MaxPoints} points", $"0 / {Lab.MaxPoints} points");
            OnPropertyChanged(nameof(Objectives));
            OnPropertyChanged(nameof(Questions));
            OnPropertyChanged(nameof(HasQuestions));
        }

        /// <summary>La langue a changé : les textes de l'exercice suivent ; les réponses restent, la vérification est à refaire.</summary>
        public void Relabel()
        {
            Build(Answers);
            IsCompleted = false;
            foreach (var name in new[] { nameof(Title), nameof(Statement), nameof(LevelLabel), nameof(DurationLabel), nameof(ExamNotice) })
                OnPropertyChanged(name);
        }

        [RelayCommand]
        private void Check()
        {
            if (!CanCheck) return;
            var result = SmarxLabGrader.Grade(Lab, _work(), Answers, Text.Culture);
            foreach (var item in Objectives)
            {
                var r = result.Objectives.Single(o => o.Objective == item.Objective);
                item.Passed = r.Passed;
                item.IsPartial = !r.Passed && r.Fraction > 0;
                // En examen, « Vérifier » dit seulement ce qui est atteint
                item.Feedback = Lab.Exam ? string.Empty : r.Feedback;
            }
            foreach (var item in Questions)
            {
                var r = result.Questions.Single(q => q.Question == item.Question);
                item.Passed = r.Fraction == null ? null : r.Passed;
                item.IsPartial = r.Fraction is > 0 and < 1;
                item.Feedback = Lab.Exam ? string.Empty : r.Feedback;
            }
            ScoreLabel = Text.T($"{result.Score:0.##} / {result.MaxPoints} points", $"{result.Score:0.##} / {result.MaxPoints} points");
            IsCompleted = !Lab.Exam && result.Completed && result.Questions.All(q => q.Passed);
        }

        [RelayCommand]
        private void Restart()
        {
            if (Lab.Exam) return;
            _restart();
            StartedOn = DateTime.Now;
            Build(null);
            IsCompleted = false;
        }

        /// <summary>La copie à enregistrer : l'exercice reçu, les serveurs de l'apprenant, ses réponses, ses indices.</summary>
        public SmarxSubmission Submission()
        {
            var now = DateTime.Now;
            return new SmarxSubmission
            {
                Student = StudentName.Trim(),
                StudentId = string.IsNullOrWhiteSpace(StudentId) ? null : StudentId.Trim(),
                LabId = Lab.Id,
                SessionId = SessionId,
                StartedOn = StartedOn,
                SavedAt = now,
                ElapsedMinutes = (int)Math.Round((now - StartedOn).TotalMinutes),
                HintsUsed = HintsUsed,
                Answers = Answers,
                Lab = Lab,
                Work = SmarxLabGrader.Clone(_work())
            };
        }

        public string SuggestedFileName => SmarxLabService.SubmissionFileName(Lab.Id, StudentName);

        public void Saved(string fileName) =>
            SubmissionStatus = Text.T($"Copie enregistrée : {fileName}", $"Copy saved: {fileName}");
    }
}
