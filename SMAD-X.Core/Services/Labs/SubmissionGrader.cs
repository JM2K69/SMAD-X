using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using SMADX.Models;
using static SMADX.Services.Labs.LabAuthoring;

namespace SMADX.Services.Labs
{
    /// <summary>Une copie corrigée : la note, ou pourquoi elle n'a pas pu être corrigée.</summary>
    public sealed record GradedSubmission(string Path, LabSubmission? Submission, LabResult? Result, IReadOnlyList<LabIssue> Issues)
    {
        public string Student => Submission?.Student is { Length: > 0 } s ? s : System.IO.Path.GetFileName(Path);
        public bool Graded => Result != null;
        public int Points => Result?.Points ?? 0;
        public int HintsUsed => Submission?.HintsUsed.Count ?? 0;
        public bool Passed(LabObjective objective) => Result?.Objectives.FirstOrDefault(o => o.Objective == objective)?.Passed ?? false;
    }

    /// <summary>
    /// Correction des copies d'une classe avec l'exemplaire de l'exercice du formateur : le domaine de départ
    /// est repréparé à la date où l'apprenant a commencé et la vérification se fait à la date de sa copie,
    /// comme l'aurait fait SMAD-X. Le score que l'apprenant a pu voir n'est jamais repris.
    /// <para>
    /// Chaque copie est aussi contrôlée : son sceau (modifiée hors de SMAD-X ?), sa session (le domaine
    /// porte-t-il les identifiants de la session de la copie ?) et, sur la classe, les copies qui partagent
    /// une session ou dont le domaine vient de la session d'un autre. Ces contrôles repèrent une copie
    /// recopiée, renommée ou retouchée ; le format étant public, ils n'arrêtent pas un faussaire déterminé.
    /// </para>
    /// </summary>
    public static class SubmissionGrader
    {
        public static GradedSubmission Grade(LabDefinition lab, string path, string culture)
        {
            LabSubmission submission;
            try { submission = new LabSubmissionService().LoadFile(path); }
            catch (Exception ex) when (ex is LabFormatException or IOException or UnauthorizedAccessException)
            {
                return new GradedSubmission(path, null, null, [new LabIssue(LabIssueLevel.Error, ex.Message)]);
            }
            return Grade(lab, submission, path, culture);
        }

        public static GradedSubmission Grade(LabDefinition lab, LabSubmission submission, string path, string culture)
        {
            var issues = new List<LabIssue>();
            void Add(LabIssueLevel level, string fr, string en) => issues.Add(new LabIssue(level, T(culture, fr, en)));

            if (!string.Equals(submission.LabId, lab.Id, StringComparison.OrdinalIgnoreCase))
            {
                Add(LabIssueLevel.Error, $"Copie de l'exercice « {submission.LabId} », pas de « {lab.Id} ».",
                                         $"Submission for lab \"{submission.LabId}\", not \"{lab.Id}\".");
                return new GradedSubmission(path, submission, null, issues);
            }
            if (string.IsNullOrWhiteSpace(submission.Student))
                Add(LabIssueLevel.Warning, "La copie ne porte pas de nom.", "The submission has no name.");
            switch (submission.SealCheck)
            {
                case SubmissionSeal.Broken:
                    Add(LabIssueLevel.Error, "Copie modifiée en dehors de SMAD-X : son sceau ne correspond plus à son contenu.",
                                             "Submission changed outside SMAD-X: its seal no longer matches its content.");
                    break;
                case SubmissionSeal.Missing:
                    Add(LabIssueLevel.Warning, "Copie sans sceau : elle n'a pas été enregistrée par SMAD-X.", "Unsealed submission: it was not saved by SMAD-X.");
                    break;
            }
            if (submission.SessionId == Guid.Empty)
                Add(LabIssueLevel.Warning, "Copie sans session de travail : impossible de vérifier que le domaine est celui de l'apprenant.",
                                           "Submission without a work session: the domain cannot be checked as the learner's own.");
            else if (SessionMatch(submission.Work.Domain, StartIds(submission.Lab), submission.SessionId) < SameSession)
                Add(LabIssueLevel.Error, "Le domaine ne vient pas de la session de cette copie : c'est sans doute le travail d'un autre.",
                                         "The domain does not come from this submission's session: it is probably someone else's work.");
            if (LabSubmissionService.StartFingerprint(submission.Lab) != LabSubmissionService.StartFingerprint(lab))
                Add(LabIssueLevel.Warning, "Faite sur un autre domaine de départ que votre exemplaire de l'exercice : la note peut être fausse.",
                                           "Done on another starting domain than your copy of the lab: the grade may be wrong.");

            var start = new LabService().PrepareStart(lab, submission.StartedOn);
            var result = new LabGrader().Grade(lab, start.Domain, submission.Work.Domain, submission.SavedAt);
            return new GradedSubmission(path, submission, result, issues);
        }

        /// <summary>Part minimale des objets du départ qui portent les identifiants de la session pour dire « même session ».</summary>
        private const double SameSession = 0.5;

        /// <summary>
        /// Identifiants des objets du domaine de départ, par DN. On prend l'exercice reçu par l'apprenant (scellé
        /// avec sa copie) : un exercice régénéré par le formateur ne fait pas passer toutes les copies pour étrangères.
        /// </summary>
        private static Dictionary<string, Guid> StartIds(LabDefinition lab) =>
            Flatten(lab.Start.Domain)
                .GroupBy(o => o.DistinguishedName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Part des objets du domaine, présents au départ (même DN), dont l'identifiant est celui de la session :
        /// 1 pour le domaine de cette session, 0 pour un domaine venu d'une autre session.
        /// </summary>
        public static double SessionMatch(ADObject work, IReadOnlyDictionary<string, Guid> startIds, Guid sessionId)
        {
            var candidates = Flatten(work).Where(o => startIds.ContainsKey(o.DistinguishedName)).ToList();
            if (candidates.Count == 0) return 1;
            return candidates.Count(o => o.Id == LabSession.SessionObjectId(sessionId, startIds[o.DistinguishedName])) / (double)candidates.Count;
        }

        /// <summary>
        /// Corrige toutes les copies et compare les copies entre elles : un apprenant qui a rendu plusieurs copies,
        /// une même session sur plusieurs copies, un domaine qui vient de la session d'un autre apprenant.
        /// </summary>
        public static IReadOnlyList<GradedSubmission> GradeAll(LabDefinition lab, IEnumerable<string> paths, string culture)
        {
            var rows = paths.Distinct(StringComparer.OrdinalIgnoreCase).Select(p => Grade(lab, p, culture)).ToList();
            var extra = rows.ToDictionary(r => r, _ => new List<LabIssue>());
            void Add(GradedSubmission row, LabIssueLevel level, string fr, string en) => extra[row].Add(new LabIssue(level, T(culture, fr, en)));
            var sameLab = rows.Where(r => r.Graded).ToList();

            foreach (var group in sameLab.Where(r => r.Submission is { Student.Length: > 0 })
                         .GroupBy(r => r.Submission!.Student.Trim(), StringComparer.CurrentCultureIgnoreCase).Where(g => g.Count() > 1))
                foreach (var row in group)
                    Add(row, LabIssueLevel.Warning, "Cet apprenant a rendu plusieurs copies.", "This learner handed in several submissions.");

            // Une même session sur plusieurs copies : une copie rendue sous plusieurs noms
            foreach (var group in sameLab.Where(r => r.Submission!.SessionId != Guid.Empty).GroupBy(r => r.Submission!.SessionId).Where(g => g.Count() > 1))
                foreach (var row in group)
                {
                    var others = string.Join(", ", group.Where(o => o != row).Select(o => o.Student));
                    Add(row, LabIssueLevel.Error, $"Même session de travail que la copie de {others} : l'une est la copie de l'autre.",
                                                  $"Same work session as {others}'s submission: one is a copy of the other.");
                }

            // Un domaine venu d'une autre session : de qui ?
            foreach (var row in sameLab.Where(r => r.Submission!.SessionId != Guid.Empty))
            {
                var startIds = StartIds(row.Submission!.Lab);
                if (SessionMatch(row.Submission.Work.Domain, startIds, row.Submission.SessionId) >= SameSession) continue;
                var owners = sameLab.Where(o => o != row && o.Submission!.SessionId != Guid.Empty && o.Submission.SessionId != row.Submission.SessionId
                                                && SessionMatch(row.Submission.Work.Domain, startIds, o.Submission.SessionId) >= SameSession)
                    .Select(o => o.Student).ToList();
                if (owners.Count > 0)
                    Add(row, LabIssueLevel.Error, $"Son domaine vient de la session de {string.Join(", ", owners)}.",
                                                  $"Its domain comes from {string.Join(", ", owners)}'s session.");
            }

            return rows
                .Select(r => extra[r].Count > 0 ? r with { Issues = [.. r.Issues, .. extra[r]] } : r)
                .OrderBy(r => r.Student, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        /// <summary>Copie à revoir : non corrigée, ou avec une erreur (modifiée, recopiée, autre exercice…).</summary>
        public static bool NeedsAttention(GradedSubmission row) => !row.Graded || row.Issues.Any(i => i.Level == LabIssueLevel.Error);

        private static IEnumerable<ADObject> Flatten(ADObject root) => new[] { root }.Concat(root.Children.SelectMany(Flatten));

        public static double? Average(IReadOnlyList<GradedSubmission> rows) =>
            rows.Any(r => r.Graded) ? rows.Where(r => r.Graded).Average(r => r.Points) : null;

        public static double? Median(IReadOnlyList<GradedSubmission> rows)
        {
            var points = rows.Where(r => r.Graded).Select(r => (double)r.Points).Order().ToList();
            if (points.Count == 0) return null;
            var middle = points.Count / 2;
            return points.Count % 2 == 1 ? points[middle] : (points[middle - 1] + points[middle]) / 2;
        }

        /// <summary>Part des copies corrigées qui atteignent l'objectif (0 à 1), null sans copie corrigée.</summary>
        public static double? SuccessRate(LabObjective objective, IReadOnlyList<GradedSubmission> rows)
        {
            var graded = rows.Where(r => r.Graded).ToList();
            return graded.Count == 0 ? null : graded.Count(r => r.Passed(objective)) / (double)graded.Count;
        }

        /// <summary>Notes au format CSV, avec « ; » et la virgule décimale en français pour Excel.</summary>
        public static string Csv(LabDefinition lab, IReadOnlyList<GradedSubmission> rows, string culture)
        {
            var french = !culture.StartsWith("en", StringComparison.OrdinalIgnoreCase);
            var separator = french ? ";" : ",";
            var numbers = CultureInfo.GetCultureInfo(french ? "fr-FR" : "en-US");
            string Cell(string value) =>
                value.Contains(separator) || value.Contains('"') || value.Contains('\n') ? $"\"{value.Replace("\"", "\"\"")}\"" : value;

            var sb = new StringBuilder();
            var header = new List<string>
            {
                T(culture, "Apprenant", "Learner"), T(culture, "Fichier", "File"), T(culture, "Enregistrée le", "Saved at"),
                T(culture, "Points", "Points"), T(culture, "Sur", "Out of"), T(culture, "Pourcentage", "Percentage"), T(culture, "Indices", "Hints")
            };
            header.AddRange(lab.Objectives.Select(o => o.Id));
            header.Add(T(culture, "Remarques", "Remarks"));
            sb.AppendLine(string.Join(separator, header.Select(Cell)));

            foreach (var r in rows)
            {
                var cells = new List<string>
                {
                    r.Student, Path.GetFileName(r.Path), r.Submission?.SavedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? "",
                    r.Graded ? r.Points.ToString(numbers) : "", lab.MaxPoints.ToString(numbers),
                    r.Graded && lab.MaxPoints > 0 ? (100.0 * r.Points / lab.MaxPoints).ToString("0.#", numbers) : "", r.HintsUsed.ToString(numbers)
                };
                cells.AddRange(lab.Objectives.Select(o => !r.Graded ? "" : r.Passed(o) ? o.Points.ToString(numbers) : "0"));
                cells.Add(string.Join(" ", r.Issues.Select(i => i.Message)));
                sb.AppendLine(string.Join(separator, cells.Select(Cell)));
            }
            return sb.ToString().ReplaceLineEndings("\n");
        }

        /// <summary>Bilan Markdown de la classe : résumé, notes de chacun et réussite par objectif.</summary>
        public static string ClassReport(LabDefinition lab, IReadOnlyList<GradedSubmission> rows, string culture)
        {
            var numbers = CultureInfo.GetCultureInfo(culture.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? "en-US" : "fr-FR");
            string N(double value) => value.ToString("0.#", numbers);
            string Pct(double rate) => (100 * rate).ToString("0", numbers) + " %";
            string Cell(string text) => text.Replace("|", "\\|").ReplaceLineEndings(" ");
            var graded = rows.Where(r => r.Graded).ToList();

            var sb = new StringBuilder();
            sb.AppendLine($"# {Cell(lab.Title.Resolve(culture))} — {T(culture, "bilan de la classe", "class results")}").AppendLine();
            sb.AppendLine($"- **{T(culture, "Exercice", "Lab")}** : `{lab.Id}`{(lab.Exam ? T(culture, " (mode examen)", " (exam mode)") : "")}");
            sb.AppendLine($"- **{T(culture, "Copies corrigées", "Graded submissions")}** : {graded.Count} / {rows.Count}");
            if (Average(rows) is { } average && Median(rows) is { } median)
            {
                sb.AppendLine($"- **{T(culture, "Moyenne", "Average")}** : {N(average)} / {lab.MaxPoints}{(lab.MaxPoints > 0 ? $" ({Pct(average / lab.MaxPoints)})" : "")}");
                sb.AppendLine($"- **{T(culture, "Médiane", "Median")}** : {N(median)} / {lab.MaxPoints}");
                sb.AppendLine($"- **{T(culture, "Plus basse, plus haute", "Lowest, highest")}** : {graded.Min(r => r.Points)}, {graded.Max(r => r.Points)}");
            }
            sb.AppendLine();

            sb.AppendLine($"## {T(culture, "Notes", "Grades")}").AppendLine();
            sb.AppendLine($"| {T(culture, "Apprenant", "Learner")} | {T(culture, "Note", "Grade")} | {T(culture, "Indices", "Hints")} | {string.Join(" | ", lab.Objectives.Select(o => o.Id))} | {T(culture, "Remarques", "Remarks")} |");
            sb.AppendLine($"|---|---|---|{string.Concat(lab.Objectives.Select(_ => "---|"))}---|");
            foreach (var r in rows)
            {
                var marks = lab.Objectives.Select(o => !r.Graded ? "—" : r.Passed(o) ? "✔" : "✘");
                sb.AppendLine($"| {Cell(r.Student)} | {(r.Graded ? $"{r.Points} / {lab.MaxPoints}" : "—")} | {r.HintsUsed} | {string.Join(" | ", marks)} | {Cell(string.Join(" ", r.Issues.Select(i => i.Message)))} |");
            }
            sb.AppendLine();

            sb.AppendLine($"## {T(culture, "Réussite par objectif", "Success per objective")}").AppendLine();
            sb.AppendLine($"| {T(culture, "Objectif", "Objective")} | {T(culture, "Points", "Points")} | {T(culture, "Atteint par", "Met by")} |");
            sb.AppendLine("|---|---|---|");
            foreach (var o in lab.Objectives)
            {
                var rate = SuccessRate(o, rows);
                sb.AppendLine($"| {o.Id} · {Cell(o.Title.Resolve(culture))} | {o.Points} | {(rate is { } value ? $"{graded.Count(r => r.Passed(o))} / {graded.Count} ({Pct(value)})" : "—")} |");
            }
            return sb.ToString().ReplaceLineEndings("\n");
        }
    }
}
