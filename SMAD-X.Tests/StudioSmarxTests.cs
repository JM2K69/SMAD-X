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
        public Task<string?> PickFolderAsync(string title, string? startFolder = null) => Task.FromResult<string?>(null);
        public Task<string?> SaveFileAsync(string title, string suggestedName, string filterName, string pattern) => Task.FromResult(SavePath);
        public Task<RecipeRequest?> ChooseRecipeAsync() => Task.FromResult<RecipeRequest?>(null);
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
        Assert.StartsWith("Apprenant;Identifiant;Note", vm.GradesCsv());
        Assert.Equal(6, vm.GradesCsv().Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
    }
}
