using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using SMADX.Models;

namespace SMADX.Services.Labs
{
    /// <summary>Les copies d'un même exercice, dans un lot.</summary>
    public sealed record BatchLab(string LabId, IReadOnlyList<string> Paths);

    /// <summary>Un exercice du lot, corrigé : l'exercice qui a corrigé (fourni, ou celui ouvert dans Studio) et ses copies.</summary>
    public sealed record GradedBatchLab(LabDefinition Lab, IReadOnlyList<GradedSubmission> Rows);

    /// <summary>
    /// Un lot de copies de plusieurs exercices : un dossier de devoirs (un sous-dossier par apprenant, une copie par exercice),
    /// rangé par exercice d'après l'identifiant que rappelle chaque copie. Le bilan (<see cref="Csv"/>) donne la note de chaque
    /// apprenant à chaque exercice du lot.
    /// </summary>
    public sealed class SubmissionBatch
    {
        public SubmissionBatch(IEnumerable<string> paths)
        {
            var groups = new SortedDictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var path in paths.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (LabSubmissionService.ReadLabId(path) is not { } id)
                {
                    _unreadable.Add(path);
                    continue;
                }
                if (!groups.TryGetValue(id, out var list)) groups[id] = list = new List<string>();
                list.Add(path);
            }
            Labs = groups.Select(g => new BatchLab(g.Key, g.Value)).ToList();
        }

        private readonly List<string> _unreadable = new();

        /// <summary>Les exercices du lot, triés par identifiant (l'ordre des fichiers dans un dossier).</summary>
        public IReadOnlyList<BatchLab> Labs { get; }

        /// <summary>Fichiers qui ne sont pas des copies SMAD-X lisibles.</summary>
        public IReadOnlyList<string> Unreadable => _unreadable;

        public int Count => Labs.Sum(l => l.Paths.Count);

        /// <summary>Plusieurs exercices : un lot à corriger exercice par exercice.</summary>
        public bool HasSeveralLabs => Labs.Count > 1;

        public BatchLab? Find(string labId) => Labs.FirstOrDefault(l => string.Equals(l.LabId, labId, StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// Clé d'un apprenant d'une copie à l'autre : son identifiant (« a.laurent », sans tenir compte de la casse), sinon son
        /// nom (casse et espaces ignorés). Le nom saisi varie souvent plus que l'identifiant d'une copie à l'autre.
        /// </summary>
        public static string StudentKey(GradedSubmission row) =>
            row.StudentId is { Length: > 0 } id ? "id:" + id.Trim().ToLowerInvariant()
            : "name:" + string.Join(' ', row.Student.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();

        private static string T(string culture, string fr, string en) => culture.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? en : fr;

        /// <summary>
        /// Bilan du lot : une ligne par apprenant, sa note sur le barème à chaque exercice, le nombre d'exercices rendus, la
        /// moyenne des exercices rendus et la moyenne sur tous les exercices du lot (un exercice non rendu compte 0). Deux copies
        /// d'un même exercice : la plus récente compte. Une copie à revoir (non corrigée, modifiée, recopiée) est signalée.
        /// Même séparateur et mêmes nombres que le tableau des notes d'un exercice.
        /// </summary>
        public static string Csv(IReadOnlyList<GradedBatchLab> labs, string culture, int scale = SubmissionGrader.DefaultScale)
        {
            var french = !culture.StartsWith("en", StringComparison.OrdinalIgnoreCase);
            var separator = french ? ";" : ",";
            var numbers = CultureInfo.GetCultureInfo(french ? "fr-FR" : "en-US");
            string Cell(string value) => SafeText.CsvCell(value, separator);

            // Chaque apprenant, dans l'ordre de ses noms ; son nom le plus fréquent et son identifiant tel qu'il l'a écrit
            var students = labs.SelectMany(l => l.Rows).Where(r => r.Submission != null).GroupBy(StudentKey)
                .Select(g => (Key: g.Key,
                              Name: g.GroupBy(r => r.Student).OrderByDescending(n => n.Count()).ThenBy(n => n.Key, StringComparer.Ordinal).First().Key,
                              Id: g.Select(r => r.StudentId).FirstOrDefault(i => i.Length > 0) ?? ""))
                .OrderBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase).ToList();

            var sb = new StringBuilder();
            var header = new List<string> { T(culture, "Apprenant", "Learner"), T(culture, "Identifiant", "ID") };
            header.AddRange(labs.Select(l => T(culture, $"{l.Lab.Id} (sur {scale})", $"{l.Lab.Id} (out of {scale})")));
            header.Add(T(culture, "Exercices rendus", "Labs handed in"));
            header.Add(T(culture, $"Moyenne des exercices rendus (sur {scale})", $"Average of the labs handed in (out of {scale})"));
            header.Add(T(culture, $"Moyenne sur les {labs.Count} exercices (sur {scale})", $"Average over the {labs.Count} labs (out of {scale})"));
            header.Add(T(culture, "Remarques", "Remarks"));
            sb.AppendLine(string.Join(separator, header.Select(Cell)));

            foreach (var student in students)
            {
                var cells = new List<string> { student.Name, student.Id };
                var grades = new List<double>();
                var remarks = new List<string>();
                var handedIn = 0;
                foreach (var lab in labs)
                {
                    var copies = lab.Rows.Where(r => r.Submission != null && StudentKey(r) == student.Key)
                        .OrderByDescending(r => r.Submission!.SavedAt).ToList();
                    if (copies.Count == 0)
                    {
                        cells.Add("");
                        continue;
                    }
                    var copy = copies[0];
                    handedIn++;
                    if (copies.Count > 1)
                        remarks.Add(T(culture, $"{lab.Lab.Id} : {copies.Count} copies, la plus récente compte", $"{lab.Lab.Id}: {copies.Count} submissions, the latest counts"));
                    if (copy.Graded)
                    {
                        grades.Add(copy.Scaled(scale));
                        cells.Add(SubmissionGrader.Format(copy.Scaled(scale), numbers));
                    }
                    else cells.Add("");
                    if (SubmissionGrader.NeedsAttention(copy))
                        remarks.Add(T(culture, $"{lab.Lab.Id} : copie à revoir dans Studio", $"{lab.Lab.Id}: submission to check in Studio"));
                    else if (copy.ToReview > 0)
                        remarks.Add(T(culture, $"{lab.Lab.Id} : réponses à noter dans Studio", $"{lab.Lab.Id}: answers to grade in Studio"));
                }
                cells.Add(handedIn.ToString(numbers));
                cells.Add(grades.Count > 0 ? SubmissionGrader.Format(Math.Round(grades.Average(), 2), numbers) : "");
                cells.Add(labs.Count > 0 ? SubmissionGrader.Format(Math.Round(grades.Sum() / labs.Count, 2), numbers) : "");
                cells.Add(string.Join(" · ", remarks));
                sb.AppendLine(string.Join(separator, cells.Select(Cell)));
            }
            return sb.ToString().ReplaceLineEndings("\n");
        }
    }
}
