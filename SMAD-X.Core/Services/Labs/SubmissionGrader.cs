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
        /// <summary>Notes du formateur sur cette copie (points donnés à la main, appréciation), s'il y en a.</summary>
        public SubmissionMarks? Marks { get; init; }

        /// <summary>Nom affiché : celui de la copie, ou son fichier ; sur une ligne, sans caractère de contrôle.</summary>
        public string Student => SafeText.Name(Submission?.Student is { Length: > 0 } s && SafeText.Name(s).Length > 0 ? s : System.IO.Path.GetFileName(Path));
        public string StudentId => SafeText.Name(Submission?.StudentId ?? string.Empty);
        public bool Graded => Result != null;
        public double Points => Result?.Points ?? 0;
        public int HintsUsed => Submission?.HintsUsed.Count ?? 0;
        public int? ElapsedMinutes => Submission?.ElapsedMinutes;
        public bool Passed(LabObjective objective) => Result?.Objectives.FirstOrDefault(o => o.Objective == objective)?.Passed ?? false;
        public bool Passed(LabQuestion question) => Result?.Questions.FirstOrDefault(q => q.Question == question)?.Passed ?? false;
        public double Earned(LabObjective objective) => Result?.Objectives.FirstOrDefault(o => o.Objective == objective)?.EarnedPoints ?? 0;
        public LabQuestionResult? Question(LabQuestion question) => Result?.Questions.FirstOrDefault(q => q.Question == question);

        /// <summary>Questions que le formateur doit encore noter : la note n'est pas définitive.</summary>
        public int ToReview => Result?.Questions.Count(q => q.NeedsReview) ?? 0;

        /// <summary>Note ramenée à un barème (sur 20…), arrondie au centième.</summary>
        public double Scaled(int scale) => Result is { MaxPoints: > 0 } r ? Math.Round(scale * r.Points / r.MaxPoints, 2) : 0;
    }

    /// <summary>
    /// Correction des copies d'une classe avec l'exemplaire de l'exercice du formateur : le domaine de départ
    /// est repréparé à la date où l'apprenant a commencé et la vérification se fait à la date de sa copie,
    /// comme l'aurait fait SMAD-X. Le score que l'apprenant a pu voir n'est jamais repris. Les questions sont
    /// corrigées avec le corrigé de l'exemplaire du formateur, puis avec les points qu'il a donnés à la main
    /// (<see cref="SubmissionMarks"/>).
    /// <para>
    /// Chaque copie est aussi contrôlée : son sceau (modifiée hors de SMAD-X ?), sa session (le domaine
    /// porte-t-il les identifiants de la session de la copie ?) et, sur la classe, les copies qui partagent
    /// une session ou dont le domaine vient de la session d'un autre. Ces contrôles repèrent une copie
    /// recopiée, renommée ou retouchée ; le format étant public, ils n'arrêtent pas un faussaire déterminé.
    /// </para>
    /// </summary>
    public static class SubmissionGrader
    {
        /// <summary>Barème par défaut des notes exportées.</summary>
        public const int DefaultScale = 20;

        public static GradedSubmission Grade(LabDefinition lab, string path, string culture)
        {
            LabSubmission submission;
            try { submission = new LabSubmissionService().LoadFile(path); }
            catch (Exception ex) when (ex is LabFormatException or IOException or UnauthorizedAccessException)
            {
                return new GradedSubmission(path, null, null, [new LabIssue(LabIssueLevel.Error, ex.Message)]);
            }
            return Grade(lab, submission, path, culture, SubmissionMarksService.Load(path, submission));
        }

        public static GradedSubmission Grade(LabDefinition lab, LabSubmission submission, string path, string culture, SubmissionMarks? marks = null)
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
            // L'exercice reçu (dans la copie) remplacé par un autre domaine de départ échapperait au contrôle de session
            var replaced = !Flatten(submission.Lab.Start.Domain).Select(o => o.DistinguishedName)
                .Intersect(Flatten(lab.Start.Domain).Select(o => o.DistinguishedName), StringComparer.OrdinalIgnoreCase).Any();
            if (replaced)
                Add(LabIssueLevel.Error, "Le domaine de départ de la copie n'a rien de commun avec votre exercice : l'exercice reçu a été remplacé.",
                                         "The submission's starting domain has nothing in common with your lab: the lab received was replaced.");
            if (submission.SessionId == Guid.Empty)
                Add(LabIssueLevel.Warning, "Copie sans session de travail : impossible de vérifier que le domaine est celui de l'apprenant.",
                                           "Submission without a work session: the domain cannot be checked as the learner's own.");
            else if (SessionMatch(submission.Work.Domain, StartIds(submission.Lab), submission.SessionId) is not { } match)
                Add(LabIssueLevel.Error, "Contrôle de session impossible : aucun objet du domaine ne vient du départ de l'exercice.",
                                         "Session check impossible: no object of the domain comes from the lab's start.");
            else if (match < SameSession)
                Add(LabIssueLevel.Error, "Le domaine ne vient pas de la session de cette copie : c'est sans doute le travail d'un autre.",
                                         "The domain does not come from this submission's session: it is probably someone else's work.");
            if (!replaced && LabSubmissionService.StartFingerprint(submission.Lab) != LabSubmissionService.StartFingerprint(lab))
                Add(LabIssueLevel.Warning, "Faite sur un autre domaine de départ que votre exemplaire de l'exercice : la note peut être fausse.",
                                           "Done on another starting domain than your copy of the lab: the grade may be wrong.");

            // Examen : l'exercice reçu ne doit pas avoir été rouvert hors mode examen, ni être votre exemplaire
            if (lab.Exam)
            {
                if (!submission.Lab.Exam)
                    Add(LabIssueLevel.Error, "Copie d'examen faite hors mode examen : l'exercice reçu a été modifié pour retrouver vérification et « Recommencer ».",
                                             "Exam submission made outside exam mode: the lab received was changed to get Check and Restart back.");
                else if (submission.Lab.ExamCheck != lab.ExamCheck || submission.Lab.ExamClosedTools != lab.ExamClosedTools)
                    Add(LabIssueLevel.Error, "Les réglages d'examen de l'exercice reçu ne sont pas les vôtres : il a été modifié.",
                                             "The exam settings of the lab received are not yours: it was changed.");
                if (submission.Lab.IsTrainerCopy)
                    Add(LabIssueLevel.Error, "Copie faite sur un exercice qui contenait les indices ou les réponses : l'apprenant a reçu votre exemplaire au lieu du sujet.",
                                             "Submission made on a lab that contained the hints or the answers: the learner received your copy instead of the subject.");
                if (submission.HintsUsed.Count > 0)
                    Add(LabIssueLevel.Error, $"Indices consultés pendant l'examen : {string.Join(", ", submission.HintsUsed)}.",
                                             $"Hints viewed during the exam: {string.Join(", ", submission.HintsUsed)}.");
                if (submission.ElapsedMinutes is { } elapsed && elapsed > lab.DurationMinutes)
                    Add(LabIssueLevel.Warning, $"Temps dépassé : {elapsed} min pour {lab.DurationMinutes} prévues.",
                                               $"Time exceeded: {elapsed} min for {lab.DurationMinutes} planned.");
            }
            foreach (var q in lab.Questions.Where(q => !q.HasAnswerKey && q.Kind != LabQuestionKind.Text))
                Add(LabIssueLevel.Warning, $"La question {q.Id} n'a pas de corrigé dans votre exercice : ouvrez votre exemplaire, pas le sujet.",
                                           $"Question {q.Id} has no answer key in your lab: open your copy, not the subject.");

            // La note dépend des règles d'audit de la version qui corrige
            if (submission.RulesVersion == null)
                Add(LabIssueLevel.Warning, $"Copie faite avec une version de SMAD-X antérieure à la 0.9, corrigée avec la {EngineInfo.Version} : la note peut différer.",
                                           $"Submission made with a SMAD-X version older than 0.9, graded with {EngineInfo.Version}: the grade may differ.");
            else if (submission.RulesVersion != EngineInfo.RulesVersion)
                Add(LabIssueLevel.Warning, $"Copie faite avec SMAD-X {submission.EngineVersion}, dont les règles d'audit diffèrent de celles de la {EngineInfo.Version} qui la corrige : la note peut différer.",
                                           $"Submission made with SMAD-X {submission.EngineVersion}, whose audit rules differ from those of {EngineInfo.Version} grading it: the grade may differ.");

            try
            {
                var start = new LabService().PrepareStart(lab, submission.StartedOn);
                var result = new LabGrader().Grade(lab, start.Domain, submission.Work.Domain, submission.SavedAt, submission.Answers);
                if (marks != null)
                    result = result with
                    {
                        Questions = result.Questions.Select(q =>
                        {
                            if (!marks.Questions.TryGetValue(q.Question.Id, out var mark)) return q;
                            if (mark.Answer == (q.Answer?.Signature() ?? string.Empty)) return LabQuestionGrader.Marked(q, mark.Points);
                            Add(LabIssueLevel.Warning, $"La réponse à {q.Question.Id} a changé depuis votre note : à noter de nouveau.",
                                                       $"The answer to {q.Question.Id} changed since you graded it: grade it again.");
                            return q;
                        }).ToList()
                    };
                return new GradedSubmission(path, submission, result, issues) { Marks = marks };
            }
            catch (Exception ex)
            {
                // Une copie qui fait échouer la correction ne bloque pas le reste de la classe
                Add(LabIssueLevel.Error, $"Copie impossible à corriger : {ex.Message}", $"Submission could not be graded: {ex.Message}");
                return new GradedSubmission(path, submission, null, issues) { Marks = marks };
            }
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
        /// 1 pour le domaine de cette session, 0 pour un domaine venu d'une autre session ; null si aucun objet
        /// ne vient du départ, et que le contrôle est donc impossible.
        /// </summary>
        public static double? SessionMatch(ADObject work, IReadOnlyDictionary<string, Guid> startIds, Guid sessionId)
        {
            var candidates = Flatten(work).Where(o => startIds.ContainsKey(o.DistinguishedName)).ToList();
            if (candidates.Count == 0) return null;
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
                if (SessionMatch(row.Submission.Work.Domain, startIds, row.Submission.SessionId) is not { } own || own >= SameSession) continue;
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
            var points = rows.Where(r => r.Graded).Select(r => r.Points).Order().ToList();
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

        /// <summary>Part des copies corrigées qui répondent juste à la question (0 à 1), null sans copie corrigée.</summary>
        public static double? SuccessRate(LabQuestion question, IReadOnlyList<GradedSubmission> rows)
        {
            var graded = rows.Where(r => r.Graded).ToList();
            return graded.Count == 0 ? null : graded.Count(r => r.Passed(question)) / (double)graded.Count;
        }

        /// <summary>Nombre de points, au centième : « 7,5 », « 12 ».</summary>
        public static string Format(double points, CultureInfo numbers) => points.ToString("0.##", numbers);

        private static CultureInfo Numbers(string culture) => CultureInfo.GetCultureInfo(culture.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? "en-US" : "fr-FR");

        /// <summary>Points d'une question pour un tableau : les points, « à noter » si le formateur doit la relire.</summary>
        private static string QuestionCell(GradedSubmission row, LabQuestion question, CultureInfo numbers, string culture) =>
            !row.Graded || row.Question(question) is not { } q ? ""
            : q.NeedsReview ? T(culture, "à noter", "to grade")
            : Format(q.EarnedPoints, numbers);

        /// <summary>
        /// Notes au format CSV, avec « ; » et la virgule décimale en français pour Excel : points, note sur le barème
        /// (à copier dans l'ENT), points de chaque objectif et de chaque question.
        /// </summary>
        public static string Csv(LabDefinition lab, IReadOnlyList<GradedSubmission> rows, string culture, int scale = DefaultScale)
        {
            var french = !culture.StartsWith("en", StringComparison.OrdinalIgnoreCase);
            var separator = french ? ";" : ",";
            var numbers = Numbers(culture);
            string Cell(string value) => SafeText.CsvCell(value, separator);

            var sb = new StringBuilder();
            var header = new List<string>
            {
                T(culture, "Apprenant", "Learner"), T(culture, "Identifiant", "ID"), T(culture, "Fichier", "File"), T(culture, "Enregistrée le", "Saved at"),
                T(culture, "Points", "Points"), T(culture, "Sur", "Out of"), T(culture, $"Note sur {scale}", $"Grade out of {scale}"),
                T(culture, "Pourcentage", "Percentage"), T(culture, "Indices", "Hints"), T(culture, "Temps (min)", "Time (min)"),
                T(culture, "À noter", "To grade")
            };
            header.AddRange(lab.Objectives.Select(o => o.Id));
            header.AddRange(lab.Questions.Select(q => q.Id));
            header.Add(T(culture, "Remarques", "Remarks"));
            sb.AppendLine(string.Join(separator, header.Select(Cell)));

            foreach (var r in rows)
            {
                var cells = new List<string>
                {
                    r.Student, r.StudentId, SafeText.Name(Path.GetFileName(r.Path)), r.Submission?.SavedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? "",
                    r.Graded ? Format(r.Points, numbers) : "", lab.MaxPoints.ToString(numbers), r.Graded ? Format(r.Scaled(scale), numbers) : "",
                    r.Graded && lab.MaxPoints > 0 ? (100.0 * r.Points / lab.MaxPoints).ToString("0.#", numbers) : "", r.HintsUsed.ToString(numbers),
                    r.Submission?.ElapsedMinutes?.ToString(numbers) ?? "", r.Graded ? r.ToReview.ToString(numbers) : ""
                };
                cells.AddRange(lab.Objectives.Select(o => !r.Graded ? "" : Format(r.Earned(o), numbers)));
                cells.AddRange(lab.Questions.Select(q => QuestionCell(r, q, numbers, culture)));
                cells.Add(string.Join(" ", r.Issues.Select(i => i.Message)));
                sb.AppendLine(string.Join(separator, cells.Select(Cell)));
            }
            return sb.ToString().ReplaceLineEndings("\n");
        }

        /// <summary>
        /// Notes pour l'import de Moodle (Notes > Importer > Fichier CSV) : virgule, point décimal, une ligne par copie
        /// corrigée, l'identifiant de l'apprenant (à associer à son adresse ou à son numéro d'identification) et la
        /// note sur le barème de l'élément d'évaluation.
        /// </summary>
        public static string MoodleCsv(LabDefinition lab, IReadOnlyList<GradedSubmission> rows, string culture, int scale = DefaultScale)
        {
            string Cell(string value) => SafeText.CsvCell(value, ",");
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", new[] { T(culture, "Identifiant", "ID"), T(culture, "Apprenant", "Learner"), lab.Id }.Select(Cell)));
            foreach (var r in rows.Where(r => r.Graded))
                sb.AppendLine(string.Join(",", new[] { r.StudentId, r.Student, Format(r.Scaled(scale), CultureInfo.InvariantCulture) }.Select(Cell)));
            return sb.ToString().ReplaceLineEndings("\n");
        }

        /// <summary>Bilan Markdown de la classe : résumé, notes de chacun et réussite par objectif et par question.</summary>
        public static string ClassReport(LabDefinition lab, IReadOnlyList<GradedSubmission> rows, string culture)
        {
            var numbers = Numbers(culture);
            string N(double value) => value.ToString("0.#", numbers);
            string Pct(double rate) => (100 * rate).ToString("0", numbers) + " %";
            // Le nom vient de la copie : ni lien, ni image, ni balise dans le bilan
            string Cell(string text) => SafeText.Markdown(text);
            var graded = rows.Where(r => r.Graded).ToList();
            var items = lab.Objectives.Select(o => o.Id).Concat(lab.Questions.Select(q => q.Id)).ToList();

            var sb = new StringBuilder();
            sb.AppendLine($"# {Cell(lab.Title.Resolve(culture))} — {T(culture, "bilan de la classe", "class results")}").AppendLine();
            sb.AppendLine($"- **{T(culture, "Exercice", "Lab")}** : `{lab.Id}`{(lab.Exam ? T(culture, " (mode examen)", " (exam mode)") : "")}");
            sb.AppendLine($"- **{T(culture, "Corrigé avec", "Graded with")}** : SMAD-X {EngineInfo.Version}");
            sb.AppendLine($"- **{T(culture, "Copies corrigées", "Graded submissions")}** : {graded.Count} / {rows.Count}");
            if (Average(rows) is { } average && Median(rows) is { } median)
            {
                sb.AppendLine($"- **{T(culture, "Moyenne", "Average")}** : {N(average)} / {lab.MaxPoints}{(lab.MaxPoints > 0 ? $" ({Pct(average / lab.MaxPoints)})" : "")}");
                sb.AppendLine($"- **{T(culture, "Médiane", "Median")}** : {N(median)} / {lab.MaxPoints}");
                sb.AppendLine($"- **{T(culture, "Plus basse, plus haute", "Lowest, highest")}** : {Format(graded.Min(r => r.Points), numbers)}, {Format(graded.Max(r => r.Points), numbers)}");
            }
            if (graded.Sum(r => r.ToReview) is > 0 and var toReview)
                sb.AppendLine($"- **{T(culture, "Réponses à noter", "Answers to grade")}** : {toReview}");
            sb.AppendLine();

            sb.AppendLine($"## {T(culture, "Notes", "Grades")}").AppendLine();
            sb.AppendLine($"| {T(culture, "Apprenant", "Learner")} | {T(culture, "Note", "Grade")} | {T(culture, "Indices", "Hints")} | {T(culture, "Temps", "Time")} | {string.Join(" | ", items)} | {T(culture, "Remarques", "Remarks")} |");
            sb.AppendLine($"|---|---|---|---|{string.Concat(items.Select(_ => "---|"))}---|");
            foreach (var r in rows)
            {
                var marks = lab.Objectives.Select(o => !r.Graded ? "—" : r.Passed(o) ? "✔" : r.Earned(o) > 0 ? "◐" : "✘")
                    .Concat(lab.Questions.Select(q => r.Question(q) is not { } a ? "—" : a.NeedsReview ? "?" : a.Passed ? "✔" : a.EarnedPoints > 0 ? "◐" : "✘"));
                var time = r.ElapsedMinutes is { } minutes ? $"{minutes} min" : "—";
                sb.AppendLine($"| {Cell(r.Student)} | {(r.Graded ? $"{Format(r.Points, numbers)} / {lab.MaxPoints}" : "—")} | {r.HintsUsed} | {time} | {string.Join(" | ", marks)} | {Cell(string.Join(" ", r.Issues.Select(i => i.Message)))} |");
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
            foreach (var q in lab.Questions)
            {
                var rate = SuccessRate(q, rows);
                sb.AppendLine($"| {q.Id} · {Cell(q.Title.Resolve(culture))} | {q.Points} | {(rate is { } value ? $"{graded.Count(r => r.Passed(q))} / {graded.Count} ({Pct(value)})" : "—")} |");
            }
            return sb.ToString().ReplaceLineEndings("\n");
        }

        /// <summary>
        /// Compte rendu individuel, à remettre à l'apprenant : sa note, ce qu'il a atteint et pourquoi, ses réponses et
        /// l'appréciation du formateur. Sans le corrigé des questions ni les contrôles de la copie (réservés au formateur).
        /// </summary>
        public static string StudentReport(LabDefinition lab, GradedSubmission row, string culture, int scale = DefaultScale)
        {
            var numbers = Numbers(culture);
            string Cell(string text) => SafeText.Markdown(text).Replace("\n", " ");
            var sb = new StringBuilder();
            sb.AppendLine($"# {Cell(lab.Title.Resolve(culture))}").AppendLine();
            sb.AppendLine($"- **{T(culture, "Apprenant", "Learner")}** : {Cell(row.Student)}{(row.StudentId.Length > 0 ? $" ({Cell(row.StudentId)})" : "")}");
            if (row.Submission is { } s)
                sb.AppendLine($"- **{T(culture, "Copie enregistrée le", "Submission saved on")}** : {s.SavedAt.ToString("g", numbers)}{(s.ElapsedMinutes is { } m ? T(culture, $", après {m} min", $", after {m} min") : "")}");
            if (row.Result is not { } result)
            {
                sb.AppendLine().AppendLine(T(culture, "Cette copie n'a pas pu être corrigée\u00A0: voyez-le avec votre formateur.", "This submission could not be graded: see your trainer.")).AppendLine();
                return sb.ToString().ReplaceLineEndings("\n");
            }
            sb.AppendLine($"- **{T(culture, "Note", "Grade")}** : {Format(result.Points, numbers)} / {result.MaxPoints}{(result.MaxPoints > 0 ? T(culture, $", soit {Format(row.Scaled(scale), numbers)} / {scale}", $", that is {Format(row.Scaled(scale), numbers)} / {scale}") : "")}");
            if (row.ToReview > 0)
                sb.AppendLine($"- **{T(culture, "Note provisoire", "Provisional grade")}** : {T(culture, $"{row.ToReview} réponse(s) reste(nt) à noter par le formateur.", $"{row.ToReview} answer(s) still to be graded by the trainer.")}");
            if (row.HintsUsed > 0 && !lab.Exam)
                sb.AppendLine($"- **{T(culture, "Indices consultés", "Hints viewed")}** : {row.HintsUsed}");
            sb.AppendLine();

            sb.AppendLine($"## {T(culture, "Objectifs", "Objectives")}").AppendLine();
            sb.AppendLine($"| | {T(culture, "Objectif", "Objective")} | {T(culture, "Points", "Points")} | {T(culture, "Commentaire", "Comment")} |");
            sb.AppendLine("|---|---|---|---|");
            foreach (var o in result.Objectives.Where(o => !o.Objective.Guard || !o.Passed))
            {
                var mark = o.Passed ? "✔" : o.EarnedPoints > 0 ? "◐" : "✘";
                var points = o.Objective.Guard ? T(culture, "garde", "guard") : $"{Format(o.EarnedPoints, numbers)} / {o.Objective.Points}";
                sb.AppendLine($"| {mark} | {Cell(o.Objective.Title.Resolve(culture))} | {points} | {Cell(o.Feedback)} |");
            }
            sb.AppendLine();

            // Un tableau, comme les objectifs : le PDF prendrait une ligne « mot : mot » pour une ligne de tableau
            if (result.Questions.Count > 0)
            {
                sb.AppendLine($"## {T(culture, "Questions", "Questions")}").AppendLine();
                sb.AppendLine($"| | {T(culture, "Question", "Question")} | {T(culture, "Votre réponse", "Your answer")} | {T(culture, "Points", "Points")} |");
                sb.AppendLine("|---|---|---|---|");
                foreach (var q in result.Questions)
                {
                    var mark = q.NeedsReview ? "?" : q.Passed ? "✔" : q.EarnedPoints > 0 ? "◐" : "✘";
                    var points = q.NeedsReview ? T(culture, "à noter", "to grade") : $"{Format(q.EarnedPoints, numbers)} / {q.Question.Points}";
                    sb.AppendLine($"| {mark} | {Cell(q.Question.Title.Resolve(culture))} | {Cell(AnswerText(q.Question, q.Answer, culture))} | {points} |");
                }
                sb.AppendLine();
            }

            if (!string.IsNullOrWhiteSpace(row.Marks?.Comment))
            {
                sb.AppendLine($"## {T(culture, "Appréciation du formateur", "Trainer's comment")}").AppendLine();
                sb.AppendLine(Prose(SafeText.Markdown(row.Marks!.Comment!.Trim()))).AppendLine();
            }
            return sb.ToString().ReplaceLineEndings("\n");
        }

        /// <summary>
        /// Texte libre d'un paragraphe : l'espace avant les deux-points devient insécable (la typographie française), sans quoi
        /// le moteur PDF lit « mot : mot » comme une ligne de tableau et coupe le paragraphe en deux colonnes.
        /// </summary>
        private static string Prose(string text) => text.Replace(" :", "\u00A0:");

        /// <summary>La réponse en clair : le texte des choix cochés, les associations faites, ou le texte écrit.</summary>
        public static string AnswerText(LabQuestion question, LabAnswer? answer, string culture)
        {
            if (answer == null || answer.IsEmpty) return T(culture, "(pas de réponse)", "(no answer)");
            string Choice(string id) => question.Choices.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase)) is { } c ? c.Text.Resolve(culture) : id;
            return question.Kind switch
            {
                LabQuestionKind.Text => (answer.Text ?? string.Empty).Trim(),
                LabQuestionKind.Matching => string.Join(" ; ", (question.Prompts ?? [])
                    .Select(p => $"{p.Text.Resolve(culture)} → {(answer.Matches?.GetValueOrDefault(p.Id) is { } id ? Choice(id) : "—")}")),
                _ => string.Join(" ; ", (answer.Choices ?? []).Select(Choice))
            };
        }

        /// <summary>Nom proposé pour le compte rendu d'une copie : « id_Prénom Nom.pdf », sans caractère interdit.</summary>
        public static string StudentReportFileName(LabDefinition lab, GradedSubmission row, string extension)
        {
            var name = row.Student;
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return $"{lab.Id}_{name}{extension}";
        }
    }
}
