using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SMADX.Models;
using SMADX.Services;

namespace SMADX.Studio.ViewModels
{
    /// <summary>Un choix en cours d'édition : son texte dans la langue éditée, bonne réponse ou non.</summary>
    public sealed partial class ChoiceEditItem : ObservableObject
    {
        private readonly QuestionItem _question;
        private readonly Func<string> _culture;

        public ChoiceEditItem(QuestionItem question, LabChoice model, Func<string> culture)
        {
            _question = question;
            Model = model;
            _culture = culture;
        }

        public LabChoice Model { get; }

        public string Text
        {
            get => Model.Text.GetValueOrDefault(_culture()) ?? "";
            set { Model.Text[_culture()] = value; OnPropertyChanged(); }
        }

        /// <summary>Bonne réponse ; pour un choix unique ou un oui / non, cocher l'un décoche les autres.</summary>
        public bool Correct
        {
            get => Model.Correct;
            set
            {
                if (Model.Correct == value) return;
                Model.Correct = value;
                OnPropertyChanged();
                _question.OnCorrectChanged(this);
            }
        }

        public void Refresh() => OnPropertyChanged(nameof(Text));
    }

    /// <summary>Une paire d'association en cours d'édition : l'élément et la proposition qui lui correspond.</summary>
    public sealed partial class PairEditItem : ObservableObject
    {
        private readonly Func<string> _culture;

        public PairEditItem(LabPrompt prompt, LabChoice choice, Func<string> culture)
        {
            Prompt = prompt;
            Choice = choice;
            _culture = culture;
        }

        public LabPrompt Prompt { get; }
        public LabChoice Choice { get; }

        public string Left
        {
            get => Prompt.Text.GetValueOrDefault(_culture()) ?? "";
            set { Prompt.Text[_culture()] = value; OnPropertyChanged(); }
        }

        public string Right
        {
            get => Choice.Text.GetValueOrDefault(_culture()) ?? "";
            set { Choice.Text[_culture()] = value; OnPropertyChanged(); }
        }

        public void Refresh()
        {
            OnPropertyChanged(nameof(Left));
            OnPropertyChanged(nameof(Right));
        }
    }

    /// <summary>
    /// Une question en cours d'édition : son texte dans la langue éditée, sa forme et son corrigé. Une association
    /// s'édite par paires (l'élément et sa proposition), plus des intrus ; dans le fichier, les propositions sont des
    /// choix et chaque élément cite le sien.
    /// </summary>
    public sealed partial class QuestionItem : ObservableObject
    {
        private readonly Func<string> _culture;

        public QuestionItem(LabQuestion model, Func<string> culture)
        {
            Model = model;
            _culture = culture;
            Rebuild();
        }

        public LabQuestion Model { get; }
        public string Id => Model.Id;

        public ObservableCollection<ChoiceEditItem> Choices { get; } = new();
        public ObservableCollection<PairEditItem> Pairs { get; } = new();

        public string Title
        {
            get => Model.Title.GetValueOrDefault(_culture()) ?? "";
            set { Model.Title[_culture()] = value; OnPropertyChanged(); OnPropertyChanged(nameof(Summary)); }
        }

        public string Hint
        {
            get => Model.Hint.GetValueOrDefault(_culture()) ?? "";
            set { Model.Hint[_culture()] = value; OnPropertyChanged(); }
        }

        public int Points
        {
            get => Model.Points;
            set { Model.Points = Math.Max(0, value); OnPropertyChanged(); OnPropertyChanged(nameof(Summary)); }
        }

        public bool Partial
        {
            get => Model.Partial;
            set { Model.Partial = value && Model.CanBePartial; OnPropertyChanged(); }
        }

        /// <summary>Réponses écrites acceptées, une par ligne.</summary>
        public string AcceptedAnswers
        {
            get => string.Join("\n", Model.AcceptedAnswers ?? []);
            set
            {
                var answers = value.Split('\n').Select(a => a.Trim()).Where(a => a.Length > 0).ToList();
                Model.AcceptedAnswers = answers.Count > 0 ? answers : null;
                OnPropertyChanged();
            }
        }

        /// <summary>Changer de forme remet le corrigé en ordre : oui / non a ses deux choix, une association ses paires, une réponse écrite aucun choix.</summary>
        public LabQuestionKind Kind
        {
            get => Model.Kind;
            set
            {
                if (Model.Kind == value) return;
                Model.Kind = value;
                switch (value)
                {
                    case LabQuestionKind.TrueFalse:
                        Model.Choices =
                        [
                            new LabChoice { Id = "yes", Text = LocalizedText.Of("Oui", "Yes"), Correct = true },
                            new LabChoice { Id = "no", Text = LocalizedText.Of("Non", "No") }
                        ];
                        Model.Prompts = null;
                        Model.AcceptedAnswers = null;
                        break;
                    case LabQuestionKind.Matching:
                        Model.Prompts ??= [];
                        // Les choix déjà écrits deviennent des intrus ; les choix vides ne servent à rien
                        Model.Choices.RemoveAll(c => c.Text.Values.All(string.IsNullOrWhiteSpace));
                        foreach (var c in Model.Choices) c.Correct = false;
                        Model.AcceptedAnswers = null;
                        break;
                    case LabQuestionKind.Text:
                        Model.Choices.Clear();
                        Model.Prompts = null;
                        break;
                    default:
                        Model.Prompts = null;
                        Model.AcceptedAnswers = null;
                        // Choix unique : une seule bonne réponse reste cochée
                        if (value == LabQuestionKind.SingleChoice)
                            foreach (var c in Model.Choices.Where(c => c.Correct).Skip(1)) c.Correct = false;
                        break;
                }
                if (!Model.CanBePartial) Model.Partial = false;
                Rebuild();
                foreach (var name in new[] { nameof(Kind), nameof(KindChoice), nameof(Partial), nameof(AcceptedAnswers), nameof(IsChoice), nameof(IsTrueFalse), nameof(IsMatching),
                                             nameof(IsText), nameof(CanEditChoices), nameof(ShowChoices), nameof(CanBePartial), nameof(Summary) })
                    OnPropertyChanged(name);
            }
        }

        /// <summary>Formes proposées, avec leur nom dans la langue de l'interface.</summary>
        public static IReadOnlyList<Choice<LabQuestionKind>> KindChoices() =>
            Enum.GetValues<LabQuestionKind>().Select(k => new Choice<LabQuestionKind>(k, Capitalize(LocalizationService.Instance.Translate($"Lab.QuestionKind.{k}", StudioText.Instance.Culture)))).ToList();

        public IReadOnlyList<Choice<LabQuestionKind>> Kinds => KindChoices();

        /// <summary>La forme, comme élément de la liste (les éléments se comparent par valeur).</summary>
        public Choice<LabQuestionKind> KindChoice
        {
            get => KindChoices().First(k => k.Value == Kind);
            set { if (value != null) Kind = value.Value; OnPropertyChanged(); }
        }

        private static string Capitalize(string text) => text.Length == 0 ? text : char.ToUpper(text[0]) + text[1..];

        public bool IsChoice => Model.IsChoice;
        public bool IsTrueFalse => Model.Kind == LabQuestionKind.TrueFalse;
        public bool IsMatching => Model.Kind == LabQuestionKind.Matching;
        public bool IsText => Model.Kind == LabQuestionKind.Text;
        public bool CanBePartial => Model.CanBePartial;

        /// <summary>Choix à cocher, ou intrus d'une association.</summary>
        public bool ShowChoices => IsChoice || IsMatching;

        /// <summary>Oui / non a ses deux choix : on ne les ajoute ni ne les retire.</summary>
        public bool CanEditChoices => ShowChoices && !IsTrueFalse;

        public string Summary => $"{Model.Id} · {Points} pt · {LocalizationService.Instance.Translate($"Lab.QuestionKind.{Model.Kind}", _culture())} · {Title}";

        [RelayCommand]
        private void AddChoice()
        {
            var choice = new LabChoice { Id = NextChoiceId(), Text = new LocalizedText { [_culture()] = "" } };
            Model.Choices.Add(choice);
            Choices.Add(new ChoiceEditItem(this, choice, _culture));
        }

        [RelayCommand]
        private void RemoveChoice(ChoiceEditItem? item)
        {
            if (item == null || !CanEditChoices) return;
            Model.Choices.Remove(item.Model);
            Choices.Remove(item);
        }

        [RelayCommand]
        private void AddPair()
        {
            var choice = new LabChoice { Id = NextChoiceId(), Text = new LocalizedText { [_culture()] = "" } };
            var prompt = new LabPrompt { Id = NextPromptId(), Text = new LocalizedText { [_culture()] = "" }, Match = choice.Id };
            Model.Choices.Add(choice);
            (Model.Prompts ??= []).Add(prompt);
            Pairs.Add(new PairEditItem(prompt, choice, _culture));
        }

        [RelayCommand]
        private void RemovePair(PairEditItem? item)
        {
            if (item == null) return;
            Model.Prompts?.Remove(item.Prompt);
            Model.Choices.Remove(item.Choice);
            Pairs.Remove(item);
        }

        internal void OnCorrectChanged(ChoiceEditItem choice)
        {
            if (!choice.Correct || Model.Kind is not (LabQuestionKind.SingleChoice or LabQuestionKind.TrueFalse)) return;
            foreach (var other in Choices.Where(c => c != choice && c.Correct))
                other.Correct = false;
        }

        /// <summary>Après un changement de langue d'édition ou de numéro.</summary>
        public void Refresh()
        {
            foreach (var name in new[] { nameof(Title), nameof(Hint), nameof(Summary), nameof(Id), nameof(KindChoice) })
                OnPropertyChanged(name);
            foreach (var c in Choices) c.Refresh();
            foreach (var p in Pairs) p.Refresh();
        }

        /// <summary>Paires (association) et choix affichés (choix à cocher, ou intrus d'une association).</summary>
        private void Rebuild()
        {
            Choices.Clear();
            Pairs.Clear();
            var matched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (Model.Kind == LabQuestionKind.Matching)
                foreach (var prompt in Model.Prompts ?? [])
                    if (Model.Choices.FirstOrDefault(c => string.Equals(c.Id, prompt.Match, StringComparison.OrdinalIgnoreCase)) is { } choice)
                    {
                        matched.Add(choice.Id);
                        Pairs.Add(new PairEditItem(prompt, choice, _culture));
                    }
            foreach (var choice in Model.Choices.Where(c => !matched.Contains(c.Id)))
                Choices.Add(new ChoiceEditItem(this, choice, _culture));
        }

        /// <summary>a, b… z, puis c27, c28… : un identifiant libre.</summary>
        private string NextChoiceId()
        {
            var used = Model.Choices.Select(c => c.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            for (var c = 'a'; c <= 'z'; c++)
                if (!used.Contains(c.ToString())) return c.ToString();
            var n = 27;
            while (used.Contains($"c{n}")) n++;
            return $"c{n}";
        }

        private string NextPromptId()
        {
            var used = (Model.Prompts ?? []).Select(p => p.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var n = 1;
            while (used.Contains($"p{n}")) n++;
            return $"p{n}";
        }
    }
}
