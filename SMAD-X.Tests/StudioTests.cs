using SMADX.Models;
using SMADX.Services;
using SMADX.Services.Labs;
using SMADX.Studio;
using SMADX.Studio.ViewModels;
using static SMADX.Tests.TestHelpers;

namespace SMADX.Tests;

/// <summary>SMAD-X Studio : la fenêtre d'édition (ViewModel) et la ligne de commande.</summary>
public class StudioTests
{
    private static readonly DateTime Now = new(2026, 10, 22, 10, 0, 0);

    public StudioTests()
    {
        TierConfigurationService.Instance.ResetToDefaults();
        StudioText.Instance.Culture = "fr-FR";
    }

    private static string Sample(string lang, string id) => Path.Combine(AppContext.BaseDirectory, "Samples", "AttackPaths", lang, $"{id}.smad-x.json");

    private sealed class FakeDialogs : IStudioDialogs
    {
        public string? OpenPath { get; set; }
        public string? SavePath { get; set; }
        public RecipeRequest? Recipe { get; set; }

        public Task<string?> OpenFileAsync(string title, string filterName, string pattern) => Task.FromResult(OpenPath);
        public Task<string?> SaveFileAsync(string title, string suggestedName, string filterName, string pattern) => Task.FromResult(SavePath);
        public Task<RecipeRequest?> ChooseRecipeAsync() => Task.FromResult(Recipe);
    }

    private static (StudioViewModel Vm, FakeDialogs Dialogs) NewStudio()
    {
        var dialogs = new FakeDialogs();
        return (new StudioViewModel(dialogs, () => Now), dialogs);
    }

    private static async Task<StudioViewModel> FromRecipe(string ruleId, string culture = "fr-FR")
    {
        var (vm, dialogs) = NewStudio();
        dialogs.Recipe = new RecipeRequest([LabRecipes.All.Single(r => r.RuleId == ruleId)], culture, "studio.local");
        await vm.NewFromRecipeCommand.ExecuteAsync(null);
        return vm;
    }

    // ─── Fenêtre d'édition ───────────────────────────────────────────────────

    [Fact]
    public async Task NewFromRecipe_LoadsAValidLabWithItsNotes()
    {
        var vm = await FromRecipe("KRB-01");

        Assert.True(vm.HasLab);
        Assert.NotEmpty(vm.Objectives);
        Assert.Equal(vm.Objectives.Sum(o => o.Points), vm.TotalPoints);
        Assert.Contains("KRB-01", vm.Notes);
        Assert.DoesNotContain("KRB-01", vm.Statement);
        Assert.Contains("studio.local", vm.DomainSummary);

        vm.ValidateCommand.Execute(null);
        Assert.DoesNotContain(vm.Issues, i => i.Issue.Level == LabIssueLevel.Error);
        Assert.NotEmpty(vm.SheetPreview);
    }

    [Fact]
    public async Task Save_WritesTheNotesBesideTheLab_AndOpenReadsThemBack()
    {
        var vm = await FromRecipe("PWD-01");
        var path = TempFile(LabService.FileExtension);
        vm.Save(path);

        Assert.True(File.Exists(path));
        var notesPath = StudioViewModel.NotesPath(path);
        Assert.EndsWith(".smadx-lab.notes.md", notesPath);
        Assert.Contains("PWD-01", File.ReadAllText(notesPath));
        Assert.DoesNotContain("PWD-01\n", File.ReadAllText(path)); // les notes ne sont pas dans l'exercice

        var (other, dialogs) = NewStudio();
        dialogs.OpenPath = path;
        await other.OpenCommand.ExecuteAsync(null);
        Assert.Equal(vm.LabId, other.LabId);
        Assert.Equal(vm.Objectives.Count, other.Objectives.Count);
        Assert.Equal(vm.Notes.ReplaceLineEndings("\n"), other.Notes);
        Assert.Equal(path, other.FilePath);
    }

    [Fact]
    public async Task Save_WithoutNotes_RemovesAnOldNotesFile()
    {
        var vm = await FromRecipe("ACC-01");
        var path = TempFile(LabService.FileExtension);
        vm.Save(path);
        vm.Notes = "";
        vm.Save(path);
        Assert.False(File.Exists(StudioViewModel.NotesPath(path)));
    }

    [Fact]
    public async Task ConvertScenario_MovesTheSolutionToTheNotes()
    {
        var (vm, dialogs) = NewStudio();
        dialogs.OpenPath = Sample("fr", "01-kerberoasting");
        await vm.ConvertScenarioCommand.ExecuteAsync(null);

        Assert.True(vm.HasLab, vm.Status);
        Assert.Equal("01-kerberoasting", vm.LabId);
        Assert.NotEmpty(vm.Notes);
        Assert.DoesNotContain("## Le corriger", vm.Statement);
        Assert.NotEmpty(vm.Objectives);
    }

    [Fact]
    public async Task NewFromDomain_StartsWithTheTemplateAndRecommendedObjectives()
    {
        var (vm, dialogs) = NewStudio();
        dialogs.OpenPath = Sample("en", "01-kerberoasting");
        await vm.NewFromDomainCommand.ExecuteAsync(null);

        Assert.True(vm.HasLab, vm.Status);
        Assert.Contains("## ", vm.Statement);
        Assert.NotEmpty(vm.Objectives);
        Assert.Equal("", vm.Notes);
    }

    [Fact]
    public async Task Objectives_AddRemoveAndMove_Renumber()
    {
        var vm = await FromRecipe("DLG-01");
        var count = vm.Objectives.Count;

        vm.AddObjectiveCommand.Execute(null);
        Assert.Equal(count + 1, vm.Objectives.Count);
        var added = vm.SelectedObjective!;
        Assert.Same(vm.Objectives[^1], added);

        vm.MoveUpCommand.Execute(null);
        Assert.Same(added, vm.Objectives[^2]);
        Assert.Equal(vm.Objectives.Count, vm.Objectives.Select(o => o.Id).Distinct().Count());

        var before = vm.TotalPoints;
        added.Points = 5;
        Assert.Equal(before + 4, vm.TotalPoints);

        vm.RemoveObjectiveCommand.Execute(null);
        Assert.Equal(count, vm.Objectives.Count);
        Assert.DoesNotContain(added, vm.Objectives);
    }

    [Fact]
    public async Task Suggest_LeavesOutObjectivesAlreadyInTheLab()
    {
        var vm = await FromRecipe("KRB-02");
        vm.SuggestCommand.Execute(null);

        Assert.True(vm.ShowSuggestions);
        var existing = vm.Objectives.Select(o => System.Text.Json.JsonSerializer.Serialize(o.Model.Check)).ToHashSet();
        Assert.DoesNotContain(vm.Suggestions, s => existing.Contains(System.Text.Json.JsonSerializer.Serialize(s.Suggestion.Objective.Check)));

        if (vm.Suggestions.Count == 0) return;
        foreach (var s in vm.Suggestions) s.IsSelected = false;
        vm.Suggestions[0].IsSelected = true;
        var count = vm.Objectives.Count;
        vm.AddSelectedSuggestionsCommand.Execute(null);
        Assert.Equal(count + 1, vm.Objectives.Count);
        // L'exercice en français ne reçoit que des textes en français
        Assert.Equal(["fr-FR"], vm.Objectives[^1].Model.Title.Keys);
    }

    [Fact]
    public async Task EditCulture_SwitchesTheEditedTexts()
    {
        var (vm, dialogs) = NewStudio();
        dialogs.OpenPath = Sample("fr", "01-kerberoasting");
        await vm.NewFromDomainCommand.ExecuteAsync(null);
        vm.LabTitle = "Titre";
        vm.EditCulture = "en-US";
        Assert.Equal("", vm.LabTitle);
        vm.LabTitle = "Title";
        vm.EditCulture = "fr-FR";
        Assert.Equal("Titre", vm.LabTitle);
    }

    [Fact]
    public async Task Validate_ReportsAnObjectiveAlreadyMetAtStart()
    {
        var vm = await FromRecipe("KRB-01");
        vm.AddObjectiveCommand.Execute(null);
        var added = vm.SelectedObjective!;
        added.CheckType = LabCheckType.ObjectAbsent;
        added.Object = "does-not-exist";
        added.ObjectType = null;

        vm.ValidateCommand.Execute(null);
        Assert.Contains(vm.Issues, i => i.Issue.Level == LabIssueLevel.Error && i.Issue.ObjectiveId == added.Id);
    }

    /// <summary>Les liaisons {Binding [clé]} d'Avalonia ne se rafraîchissent que sur « Item » : sans lui, tout restait dans la langue de départ.</summary>
    [Fact]
    public void StudioText_ChangingLanguage_RaisesTheIndexerNotification()
    {
        var text = StudioText.Instance;
        text.Culture = "en-US";
        var raised = new List<string?>();
        void Handler(object? s, System.ComponentModel.PropertyChangedEventArgs e) => raised.Add(e.PropertyName);
        text.PropertyChanged += Handler;
        try
        {
            text.Culture = "fr-FR";
            Assert.Contains("Item", raised);
            Assert.Equal("Fichier", text["Menu.File"]);
        }
        finally
        {
            text.PropertyChanged -= Handler;
        }
    }

    [Fact]
    public void SetLanguage_TranslatesTheReadyStatus()
    {
        StudioText.Instance.Culture = "en-US";
        var (vm, _) = NewStudio();
        Assert.Equal("Ready.", vm.Status);
        vm.SetLanguageCommand.Execute("fr-FR");
        Assert.Equal("Prêt.", vm.Status);
    }

    [Fact]
    public void ObjectiveItem_ShowsOnlyTheFieldsOfItsCheck()
    {
        var item = new ObjectiveItem(new LabObjective { Check = new LabCheck { Type = LabCheckType.RuleClear } }, () => "fr-FR");
        Assert.True(item.ShowRule);
        Assert.False(item.ShowGroup);

        item.CheckType = LabCheckType.MemberOf;
        Assert.True(item.ShowGroup);
        Assert.False(item.ShowRule);

        item.Group = "  ";
        Assert.Null(item.Model.Check.Group);
    }

    [Fact]
    public void RecipePicker_FiltersAndChecksTheDomainName()
    {
        var picker = new RecipePickerViewModel();
        Assert.Equal(LabRecipes.All.Count, picker.Recipes.Count);

        picker.Filter = "KRB-03";
        Assert.Single(picker.Recipes);
        Assert.Equal("KRB-03", picker.Selected!.RuleId);

        picker.DomainName = "not a domain";
        Assert.False(picker.CanCreate);
        Assert.Null(picker.Request());

        picker.DomainName = "Lab.Example.local";
        Assert.Equal("lab.example.local", picker.Request()!.DomainName);
    }

    [Fact]
    public void RecipePicker_CombinesTheTickedRecipes_AndRefusesIncompatibleOnes()
    {
        var picker = new RecipePickerViewModel(Now);
        RecipeItem Item(string id) => picker.Recipes.Single(r => r.RuleId == id);

        picker.Selected = Item("PWD-01");
        Assert.Equal(["PWD-01"], picker.Chosen.Select(r => r.RuleId));
        Assert.Equal("", picker.SelectionSummary);

        Item("KRB-01").IsChecked = true;
        Item("DLG-02").IsChecked = true;
        Assert.Equal(["DLG-02", "KRB-01"], picker.Chosen.Select(r => r.RuleId)); // dans l'ordre de la liste
        Assert.Contains("DLG-02, KRB-01", picker.SelectionSummary);
        Assert.True(picker.CanCreate);
        Assert.Equal(2, picker.Request()!.Recipes.Count);

        Item("KRB-02").IsChecked = true; // crée aussi « svc-app »
        Assert.True(picker.HasIncompatibility);
        Assert.Contains("svc-app", picker.Incompatibility);
        Assert.False(picker.CanCreate);

        picker.ClearChecks();
        Assert.False(picker.HasIncompatibility);
        Assert.Equal(["PWD-01"], picker.Chosen.Select(r => r.RuleId));
    }

    [Fact]
    public async Task NewFromRecipe_WithSeveralRecipes_CombinesThemInOneLab()
    {
        var (vm, dialogs) = NewStudio();
        var recipes = new[] { "KRB-01", "DLG-02", "ACL-01" }.Select(id => LabRecipes.All.Single(r => r.RuleId == id)).ToList();
        dialogs.Recipe = new RecipeRequest(recipes, "en-US", "combined.lab");
        await vm.NewFromRecipeCommand.ExecuteAsync(null);

        Assert.Equal("anssi-krb-01-dlg-02-acl-01", vm.LabId);
        foreach (var id in new[] { "KRB-01", "DLG-02", "ACL-01" })
        {
            Assert.Contains(vm.Objectives, o => o.CheckType == LabCheckType.RuleClear && o.RuleId == id);
            Assert.Contains($"## {id}", vm.Notes);
            Assert.DoesNotContain(id, vm.Statement);
        }
        vm.ValidateCommand.Execute(null);
        Assert.DoesNotContain(vm.Issues, i => i.Issue.Level == LabIssueLevel.Error);
    }

    public static TheoryData<string, string> RecipePairs()
    {
        var data = new TheoryData<string, string>();
        var ids = LabRecipes.All.Select(r => r.RuleId).ToList();
        for (var i = 0; i < ids.Count; i++)
            for (var j = i + 1; j < ids.Count; j++)
                data.Add(ids[i], ids[j]);
        return data;
    }

    /// <summary>Toute paire de recettes donne un exercice valide, ou est refusée avec une explication.</summary>
    [Theory]
    [MemberData(nameof(RecipePairs))]
    public void EveryRecipePair_IsEitherAValidLabOrExplained(string first, string second)
    {
        var recipes = new[] { first, second }.Select(id => LabRecipes.All.Single(r => r.RuleId == id)).ToList();
        var reason = LabRecipes.Incompatibility(recipes, "fr-FR", Now.Date);
        if (reason != null)
        {
            Assert.False(string.IsNullOrWhiteSpace(reason));
            Assert.Throws<InvalidOperationException>(() => LabRecipes.CreateLab(recipes, "fr-FR", "paire.local", Now.Date));
            return;
        }
        var (lab, _) = LabRecipes.CreateLab(recipes, "fr-FR", "paire.local", Now.Date);
        var report = LabValidator.Validate(lab, "fr-FR", Now);
        Assert.False(report.HasErrors, string.Join(" | ", report.Issues.Where(i => i.Level == LabIssueLevel.Error).Select(i => i.Message)));
        Assert.Contains(lab.Objectives, o => o.Check.RuleId == first);
        Assert.Contains(lab.Objectives, o => o.Check.RuleId == second);
    }

    [Theory]
    [InlineData("corp.local", true)]
    [InlineData("a-b.c1.lab", true)]
    [InlineData("local", false)]
    [InlineData("-a.local", false)]
    [InlineData("a..local", false)]
    [InlineData("a_b.local", false)]
    public void LooksLikeDomain(string name, bool expected) => Assert.Equal(expected, RecipePickerViewModel.LooksLikeDomain(name));

    // ─── Ligne de commande ───────────────────────────────────────────────────

    private static (int Code, string Out, string Err) Run(params string[] args)
    {
        var output = new StringWriter();
        var error = new StringWriter();
        var code = Cli.Run(args, output, error);
        return (code, output.ToString(), error.ToString());
    }

    private static string TempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "smadx-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public void Cli_RecognisesItsCommandsOnly()
    {
        Assert.True(Cli.IsCommand(["recipe", "KRB-01"]));
        Assert.True(Cli.IsCommand(["--help"]));
        Assert.False(Cli.IsCommand([]));
        Assert.False(Cli.IsCommand(["lab.smadx-lab.json"]));
    }

    [Fact]
    public void Cli_ListRecipes_ListsEveryRule()
    {
        var (code, output, _) = Run("list-recipes", "--lang", "en");
        Assert.Equal(0, code);
        foreach (var recipe in LabRecipes.All)
            Assert.Contains(recipe.RuleId, output);
    }

    [Fact]
    public void Cli_Recipe_WritesTheLabAndItsNotes_ThenValidateAcceptsIt()
    {
        var dir = TempDir();
        var (code, output, error) = Run("recipe", "KRB-01", "DLG-02", "--lang", "en", "--domain", "cli.example.lab", "--out", dir, "--date", "2026-10-22");
        Assert.True(code == 0, output + error);

        var labs = Directory.GetFiles(dir, "*" + LabService.FileExtension);
        Assert.Equal(2, labs.Length);
        foreach (var lab in labs)
            Assert.True(File.Exists(StudioViewModel.NotesPath(lab)));
        var loaded = new LabService().LoadFile(labs[0]);
        Assert.Equal("en-US", loaded.Language);
        Assert.Equal("cli.example.lab", loaded.Start.Domain.Name);

        var (validate, report, _) = Run(["validate", .. labs]);
        Assert.True(validate == 0, report);
    }

    [Fact]
    public void Cli_RecipeCombine_WritesOneLab_OrExplainsTheConflict()
    {
        var dir = TempDir();
        var (code, output, error) = Run("recipe", "PWD-03", "PRIV-04", "--combine", "--lang", "fr", "--out", dir, "--date", "2026-10-22");
        Assert.True(code == 0, output + error);
        Assert.Equal(Path.Combine(dir, "anssi-pwd-03-priv-04" + LabService.FileExtension), Directory.GetFiles(dir, "*" + LabService.FileExtension).Single());

        var (conflict, _, message) = Run("recipe", "KRB-01", "KRB-02", "--combine", "--lang", "fr", "--out", TempDir());
        Assert.Equal(1, conflict);
        Assert.Contains("svc-app", message);
    }

    [Fact]
    public void Cli_Recipe_IsReproducible()
    {
        string Generate()
        {
            var dir = TempDir();
            Run("recipe", "PRIV-01", "--lang", "fr", "--out", dir, "--date", "2026-10-22");
            return File.ReadAllText(Directory.GetFiles(dir, "*" + LabService.FileExtension).Single());
        }
        Assert.Equal(Generate(), Generate());
    }

    [Fact]
    public void Cli_Convert_AcceptsAFolder()
    {
        var dir = TempDir();
        var (code, output, error) = Run("convert", Path.GetDirectoryName(Sample("fr", "x"))!, "--lang", "fr", "--out", dir, "--date", "2026-10-22");
        Assert.True(code == 0, output + error);

        var samples = Directory.GetFiles(Path.GetDirectoryName(Sample("fr", "x"))!, "*.json");
        var labs = Directory.GetFiles(dir, "*" + LabService.FileExtension);
        Assert.Equal(samples.Length, labs.Length);
        Assert.All(labs, l => Assert.DoesNotContain("## Le corriger", File.ReadAllText(l)));
    }

    [Fact]
    public void Cli_Validate_FailsOnABrokenLab()
    {
        var dir = TempDir();
        Run("recipe", "ACC-02", "--lang", "fr", "--out", dir);
        var path = Directory.GetFiles(dir, "*" + LabService.FileExtension).Single();
        var lab = new LabService().LoadFile(path);
        lab.Statement["fr-FR"] += "\n\n## Le corriger\n\nDésactivez le compte.\n";
        new LabService().SaveFile(lab, path);

        var (code, output, _) = Run("validate", path);
        Assert.Equal(1, code);
        Assert.Contains("error", output);
    }

    [Theory]
    [InlineData("recipe")]
    [InlineData("recipe", "NOPE-99")]
    [InlineData("recipe", "KRB-01", "--lang", "de")]
    [InlineData("recipe", "KRB-01", "--domain", "bad name")]
    [InlineData("convert", "--unknown")]
    [InlineData("validate", "--date", "22/10/2026", "x.json")]
    public void Cli_RefusesAWrongCommand(params string[] args)
    {
        var (code, _, error) = Run(args);
        Assert.Equal(2, code);
        Assert.NotEmpty(error);
    }

    [Fact]
    public void Cli_RestoresTheLanguage()
    {
        var loc = LocalizationService.Instance;
        var previous = loc.CurrentCulture.Name;
        Run("list-recipes", "--lang", previous == "fr-FR" ? "en" : "fr");
        Assert.Equal(previous, loc.CurrentCulture.Name);
    }
}
