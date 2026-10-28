using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SMADX.Models;
using SMADX.Services.AttackPaths;
using static SMADX.Services.Labs.LabAuthoring;

namespace SMADX.Services.Labs
{
    public enum LabIssueLevel
    {
        Ok,
        Warning,
        Error
    }

    public sealed record LabIssue(LabIssueLevel Level, string Message, string? ObjectiveId = null);

    public sealed record LabValidationReport(IReadOnlyList<LabIssue> Issues, LabResult? Solution)
    {
        public bool HasErrors => Issues.Any(i => i.Level == LabIssueLevel.Error);
        public int Warnings => Issues.Count(i => i.Level == LabIssueLevel.Warning);
    }

    /// <summary>
    /// Vérifie un exercice avant de le distribuer, comme le font les tests des exercices fournis :
    /// objectifs non atteints au départ, gardes atteintes et efficaces, objets cités qui existent,
    /// textes présents, aucune solution dans l'énoncé ni dans le domaine, questions qui ont leur corrigé,
    /// et solution du formateur qui obtient tous les points des objectifs.
    /// </summary>
    public static class LabValidator
    {
        private static readonly string[] SolutionHeadings = ["## Le corriger", "## Fix it", "## Le chemin", "## The path"];

        /// <param name="solution">Domaine corrigé par le formateur (facultatif) : il doit obtenir tous les points.</param>
        public static LabValidationReport Validate(LabDefinition lab, string culture, DateTime now, ADObject? solution = null)
        {
            var issues = new List<LabIssue>();
            void Add(LabIssueLevel level, string fr, string en, string? id = null) => issues.Add(new LabIssue(level, T(culture, fr, en), id));

            if (lab.Start?.Domain == null)
            {
                Add(LabIssueLevel.Error, "L'exercice n'a pas de domaine de départ.", "The lab has no starting domain.");
                return new LabValidationReport(issues, null);
            }

            // ─── Textes ──────────────────────────────────────────────────────────
            var languages = lab.Language is { } single ? new[] { single } : Cultures;
            foreach (var lang in languages)
            {
                if (!lab.Title.ContainsKey(lang) || string.IsNullOrWhiteSpace(lab.Title[lang]))
                    Add(LabIssueLevel.Error, $"Titre manquant en {lang}.", $"Title missing in {lang}.");
                if (!lab.Statement.ContainsKey(lang) || string.IsNullOrWhiteSpace(lab.Statement[lang]))
                    Add(LabIssueLevel.Error, $"Énoncé manquant en {lang}.", $"Statement missing in {lang}.");
                foreach (var o in lab.Objectives)
                {
                    if (!o.Title.ContainsKey(lang) || string.IsNullOrWhiteSpace(o.Title[lang]))
                        Add(LabIssueLevel.Error, $"Titre de l'objectif manquant en {lang}.", $"Objective title missing in {lang}.", o.Id);
                    // Un examen est écrit sans ses indices : il n'en montre pas
                    if (!lab.Exam && !o.Guard && (!o.Hint.ContainsKey(lang) || string.IsNullOrWhiteSpace(o.Hint[lang])))
                        Add(LabIssueLevel.Warning, $"Pas d'indice en {lang}.", $"No hint in {lang}.", o.Id);
                }
            }
            if (string.IsNullOrWhiteSpace(lab.Id) || lab.Id.Any(char.IsWhiteSpace))
                Add(LabIssueLevel.Error, "L'identifiant de l'exercice est vide ou contient des espaces.", "The lab identifier is empty or contains spaces.");
            if (lab.Objectives.Count == 0)
                Add(LabIssueLevel.Error, "L'exercice n'a aucun objectif.", "The lab has no objective.");
            foreach (var dup in lab.Objectives.GroupBy(o => o.Id).Where(g => g.Count() > 1))
                Add(LabIssueLevel.Error, $"Identifiant d'objectif en double : {dup.Key}.", $"Duplicate objective identifier: {dup.Key}.");
            foreach (var o in lab.Objectives.Where(o => o.Points <= 0 && !o.Guard))
                Add(LabIssueLevel.Warning, "Objectif sans point.", "Objective worth no points.", o.Id);
            foreach (var o in lab.Objectives.Where(o => o.Guard && !CanBeGuard(o.Check)))
                Add(LabIssueLevel.Warning, "Cette vérification ne protège aucun objet : elle ne peut pas servir de garde.",
                    "This check protects no object: it cannot be a guard.", o.Id);
            foreach (var o in lab.Objectives.Where(o => o.Partial && (o.Guard || !CanBePartial(o.Check))))
                Add(LabIssueLevel.Warning, "Cette vérification ne se compte pas : le crédit partiel n'y change rien (constats d'une règle sur tout le domaine, gravité, score ou chemins seulement).",
                    "This check cannot be counted: partial credit changes nothing (a rule's findings over the whole domain, severity, score or paths only).", o.Id);

            // ─── Questions ───────────────────────────────────────────────────────
            if (lab.Questions.Select(q => q.Id).Concat(lab.Objectives.Select(o => o.Id)).GroupBy(id => id, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1) is { } dupQuestion)
                Add(LabIssueLevel.Error, $"Identifiant en double : {dupQuestion.Key}.", $"Duplicate identifier: {dupQuestion.Key}.");
            foreach (var q in lab.Questions)
            {
                foreach (var lang in languages)
                {
                    if (!q.Title.ContainsKey(lang) || string.IsNullOrWhiteSpace(q.Title[lang]))
                        Add(LabIssueLevel.Error, $"Texte de la question manquant en {lang}.", $"Question text missing in {lang}.", q.Id);
                    if (q.Kind != LabQuestionKind.Text && q.Choices.Any(c => !c.Text.ContainsKey(lang) || string.IsNullOrWhiteSpace(c.Text[lang])))
                        Add(LabIssueLevel.Error, $"Choix sans texte en {lang}.", $"Choice without text in {lang}.", q.Id);
                    if ((q.Prompts ?? []).Any(p => !p.Text.ContainsKey(lang) || string.IsNullOrWhiteSpace(p.Text[lang])))
                        Add(LabIssueLevel.Error, $"Élément à associer sans texte en {lang}.", $"Item to match without text in {lang}.", q.Id);
                }
                if (q.Points <= 0)
                    Add(LabIssueLevel.Warning, "Question sans point.", "Question worth no points.", q.Id);
                var correct = q.Choices.Count(c => c.Correct);
                var prompts = q.Prompts ?? [];
                switch (q.Kind)
                {
                    case LabQuestionKind.SingleChoice or LabQuestionKind.MultipleChoice when q.Choices.Count < 2:
                        Add(LabIssueLevel.Error, "Une question à choix demande au moins deux choix.", "A choice question needs at least two choices.", q.Id);
                        break;
                    case LabQuestionKind.SingleChoice or LabQuestionKind.TrueFalse when correct != 1:
                        Add(LabIssueLevel.Error, "Cochez une et une seule bonne réponse.", "Tick exactly one right answer.", q.Id);
                        break;
                    case LabQuestionKind.TrueFalse when q.Choices.Count != 2:
                        Add(LabIssueLevel.Error, "Oui / non : exactement deux choix.", "Yes / no: exactly two choices.", q.Id);
                        break;
                    case LabQuestionKind.Matching when prompts.Count < 2:
                        Add(LabIssueLevel.Error, "Une association demande au moins deux éléments à associer.", "A matching question needs at least two items to match.", q.Id);
                        break;
                    case LabQuestionKind.Matching when prompts.Any(p => q.Choices.All(c => !string.Equals(c.Id, p.Match, StringComparison.OrdinalIgnoreCase))):
                        Add(LabIssueLevel.Error, "Chaque élément doit être associé à l'une des propositions.", "Each item must be matched with one of the proposals.", q.Id);
                        break;
                    case LabQuestionKind.MultipleChoice when correct == 0:
                        Add(LabIssueLevel.Error, "Choix multiple : cochez au moins une bonne réponse.", "Multiple choice: tick at least one right answer.", q.Id);
                        break;
                    case LabQuestionKind.Text when q.AcceptedAnswers is not { Count: > 0 }:
                        Add(LabIssueLevel.Warning, "Aucune réponse acceptée : chaque réponse sera notée à la main dans l'onglet Copies.",
                            "No accepted answer: every answer will be graded by hand in the Submissions tab.", q.Id);
                        break;
                }
                if (q.Choices.GroupBy(c => c.Id, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1)
                    || prompts.GroupBy(p => p.Id, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
                    Add(LabIssueLevel.Error, "Deux choix ou deux éléments ont le même identifiant.", "Two choices or two items have the same identifier.", q.Id);
            }
            if (!lab.Exam && lab.Questions.Any(q => q.HasAnswerKey))
                Add(LabIssueLevel.Warning, "Hors examen, le fichier de l'exercice contient le corrigé des questions : l'apprenant peut le lire. Pour une évaluation, passez en mode examen et distribuez le sujet.",
                    "Outside exam mode, the lab file contains the answer key: the learner can read it. For an assessment, switch to exam mode and hand out the subject.");

            // ─── Pas de solution visible par l'apprenant ─────────────────────────
            foreach (var (lang, text) in lab.Statement)
                foreach (var heading in SolutionHeadings.Where(text.Contains))
                    Add(LabIssueLevel.Error, $"L'énoncé ({lang}) contient la section « {heading[3..]} » : il donne la réponse.", $"The statement ({lang}) contains the \"{heading[3..]}\" section: it gives the answer away.");
            foreach (var obj in AllObjects(lab.Start.Domain).Where(o => SolutionHeadings.Any(h => (o.Description ?? "").Contains(h))))
                Add(LabIssueLevel.Error, $"La description de « {obj.Name} » contient la solution : l'apprenant la verra dans le domaine.", $"The description of \"{obj.Name}\" contains the solution: the learner will see it in the domain.");

            var service = new LabService();
            var start = service.PrepareStart(lab, now).Domain;
            var paths = new AttackPathService(start, now).PathsOutsideTier0();
            foreach (var path in paths)
            {
                var steps = path.Steps.Select(s => s.To.Label).Where(l => l != start.Name).Distinct().ToList();
                foreach (var (lang, text) in lab.Statement)
                {
                    var cited = steps.Count(text.Contains);
                    if (steps.Count >= 2 && cited >= 2)
                        Add(LabIssueLevel.Warning, $"L'énoncé ({lang}) cite {cited} étapes du chemin depuis « {path.Start.Label} » : il risque de donner la réponse.",
                            $"The statement ({lang}) names {cited} steps of the path from \"{path.Start.Label}\": it may give the answer away.");
                }
            }

            // ─── Objets cités ────────────────────────────────────────────────────
            var names = AllObjects(start).Select(o => o.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var o in lab.Objectives)
            {
                var c = o.Check;
                foreach (var name in new[] { c.Object, c.Group, c.Trustee, c.Target }.OfType<string>().Where(n => n != AttackPathService.AnyUserId))
                    if (!names.Contains(name) && !(c.Type == LabCheckType.ObjectAbsent && name == c.Object))
                        Add(LabIssueLevel.Error, $"« {name} » n'existe pas dans le domaine de départ.", $"\"{name}\" does not exist in the starting domain.", o.Id);
                if (c.Type == LabCheckType.RuleClear && Audit.AuditRules.All.All(r => r.Id != c.RuleId))
                    Add(LabIssueLevel.Error, $"Règle d'audit inconnue : {c.RuleId}.", $"Unknown audit rule: {c.RuleId}.", o.Id);
            }

            // ─── Au départ : objectifs non atteints, gardes atteintes ───────────
            var grader = new LabGrader();
            var atStart = grader.Grade(lab, start, service.PrepareStart(lab, now).Domain, now);
            foreach (var r in atStart.Objectives)
            {
                var guard = r.Objective.Guard;
                if (guard && !r.Passed)
                    Add(LabIssueLevel.Error, $"Garde non atteinte au départ : {r.Feedback}", $"Guard not met at start: {r.Feedback}", r.Objective.Id);
                else if (!guard && r.Passed)
                    Add(LabIssueLevel.Error,
                        CanBeGuard(r.Objective.Check) ? "Objectif déjà atteint au départ : il ne demande rien à l'apprenant. Si c'est une garde, cochez « Garde »."
                                                      : "Objectif déjà atteint au départ : il ne demande rien à l'apprenant.",
                        CanBeGuard(r.Objective.Check) ? "Objective already met at start: it asks nothing of the learner. If it is a guard, tick \"Guard\"."
                                                      : "Objective already met at start: it asks nothing of the learner.", r.Objective.Id);
            }
            if (atStart.ObjectivesCompleted && lab.Objectives.Count > 0)
                Add(LabIssueLevel.Error, "L'exercice est résolu dès le départ.", "The lab is solved from the start.");

            // ─── Chaque garde refuse bien son raccourci, et le raccourci ne paie pas ──
            foreach (var o in lab.Objectives.Where(o => o.Guard && o.Check.Type is LabCheckType.ObjectExists or LabCheckType.MemberOf or LabCheckType.DelegationExists))
            {
                var working = service.PrepareStart(lab, now).Domain;
                if (!Violate(working, o.Check)) continue;
                var shortcut = grader.Grade(lab, start, working, now);
                if (shortcut.Objectives.Single(x => x.Objective == o).Passed)
                    Add(LabIssueLevel.Warning, "Garde inefficace : elle reste atteinte même quand on supprime ce qu'elle protège.", "Ineffective guard: it stays met even when what it protects is removed.", o.Id);
                else if (lab.ObjectivePoints > 0 && shortcut.Points * 2 > lab.ObjectivePoints)
                    Add(LabIssueLevel.Warning, $"Le raccourci que refuse cette garde rapporte encore {shortcut.Points:0.##} / {lab.ObjectivePoints} points : les objectifs qu'il atteint ne concernent pas l'objet protégé.",
                        $"The shortcut this guard refuses still earns {shortcut.Points:0.##} / {lab.ObjectivePoints} points: the objectives it meets do not concern the protected object.", o.Id);
            }

            // ─── Solution du formateur ───────────────────────────────────────────
            LabResult? solutionResult = null;
            if (solution != null)
            {
                // Le domaine corrigé ne répond pas aux questions : il obtient les points des objectifs
                solutionResult = grader.Grade(lab, start, solution, now);
                if (solutionResult.ObjectivesCompleted)
                    Add(LabIssueLevel.Ok, $"La solution obtient tous les points des objectifs ({solutionResult.Points:0.##} / {lab.ObjectivePoints}).", $"The solution gets all the objectives' points ({solutionResult.Points:0.##} / {lab.ObjectivePoints}).");
                else
                    foreach (var r in solutionResult.Objectives.Where(r => !r.Passed))
                        Add(LabIssueLevel.Error, $"La solution échoue : {r.Feedback}", $"The solution fails: {r.Feedback}", r.Objective.Id);
            }

            if (!issues.Any(i => i.Level != LabIssueLevel.Ok))
                Add(LabIssueLevel.Ok, "Aucun problème détecté.", "No problem found.");
            return new LabValidationReport(issues, solutionResult);
        }

        /// <summary>Réglages d'examen en une ligne : vérification permise ou non, outils fermés.</summary>
        public static string ExamSummary(LabDefinition lab, string culture)
        {
            var check = lab.ExamCheck ? T(culture, "Vérifier permis (sans score)", "Check allowed (no score)") : T(culture, "sans Vérifier", "no Check");
            var closed = Enum.GetValues<LabTools>().Where(t => t != LabTools.None && lab.ExamClosedTools.HasFlag(t))
                .Select(t => LocalizationService.Instance.Translate($"Lab.Tool.{t}", culture)).ToList();
            return closed.Count == 0
                ? $"{check}, {T(culture, "tous les outils ouverts", "all tools open")}"
                : $"{check}, {T(culture, "fermés", "closed")} : {string.Join(", ", closed)}";
        }

        /// <summary>Applique le raccourci qu'une garde doit refuser ; faux si l'objet visé est introuvable.</summary>
        private static bool Violate(ADObject domain, LabCheck check)
        {
            var name = check.Type == LabCheckType.DelegationExists ? check.Target : check.Object;
            var target = AllObjects(domain).FirstOrDefault(o => string.Equals(o.Name, name, StringComparison.OrdinalIgnoreCase));
            if (target == null) return false;
            switch (check.Type)
            {
                case LabCheckType.ObjectExists:
                    target.Parent?.Children.Remove(target);
                    return target.Parent != null;
                case LabCheckType.MemberOf:
                    return target.MemberOf.Remove(check.Group!);
                case LabCheckType.DelegationExists:
                    foreach (var d in target.Delegations.Where(d => string.Equals(d.TrusteeName, check.Trustee, StringComparison.OrdinalIgnoreCase)).ToList())
                        target.Delegations.Remove(d);
                    return true;
                default:
                    return false;
            }
        }

        // ─── Fiche formateur ─────────────────────────────────────────────────────

        /// <summary>
        /// Document réservé au formateur : objectifs, vérifications, chemins au départ, ce que change le domaine
        /// corrigé (s'il est connu, préparé à la même date que le départ) et notes (la solution).
        /// </summary>
        public static string TeacherSheet(LabDefinition lab, string culture, string? notes, DateTime now, ADObject? solution = null)
        {
            var loc = LocalizationService.Instance;
            var sb = new StringBuilder();
            sb.AppendLine($"# {lab.Title.Resolve(culture)}").AppendLine();
            sb.AppendLine(T(culture, "> **Document réservé au formateur** : il contient la solution. Ne le distribuez pas avec l'exercice.",
                "> **Trainer-only document**: it contains the solution. Do not hand it out with the lab.")).AppendLine();
            sb.AppendLine(T(culture, "| | |", "| | |")).AppendLine("|---|---|");
            sb.AppendLine($"| {T(culture, "Fichier", "File")} | `{lab.Id}{LabService.FileExtension}` |");
            sb.AppendLine($"| {T(culture, "Niveau", "Level")} | {loc.Translate($"Lab.Level.{lab.Level}", culture)} |");
            sb.AppendLine($"| {T(culture, "Durée", "Duration")} | {lab.DurationMinutes} min |");
            if (lab.Exam)
                sb.AppendLine($"| {T(culture, "Examen", "Exam")} | {ExamSummary(lab, culture)} |");
            sb.AppendLine($"| {T(culture, "Domaine", "Domain")} | `{lab.Start.Domain.Name}` |");
            sb.AppendLine($"| {T(culture, "Points", "Points")} | {lab.MaxPoints}{(lab.Questions.Count > 0 ? T(culture, $" (dont {lab.MaxPoints - lab.ObjectivePoints} pour les questions)", $" ({lab.MaxPoints - lab.ObjectivePoints} for the questions)") : "")} |").AppendLine();

            sb.AppendLine(T(culture, "## Objectifs", "## Objectives")).AppendLine();
            sb.AppendLine(T(culture, "| Id | Points | Objectif | Vérification | Garde |", "| Id | Points | Objective | Check | Guard |")).AppendLine("|---|---|---|---|---|");
            foreach (var o in lab.Objectives)
                sb.AppendLine($"| {o.Id} | {o.Points} | {o.Title.Resolve(culture)} | {Describe(o.Check, culture)}{(o.Partial ? T(culture, " (crédit partiel)", " (partial credit)") : "")} | {(o.Guard ? "✓" : "")} |");
            sb.AppendLine();

            if (lab.Questions.Count > 0)
            {
                sb.AppendLine(T(culture, "## Questions et corrigé", "## Questions and answer key")).AppendLine();
                foreach (var q in lab.Questions)
                {
                    var kind = LocalizationService.Instance.Translate($"Lab.QuestionKind.{q.Kind}", culture);
                    sb.AppendLine($"**{q.Id} · {q.Title.Resolve(culture)}** ({q.Points} pt, {kind}{(q.Partial ? T(culture, ", crédit partiel", ", partial credit") : "")})").AppendLine();
                    if (q.Kind == LabQuestionKind.Matching)
                    {
                        foreach (var p in q.Prompts ?? [])
                            sb.AppendLine($"- {p.Text.Resolve(culture)} → {q.Choices.FirstOrDefault(c => c.Id == p.Match)?.Text.Resolve(culture) ?? "—"}");
                        foreach (var c in q.Choices.Where(c => (q.Prompts ?? []).All(p => p.Match != c.Id)))
                            sb.AppendLine($"- {T(culture, "Intrus", "Distractor")} : {c.Text.Resolve(culture)}");
                    }
                    else
                        foreach (var c in q.Choices)
                            sb.AppendLine($"- {(c.Correct ? "✔" : "✘")} {c.Text.Resolve(culture)}");
                    if (q.Kind == LabQuestionKind.Text)
                        sb.AppendLine(q.AcceptedAnswers is { Count: > 0 } accepted
                            ? T(culture, "- Réponses acceptées : ", "- Accepted answers: ") + string.Join(" ; ", accepted)
                            : T(culture, "- Notée à la main.", "- Graded by hand."));
                    sb.AppendLine();
                }
            }

            var start = new LabService().PrepareStart(lab, now).Domain;
            var paths = new AttackPathService(start, now).PathsOutsideTier0();
            sb.AppendLine(T(culture, "## Chemins d'attaque au départ", "## Attack paths at start")).AppendLine();
            if (paths.Count == 0)
                sb.AppendLine(T(culture, "Aucun chemin hors Tier 0.", "No path outside Tier 0.")).AppendLine();
            foreach (var p in paths)
            {
                var label = p.Start.IsAnyUser ? T(culture, "N'importe quel utilisateur du domaine", "Any domain user") : p.Start.Label;
                sb.AppendLine($"**{label}** : " + string.Join(" → ", p.Steps.Select(s => $"{s.To.Label} ({loc.Translate($"AttackPath.Kind.{s.Kind}", culture)})"))).AppendLine();
            }

            var audit = new Audit.ADSecurityAuditService().Run(start, now);
            sb.AppendLine(T(culture, "## Audit au départ", "## Audit at start")).AppendLine();
            sb.AppendLine(T(culture, $"Score {audit.Score} / 100, niveau ANSSI estimé {audit.EstimatedAnssiLevel}.", $"Score {audit.Score} / 100, estimated ANSSI level {audit.EstimatedAnssiLevel}.")).AppendLine();
            foreach (var rule in audit.FailedRules)
                sb.AppendLine($"- **{rule.Id}** : {string.Join(", ", audit.Findings.Where(f => f.Rule == rule).Select(f => f.Object.Name).Distinct())}");
            sb.AppendLine();

            if (solution != null)
            {
                sb.AppendLine(T(culture, "## Domaine corrigé", "## Fixed domain")).AppendLine();
                var changes = ADDiffService.Compare(new DomainSnapshot { Root = start }, new DomainSnapshot { Root = solution });
                if (changes.Count == 0)
                    sb.AppendLine(T(culture, "Identique au domaine de départ.", "Same as the starting domain.")).AppendLine();
                foreach (var change in changes)
                {
                    var what = change.ChangeType switch
                    {
                        ChangeType.Added => T(culture, "ajouté", "added"),
                        ChangeType.Removed => T(culture, "supprimé", "removed"),
                        _ => change.ChangedFieldsSummary
                    };
                    sb.AppendLine($"- **{change.ObjectName}** : {what}");
                }
                var solved = new LabGrader().Grade(lab, start, solution, now);
                var met = solved.Objectives.Count(o => o.Passed);
                sb.AppendLine().AppendLine(T(culture, $"Objectifs atteints : {met} / {solved.Objectives.Count}.", $"Objectives met: {met} / {solved.Objectives.Count}.")).AppendLine();
            }

            sb.AppendLine(T(culture, "## Solution et notes", "## Solution and notes")).AppendLine();
            sb.AppendLine(string.IsNullOrWhiteSpace(notes) ? T(culture, "*Aucune note.*", "*No notes.*") : notes.Trim()).AppendLine();
            return sb.ToString().ReplaceLineEndings("\n");
        }
    }
}
