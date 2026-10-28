using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using SMADX.Models;

namespace SMADX.Services.Labs
{
    /// <summary>Résultat d'une question : la part des points obtenue, ou null quand le formateur doit la noter.</summary>
    public sealed record LabQuestionResult(LabQuestion Question, LabAnswer? Answer, double? Fraction, string Feedback)
    {
        /// <summary>À relire par le formateur : pas de corrigé, ou réponse écrite qu'aucune réponse acceptée ne reconnaît.</summary>
        public bool NeedsReview { get; init; }

        /// <summary>Points donnés à la main par le formateur.</summary>
        public bool Manual { get; init; }

        public bool Passed => Fraction >= 1;
        public double EarnedPoints => Math.Round(Question.Points * (Fraction ?? 0), 2);
    }

    /// <summary>
    /// Corrige une question avec son corrigé : choix unique et oui / non (tout ou rien), choix multiple (tout ou rien,
    /// ou crédit partiel : chaque bon choix coché compte, chaque mauvais choix coché décompte), association (tout ou
    /// rien, ou crédit partiel : la part des éléments bien associés), réponse écrite (reconnue parmi les réponses
    /// acceptées). Sans corrigé (le sujet distribué), la réponse attend la note du formateur.
    /// </summary>
    public static class LabQuestionGrader
    {
        private static LocalizationService Loc => LocalizationService.Instance;

        public static LabQuestionResult Grade(LabQuestion question, LabAnswer? answer)
        {
            if (answer == null || answer.IsEmpty)
                return new LabQuestionResult(question, answer, 0, Loc["Lab.Feedback.NoAnswer"]);
            if (!question.HasAnswerKey)
                return new LabQuestionResult(question, answer, null, Loc["Lab.Feedback.AnswerSaved"]) { NeedsReview = true };

            if (question.Kind == LabQuestionKind.Text)
            {
                var given = Normalize(answer.Text);
                return question.AcceptedAnswers!.Any(a => Normalize(a) == given)
                    ? new LabQuestionResult(question, answer, 1, Loc["Lab.Feedback.RightAnswer"])
                    : new LabQuestionResult(question, answer, 0, Loc["Lab.Feedback.TextNotRecognised"]) { NeedsReview = true };
            }

            if (question.Kind == LabQuestionKind.Matching)
            {
                var prompts = question.Prompts ?? [];
                var given = answer.Matches ?? new Dictionary<string, string>();
                var right = prompts.Count(p => p.Match != null && given.TryGetValue(p.Id, out var c) && string.Equals(c, p.Match, StringComparison.OrdinalIgnoreCase));
                if (prompts.Count > 0 && right == prompts.Count)
                    return new LabQuestionResult(question, answer, 1, Loc["Lab.Feedback.RightAnswer"]);
                if (question.Partial && right > 0)
                    return new LabQuestionResult(question, answer, right / (double)prompts.Count, Loc["Lab.Feedback.PartialAnswer"]);
                return new LabQuestionResult(question, answer, 0, Loc["Lab.Feedback.WrongAnswer"]);
            }

            var correct = question.Choices.Where(c => c.Correct).Select(c => c.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var chosen = (answer.Choices ?? []).Where(id => question.Choices.Any(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase)))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (chosen.SetEquals(correct))
                return new LabQuestionResult(question, answer, 1, Loc["Lab.Feedback.RightAnswer"]);
            if (question.Kind == LabQuestionKind.MultipleChoice && question.Partial)
            {
                var fraction = Math.Max(0, (chosen.Count(correct.Contains) - chosen.Count(c => !correct.Contains(c))) / (double)correct.Count);
                if (fraction > 0)
                    return new LabQuestionResult(question, answer, fraction, Loc["Lab.Feedback.PartialAnswer"]);
            }
            return new LabQuestionResult(question, answer, 0, Loc["Lab.Feedback.WrongAnswer"]);
        }

        /// <summary>Points donnés par le formateur : ils remplacent la correction automatique.</summary>
        public static LabQuestionResult Marked(LabQuestionResult result, double points) =>
            result with
            {
                Fraction = result.Question.Points <= 0 ? 0 : Math.Clamp(points / result.Question.Points, 0, 1),
                Feedback = Loc["Lab.Feedback.Marked"],
                Manual = true,
                NeedsReview = false
            };

        /// <summary>Réponse écrite comparée sans casse, sans accents, sans ponctuation finale ni espaces superflus.</summary>
        public static string Normalize(string? text)
        {
            var decomposed = (text ?? string.Empty).Trim().TrimEnd('.', '!', '?', ';').Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            var space = false;
            foreach (var c in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
                if (char.IsWhiteSpace(c)) { space = sb.Length > 0; continue; }
                if (space) sb.Append(' ');
                space = false;
                sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }
    }
}
