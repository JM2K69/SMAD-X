using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SMARX.Models;

namespace SMARX.Services.Labs
{
    /// <summary>
    /// Le dossier d'un exercice SMAR-X, rangé par rôle comme ceux de SMAD-X : ce que garde le formateur, ce qui se
    /// distribue, les copies rendues et les résultats. Les noms sont en français ou en anglais selon la langue choisie à
    /// la création. Un dossier n'est un exercice SMAR-X que si son dossier du formateur contient un .smarx-lab.json.
    /// <code>
    /// smx-arbo-lumen-windows/
    ///   LISEZMOI.md
    ///   1-formateur/       smx-arbo-lumen-windows.smarx-lab.json (l'exemplaire), notes-formateur.md, serveurs-corriges.smar-x.json
    ///   2-a-distribuer/    smx-arbo-lumen-windows.smarx-lab.json (le sujet)
    ///   3-copies-rendues/  les copies (.smarx-submission.json)
    ///   4-resultats/       notes.csv, notes-moodle.csv, bilan-classe.md, comptes-rendus/ (un PDF par apprenant)
    /// </code>
    /// </summary>
    public sealed class SmarxLabProject
    {
        private static readonly (string Fr, string En) Trainer = ("1-formateur", "1-trainer");
        private static readonly (string Fr, string En) HandOut = ("2-a-distribuer", "2-to-hand-out");
        private static readonly (string Fr, string En) Submissions = ("3-copies-rendues", "3-submissions");
        private static readonly (string Fr, string En) Results = ("4-resultats", "4-results");

        private SmarxLabProject(string root, bool french)
        {
            Root = Path.GetFullPath(root);
            French = french;
        }

        public string Root { get; }
        public bool French { get; }

        private string N((string Fr, string En) name) => French ? name.Fr : name.En;
        private string N(string fr, string en) => French ? fr : en;

        public string TrainerFolder => Path.Combine(Root, N(Trainer));
        public string HandOutFolder => Path.Combine(Root, N(HandOut));
        public string SubmissionsFolder => Path.Combine(Root, N(Submissions));
        public string ResultsFolder => Path.Combine(Root, N(Results));

        public string ReadMePath => Path.Combine(Root, N("LISEZMOI.md", "README.md"));
        public string NotesPath => Path.Combine(TrainerFolder, N("notes-formateur.md", "trainer-notes.md"));
        public string SolutionPath => Path.Combine(TrainerFolder, N("serveurs-corriges", "fixed-servers") + SmarxDocument.FileExtension);
        public string GradesPath => Path.Combine(ResultsFolder, N("notes.csv", "grades.csv"));
        public string MoodlePath => Path.Combine(ResultsFolder, N("notes-moodle.csv", "grades-moodle.csv"));
        public string ClassReportPath => Path.Combine(ResultsFolder, N("bilan-classe.md", "class-results.md"));
        public string ReportsFolder => Path.Combine(ResultsFolder, N("comptes-rendus", "reports"));

        public string LabPath(string labId) => Path.Combine(TrainerFolder, labId + SmarxLab.FileExtension);
        public string SubjectPath(string labPath) => Path.Combine(HandOutFolder, Path.GetFileName(labPath));

        /// <summary>Nouveau dossier d'exercice, avec ses quatre sous-dossiers.</summary>
        public static SmarxLabProject Create(string root, string culture)
        {
            var project = new SmarxLabProject(root, !culture.StartsWith("en", StringComparison.OrdinalIgnoreCase));
            foreach (var folder in new[] { project.TrainerFolder, project.HandOutFolder, project.SubmissionsFolder, project.ResultsFolder })
                Directory.CreateDirectory(folder);
            return project;
        }

        /// <summary>Le dossier d'exercice d'un exemplaire rangé dans 1-formateur (ou 1-trainer), sinon null.</summary>
        public static SmarxLabProject? Of(string labPath)
        {
            if (!labPath.EndsWith(SmarxLab.FileExtension, StringComparison.OrdinalIgnoreCase)) return null;
            var folder = Path.GetDirectoryName(Path.GetFullPath(labPath));
            var name = Path.GetFileName(folder);
            if (folder == null || Path.GetDirectoryName(folder) is not { } root) return null;
            if (string.Equals(name, Trainer.Fr, StringComparison.OrdinalIgnoreCase)) return new SmarxLabProject(root, french: true);
            if (string.Equals(name, Trainer.En, StringComparison.OrdinalIgnoreCase)) return new SmarxLabProject(root, french: false);
            return null;
        }

        /// <summary>Un dossier d'exercice SMAR-X : son dossier du formateur contient un .smarx-lab.json. Sinon null.</summary>
        public static SmarxLabProject? OfRoot(string root)
        {
            foreach (var (name, french) in new[] { (Trainer.Fr, true), (Trainer.En, false) })
            {
                var trainer = Path.Combine(root, name);
                if (Directory.Exists(trainer) && Directory.EnumerateFiles(trainer, "*" + SmarxLab.FileExtension).Any())
                    return new SmarxLabProject(root, french);
            }
            return null;
        }

        /// <summary>L'exemplaire du dossier : le seul de 1-formateur, ou celui qui porte le nom du dossier. Null sinon.</summary>
        public string? FindLab()
        {
            if (!Directory.Exists(TrainerFolder)) return null;
            var labs = Directory.GetFiles(TrainerFolder, "*" + SmarxLab.FileExtension);
            return labs.Length == 1 ? labs[0]
                : labs.FirstOrDefault(l => string.Equals(Path.GetFileName(l), Path.GetFileName(Root) + SmarxLab.FileExtension, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Dossier d'un nouvel exercice dans le dossier choisi : le dossier lui-même s'il est vide, sinon un sous-dossier au
        /// nom de l'exercice, numéroté s'il existe déjà. Un exercice existant n'est jamais écrasé.
        /// </summary>
        public static string RootForNew(string chosen, string labId)
        {
            if (!Directory.Exists(chosen) || !Directory.EnumerateFileSystemEntries(chosen).Any()) return chosen;
            var root = Path.Combine(chosen, labId);
            for (var n = 2; Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any(); n++)
                root = Path.Combine(chosen, $"{labId}-{n}");
            return root;
        }

        /// <summary>Copies rendues, dans 3-copies-rendues et ses sous-dossiers.</summary>
        public IReadOnlyList<string> SubmissionFiles() =>
            Directory.Exists(SubmissionsFolder)
                ? Directory.GetFiles(SubmissionsFolder, "*" + SmarxSubmission.FileExtension, SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToList()
                : [];

        /// <summary>
        /// Enregistre l'exercice dans son dossier : l'exemplaire, les notes du formateur, les serveurs corrigés (s'ils sont
        /// connus), le sujet à distribuer et le LISEZMOI. Renvoie le chemin de l'exemplaire.
        /// </summary>
        public string Save(SmarxLab lab, string labPath, string? notes, SmarxDocument? solution)
        {
            foreach (var folder in new[] { TrainerFolder, HandOutFolder, SubmissionsFolder, ResultsFolder })
                Directory.CreateDirectory(folder);
            File.WriteAllText(labPath, SmarxLabService.Serialize(lab));
            if (!string.IsNullOrWhiteSpace(notes)) File.WriteAllText(NotesPath, notes.ReplaceLineEndings("\n"));
            if (solution != null) File.WriteAllText(SolutionPath, solution.Serialize());
            File.WriteAllText(SubjectPath(labPath), SmarxLabService.SerializeSubject(lab));
            File.WriteAllText(ReadMePath, ReadMe(lab, Path.GetFileName(labPath)));
            return labPath;
        }

        /// <summary>Notes (CSV pour Excel), import Moodle et bilan de la classe, dans 4-resultats.</summary>
        public IReadOnlyList<string> SaveResults(SmarxLab lab, IReadOnlyList<SmarxGradedSubmission> rows, string culture, int scale)
        {
            Directory.CreateDirectory(ResultsFolder);
            File.WriteAllText(GradesPath, SmarxClassGrader.Csv(lab, rows, culture, scale), new UTF8Encoding(true));
            File.WriteAllText(MoodlePath, SmarxClassGrader.MoodleCsv(lab, rows, culture, scale), new UTF8Encoding(false));
            File.WriteAllText(ClassReportPath, SmarxClassGrader.ClassReport(lab, rows, culture));
            return [GradesPath, MoodlePath, ClassReportPath];
        }

        /// <summary>Le rôle de chaque dossier, à la racine du dossier de l'exercice.</summary>
        public string ReadMe(SmarxLab lab, string labFile)
        {
            var title = lab.Title.Resolve(French ? "fr-FR" : "en-US");
            var trainer = Path.GetFileName(TrainerFolder);
            var handOut = Path.GetFileName(HandOutFolder);
            var submissions = Path.GetFileName(SubmissionsFolder);
            var results = Path.GetFileName(ResultsFolder);
            var text = French
                ? $"""
                  # {title}

                  Dossier de l'exercice SMAR-X `{lab.Id}`, rangé par SMAD-X Studio. Chaque dossier a un rôle :

                  | Dossier | Rôle |
                  |---|---|
                  | `{trainer}` | **à garder pour vous** : l'exemplaire `{labFile}` (indices et corrigé), les notes du formateur et les serveurs corrigés. Ouvrez l'exemplaire dans SMAD-X Studio. |
                  | `{handOut}` | **le seul fichier à donner** aux apprenants : le sujet `{labFile}`, à ouvrir dans SMAR-X, sans indices en examen ni corrigé des questions. Studio le réécrit à chaque enregistrement. |
                  | `{submissions}` | déposez ici les copies (`.smarx-submission.json`) rendues par les apprenants : Studio les corrige à l'ouverture de l'exercice, et y range vos notes à la main (`.smarx-marks.json`). |
                  | `{results}` | les notes (CSV pour Excel et l'ENT, import Moodle), le bilan de la classe et un compte rendu PDF par apprenant, exportés depuis l'onglet Copies. |

                  Ne distribuez jamais le dossier `{trainer}` : il contient la solution.
                  """
                : $"""
                  # {title}

                  Folder of the SMAR-X lab `{lab.Id}`, laid out by SMAD-X Studio. Each folder has a role:

                  | Folder | Role |
                  |---|---|
                  | `{trainer}` | **keep it to yourself**: your copy `{labFile}` (hints and answer key), the trainer notes and the fixed servers. Open your copy in SMAD-X Studio. |
                  | `{handOut}` | **the only file to give** learners: the subject `{labFile}`, to open in SMAR-X, without hints in an exam nor answer key. Studio rewrites it at every save. |
                  | `{submissions}` | drop here the copies (`.smarx-submission.json`) handed in by learners: Studio grades them when the lab opens, and stores your grades by hand beside them (`.smarx-marks.json`). |
                  | `{results}` | the grades (CSV for Excel and the school platform, Moodle import), the class results and one PDF report per learner, exported from the Submissions tab. |

                  Never hand out the `{trainer}` folder: it holds the solution.
                  """;
            return text.ReplaceLineEndings("\n") + "\n";
        }
    }
}
