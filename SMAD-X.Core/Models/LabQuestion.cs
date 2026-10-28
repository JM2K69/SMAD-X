using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace SMADX.Models
{
    /// <summary>Forme d'une question de compréhension.</summary>
    public enum LabQuestionKind
    {
        /// <summary>Une seule bonne réponse parmi les choix.</summary>
        SingleChoice,
        /// <summary>Plusieurs bonnes réponses ; crédit partiel possible (<see cref="LabQuestion.Partial"/>).</summary>
        MultipleChoice,
        /// <summary>Une affirmation, à laquelle on répond oui ou non : deux choix, dont un seul est juste.</summary>
        TrueFalse,
        /// <summary>
        /// Associer chaque élément (<see cref="LabQuestion.Prompts"/>) à l'une des propositions (<see cref="LabQuestion.Choices"/>),
        /// parmi lesquelles des intrus sont possibles ; crédit partiel possible.
        /// </summary>
        Matching,
        /// <summary>Réponse écrite, reconnue parmi les réponses acceptées ou notée par le formateur.</summary>
        Text
    }

    /// <summary>
    /// Question de compréhension, posée avec les objectifs et notée avec eux. Son corrigé (les bons choix, les
    /// associations, les réponses acceptées) reste dans l'exemplaire du formateur : le sujet distribué est écrit
    /// sans lui (voir <see cref="Services.Labs.LabService.SerializeSubject"/>), et la copie est corrigée dans Studio.
    /// </summary>
    public sealed class LabQuestion
    {
        public string Id { get; set; } = string.Empty;

        /// <summary>La question.</summary>
        public LocalizedText Title { get; set; } = new();
        public LocalizedText Hint { get; set; } = new();
        public LabQuestionKind Kind { get; set; } = LabQuestionKind.SingleChoice;
        public int Points { get; set; } = 1;

        /// <summary>Choix proposés ; pour une association, les propositions à associer (intrus compris).</summary>
        public List<LabChoice> Choices { get; set; } = new();

        /// <summary>Association : les éléments à associer chacun à une proposition.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<LabPrompt>? Prompts { get; set; }

        /// <summary>Réponses écrites acceptées (question à réponse écrite), comparées sans casse, accents ni espaces superflus.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? AcceptedAnswers { get; set; }

        /// <summary>Choix multiple ou association : une réponse incomplète rapporte une part des points. Écrit seulement s'il est activé.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool Partial { get; set; }

        /// <summary>Le corrigé est là : bons choix marqués, associations, ou réponses écrites acceptées.</summary>
        [JsonIgnore]
        public bool HasAnswerKey => Choices.Any(c => c.Correct) || AcceptedAnswers is { Count: > 0 } || (Prompts ?? []).Any(p => p.Match != null);

        /// <summary>La réponse se donne en cochant des choix : choix unique, choix multiple, oui / non.</summary>
        [JsonIgnore]
        public bool IsChoice => Kind is LabQuestionKind.SingleChoice or LabQuestionKind.MultipleChoice or LabQuestionKind.TrueFalse;

        /// <summary>Le crédit partiel s'applique : choix multiple et association.</summary>
        [JsonIgnore]
        public bool CanBePartial => Kind is LabQuestionKind.MultipleChoice or LabQuestionKind.Matching;
    }

    /// <summary>Un choix d'une question, repéré par un identifiant stable (a, b, c…) : les réponses le citent.</summary>
    public sealed class LabChoice
    {
        public string Id { get; set; } = string.Empty;
        public LocalizedText Text { get; set; } = new();

        /// <summary>Bonne réponse : fait partie du corrigé, absent du sujet. Écrit seulement s'il est vrai.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool Correct { get; set; }
    }

    /// <summary>Un élément à associer, repéré par un identifiant stable (p1, p2…).</summary>
    public sealed class LabPrompt
    {
        public string Id { get; set; } = string.Empty;
        public LocalizedText Text { get; set; } = new();

        /// <summary>Identifiant de la proposition qui lui correspond : fait partie du corrigé, absent du sujet.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Match { get; set; }
    }

    /// <summary>Réponse d'un apprenant à une question : les choix cochés, les associations ou le texte écrit.</summary>
    public sealed class LabAnswer
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? Choices { get; set; }

        /// <summary>Association : identifiant de l'élément → identifiant de la proposition choisie.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Dictionary<string, string>? Matches { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Text { get; set; }

        [JsonIgnore]
        public bool IsEmpty => (Choices is null || Choices.Count == 0) && (Matches is null || Matches.Count == 0) && string.IsNullOrWhiteSpace(Text);

        /// <summary>La réponse en une ligne (choix triés, associations, ou texte) : une note du formateur s'applique à cette réponse-là.</summary>
        public string Signature() =>
            Choices is { Count: > 0 } ? string.Join(",", Choices.Order())
            : Matches is { Count: > 0 } ? string.Join(";", Matches.OrderBy(m => m.Key).Select(m => $"{m.Key}={m.Value}"))
            : (Text ?? string.Empty).Trim();
    }
}
