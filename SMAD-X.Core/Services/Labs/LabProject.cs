using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using SMADX.Models;

namespace SMADX.Services.Labs
{
    /// <summary>
    /// Le dossier d'un exercice, rangé par rôle : ce que garde le formateur, ce qui se distribue, les copies rendues
    /// et les résultats. Les noms des dossiers et des fichiers sont simples, en français ou en anglais selon la
    /// langue choisie à la création ; un exercice dans <c>1-formateur</c> (ou <c>1-trainer</c>) est reconnu comme tel.
    /// <code>
    /// revue-ad/
    ///   LISEZMOI.md
    ///   1-formateur/       revue-ad.smadx-lab.json (l'exemplaire), notes-formateur.md, domaine-corrige.smad-x.json, fiche-formateur.md
    ///   2-a-distribuer/    revue-ad.smadx-lab.json (le sujet)
    ///   3-copies-rendues/  les copies, et à côté de chacune les notes à la main du formateur
    ///   4-resultats/       notes.csv, notes-moodle.csv, bilan-classe.md, comptes-rendus/
    /// </code>
    /// </summary>
    public sealed class LabProject
    {
        private static readonly (string Fr, string En) Trainer = ("1-formateur", "1-trainer");
        private static readonly (string Fr, string En) HandOut = ("2-a-distribuer", "2-to-hand-out");
        private static readonly (string Fr, string En) Submissions = ("3-copies-rendues", "3-submissions");
        private static readonly (string Fr, string En) Results = ("4-resultats", "4-results");

        private LabProject(string root, bool french)
        {
            Root = Path.GetFullPath(root);
            French = french;
        }

        public string Root { get; }

        /// <summary>Noms français (1-formateur…) ou anglais (1-trainer…).</summary>
        public bool French { get; }

        private string N((string Fr, string En) name) => French ? name.Fr : name.En;
        private string N(string fr, string en) => French ? fr : en;

        public string TrainerFolder => Path.Combine(Root, N(Trainer));
        public string HandOutFolder => Path.Combine(Root, N(HandOut));
        public string SubmissionsFolder => Path.Combine(Root, N(Submissions));
        public string ResultsFolder => Path.Combine(Root, N(Results));

        public string ReadMePath => Path.Combine(Root, N("LISEZMOI.md", "README.md"));
        public string NotesPath => Path.Combine(TrainerFolder, N("notes-formateur.md", "trainer-notes.md"));
        public string SolutionPath => Path.Combine(TrainerFolder, N("domaine-corrige.smad-x.json", "fixed-domain.smad-x.json"));
        public string SheetPath => Path.Combine(TrainerFolder, N("fiche-formateur.md", "trainer-sheet.md"));
        public string GradesPath => Path.Combine(ResultsFolder, N("notes.csv", "grades.csv"));
        public string MoodlePath => Path.Combine(ResultsFolder, N("notes-moodle.csv", "grades-moodle.csv"));
        public string ClassReportPath => Path.Combine(ResultsFolder, N("bilan-classe.md", "class-results.md"));
        public string ReportsFolder => Path.Combine(ResultsFolder, N("comptes-rendus", "reports"));

        /// <summary>L'exemplaire du formateur : « 1-formateur/&lt;id&gt;.smadx-lab.json ».</summary>
        public string LabPath(string labId) => Path.Combine(TrainerFolder, labId + LabService.FileExtension);

        /// <summary>Le sujet : le même nom que l'exemplaire, dans le dossier à distribuer.</summary>
        public string SubjectPath(string labPath) => Path.Combine(HandOutFolder, Path.GetFileName(labPath));

        /// <summary>Nouveau dossier d'exercice, avec ses quatre sous-dossiers.</summary>
        public static LabProject Create(string root, string culture)
        {
            var project = new LabProject(root, !culture.StartsWith("en", StringComparison.OrdinalIgnoreCase));
            foreach (var folder in new[] { project.TrainerFolder, project.HandOutFolder, project.SubmissionsFolder, project.ResultsFolder })
                Directory.CreateDirectory(folder);
            return project;
        }

        /// <summary>Le dossier d'exercice d'un exemplaire rangé dans 1-formateur (ou 1-trainer), sinon null.</summary>
        public static LabProject? Of(string labPath)
        {
            var folder = Path.GetDirectoryName(Path.GetFullPath(labPath));
            var name = Path.GetFileName(folder);
            if (folder == null || Path.GetDirectoryName(folder) is not { } root) return null;
            if (string.Equals(name, Trainer.Fr, StringComparison.OrdinalIgnoreCase)) return new LabProject(root, french: true);
            if (string.Equals(name, Trainer.En, StringComparison.OrdinalIgnoreCase)) return new LabProject(root, french: false);
            return null;
        }

        /// <summary>Un dossier d'exercice (il contient 1-formateur ou 1-trainer), sinon null.</summary>
        public static LabProject? OfRoot(string root)
        {
            if (Directory.Exists(Path.Combine(root, Trainer.Fr))) return new LabProject(root, french: true);
            if (Directory.Exists(Path.Combine(root, Trainer.En))) return new LabProject(root, french: false);
            return null;
        }

        /// <summary>L'exemplaire rangé dans le dossier, s'il y en a un seul.</summary>
        public string? FindLab()
        {
            if (!Directory.Exists(TrainerFolder)) return null;
            var labs = Directory.GetFiles(TrainerFolder, "*" + LabService.FileExtension);
            return labs.Length == 1 ? labs[0] : null;
        }

        /// <summary>
        /// Dossier où ranger l'exercice dans le dossier choisi par le formateur : le dossier lui-même s'il est vide, ou
        /// s'il est déjà le dossier de cet exercice ; sinon un sous-dossier au nom de l'exercice.
        /// </summary>
        public static string RootFor(string chosen, string labId)
        {
            if (!Directory.Exists(chosen) || !Directory.EnumerateFileSystemEntries(chosen).Any()) return chosen;
            if (OfRoot(chosen) is { } existing && File.Exists(existing.LabPath(labId))) return chosen;
            return Path.Combine(chosen, labId);
        }

        /// <summary>Le fichier est dans un dossier à distribuer (2-a-distribuer, 2-to-hand-out) : c'est un sujet.</summary>
        public static bool IsHandOut(string path) =>
            Path.GetFileName(Path.GetDirectoryName(Path.GetFullPath(path))) is { } folder
            && (string.Equals(folder, HandOut.Fr, StringComparison.OrdinalIgnoreCase) || string.Equals(folder, HandOut.En, StringComparison.OrdinalIgnoreCase));

        /// <summary>Copies rendues, dans 3-copies-rendues et ses sous-dossiers.</summary>
        public IReadOnlyList<string> SubmissionFiles() =>
            Directory.Exists(SubmissionsFolder)
                ? Directory.GetFiles(SubmissionsFolder, "*" + LabSubmissionService.FileExtension, SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToList()
                : [];

        // ─── Écriture ────────────────────────────────────────────────────────────

        /// <summary>
        /// Enregistre l'exercice dans son dossier : l'exemplaire, les notes du formateur, le domaine corrigé (s'il est
        /// connu), la fiche formateur, le sujet à distribuer et le LISEZMOI. Renvoie le chemin de l'exemplaire.
        /// </summary>
        /// <param name="solution">Domaine corrigé aux dates du départ (voir <see cref="LabService.PrepareSolution"/>).</param>
        public string Save(LabDefinition lab, string labPath, string? notes, ADRootDocument? solution, DateTime now)
        {
            foreach (var folder in new[] { TrainerFolder, HandOutFolder, SubmissionsFolder, ResultsFolder })
                Directory.CreateDirectory(folder);
            var labs = new LabService();
            labs.SaveFile(lab, labPath);
            if (!string.IsNullOrWhiteSpace(notes))
                File.WriteAllText(NotesPath, notes.ReplaceLineEndings("\n"));
            else if (File.Exists(NotesPath))
                File.Delete(NotesPath);
            if (solution != null)
                File.WriteAllText(SolutionPath, JsonSerializer.Serialize(solution, ADDataService.SerializerOptions));
            var culture = French ? "fr-FR" : "en-US";
            var prepared = solution != null ? labs.PrepareSolution(lab, solution, now).Domain : null;
            File.WriteAllText(SheetPath, LabValidator.TeacherSheet(lab, lab.Language ?? culture, notes, now, prepared));
            labs.SaveSubject(lab, SubjectPath(labPath));
            File.WriteAllText(ReadMePath, ReadMe(lab, Path.GetFileName(labPath)));
            return labPath;
        }

        /// <summary>Notes, import Moodle, bilan et un compte rendu PDF par copie corrigée, dans 4-resultats.</summary>
        public IReadOnlyList<string> SaveResults(LabDefinition lab, IReadOnlyList<GradedSubmission> rows, string culture, int scale)
        {
            Directory.CreateDirectory(ReportsFolder);
            File.WriteAllText(GradesPath, SubmissionGrader.Csv(lab, rows, culture, scale), new UTF8Encoding(true));
            File.WriteAllText(MoodlePath, SubmissionGrader.MoodleCsv(lab, rows, culture, scale), new UTF8Encoding(false));
            File.WriteAllText(ClassReportPath, SubmissionGrader.ClassReport(lab, rows, culture));
            var written = new List<string> { GradesPath, MoodlePath, ClassReportPath };
            written.AddRange(SaveReports(lab, rows, culture, scale));
            return written;
        }

        /// <summary>Un compte rendu PDF par copie corrigée, dans 4-resultats/comptes-rendus.</summary>
        public IReadOnlyList<string> SaveReports(LabDefinition lab, IReadOnlyList<GradedSubmission> rows, string culture, int scale)
        {
            Directory.CreateDirectory(ReportsFolder);
            var written = new List<string>();
            foreach (var row in rows.Where(r => r.Graded))
            {
                var pdf = Path.Combine(ReportsFolder, SubmissionGrader.StudentReportFileName(lab, row, ".pdf"));
                ADDocumentReportService.MarkdownToPdf(SubmissionGrader.StudentReport(lab, row, culture, scale), pdf);
                written.Add(pdf);
            }
            return written;
        }

        /// <summary>Le rôle de chaque dossier, à la racine du dossier de l'exercice.</summary>
        public string ReadMe(LabDefinition lab, string labFile)
        {
            var title = lab.Title.Resolve(French ? "fr-FR" : "en-US");
            var trainer = Path.GetFileName(TrainerFolder);
            var handOut = Path.GetFileName(HandOutFolder);
            var submissions = Path.GetFileName(SubmissionsFolder);
            var results = Path.GetFileName(ResultsFolder);
            var text = French
                ? $"""
                  # {title}

                  Dossier de l'exercice `{lab.Id}`, rangé par SMAD-X Studio. Chaque dossier a un rôle :

                  | Dossier | Rôle |
                  |---|---|
                  | `{trainer}` | **à garder pour vous** : l'exemplaire `{labFile}` (indices et corrigé), les notes du formateur, le domaine corrigé et la fiche formateur. Ouvrez l'exemplaire dans SMAD-X Studio. |
                  | `{handOut}` | **le seul fichier à donner** aux apprenants : le sujet `{labFile}`, sans indices en examen ni corrigé des questions. Studio le réécrit à chaque enregistrement. |
                  | `{submissions}` | déposez ici les copies (`.smadx-submission.json`) rendues par les apprenants : Studio les corrige à l'ouverture de l'exercice, et y range vos notes à la main (`.smadx-marks.json`). |
                  | `{results}` | les notes (CSV pour Excel et l'ENT, import Moodle), le bilan de la classe et un compte rendu PDF par apprenant, exportés depuis l'onglet Copies. |

                  Ne distribuez jamais le dossier `{trainer}` : il contient la solution.
                  """
                : $"""
                  # {title}

                  Folder of the lab `{lab.Id}`, laid out by SMAD-X Studio. Each folder has a role:

                  | Folder | Role |
                  |---|---|
                  | `{trainer}` | **keep it to yourself**: your copy `{labFile}` (hints and answer key), the trainer notes, the fixed domain and the trainer sheet. Open your copy in SMAD-X Studio. |
                  | `{handOut}` | **the only file to give** learners: the subject `{labFile}`, without hints in an exam nor answer key. Studio rewrites it at every save. |
                  | `{submissions}` | drop here the submissions (`.smadx-submission.json`) handed in by learners: Studio grades them when the lab opens, and stores your grades by hand beside them (`.smadx-marks.json`). |
                  | `{results}` | the grades (CSV for Excel and the school platform, Moodle import), the class results and one PDF report per learner, exported from the Submissions tab. |

                  Never hand out the `{trainer}` folder: it holds the solution.
                  """;
            return text.ReplaceLineEndings("\n") + "\n";
        }
    }
}
