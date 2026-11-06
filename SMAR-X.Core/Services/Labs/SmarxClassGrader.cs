using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using SMADX.Models;
using SMADX.Services;
using SMADX.Services.Labs;
using SMARX.Models;

namespace SMARX.Services.Labs
{
    /// <summary>Une copie SMAR-X corrigée : la copie (null si illisible), son résultat et les remarques pour le formateur.</summary>
    public sealed record SmarxGradedSubmission(string Path, SmarxSubmission? Submission, SmarxLabResult? Result, IReadOnlyList<LabIssue> Issues)
    {
        /// <summary>Notes du formateur sur cette copie (points donnés à la main, appréciation), s'il y en a.</summary>
        public SubmissionMarks? Marks { get; init; }

        public string Student => SafeText.Name(Submission?.Student is { Length: > 0 } s ? s : System.IO.Path.GetFileName(Path));
        public string StudentId => SafeText.Name(Submission?.StudentId ?? "");
        public bool Graded => Result != null;
        public double Points => Result?.Score ?? 0;
        public int HintsUsed => Submission?.HintsUsed.Count ?? 0;
        /// <summary>Réponses écrites à noter à la main (sans corrigé).</summary>
        public int ToReview => Result?.Questions.Count(q => q.NeedsReview) ?? 0;
        public double Scaled(int scale) => Result is { MaxPoints: > 0 } r ? Math.Round(scale * r.Score / r.MaxPoints, 2) : 0;
    }

    /// <summary>
    /// Correction des copies SMAR-X d'une classe avec l'exemplaire du formateur (le corrigé des questions compris), pour
    /// Studio et sa ligne de commande : la note de chacun, les remarques (sceau rompu ou absent, autre exercice, même
    /// session de travail qu'une autre copie, réponses à noter à la main), et les exports CSV, Moodle et Markdown.
    /// </summary>
    public static class SmarxClassGrader
    {
        public const int DefaultScale = 20;

        public static IReadOnlyList<SmarxGradedSubmission> GradeAll(SmarxLab lab, IEnumerable<string> paths, string culture)
        {
            var copies = paths.Distinct(StringComparer.OrdinalIgnoreCase).Select(path =>
            {
                try { return (path, Submission: (SmarxSubmission?)SmarxLabService.ParseSubmission(File.ReadAllText(path)), Error: (string?)null); }
                catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException) { return (path, Submission: null, Error: ex.Message); }
            }).ToList();

            return copies.Select(copy =>
            {
                var issues = new List<LabIssue>();
                var submission = copy.Submission;
                if (submission == null)
                {
                    issues.Add(new(LabIssueLevel.Error, T(culture, $"Copie illisible : {copy.Error}", $"Unreadable copy: {copy.Error}")));
                    return new SmarxGradedSubmission(copy.path, null, null, issues);
                }
                if (submission.SealCheck == SubmissionSeal.Broken)
                    issues.Add(new(LabIssueLevel.Error, T(culture, "Copie modifiée après son enregistrement (sceau rompu).", "Copy edited after it was saved (broken seal).")));
                else if (submission.SealCheck == SubmissionSeal.Missing)
                    issues.Add(new(LabIssueLevel.Warning, T(culture, "Copie sans sceau.", "Copy without a seal.")));
                if (!string.Equals(submission.LabId, lab.Id, StringComparison.OrdinalIgnoreCase))
                    issues.Add(new(LabIssueLevel.Error, T(culture, $"Copie d'un autre exercice ({SafeText.Name(submission.LabId)}).", $"Copy of another lab ({SafeText.Name(submission.LabId)}).")));
                var twins = copies.Where(c => c.Submission != null && c.Submission != submission && c.Submission.SessionId == submission.SessionId)
                    .Select(c => SafeText.Name(c.Submission!.Student)).ToList();
                if (twins.Count > 0)
                    issues.Add(new(LabIssueLevel.Error, T(culture, $"Même session de travail que la copie de {string.Join(", ", twins)} : l'une est la copie de l'autre.",
                                                                   $"Same work session as the copy of {string.Join(", ", twins)}: one is a copy of the other.")));
                var result = SmarxLabGrader.Grade(lab, submission.Work, submission.Answers, culture);
                // Les points donnés à la main, tant que la réponse notée est celle de la copie
                var marks = SmarxMarksService.Load(copy.path, submission);
                if (marks != null)
                    result = result with
                    {
                        Questions = result.Questions.Select(q =>
                        {
                            if (!marks.Questions.TryGetValue(q.Question.Id, out var mark)) return q;
                            if (mark.Answer == (q.Answer?.Signature() ?? string.Empty)) return LabQuestionGrader.Marked(q, mark.Points);
                            issues.Add(new(LabIssueLevel.Warning, T(culture, $"La réponse à {q.Question.Id} a changé depuis votre note : à noter de nouveau.",
                                                                             $"The answer to {q.Question.Id} changed since you graded it: grade it again.")));
                            return q;
                        }).ToList()
                    };
                var toReview = result.Questions.Count(q => q.NeedsReview);
                if (toReview > 0)
                    issues.Add(new(LabIssueLevel.Warning, T(culture, $"{toReview} réponse(s) à noter à la main.", $"{toReview} answer(s) to grade by hand.")));
                return new SmarxGradedSubmission(copy.path, submission, result, issues) { Marks = marks };
            }).ToList();
        }

        /// <summary>
        /// Compte rendu individuel, à remettre à l'apprenant : sa note, chaque objectif avec son explication, ses réponses
        /// et l'appréciation du formateur. Sans le corrigé des questions ni les remarques sur la copie (réservées au formateur).
        /// </summary>
        public static string StudentReport(SmarxLab lab, SmarxGradedSubmission row, string culture, int scale = DefaultScale)
        {
            var numbers = Numbers(culture);
            string Cell(string text) => SafeText.Markdown(text).Replace("\n", " ");
            var sb = new StringBuilder();
            sb.Append($"# {Cell(lab.Title.Resolve(culture))}\n\n");
            sb.Append($"- **{T(culture, "Apprenant", "Learner")}** : {Cell(row.Student)}{(row.StudentId.Length > 0 ? $" ({Cell(row.StudentId)})" : "")}\n");
            if (row.Submission is { } s)
                sb.Append($"- **{T(culture, "Copie enregistrée le", "Copy saved on")}** : {s.SavedAt.ToString("g", numbers)}\n");
            if (row.Result is not { } result)
            {
                sb.Append('\n').Append(T(culture, "Cette copie n'a pas pu être corrigée : voyez-le avec votre formateur.", "This copy could not be graded: see your trainer.")).Append("\n\n");
                return sb.ToString();
            }
            sb.Append($"- **{T(culture, "Note", "Grade")}** : {Format(result.Score, numbers)} / {result.MaxPoints}{(result.MaxPoints > 0 ? T(culture, $", soit {Format(row.Scaled(scale), numbers)} / {scale}", $", that is {Format(row.Scaled(scale), numbers)} / {scale}") : "")}\n");
            if (row.ToReview > 0)
                sb.Append($"- **{T(culture, "Note provisoire", "Provisional grade")}** : {T(culture, $"{row.ToReview} réponse(s) reste(nt) à noter par le formateur.", $"{row.ToReview} answer(s) still to be graded by the trainer.")}\n");
            if (row.HintsUsed > 0 && !lab.Exam)
                sb.Append($"- **{T(culture, "Indices consultés", "Hints viewed")}** : {row.HintsUsed}\n");
            sb.Append('\n');

            sb.Append($"## {T(culture, "Objectifs", "Objectives")}\n\n");
            sb.Append($"| | {T(culture, "Objectif", "Objective")} | {T(culture, "Points", "Points")} | {T(culture, "Commentaire", "Comment")} |\n");
            sb.Append("|---|---|---|---|\n");
            foreach (var o in result.Objectives.Where(o => !o.Objective.Guard || !o.Passed))
            {
                var mark = o.Passed ? "✔" : o.EarnedPoints > 0 ? "◐" : "✘";
                var points = o.Objective.Guard ? T(culture, "garde", "guard") : $"{Format(o.EarnedPoints, numbers)} / {o.Objective.Points}";
                sb.Append($"| {mark} | {Cell(o.Objective.Title.Resolve(culture))} | {points} | {Cell(o.Feedback)} |\n");
            }
            sb.Append('\n');

            if (result.Questions.Count > 0)
            {
                sb.Append($"## {T(culture, "Questions", "Questions")}\n\n");
                sb.Append($"| | {T(culture, "Question", "Question")} | {T(culture, "Votre réponse", "Your answer")} | {T(culture, "Points", "Points")} |\n");
                sb.Append("|---|---|---|---|\n");
                foreach (var q in result.Questions)
                {
                    var mark = q.NeedsReview ? "?" : q.Passed ? "✔" : q.EarnedPoints > 0 ? "◐" : "✘";
                    var points = q.NeedsReview ? T(culture, "à noter", "to grade") : $"{Format(q.EarnedPoints, numbers)} / {q.Question.Points}";
                    sb.Append($"| {mark} | {Cell(q.Question.Title.Resolve(culture))} | {Cell(LabQuestionGrader.AnswerText(q.Question, q.Answer, culture))} | {points} |\n");
                }
                sb.Append('\n');
            }

            if (!string.IsNullOrWhiteSpace(row.Marks?.Comment))
            {
                // L'espace avant les deux-points devient insécable : le moteur PDF lirait « mot : mot » comme un tableau
                sb.Append($"## {T(culture, "Appréciation du formateur", "Trainer's comment")}\n\n");
                sb.Append(SafeText.Markdown(row.Marks!.Comment!.Trim()).Replace(" :", " :")).Append("\n\n");
            }
            return sb.ToString();
        }

        /// <summary>Nom proposé pour le compte rendu d'une copie : « id_Prénom Nom.pdf », sans caractère interdit.</summary>
        public static string StudentReportFileName(SmarxLab lab, SmarxGradedSubmission row, string extension)
        {
            var name = row.Student;
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return $"{lab.Id}_{name}{extension}";
        }

        /// <summary>Réussite de la classe sur un objectif ou une question : combien de copies corrigées l'atteignent.</summary>
        public static (int Met, int Graded) Success(IReadOnlyList<SmarxGradedSubmission> rows, string id)
        {
            var graded = rows.Where(r => r.Result != null).ToList();
            var met = graded.Count(r => r.Result!.Objectives.Any(o => o.Objective.Id == id && o.Passed)
                                        || r.Result.Questions.Any(q => q.Question.Id == id && q.Passed));
            return (met, graded.Count);
        }

        /// <summary>Copie à regarder : non corrigée, ou avec une remarque grave.</summary>
        public static bool NeedsAttention(SmarxGradedSubmission row) => !row.Graded || row.Issues.Any(i => i.Level == LabIssueLevel.Error);

        public static double? Average(IReadOnlyList<SmarxGradedSubmission> rows) =>
            rows.Any(r => r.Graded) ? rows.Where(r => r.Graded).Average(r => r.Points) : null;

        public static double? Median(IReadOnlyList<SmarxGradedSubmission> rows)
        {
            var points = rows.Where(r => r.Graded).Select(r => r.Points).Order().ToList();
            if (points.Count == 0) return null;
            var middle = points.Count / 2;
            return points.Count % 2 == 1 ? points[middle] : (points[middle - 1] + points[middle]) / 2;
        }

        /// <summary>« ++-~ +? » : un signe par objectif, puis par question (+ atteint, ~ en partie, - non atteint, ? à noter).</summary>
        public static string Marks(SmarxGradedSubmission row) => row.Result is not { } result ? ""
            : string.Concat(result.Objectives.Select(o => o.Passed ? "+" : o.EarnedPoints > 0 ? "~" : "-"))
              + (result.Questions.Count > 0 ? " " + string.Concat(result.Questions.Select(q => q.NeedsReview ? "?" : q.Passed ? "+" : q.EarnedPoints > 0 ? "~" : "-")) : "");

        /// <summary>Les notes de la classe, une copie par ligne : point-virgule en français, virgule en anglais.</summary>
        public static string Csv(SmarxLab lab, IReadOnlyList<SmarxGradedSubmission> rows, string culture, int scale = DefaultScale)
        {
            var separator = culture.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? "," : ";";
            var numbers = Numbers(culture);
            string Cell(string value) => SafeText.CsvCell(value, separator);
            var sb = new StringBuilder();
            var header = new List<string>
            {
                T(culture, "Apprenant", "Learner"), T(culture, "Identifiant", "ID"), T(culture, "Fichier", "File"), T(culture, "Enregistrée le", "Saved at"),
                T(culture, "Points", "Points"), T(culture, "Sur", "Out of"), T(culture, $"Note sur {scale}", $"Grade out of {scale}"),
                T(culture, "Pourcentage", "Percentage"), T(culture, "Indices", "Hints"), T(culture, "Temps (min)", "Time (min)"), T(culture, "À noter", "To grade")
            };
            header.AddRange(lab.Objectives.Select(o => o.Id));
            header.AddRange(lab.Questions.Select(q => q.Id));
            header.Add(T(culture, "Remarques", "Remarks"));
            sb.AppendLine(string.Join(separator, header.Select(Cell)));
            foreach (var r in rows)
            {
                var cells = new List<string>
                {
                    r.Student, r.StudentId, SafeText.Name(System.IO.Path.GetFileName(r.Path)),
                    r.Submission?.SavedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) ?? "",
                    r.Graded ? Format(r.Points, numbers) : "", lab.MaxPoints.ToString(numbers), r.Graded ? Format(r.Scaled(scale), numbers) : "",
                    r.Graded && lab.MaxPoints > 0 ? (100.0 * r.Points / lab.MaxPoints).ToString("0.#", numbers) : "", r.HintsUsed.ToString(numbers),
                    r.Submission?.ElapsedMinutes?.ToString(numbers) ?? "", r.Graded ? r.ToReview.ToString(numbers) : ""
                };
                cells.AddRange(lab.Objectives.Select(o => r.Result?.Objectives.FirstOrDefault(x => x.Objective.Id == o.Id) is { } x ? Format(x.EarnedPoints, numbers) : ""));
                cells.AddRange(lab.Questions.Select(q => r.Result?.Questions.FirstOrDefault(x => x.Question.Id == q.Id) is { } x
                    ? x.NeedsReview ? T(culture, "à noter", "to grade") : Format(x.EarnedPoints, numbers) : ""));
                cells.Add(string.Join(" ", r.Issues.Select(i => i.Message)));
                sb.AppendLine(string.Join(separator, cells.Select(Cell)));
            }
            return sb.ToString().ReplaceLineEndings("\n");
        }

        /// <summary>Notes pour l'import de Moodle : virgule, point décimal, l'identifiant, l'apprenant et la note sur le barème.</summary>
        public static string MoodleCsv(SmarxLab lab, IReadOnlyList<SmarxGradedSubmission> rows, string culture, int scale = DefaultScale)
        {
            string Cell(string value) => SafeText.CsvCell(value, ",");
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", new[] { T(culture, "Identifiant", "ID"), T(culture, "Apprenant", "Learner"), lab.Id }.Select(Cell)));
            foreach (var r in rows.Where(r => r.Graded))
                sb.AppendLine(string.Join(",", new[] { r.StudentId, r.Student, Format(r.Scaled(scale), CultureInfo.InvariantCulture) }.Select(Cell)));
            return sb.ToString().ReplaceLineEndings("\n");
        }

        /// <summary>Bilan Markdown de la classe : résumé, notes de chacun et réussite par objectif et par question.</summary>
        public static string ClassReport(SmarxLab lab, IReadOnlyList<SmarxGradedSubmission> rows, string culture)
        {
            var numbers = Numbers(culture);
            string N(double value) => value.ToString("0.#", numbers);
            string Cell(string text) => SafeText.Markdown(text);
            var graded = rows.Where(r => r.Graded).ToList();
            var sb = new StringBuilder();
            sb.AppendLine($"# {Cell(lab.Title.Resolve(culture))} — {T(culture, "bilan de la classe", "class results")}").AppendLine();
            sb.AppendLine($"- **{T(culture, "Exercice", "Lab")}** : `{lab.Id}`{(lab.Exam ? T(culture, " (mode examen)", " (exam mode)") : "")}");
            sb.AppendLine($"- **{T(culture, "Corrigé avec", "Graded with")}** : SMAR-X {SmarxLabService.EngineVersion}");
            sb.AppendLine($"- **{T(culture, "Copies corrigées", "Graded submissions")}** : {graded.Count} / {rows.Count}");
            if (Average(rows) is { } average && Median(rows) is { } median)
                sb.AppendLine($"- **{T(culture, "Moyenne", "Average")}** : {N(average)} / {lab.MaxPoints} — **{T(culture, "médiane", "median")}** : {N(median)}");
            sb.AppendLine();
            sb.AppendLine($"## {T(culture, "Notes", "Grades")}").AppendLine();
            sb.AppendLine($"| {T(culture, "Apprenant", "Learner")} | {T(culture, "Note", "Grade")} | {T(culture, "Indices", "Hints")} | {T(culture, "Remarques", "Remarks")} |");
            sb.AppendLine("|---|---|---|---|");
            foreach (var r in rows.OrderBy(r => r.Student, StringComparer.CurrentCulture))
                sb.AppendLine($"| {Cell(r.Student)} | {(r.Graded ? $"{Format(r.Points, numbers)} / {lab.MaxPoints}" : "—")} | {r.HintsUsed} | {Cell(string.Join(" ", r.Issues.Select(i => i.Message)))} |");
            sb.AppendLine();
            sb.AppendLine($"## {T(culture, "Réussite par objectif et par question", "Success per objective and question")}").AppendLine();
            sb.AppendLine($"| | {T(culture, "Intitulé", "Title")} | {T(culture, "Réussite", "Success")} |");
            sb.AppendLine("|---|---|---|");
            foreach (var o in lab.Objectives)
            {
                var met = graded.Count(r => r.Result!.Objectives.Any(x => x.Objective.Id == o.Id && x.Passed));
                sb.AppendLine($"| {o.Id} | {Cell(o.Title.Resolve(culture))}{(o.Guard ? T(culture, " (garde)", " (guard)") : "")} | {met} / {graded.Count} |");
            }
            foreach (var q in lab.Questions)
            {
                var met = graded.Count(r => r.Result!.Questions.Any(x => x.Question.Id == q.Id && x.Passed));
                sb.AppendLine($"| {q.Id} | {Cell(q.Title.Resolve(culture))} | {met} / {graded.Count} |");
            }
            return sb.ToString().ReplaceLineEndings("\n");
        }

        public static string Format(double points, CultureInfo numbers) => points.ToString("0.##", numbers);

        private static CultureInfo Numbers(string culture) => CultureInfo.GetCultureInfo(culture.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? "en-US" : "fr-FR");

        private static string T(string culture, string fr, string en) => culture.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? en : fr;
    }
}
