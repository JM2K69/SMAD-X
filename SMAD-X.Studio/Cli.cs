using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using SMADX.Models;
using SMADX.Services;
using SMADX.Services.Labs;

namespace SMADX.Studio
{
    /// <summary>
    /// Ligne de commande de Studio, pour produire des exercices en série et corriger une classe :
    /// <c>list-recipes</c>, <c>recipe</c>, <c>convert</c>, <c>validate</c> et <c>grade</c>. Chaque exercice est
    /// écrit avec, à côté, ses notes du formateur (<c>.notes.md</c>), jamais dans l'exercice.
    /// </summary>
    public static class Cli
    {
        public static readonly string[] Commands = ["list-recipes", "recipe", "convert", "validate", "grade", "help", "--help", "-h", "/?"];

        public static bool IsCommand(string[] args) => args.Length > 0 && Commands.Contains(args[0], StringComparer.OrdinalIgnoreCase);

        /// <returns>0 si tout va bien, 1 si un exercice a des erreurs ou une copie est à revoir (non corrigée, modifiée, recopiée), 2 pour une commande incorrecte.</returns>
        public static int Run(string[] args, TextWriter output, TextWriter error)
        {
            var loc = LocalizationService.Instance;
            var previous = loc.CurrentCulture.Name;
            try
            {
                var options = Options.Parse(args.Skip(1));
                loc.SetLanguage(options.Culture);
                return args[0].ToLowerInvariant() switch
                {
                    "list-recipes" => ListRecipes(options, output),
                    "recipe" => Recipe(options, output, error),
                    "convert" => Convert(options, output, error),
                    "validate" => Validate(options, output, error),
                    "grade" => Grade(options, output, error),
                    _ => Help(output)
                };
            }
            catch (CliException ex)
            {
                error.WriteLine(ex.Message);
                error.WriteLine("SMAD-X.Studio help");
                return 2;
            }
            finally
            {
                loc.SetLanguage(previous);
            }
        }

        private static int Help(TextWriter output)
        {
            output.WriteLine("""
                SMAD-X Studio — lab authoring / création d'exercices

                Without arguments, opens the window. / Sans argument, ouvre la fenêtre.

                  SMAD-X.Studio list-recipes [--lang fr|en]
                  SMAD-X.Studio recipe <RULE-ID>...|all [--combine] [--exam] [--lang fr|en] [--domain corp.example.lab] [--out <folder>] [--date yyyy-MM-dd]
                  SMAD-X.Studio convert <scenario.smad-x.json>... [--exam] [--lang fr|en] [--out <folder>] [--date yyyy-MM-dd]
                  SMAD-X.Studio validate <lab.smadx-lab.json>... [--solution <fixed.smad-x.json>] [--lang fr|en]
                  SMAD-X.Studio grade <lab.smadx-lab.json> <submission.smadx-submission.json|folder>... [--csv <grades.csv>] [--report <results.md>] [--lang fr|en]

                --exam: exam mode in SMAD-X (no hints, no checking, no start over).
                --exam : mode examen dans SMAD-X (ni indice, ni vérification, ni « Recommencer »).
                grade: grades the learners' submissions with your lab; + objective met, - not met.
                grade : corrige les copies des apprenants avec votre exercice ; + objectif atteint, - non atteint.

                recipe writes one lab per rule; with --combine, one lab with every misconfiguration in the same domain.
                recipe écrit un exercice par règle ; avec --combine, un seul exercice avec tous les manques dans le même domaine.

                Each lab is written as <id>.smadx-lab.json, with the trainer notes beside it in
                <id>.smadx-lab.notes.md: never hand the notes to learners.
                Chaque exercice est écrit dans <id>.smadx-lab.json, avec les notes du formateur à côté
                dans <id>.smadx-lab.notes.md : ne les donnez jamais aux apprenants.

                Exit code: 0 ok, 1 a lab has errors or a submission needs attention (not graded, changed, copied), 2 wrong command.
                """);
            return 0;
        }

        private static int ListRecipes(Options options, TextWriter output)
        {
            foreach (var recipe in LabRecipes.All)
            {
                var title = LocalizationService.Instance.Translate($"Audit.{recipe.RuleId}", options.Culture).Split('\n')[0].TrimStart('#', ' ').Trim();
                output.WriteLine($"{recipe.RuleId,-8} {recipe.Rule.Severity,-9} {recipe.Anssi?.Id ?? "-",-12} {title}");
            }
            return 0;
        }

        private static int Recipe(Options options, TextWriter output, TextWriter error)
        {
            if (options.Inputs.Count == 0) throw new CliException("recipe: give a rule id (KRB-01...) or « all ».");
            var recipes = options.Inputs.Any(i => i.Equals("all", StringComparison.OrdinalIgnoreCase))
                ? LabRecipes.All.ToList()
                : options.Inputs.Select(id => LabRecipes.All.FirstOrDefault(r => r.RuleId.Equals(id, StringComparison.OrdinalIgnoreCase))
                                               ?? throw new CliException($"recipe: unknown rule « {id} » (see list-recipes).")).ToList();
            if (options.Combine)
            {
                // Un seul exercice : tous les manques dans le même domaine
                if (LabRecipes.Incompatibility(recipes, options.Culture, options.Date) is { } reason)
                {
                    error.WriteLine(reason);
                    return 1;
                }
                var (combined, combinedNotes) = LabRecipes.CreateLab(recipes, options.Culture, options.Domain, options.Date);
                return Write(combined, combinedNotes, options, output, error);
            }
            var result = 0;
            foreach (var recipe in recipes)
            {
                var (lab, notes) = LabRecipes.CreateLab(recipe, options.Culture, options.Domain, options.Date);
                result = Math.Max(result, Write(lab, notes, options, output, error));
            }
            return result;
        }

        private static int Convert(Options options, TextWriter output, TextWriter error)
        {
            if (options.Inputs.Count == 0) throw new CliException("convert: give one or more .smad-x.json files.");
            var data = new ADDataService();
            var result = 0;
            foreach (var path in Expand(options.Inputs))
            {
                var loaded = data.LoadDocumentAsync(path).GetAwaiter().GetResult();
                if (loaded.Document == null)
                {
                    error.WriteLine($"{path}: {loaded.Error ?? loaded.Status.ToString()}");
                    result = 1;
                    continue;
                }
                var id = Path.GetFileName(path).Split('.')[0];
                var (lab, notes) = LabAuthoring.ConvertScenario(loaded.Document, options.Culture, id, options.Date);
                result = Math.Max(result, Write(lab, notes, options, output, error));
            }
            return result;
        }

        private static int Validate(Options options, TextWriter output, TextWriter error)
        {
            if (options.Inputs.Count == 0) throw new CliException("validate: give one or more .smadx-lab.json files.");
            ADObject? solution = null;
            if (options.Solution != null)
            {
                var loaded = new ADDataService().LoadDocumentAsync(options.Solution).GetAwaiter().GetResult();
                solution = loaded.Document?.Domain ?? throw new CliException($"{options.Solution}: {loaded.Error ?? loaded.Status.ToString()}");
            }
            var labs = new LabService();
            var result = 0;
            foreach (var path in Expand(options.Inputs))
            {
                LabDefinition lab;
                try { lab = labs.LoadFile(path); }
                catch (Exception ex) { error.WriteLine($"{path}: {ex.Message}"); result = 1; continue; }
                output.WriteLine(path);
                result = Math.Max(result, Report(lab, solution, options, output));
            }
            return result;
        }

        /// <summary>
        /// Corrige les copies d'une classe avec l'exercice du formateur, affiche les notes et, sur demande,
        /// écrit le tableau des notes (CSV) et le bilan de la classe (Markdown).
        /// </summary>
        private static int Grade(Options options, TextWriter output, TextWriter error)
        {
            if (options.Inputs.Count < 2) throw new CliException("grade: give the lab (.smadx-lab.json), then the submissions or their folder.");
            LabDefinition lab;
            try { lab = new LabService().LoadFile(options.Inputs[0]); }
            catch (Exception ex) when (ex is LabFormatException or IOException or UnauthorizedAccessException)
            {
                error.WriteLine($"{options.Inputs[0]}: {ex.Message}");
                return 1;
            }

            var paths = options.Inputs.Skip(1).SelectMany<string, string>(i => Directory.Exists(i)
                ? Directory.GetFiles(i, "*" + LabSubmissionService.FileExtension, SearchOption.AllDirectories).Order(StringComparer.Ordinal)
                : new[] { i }).ToList();
            var rows = SubmissionGrader.GradeAll(lab, paths, options.Culture);
            var width = Math.Max(12, rows.Select(r => r.Student.Length).DefaultIfEmpty(0).Max());
            foreach (var r in rows)
            {
                var grade = r.Graded ? $"{r.Points}/{lab.MaxPoints}" : "-";
                var marks = r.Graded ? string.Concat(lab.Objectives.Select(o => r.Passed(o) ? "+" : "-")) : "";
                output.WriteLine($"{r.Student.PadRight(width)}  {grade,7}  hints {r.HintsUsed}  {marks}");
                foreach (var issue in r.Issues)
                    output.WriteLine($"  {(issue.Level == LabIssueLevel.Error ? "error" : "warning")}: {issue.Message}");
            }
            var graded = rows.Count(r => r.Graded);
            var numbers = CultureInfo.GetCultureInfo(options.Culture);
            output.WriteLine(SubmissionGrader.Average(rows) is { } average && SubmissionGrader.Median(rows) is { } median
                ? $"{graded}/{rows.Count} graded, average {average.ToString("0.#", numbers)}/{lab.MaxPoints}, median {median.ToString("0.#", numbers)}"
                : $"{graded}/{rows.Count} graded");

            if (options.Csv != null)
            {
                File.WriteAllText(options.Csv, SubmissionGrader.Csv(lab, rows, options.Culture), new System.Text.UTF8Encoding(true));
                output.WriteLine(options.Csv);
            }
            if (options.Report != null)
            {
                File.WriteAllText(options.Report, SubmissionGrader.ClassReport(lab, rows, options.Culture));
                output.WriteLine(options.Report);
            }
            return rows.Count == 0 || rows.Any(SubmissionGrader.NeedsAttention) ? 1 : 0;
        }

        /// <summary>Valide puis écrit l'exercice et ses notes ; un exercice en erreur est quand même écrit, pour être corrigé dans Studio.</summary>
        private static int Write(LabDefinition lab, string notes, Options options, TextWriter output, TextWriter error)
        {
            if (options.Exam) lab.Exam = true;
            Directory.CreateDirectory(options.Out);
            var path = Path.Combine(options.Out, lab.Id + LabService.FileExtension);
            new LabService().SaveFile(lab, path);
            var notesPath = ViewModels.StudioViewModel.NotesPath(path);
            if (!string.IsNullOrWhiteSpace(notes)) File.WriteAllText(notesPath, notes.ReplaceLineEndings("\n"));
            output.WriteLine(path);
            return Report(lab, null, options, output);
        }

        private static int Report(LabDefinition lab, ADObject? solution, Options options, TextWriter output)
        {
            var report = LabValidator.Validate(lab, options.Culture, options.Date, solution);
            foreach (var issue in report.Issues.Where(i => i.Level != LabIssueLevel.Ok || options.Verbose))
            {
                var mark = issue.Level switch { LabIssueLevel.Error => "error", LabIssueLevel.Warning => "warning", _ => "ok" };
                output.WriteLine($"  {mark}: {(issue.ObjectiveId is { } id ? id + " — " : "")}{issue.Message}");
            }
            var errors = report.Issues.Count(i => i.Level == LabIssueLevel.Error);
            output.WriteLine($"  {errors} error(s), {report.Warnings} warning(s), {lab.Objectives.Count} objective(s), {lab.Objectives.Sum(o => o.Points)} pt");
            return report.HasErrors ? 1 : 0;
        }

        /// <summary>Un dossier donne tous les fichiers .json qu'il contient, sauf les copies d'apprenants.</summary>
        private static IEnumerable<string> Expand(IEnumerable<string> inputs) =>
            inputs.SelectMany<string, string>(i => Directory.Exists(i)
                ? Directory.GetFiles(i, "*.json").Where(f => !f.EndsWith(LabSubmissionService.FileExtension, StringComparison.OrdinalIgnoreCase)).Order(StringComparer.Ordinal)
                : new[] { i });

        private sealed class CliException(string message) : Exception(message);

        private sealed class Options
        {
            public List<string> Inputs { get; } = new();
            public string Culture { get; private set; } = StudioText.Instance.Culture;
            public string Domain { get; private set; } = "corp.example.lab";
            public string Out { get; private set; } = Directory.GetCurrentDirectory();
            public string? Solution { get; private set; }
            public DateTime Date { get; private set; } = DateTime.Today;
            public bool Verbose { get; private set; }
            public bool Combine { get; private set; }
            public bool Exam { get; private set; }
            public string? Csv { get; private set; }
            public string? Report { get; private set; }

            public static Options Parse(IEnumerable<string> args)
            {
                var options = new Options();
                using var e = args.GetEnumerator();
                string Value(string name) => e.MoveNext() ? e.Current : throw new CliException($"{name}: value missing.");
                while (e.MoveNext())
                {
                    switch (e.Current.ToLowerInvariant())
                    {
                        case "--lang":
                            var lang = Value("--lang").ToLowerInvariant();
                            options.Culture = lang.StartsWith("fr") ? "fr-FR" : lang.StartsWith("en") ? "en-US" : throw new CliException($"--lang: « {lang} » is not fr or en.");
                            break;
                        case "--domain":
                            options.Domain = Value("--domain").Trim().ToLowerInvariant();
                            if (!ViewModels.RecipePickerViewModel.LooksLikeDomain(options.Domain)) throw new CliException($"--domain: « {options.Domain} » is not a domain name.");
                            break;
                        case "--out": options.Out = Value("--out"); break;
                        case "--solution": options.Solution = Value("--solution"); break;
                        case "--date":
                            var text = Value("--date");
                            options.Date = DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
                                ? d : throw new CliException($"--date: « {text} » is not yyyy-MM-dd.");
                            break;
                        case "--verbose": case "-v": options.Verbose = true; break;
                        case "--combine": options.Combine = true; break;
                        case "--exam": options.Exam = true; break;
                        case "--csv": options.Csv = Value("--csv"); break;
                        case "--report": options.Report = Value("--report"); break;
                        default:
                            if (e.Current.StartsWith("--")) throw new CliException($"Unknown option « {e.Current} ».");
                            options.Inputs.Add(e.Current);
                            break;
                    }
                }
                return options;
            }
        }
    }
}
