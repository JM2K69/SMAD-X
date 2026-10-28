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
    /// <c>list-recipes</c>, <c>recipe</c>, <c>convert</c>, <c>validate</c>, <c>export-subject</c> et <c>grade</c>. Chaque exercice est
    /// écrit avec, à côté, ses notes du formateur (<c>.notes.md</c>), jamais dans l'exercice, et, pour une
    /// recette, son domaine corrigé (<c>.solution.smad-x.json</c>).
    /// </summary>
    public static class Cli
    {
        public static readonly string[] Commands = ["list-recipes", "recipe", "convert", "validate", "export-subject", "grade", "help", "--help", "-h", "/?"];

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
                    "export-subject" => ExportSubject(options, output, error),
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
                  SMAD-X.Studio recipe <RULE-ID>...|all [--combine] [--exam [--exam-check] [--exam-close <tools>]] [--lang fr|en] [--domain corp.example.lab] [--out <folder>] [--date yyyy-MM-dd]
                  SMAD-X.Studio convert <scenario.smad-x.json>... [--exam [--exam-check] [--exam-close <tools>]] [--lang fr|en] [--out <folder>] [--date yyyy-MM-dd]
                  SMAD-X.Studio validate <lab.smadx-lab.json>... [--solution <fixed.smad-x.json>] [--lang fr|en]
                  SMAD-X.Studio export-subject <lab.smadx-lab.json>... [--out <folder>] [--lang fr|en]
                  SMAD-X.Studio grade <lab folder> [--scale 20] [--lang fr|en]
                  SMAD-X.Studio grade <lab.smadx-lab.json> <submission.smadx-submission.json|folder>... [--csv <grades.csv>] [--moodle <moodle.csv>] [--reports <folder>] [--scale 20] [--report <results.md>] [--lang fr|en]

                --exam: exam mode in SMAD-X (no hints, no score, no start over; time measured).
                --exam : mode examen dans SMAD-X (ni indice, ni score, ni « Recommencer » ; temps mesuré).
                export-subject: the subject to hand out, without hints (exam) nor answer key (questions); into the lab
                folder's 2-to-hand-out, or --out.
                export-subject : le sujet à distribuer, sans indices (examen) ni corrigé (questions) ; dans le dossier
                2-a-distribuer de l'exercice, ou dans --out.
                --exam-check: allows Check during the exam (objectives met, no score). / autorise « Vérifier » pendant l'examen.
                --exam-close audit,fixes,paths,comparison: tools closed during the exam (all open by default).
                --exam-close audit,fixes,paths,comparison : outils fermés pendant l'examen (tous ouverts par défaut).
                grade: grades the learners' submissions with your copy of the lab (with the answer key); + met, ~ in part,
                - not met, ? to grade by hand in Studio. With a lab folder, the submissions of 3-submissions are graded and
                every result is written to 4-results; otherwise --csv, --moodle (Moodle grade import), --reports (one PDF
                per learner) and --report choose them. Grades are out of --scale (20 by default).
                grade : corrige les copies avec votre exemplaire de l'exercice (avec le corrigé) ; + atteint, ~ en partie,
                - non atteint, ? à noter à la main dans Studio. Avec un dossier d'exercice, les copies de 3-copies-rendues
                sont corrigées et tous les résultats écrits dans 4-resultats ; sinon --csv, --moodle (import Moodle),
                --reports (un PDF par apprenant) et --report les choisissent. Notes sur --scale (20 par défaut).

                recipe writes one lab per rule; with --combine, one lab with every misconfiguration in the same domain.
                recipe écrit un exercice par règle ; avec --combine, un seul exercice avec tous les manques dans le même domaine.

                Each lab is written to its own folder <out>/<id>, laid out by role:
                  1-trainer/       your copy <id>.smadx-lab.json, trainer-notes.md, fixed-domain.smad-x.json, trainer-sheet.md
                  2-to-hand-out/   the subject <id>.smadx-lab.json: the only file to give learners
                  3-submissions/   drop the learners' submissions here; grade <lab folder> grades them
                  4-results/       grades, Moodle import, class results and PDF reports
                Chaque exercice est écrit dans son dossier <out>/<id>, rangé par rôle :
                  1-formateur/      votre exemplaire <id>.smadx-lab.json, notes-formateur.md, domaine-corrige.smad-x.json, fiche-formateur.md
                  2-a-distribuer/   le sujet <id>.smadx-lab.json : le seul fichier à donner aux apprenants
                  3-copies-rendues/ déposez-y les copies ; grade <dossier de l'exercice> les corrige
                  4-resultats/      notes, import Moodle, bilan et comptes rendus PDF

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
                var combined = LabRecipes.CreateLab(recipes, options.Culture, options.Domain, options.Date);
                return Write(combined.Lab, combined.TeacherNotes, options, output, error, combined.Solution);
            }
            var result = 0;
            foreach (var recipe in recipes)
            {
                var created = LabRecipes.CreateLab(recipe, options.Culture, options.Domain, options.Date);
                result = Math.Max(result, Write(created.Lab, created.TeacherNotes, options, output, error, created.Solution));
            }
            return result;
        }

        private static int Convert(Options options, TextWriter output, TextWriter error)
        {
            if (options.Inputs.Count == 0) throw new CliException("convert: give one or more .smad-x.json files.");
            var data = new ADDataService();
            var result = 0;
            foreach (var path in ExpandScenarios(options.Inputs))
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
            foreach (var path in ExpandLabs(options.Inputs))
            {
                LabDefinition lab;
                ADObject? fixedDomain = null;
                try
                {
                    lab = labs.LoadFile(path);
                    // Sans --solution : le domaine corrigé rangé avec l'exercice, décalé comme le départ
                    var solutionPath = LabProject.Of(path)?.SolutionPath ?? ViewModels.StudioViewModel.SolutionPath(path);
                    if (solution == null && File.Exists(solutionPath))
                    {
                        var loaded = new ADDataService().LoadDocumentAsync(solutionPath).GetAwaiter().GetResult();
                        fixedDomain = labs.PrepareSolution(lab, loaded.Document ?? throw new InvalidOperationException($"{solutionPath}: {loaded.Error ?? loaded.Status.ToString()}"), options.Date).Domain;
                    }
                }
                catch (Exception ex) { error.WriteLine($"{path}: {ex.Message}"); result = 1; continue; }
                output.WriteLine(path);
                result = Math.Max(result, Report(lab, solution ?? fixedDomain, options, output));
            }
            return result;
        }

        /// <summary>Écrit le sujet de chaque exercice : à côté de lui, ou dans --out.</summary>
        private static int ExportSubject(Options options, TextWriter output, TextWriter error)
        {
            if (options.Inputs.Count == 0) throw new CliException("export-subject: give one or more .smadx-lab.json files.");
            var labs = new LabService();
            var result = 0;
            foreach (var path in ExpandLabs(options.Inputs))
            {
                try
                {
                    var lab = labs.LoadFile(path);
                    var project = options.OutGiven ? null : LabProject.Of(path);
                    var folder = options.OutGiven ? options.Out : project?.HandOutFolder ?? Path.GetDirectoryName(Path.GetFullPath(path))!;
                    Directory.CreateDirectory(folder);
                    var subject = project?.SubjectPath(path) ?? Path.Combine(folder, LabService.SubjectFileName(lab.Id, options.Culture));
                    if (string.Equals(Path.GetFullPath(subject), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase))
                        throw new IOException($"{subject}: the subject would replace the lab itself.");
                    labs.SaveSubject(lab, subject);
                    output.WriteLine(subject);
                }
                catch (Exception ex) when (ex is LabFormatException or IOException or UnauthorizedAccessException)
                {
                    error.WriteLine($"{path}: {ex.Message}");
                    result = 1;
                }
            }
            return result;
        }

        /// <summary>
        /// Corrige les copies d'une classe avec l'exercice du formateur, affiche les notes et, sur demande,
        /// écrit le tableau des notes (CSV), l'import Moodle, les comptes rendus PDF et le bilan de la classe (Markdown).
        /// </summary>
        private static int Grade(Options options, TextWriter output, TextWriter error)
        {
            if (options.Inputs.Count == 0) throw new CliException("grade: give the lab folder, or the lab (.smadx-lab.json) then the submissions or their folder.");
            // Un dossier d'exercice (ou son exemplaire) : ses copies rendues, et ses résultats dans 4-resultats
            var labPath = options.Inputs[0];
            var project = Directory.Exists(labPath) ? LabProject.OfRoot(labPath) : LabProject.Of(labPath);
            if (Directory.Exists(labPath))
                labPath = project?.FindLab() ?? throw new CliException($"grade: « {labPath} » is not a lab folder (1-formateur or 1-trainer with one lab).");
            if (options.Inputs.Count < 2 && project == null)
                throw new CliException("grade: give the submissions or their folder after the lab.");
            LabDefinition lab;
            try { lab = new LabService().LoadFile(labPath); }
            catch (Exception ex) when (ex is LabFormatException or IOException or UnauthorizedAccessException)
            {
                error.WriteLine($"{labPath}: {ex.Message}");
                return 1;
            }

            var paths = options.Inputs.Count < 2 ? project!.SubmissionFiles().ToList()
                : options.Inputs.Skip(1).SelectMany<string, string>(i => Directory.Exists(i)
                    ? Directory.GetFiles(i, "*" + LabSubmissionService.FileExtension, SearchOption.AllDirectories).Order(StringComparer.Ordinal)
                    : new[] { i }).ToList();
            var rows = SubmissionGrader.GradeAll(lab, paths, options.Culture);
            var width = Math.Max(12, rows.Select(r => r.Student.Length).DefaultIfEmpty(0).Max());
            var numbers = CultureInfo.GetCultureInfo(options.Culture);
            foreach (var r in rows)
            {
                var grade = r.Graded ? $"{SubmissionGrader.Format(r.Points, numbers)}/{lab.MaxPoints}" : "-";
                var marks = r.Result is not { } result ? ""
                    : string.Concat(result.Objectives.Select(o => o.Passed ? "+" : o.EarnedPoints > 0 ? "~" : "-"))
                      + (result.Questions.Count > 0 ? " " + string.Concat(result.Questions.Select(q => q.NeedsReview ? "?" : q.Passed ? "+" : q.EarnedPoints > 0 ? "~" : "-")) : "");
                output.WriteLine($"{r.Student.PadRight(width)}  {grade,7}  hints {r.HintsUsed}  {marks}");
                // Aucun caractère de contrôle venu d'une copie ne peut masquer ou imiter une ligne
                foreach (var issue in r.Issues)
                    output.WriteLine($"  {(issue.Level == LabIssueLevel.Error ? "error" : "warning")}: {SafeText.Name(issue.Message)}");
            }
            var graded = rows.Count(r => r.Graded);
            if (rows.Sum(r => r.ToReview) is > 0 and var toReview)
                output.WriteLine($"{toReview} answer(s) to grade by hand in Studio");
            output.WriteLine(SubmissionGrader.Average(rows) is { } average && SubmissionGrader.Median(rows) is { } median
                ? $"{graded}/{rows.Count} graded, average {average.ToString("0.#", numbers)}/{lab.MaxPoints}, median {median.ToString("0.#", numbers)}"
                : $"{graded}/{rows.Count} graded");

            if (options.Csv != null)
            {
                File.WriteAllText(options.Csv, SubmissionGrader.Csv(lab, rows, options.Culture, options.Scale), new System.Text.UTF8Encoding(true));
                output.WriteLine(options.Csv);
            }
            if (options.Moodle != null)
            {
                File.WriteAllText(options.Moodle, SubmissionGrader.MoodleCsv(lab, rows, options.Culture, options.Scale), new System.Text.UTF8Encoding(false));
                output.WriteLine(options.Moodle);
            }
            if (options.Reports != null)
            {
                Directory.CreateDirectory(options.Reports);
                foreach (var row in rows.Where(r => r.Graded))
                {
                    var pdf = Path.Combine(options.Reports, SubmissionGrader.StudentReportFileName(lab, row, ".pdf"));
                    ADDocumentReportService.MarkdownToPdf(SubmissionGrader.StudentReport(lab, row, options.Culture, options.Scale), pdf);
                    output.WriteLine(pdf);
                }
            }
            if (options.Report != null)
            {
                File.WriteAllText(options.Report, SubmissionGrader.ClassReport(lab, rows, options.Culture));
                output.WriteLine(options.Report);
            }
            if (project != null && options.Csv == null && options.Moodle == null && options.Reports == null && options.Report == null)
                foreach (var written in project.SaveResults(lab, rows, options.Culture, options.Scale))
                    output.WriteLine(written);
            return rows.Count == 0 || rows.Any(SubmissionGrader.NeedsAttention) ? 1 : 0;
        }

        /// <summary>
        /// Valide puis écrit l'exercice dans son dossier (exemplaire, notes, domaine corrigé, fiche, sujet) ; un exercice
        /// en erreur est quand même écrit, pour être corrigé dans Studio.
        /// </summary>
        private static int Write(LabDefinition lab, string notes, Options options, TextWriter output, TextWriter error, ADRootDocument? fixedDomain = null)
        {
            if (options.Exam)
            {
                lab.Exam = true;
                lab.ExamCheck = options.ExamCheck;
                lab.ExamClosedTools = options.ExamClosedTools;
            }
            // Un dossier par exercice, rangé par rôle : l'exemplaire et ce qui l'accompagne, le sujet à distribuer…
            var project = LabProject.Create(Path.Combine(options.Out, lab.Id), options.Culture);
            var path = project.Save(lab, project.LabPath(lab.Id), notes, fixedDomain, options.Date);
            output.WriteLine(path);
            return Report(lab, fixedDomain != null ? new LabService().PrepareSolution(lab, fixedDomain, options.Date).Domain : null, options, output);
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

        /// <summary>Un dossier donne ses exemplaires d'exercices, sous-dossiers compris, mais pas les sujets à distribuer.</summary>
        private static IEnumerable<string> ExpandLabs(IEnumerable<string> inputs) =>
            inputs.SelectMany<string, string>(i => Directory.Exists(i)
                ? Directory.GetFiles(i, "*" + LabService.FileExtension, SearchOption.AllDirectories)
                    .Where(f => !LabProject.IsHandOut(f) && !f.EndsWith(".sujet" + LabService.FileExtension, StringComparison.OrdinalIgnoreCase)
                                && !f.EndsWith(".subject" + LabService.FileExtension, StringComparison.OrdinalIgnoreCase))
                    .Order(StringComparer.Ordinal)
                : new[] { i });

        /// <summary>Un dossier donne ses scénarios .smad-x.json, sauf les domaines corrigés.</summary>
        private static IEnumerable<string> ExpandScenarios(IEnumerable<string> inputs) =>
            inputs.SelectMany<string, string>(i => Directory.Exists(i)
                ? Directory.GetFiles(i, "*.smad-x.json").Where(f => !f.EndsWith(".solution.smad-x.json", StringComparison.OrdinalIgnoreCase)).Order(StringComparer.Ordinal)
                : new[] { i });

        private sealed class CliException(string message) : Exception(message);

        private sealed class Options
        {
            public List<string> Inputs { get; } = new();
            public string Culture { get; private set; } = StudioText.Instance.Culture;
            public string Domain { get; private set; } = "corp.example.lab";
            public string Out { get; private set; } = Directory.GetCurrentDirectory();
            public bool OutGiven { get; private set; }
            public string? Solution { get; private set; }
            public DateTime Date { get; private set; } = DateTime.Today;
            public bool Verbose { get; private set; }
            public bool Combine { get; private set; }
            public bool Exam { get; private set; }
            public bool ExamCheck { get; private set; }
            public LabTools ExamClosedTools { get; private set; }
            public string? Csv { get; private set; }
            public string? Report { get; private set; }
            public string? Moodle { get; private set; }
            public string? Reports { get; private set; }
            public int Scale { get; private set; } = SubmissionGrader.DefaultScale;

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
                        case "--out": options.Out = Value("--out"); options.OutGiven = true; break;
                        case "--solution": options.Solution = Value("--solution"); break;
                        case "--date":
                            var text = Value("--date");
                            options.Date = DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
                                ? d : throw new CliException($"--date: « {text} » is not yyyy-MM-dd.");
                            break;
                        case "--verbose": case "-v": options.Verbose = true; break;
                        case "--combine": options.Combine = true; break;
                        case "--exam": options.Exam = true; break;
                        case "--exam-check": options.ExamCheck = true; break;
                        case "--exam-close":
                            foreach (var tool in Value("--exam-close").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                                options.ExamClosedTools |= tool.ToLowerInvariant() switch
                                {
                                    "audit" => LabTools.Audit,
                                    "fixes" => LabTools.FixGuidance,
                                    "paths" => LabTools.AttackPaths,
                                    "comparison" => LabTools.Comparison,
                                    _ => throw new CliException($"--exam-close: « {tool} » is not audit, fixes, paths or comparison.")
                                };
                            break;
                        case "--csv": options.Csv = Value("--csv"); break;
                        case "--report": options.Report = Value("--report"); break;
                        case "--moodle": options.Moodle = Value("--moodle"); break;
                        case "--reports": options.Reports = Value("--reports"); break;
                        case "--scale":
                            var scale = Value("--scale");
                            options.Scale = int.TryParse(scale, NumberStyles.None, CultureInfo.InvariantCulture, out var s) && s > 0
                                ? s : throw new CliException($"--scale: « {scale} » is not a positive whole number.");
                            break;
                        default:
                            if (e.Current.StartsWith("--")) throw new CliException($"Unknown option « {e.Current} ».");
                            options.Inputs.Add(e.Current);
                            break;
                    }
                }
                if ((options.ExamCheck || options.ExamClosedTools != LabTools.None) && !options.Exam)
                    throw new CliException("--exam-check and --exam-close need --exam.");
                return options;
            }
        }
    }
}
