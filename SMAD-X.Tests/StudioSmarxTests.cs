using SMADX.Models;
using SMADX.Services;
using SMADX.Services.Labs;
using SMADX.Studio;
using SMADX.Studio.ViewModels;
using SMARX.Labs;
using SMARX.Models;
using SMARX.Services.Labs;

namespace SMADX.Tests;

/// <summary>
/// SMAD-X Studio et les exercices SMAR-X : création, objectifs, validation (le départ échoue, la solution réussit),
/// enregistrement avec la solution à côté, et correction des copies d'une classe.
/// </summary>
public class StudioSmarxTests
{
    public StudioSmarxTests() => StudioText.Instance.Culture = "fr-FR";

    private sealed class Dialogs : IStudioDialogs
    {
        public string? OpenPath { get; set; }
        public string? SavePath { get; set; }
        public List<string> OpenPaths { get; } = new();
        public Task<string?> OpenFileAsync(string title, string filterName, string pattern) => Task.FromResult(OpenPath);
        public Task<IReadOnlyList<string>> OpenFilesAsync(string title, string filterName, string pattern) => Task.FromResult<IReadOnlyList<string>>(OpenPaths);
        public Task<string?> PickFolderAsync(string title, string? startFolder = null) => Task.FromResult(Folder);
        public Task<string?> SaveFileAsync(string title, string suggestedName, string filterName, string pattern) => Task.FromResult(SavePath);
        public Task<RecipeRequest?> ChooseRecipeAsync() => Task.FromResult<RecipeRequest?>(null);
        public SmarxRecipeRequest? SmarxRecipe { get; set; }
        public string? Folder { get; set; }
        public Task<SmarxRecipeRequest?> ChooseSmarxRecipeAsync() => Task.FromResult(SmarxRecipe);
        public Task<UnsavedChoice> AskUnsavedAsync() => Task.FromResult(UnsavedChoice.Cancel);
        public Task ShowFolderAsync(string folder) => Task.CompletedTask;
    }

    private static string TempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "smadx-studio-smarx-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static SmarxStudioViewModel Open(SmarxLab lab, Dialogs? dialogs = null, string? path = null) =>
        new(lab, path, dialogs ?? new Dialogs(), _ => { }, "fr-FR");

    /// <summary>La solution du dossier d'équipe, comme le formateur l'enregistrerait depuis SMAR-X.</summary>
    private static SmarxDocument TeamFolderSolution(SmarxLab lab)
    {
        var work = SmarxLabGrader.Clone(lab.Start);
        var host = work.Linux[0];
        var folder = host.Find(SmarxBuiltInLabs.TeamFolder)!;
        folder.Group = "projet";
        folder.ModeBits = Convert.ToInt32("2770", 8);
        folder.DefaultAcl = new PosixDefaultAcl { UserObj = "rwx", GroupObj = "rwx", Other = "---" };
        var plan = host.Find(SmarxBuiltInLabs.TeamFolder + "/plan.txt")!;
        plan.Group = "projet";
        plan.ModeBits = Convert.ToInt32("0660", 8);
        return work;
    }

    [Fact]
    public void Studio_OpensASmarxLab_InsteadOfTheSmadxLab()
    {
        var dialogs = new Dialogs();
        var studio = new StudioViewModel(dialogs);
        Assert.True(studio.IsEmpty);

        studio.NewSmarxSampleCommand.Execute(null);

        Assert.True(studio.HasSmarxLab);
        Assert.False(studio.HasLab);
        Assert.False(studio.IsEmpty);
        Assert.Single(studio.Smarx!.Lab.Start.Linux);
        Assert.Single(studio.Smarx.Lab.Start.Windows);
        Assert.True(studio.SaveCommand.CanExecute(null));
    }

    [Fact]
    public void Validation_WithoutSolution_OnlyWarns_AndAnObjectiveMetAtStartIsAnError()
    {
        var vm = Open(SmarxBuiltInLabs.LinuxTeamFolder());

        var issues = vm.Check();
        Assert.DoesNotContain(issues, i => i.Level == LabIssueLevel.Error);
        Assert.Contains(issues, i => i.Level == LabIssueLevel.Warning && i.ObjectiveId == null);

        // Un objectif atteint dès le départ (root lit plan.txt) ne mesure rien
        vm.AddObjectiveCommand.Execute(null);
        var added = vm.SelectedObjective!;
        added.Account = "root";
        added.Path = SmarxBuiltInLabs.TeamFolder + "/plan.txt";
        added.Operation = "Read";
        Assert.Contains(vm.Check(), i => i.Level == LabIssueLevel.Error && i.ObjectiveId == added.Model.Id);
    }

    [Fact]
    public void Validation_WithTheSolution_MeetsEveryObjective()
    {
        var lab = SmarxBuiltInLabs.LinuxTeamFolder();
        var vm = Open(lab);
        vm.Solution = TeamFolderSolution(lab);

        var issues = vm.Check();

        Assert.DoesNotContain(issues, i => i.Level is LabIssueLevel.Error or LabIssueLevel.Warning);
        Assert.Contains(issues, i => i.Level == LabIssueLevel.Ok);
    }

    [Fact]
    public void Objectives_AreRenumbered_AndAGuardEarnsNothing()
    {
        var vm = Open(SmarxBuiltInLabs.WindowsDepartmentShare());
        var points = vm.TotalPoints;

        vm.AddObjectiveCommand.Execute(null);
        var item = vm.SelectedObjective!;
        Assert.Equal($"o{vm.Objectives.Count}", item.Model.Id);
        Assert.Equal(SmarxSystem.Linux == item.Model.Check.System ? SmarxSystem.Linux : SmarxSystem.Windows, item.Model.Check.System);
        Assert.Equal(points + 1, vm.TotalPoints);

        item.TypeChoice = vm.CheckTypes.Single(c => c.Value == SmarxCheckType.AccountUnchanged);
        Assert.True(item.Guard);
        Assert.Equal(0, item.Points);
        Assert.Equal(points, vm.TotalPoints);
        Assert.Equal("*", item.Account);

        item.TypeChoice = vm.CheckTypes.Single(c => c.Value == SmarxCheckType.ExactAccess);
        item.NeedsText = "bob Read D:\\Partages\\RH\\salaires.xlsx\n\n bob Write D:\\Partages\\RH\\salaires.xlsx ";
        Assert.Equal(["bob Read D:\\Partages\\RH\\salaires.xlsx", "bob Write D:\\Partages\\RH\\salaires.xlsx"], item.Model.Check.Needs);
        Assert.True(item.ShowExact);
        Assert.False(item.ShowRule);
    }

    [Fact]
    public async Task Save_WritesTheLab_AndTheSolutionBesideIt()
    {
        var folder = TempDir();
        var lab = SmarxBuiltInLabs.LinuxTeamFolder();
        var dialogs = new Dialogs { SavePath = Path.Combine(folder, lab.Id + SmarxLab.FileExtension) };
        var vm = Open(lab, dialogs);
        vm.Solution = TeamFolderSolution(lab);

        await vm.SaveAsync(choosePath: false);

        var path = dialogs.SavePath!;
        Assert.Equal(path, vm.FilePath);
        Assert.Equal(lab.Id, SmarxLabService.Parse(File.ReadAllText(path)).Id);
        Assert.True(File.Exists(SmarxStudioViewModel.SolutionPath(path)));
        // La solution est à côté, jamais dans l'exercice
        Assert.Equal("root", SmarxLabService.Parse(File.ReadAllText(path)).Start.Linux[0].Find(SmarxBuiltInLabs.TeamFolder)!.Group);
        Assert.Equal("projet", SmarxDocument.Parse(File.ReadAllText(SmarxStudioViewModel.SolutionPath(path))).Linux[0].Find(SmarxBuiltInLabs.TeamFolder)!.Group);

        // Rouvert, l'exercice retrouve sa solution
        var reopened = Open(SmarxLabService.Parse(File.ReadAllText(path)), path: path);
        Assert.NotNull(reopened.Solution);
        Assert.DoesNotContain(reopened.Check(), i => i.Level == LabIssueLevel.Error);
    }

    [Fact]
    public void Submissions_AreGraded_AndTamperedOrCopiedOnesAreFlagged()
    {
        var folder = TempDir();
        var lab = SmarxBuiltInLabs.LinuxTeamFolder();
        string Write(string student, SmarxDocument work, Guid session)
        {
            var path = Path.Combine(folder, SmarxLabService.SubmissionFileName(lab.Id, student));
            File.WriteAllText(path, SmarxLabService.SerializeSubmission(new SmarxSubmission
            {
                Student = student, LabId = lab.Id, SessionId = session, StartedOn = new DateTime(2026, 11, 6, 9, 0, 0),
                SavedAt = new DateTime(2026, 11, 6, 9, 30, 0), Lab = lab, Work = work
            }));
            return path;
        }
        var good = Write("Camille Martin", TeamFolderSolution(lab), Guid.NewGuid());
        var start = Write("Hugo Petit", SmarxLabGrader.Clone(lab.Start), Guid.NewGuid());
        var shared = Guid.NewGuid();
        var copyA = Write("Inès Moreau", TeamFolderSolution(lab), shared);
        var copyB = Write("Lucas Bernard", TeamFolderSolution(lab), shared);
        var tampered = Write("Nathan Roux", SmarxLabGrader.Clone(lab.Start), Guid.NewGuid());
        File.WriteAllText(tampered, File.ReadAllText(tampered).Replace("Nathan Roux", "Nathan R."));

        var vm = Open(lab);
        vm.AddSubmissions([good, start, copyA, copyB, tampered]);

        var rows = vm.Submissions.ToDictionary(r => r.FileName);
        Assert.Equal(lab.MaxPoints, rows[Path.GetFileName(good)].Points);
        Assert.Empty(rows[Path.GetFileName(good)].Remarks);
        Assert.True(rows[Path.GetFileName(start)].Points < lab.MaxPoints);
        Assert.Contains("Lucas Bernard", rows[Path.GetFileName(copyA)].Remarks);
        Assert.Contains("#E74C3C", rows[Path.GetFileName(tampered)].RemarksColor);
        Assert.Equal(lab.Objectives.Count, rows[Path.GetFileName(good)].Marks.Count);
        Assert.StartsWith("Apprenant;Identifiant;Fichier;Enregistrée le;Points;Sur;Note sur 20", vm.GradesCsv());
        Assert.Equal(6, vm.GradesCsv().Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
    }

    /// <summary>Des copies pour la ligne de commande : une solution, un départ inchangé, une copie retouchée.</summary>
    private static (string Folder, string Lab) ClassFolder()
    {
        var folder = TempDir();
        var lab = SmarxBuiltInLabs.LinuxTeamFolder();
        var labPath = Path.Combine(folder, lab.Id + SmarxLab.FileExtension);
        File.WriteAllText(labPath, SmarxLabService.Serialize(lab));
        var copies = Path.Combine(folder, "copies");
        Directory.CreateDirectory(copies);
        foreach (var (student, work) in new[] { ("Camille Martin", TeamFolderSolution(lab)), ("Hugo Petit", SmarxLabGrader.Clone(lab.Start)), ("Nathan Roux", SmarxLabGrader.Clone(lab.Start)) })
            File.WriteAllText(Path.Combine(copies, SmarxLabService.SubmissionFileName(lab.Id, student)), SmarxLabService.SerializeSubmission(new SmarxSubmission
            {
                Student = student, LabId = lab.Id, SessionId = Guid.NewGuid(), StartedOn = new DateTime(2026, 11, 6, 9, 0, 0),
                SavedAt = new DateTime(2026, 11, 6, 9, 30, 0), Lab = lab, Work = work
            }));
        var tampered = Path.Combine(copies, SmarxLabService.SubmissionFileName(lab.Id, "Nathan Roux"));
        File.WriteAllText(tampered, File.ReadAllText(tampered).Replace("Nathan Roux", "Nathan R."));
        return (folder, labPath);
    }

    private static (int Code, string Output, string Error) Cli(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = SMADX.Studio.Cli.Run(args, output, error);
        return (code, output.ToString(), error.ToString());
    }

    /// <summary>La ligne de commande corrige un exercice SMAR-X comme un exercice SMAD-X : notes, signes, remarques, exports.</summary>
    [Fact]
    public void Cli_Grade_GradesASmarxLab_WithItsExports()
    {
        var (folder, lab) = ClassFolder();
        var csv = Path.Combine(folder, "notes.csv");
        var moodle = Path.Combine(folder, "moodle.csv");
        var report = Path.Combine(folder, "bilan.md");

        var (code, output, _) = Cli("grade", lab, Path.Combine(folder, "copies"), "--csv", csv, "--moodle", moodle, "--report", report, "--lang", "fr");

        // La copie retouchée demande une attention : code 1
        Assert.Equal(1, code);
        Assert.Matches(@"Camille Martin\s+9/9\s+hints 0\s+\+{6}", output);
        Assert.Matches(@"Hugo Petit\s+0/9", output);
        Assert.Contains("error: Copie modifiée après son enregistrement (sceau rompu).", output);
        Assert.Contains("3/3 graded", output);
        Assert.StartsWith("Apprenant;Identifiant;Fichier", File.ReadAllText(csv).TrimStart('\uFEFF'));
        Assert.Contains("Camille Martin,20", File.ReadAllText(moodle));
        Assert.StartsWith("# Le dossier de l'équipe projet — bilan de la classe", File.ReadAllText(report));
    }

    /// <summary>Le dossier qui contient l'exercice : ses copies sont cherchées dedans ; --reports écrit un compte rendu PDF par copie corrigée.</summary>
    [Fact]
    public void Cli_Grade_TakesTheLabFolder_AndWritesPdfReports()
    {
        var (folder, lab) = ClassFolder();

        var (code, output, _) = Cli("grade", folder, "--lang", "en");
        Assert.Equal(1, code);
        Assert.Contains("3/3 graded", output);
        Assert.Contains("error: Copy edited after it was saved (broken seal).", output);

        var pdf = Path.Combine(folder, "pdf");
        Cli("grade", lab, folder, "--reports", pdf, "--lang", "fr");
        Assert.Equal(3, Directory.GetFiles(pdf, "*.pdf").Length);
    }

    /// <summary>
    /// Une recette SMAR-X en ligne de commande : l'exercice est écrit dans son dossier rangé par rôle ; une copie déposée
    /// dans 3-copies-rendues est corrigée par « grade &lt;dossier&gt; », qui écrit tous les résultats dans 4-resultats.
    /// </summary>
    [Fact]
    public void Cli_Recipe_WritesASmarxLabFolder_AndGradeFillsItsResults()
    {
        var output = TempDir();
        var created = Cli("recipe", "SMX-OUVERT", "--company", "Lumen", "--platform", "windows", "--lang", "fr", "--out", output);
        Assert.Equal(0, created.Code);
        var folder = Path.Combine(output, "smx-ouvert-lumen-windows");
        var project = SmarxLabProject.OfRoot(folder)!;
        Assert.True(File.Exists(project.NotesPath));
        Assert.True(File.Exists(project.SolutionPath));
        Assert.True(File.Exists(project.ReadMePath));
        var subject = SmarxLabService.Parse(File.ReadAllText(project.SubjectPath(project.FindLab()!)));

        File.WriteAllText(Path.Combine(project.SubmissionsFolder, SmarxLabService.SubmissionFileName(subject.Id, "Camille Martin")), SmarxLabService.SerializeSubmission(new SmarxSubmission
        {
            Student = "Camille Martin", LabId = subject.Id, SessionId = Guid.NewGuid(), StartedOn = new DateTime(2026, 11, 6, 9, 0, 0),
            SavedAt = new DateTime(2026, 11, 6, 9, 30, 0), Lab = subject, Work = SmarxDocument.Parse(File.ReadAllText(project.SolutionPath))
        }));
        var graded = Cli("grade", folder, "--lang", "fr");

        Assert.Equal(0, graded.Code);
        Assert.Matches(@"Camille Martin\s+7/7", graded.Output);
        Assert.True(File.Exists(project.GradesPath));
        Assert.True(File.Exists(project.MoodlePath));
        Assert.True(File.Exists(project.ClassReportPath));
        Assert.Single(Directory.GetFiles(project.ReportsFolder, "*.pdf"));

        var refused = Cli("recipe", "SMX-AUDIT", "--platform", "windows", "--lang", "fr", "--out", output);
        Assert.Equal(1, refused.Code);
        Assert.Contains("non disponible", refused.Error);
    }

    /// <summary>
    /// Une recette SMAR-X dans Studio : l'exercice est généré, validé (sa solution atteint tout) et rangé dans son dossier
    /// par rôle. Rouvert, il retrouve ses serveurs corrigés et ses notes, corrige les copies de 3-copies-rendues, et ses
    /// exports vont dans 4-resultats sans rien demander.
    /// </summary>
    [Fact]
    public async Task Studio_CreatesASmarxLabFromARecipe_InItsRoleFolder()
    {
        var folder = TempDir();
        var dialogs = new Dialogs
        {
            Folder = folder,
            SmarxRecipe = new SmarxRecipeRequest(SmarxRecipes.Find("SMX-GROUPE")!, SmarxOrganization.Sample(SmarxPlatform.Linux), "fr-FR")
        };
        var studio = new StudioViewModel(dialogs);

        await studio.NewSmarxFromRecipeCommand.ExecuteAsync(null);

        var smarx = studio.Smarx!;
        Assert.DoesNotContain(smarx.Issues, i => i.Issue.Level == LabIssueLevel.Error);
        var project = smarx.Project!;
        Assert.Equal(folder, project.Root.TrimEnd(Path.DirectorySeparatorChar));
        Assert.True(File.Exists(project.SolutionPath));
        Assert.True(File.Exists(project.NotesPath));
        Assert.True(File.Exists(project.SubjectPath(smarx.FilePath!)));

        var subject = SmarxLabService.Parse(File.ReadAllText(project.SubjectPath(smarx.FilePath!)));
        File.WriteAllText(Path.Combine(project.SubmissionsFolder, SmarxLabService.SubmissionFileName(subject.Id, "Camille Martin")), SmarxLabService.SerializeSubmission(new SmarxSubmission
        {
            Student = "Camille Martin", LabId = subject.Id, SessionId = Guid.NewGuid(), StartedOn = new DateTime(2026, 11, 6, 9, 0, 0),
            SavedAt = new DateTime(2026, 11, 6, 9, 30, 0), Lab = subject, Work = SmarxDocument.Parse(File.ReadAllText(project.SolutionPath))
        }));

        studio.LoadSmarx(SmarxLabService.Parse(File.ReadAllText(smarx.FilePath!)), smarx.FilePath);
        var reopened = studio.Smarx!;
        Assert.NotNull(reopened.Solution);
        Assert.Contains("usermod", reopened.TeacherNotes);
        Assert.Equal(subject.MaxPoints, reopened.Submissions.Single().Points);

        await reopened.ExportGradesCommand.ExecuteAsync(null);
        Assert.True(File.Exists(project.GradesPath));
    }

    /// <summary>
    /// La copie sélectionnée dans Studio : des points donnés à la main et une appréciation, enregistrés à côté de la copie
    /// (.smarx-marks.json) ; la note change, la réussite par objectif suit, et le compte rendu PDF s'écrit.
    /// </summary>
    [Fact]
    public void Studio_GradesASmarxQuestionByHand_AndWritesTheReports()
    {
        var folder = TempDir();
        var lab = SmarxBuiltInLabs.SambaDiagnosis();
        var copy = Path.Combine(folder, SmarxLabService.SubmissionFileName(lab.Id, "Camille Martin"));
        File.WriteAllText(copy, SmarxLabService.SerializeSubmission(new SmarxSubmission
        {
            Student = "Camille Martin", LabId = lab.Id, SessionId = Guid.NewGuid(), StartedOn = new DateTime(2026, 11, 6, 9, 0, 0),
            SavedAt = new DateTime(2026, 11, 6, 9, 30, 0), Lab = lab, Work = SmarxLabGrader.Clone(lab.Start),
            Answers = { ["q1"] = new LabAnswer { Choices = ["b"] } }
        }));
        var studio = new StudioViewModel(new Dialogs());
        studio.LoadSmarx(lab, null);
        var smarx = studio.Smarx!;
        smarx.AddSubmissions([copy]);
        Assert.Equal(lab.Objectives.Count + lab.Questions.Count, smarx.ObjectiveRates.Count);

        smarx.SelectedSubmission = smarx.Submissions.Single();
        smarx.SelectedAnswers.Single(a => a.Result.Question.Id == "q1").Points = 0.5;
        smarx.Comment = "Bonne piste.";
        smarx.SaveMarksCommand.Execute(null);

        Assert.True(File.Exists(SmarxMarksService.MarksPath(copy)));
        Assert.Equal(0.5, smarx.Submissions.Single().Points);
        Assert.Equal("Bonne piste.", smarx.Comment);
        Assert.Equal(1, smarx.ExportReports(Path.Combine(folder, "pdf")));
    }
}
