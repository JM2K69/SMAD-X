using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SMADX.Models;

namespace SMADX.ViewModels
{
    // Questions de compréhension dans la fenêtre d'exercice, communes à SMAD-X et SMAR-X : la réponse de
    // l'apprenant, son statut après correction, l'indice.

    /// <summary>Pictogrammes et couleurs d'un objectif ou d'une question corrigés.</summary>
    public static class Status
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
}
