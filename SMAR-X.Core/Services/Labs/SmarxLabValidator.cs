using System;
using System.Collections.Generic;
using System.Linq;
using SMADX.Models;
using SMADX.Services.Labs;
using SMARX.Models;

namespace SMARX.Services.Labs
{
    /// <summary>
    /// Valider un exercice SMAR-X : il a un identifiant, un titre et des objectifs ; au départ, aucun objectif noté n'est
    /// atteint et toutes les gardes tiennent ; la solution (les serveurs corrigés), si elle est là, atteint tout.
    /// </summary>
    public static class SmarxLabValidator
    {
        public static IReadOnlyList<LabIssue> Check(SmarxLab lab, SmarxDocument? solution, string culture)
        {
            var issues = new List<LabIssue>();
            if (string.IsNullOrWhiteSpace(lab.Id)) issues.Add(new(LabIssueLevel.Error, T(culture, "L'exercice n'a pas d'identifiant.", "The lab has no ID.")));
            foreach (var language in new[] { "fr-FR", "en-US" })
                if (string.IsNullOrWhiteSpace(lab.Title.GetValueOrDefault(language)))
                    issues.Add(new(LabIssueLevel.Warning, T(culture, $"Pas de titre en {language}.", $"No title in {language}.")));
            if (lab.Objectives.Count == 0) issues.Add(new(LabIssueLevel.Error, T(culture, "L'exercice n'a aucun objectif.", "The lab has no objective.")));

            foreach (var r in lab.Objectives.Select(o => SmarxLabGrader.Evaluate(lab, o, lab.Start, culture)))
            {
                if (!r.Objective.Guard && r.Passed)
                    issues.Add(new(LabIssueLevel.Error, T(culture, $"Déjà atteint au départ : {r.Feedback}", $"Already met at the start: {r.Feedback}"), r.Objective.Id));
                if (r.Objective.Guard && !r.Passed)
                    issues.Add(new(LabIssueLevel.Error, T(culture, $"Garde violée dès le départ : {r.Feedback}", $"Guard broken at the start: {r.Feedback}"), r.Objective.Id));
            }

            foreach (var question in lab.Questions.Where(q => !q.HasAnswerKey && q.Kind != LabQuestionKind.Text))
                issues.Add(new(LabIssueLevel.Warning, T(culture, "Sans corrigé : à noter à la main.", "No answer key: to be graded by hand."), question.Id));

            if (solution == null)
                issues.Add(new(LabIssueLevel.Warning, T(culture,
                    "Pas de solution : choisissez les serveurs corrigés (.smar-x.json, enregistrés depuis SMAR-X) pour vérifier que l'exercice se résout.",
                    "No solution: choose the fixed servers (.smar-x.json, saved from SMAR-X) to check that the lab can be solved.")));
            else
            {
                var result = SmarxLabGrader.Grade(lab, solution, null, culture);
                foreach (var r in result.Objectives.Where(o => !o.Passed))
                    issues.Add(new(LabIssueLevel.Error, T(culture, $"La solution ne l'atteint pas : {r.Feedback}", $"The solution does not meet it: {r.Feedback}"), r.Objective.Id));
                if (result.Objectives.All(o => o.Passed))
                    issues.Add(new(LabIssueLevel.Ok, T(culture,
                        $"La solution atteint tous les objectifs ({result.Objectives.Sum(o => o.EarnedPoints):0.##} points).",
                        $"The solution meets every objective ({result.Objectives.Sum(o => o.EarnedPoints):0.##} points).")));
            }
            return issues;
        }

        private static string T(string culture, string fr, string en) =>
            culture.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? en : fr;
    }
}
