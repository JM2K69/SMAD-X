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
        public UnsavedChoice UnsavedAnswer { get; set; } = UnsavedChoice.Cancel;
        public Task<UnsavedChoice> AskUnsavedAsync() => Task.FromResult(UnsavedAnswer);
        public string? OpenPath { get; set; }
        public string? SavePath { get; set; }
        public RecipeRequest? Recipe { get; set; }

        public List<string> OpenPaths { get; } = new();
        public string? Folder { get; set; }

        public Task<string?> OpenFileAsync(string title, string filterName, string pattern) => Task.FromResult(OpenPath);
        public Task<IReadOnlyList<string>> OpenFilesAsync(string title, string filterName, string pattern) => Task.FromResult<IReadOnlyList<string>>(OpenPaths);
        public Task<string?> PickFolderAsync(string title) => Task.FromResult(Folder);
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
    public async Task ClosingStudio_AsksBeforeLosingAnUnsavedLab()
    {
        var vm = await FromRecipe("KRB-01");
        Assert.True(vm.HasUnsavedChanges); // tout juste créé, pas encore enregistré
        Assert.False(await vm.ConfirmCloseAsync());

        vm.Save(TempFile(LabService.FileExtension));
        Assert.False(vm.HasUnsavedChanges);
        Assert.True(await vm.ConfirmCloseAsync());

        vm.Notes = "Corrigé : retirer le SPN.";
        Assert.True(vm.HasUnsavedChanges);
    }

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
    public async Task NewFromRecipe_GeneratesTheFixedDomain_SavedBesideTheLabAndReadBack()
    {
        var vm = await FromRecipe("KRB-01");
        Assert.True(vm.HasFixedDomain);

        vm.ValidateCommand.Execute(null);
        Assert.DoesNotContain(vm.Issues, i => i.Issue.Level == LabIssueLevel.Error);
        Assert.Contains(vm.Issues, i => i.Issue.Level == LabIssueLevel.Ok && i.Issue.Message.Contains("tous les points"));
        Assert.Contains("## Domaine corrigé", vm.SheetPreview);
        Assert.Contains("svc-app", vm.SheetPreview);

        var path = TempFile(LabService.FileExtension);
        vm.Save(path);
        var solutionPath = StudioViewModel.SolutionPath(path);
        Assert.EndsWith(".smadx-lab.solution.smad-x.json", solutionPath);
        // Un domaine ordinaire, que SMAD-X ouvre ; pas dans l'exercice
        var solution = await new ADDataService().LoadDocumentAsync(solutionPath);
        Assert.NotNull(solution.Document);
        Assert.DoesNotContain("gmsa-svc-app", File.ReadAllText(path));

        var (other, dialogs) = NewStudio();
        dialogs.OpenPath = path;
        await other.OpenCommand.ExecuteAsync(null);
        Assert.True(other.HasFixedDomain);
        other.ValidateCommand.Execute(null);
        Assert.Contains(other.Issues, i => i.Issue.Level == LabIssueLevel.Ok && i.Issue.Message.Contains("tous les points"));

        // Une solution choisie à la main remplace celle de l'exercice pour la validation
        other.SetSolution(other.Lab!.Start.Domain, "départ");
        Assert.False(other.HasFixedDomain);
        other.ValidateCommand.Execute(null);
        Assert.Contains(other.Issues, i => i.Issue.Message.Contains("La solution échoue"));
    }

    [Fact]
    public async Task Questions_AreEditedInEveryForm_SavedInTheTrainerCopy_AndLeftOutOfTheSubject()
    {
        var vm = await FromRecipe("ACC-01");
        vm.IsExam = true;

        vm.AddQuestionCommand.Execute(null);
        var single = vm.SelectedQuestion!;
        Assert.Equal("q1", single.Id);
        Assert.Equal(2, single.Choices.Count);
        single.Title = "Quel compte est en cause ?";
        single.Choices[0].Text = "Guest";
        single.Choices[1].Text = "krbtgt";
        single.Choices[0].Correct = true;
        single.Choices[1].Correct = true; // choix unique : décoche l'autre
        Assert.False(single.Choices[0].Correct);
        single.Choices[0].Correct = true;

        vm.AddQuestionCommand.Execute(null);
        var yesNo = vm.SelectedQuestion!;
        yesNo.Title = "Le compte Invité doit rester désactivé.";
        yesNo.KindChoice = yesNo.Kinds.Single(k => k.Value == LabQuestionKind.TrueFalse);
        Assert.Equal(["yes", "no"], yesNo.Model.Choices.Select(c => c.Id));
        Assert.False(yesNo.CanEditChoices);

        vm.AddQuestionCommand.Execute(null);
        var matching = vm.SelectedQuestion!;
        matching.Title = "Associez chaque compte à son rôle.";
        matching.Kind = LabQuestionKind.Matching;
        Assert.Empty(matching.Choices); // les deux choix vides de départ ne deviennent pas des intrus
        matching.AddPairCommand.Execute(null);
        matching.AddPairCommand.Execute(null);
        matching.Pairs[0].Left = "krbtgt";
        matching.Pairs[0].Right = "Signe les tickets";
        matching.Pairs[1].Left = "Guest";
        matching.Pairs[1].Right = "Accès anonyme";
        matching.AddChoiceCommand.Execute(null);
        matching.Choices.Last().Text = "Intrus";
        matching.Partial = true;
        Assert.Equal(["p1", "p2"], matching.Model.Prompts!.Select(p => p.Id));
        Assert.All(matching.Model.Prompts!, p => Assert.Contains(matching.Model.Choices, c => c.Id == p.Match));

        vm.AddQuestionCommand.Execute(null);
        var written = vm.SelectedQuestion!;
        written.Title = "Comment s'appelle ce point de contrôle ?";
        written.Kind = LabQuestionKind.Text;
        written.AcceptedAnswers = "compte invité\n\n guest account ";
        Assert.Equal(["compte invité", "guest account"], written.Model.AcceptedAnswers);
        Assert.Empty(written.Model.Choices);

        Assert.Equal(vm.Lab!.ObjectivePoints + 4, vm.TotalPoints);
        vm.ValidateCommand.Execute(null);
        Assert.DoesNotContain(vm.Issues, i => i.Issue.Level == LabIssueLevel.Error && i.Issue.ObjectiveId?.StartsWith('q') == true);

        var path = TempFile(LabService.FileExtension);
        vm.Save(path);
        var (other, dialogs) = NewStudio();
        dialogs.OpenPath = path;
        await other.OpenCommand.ExecuteAsync(null);
        Assert.Equal(4, other.Questions.Count);
        Assert.Equal(2, other.Questions[2].Pairs.Count);
        Assert.Single(other.Questions[2].Choices); // l'intrus
        Assert.True(other.Lab!.IsTrainerCopy);

        // Le sujet ne remplace pas l'exemplaire, et se distribue sans corrigé
        other.ExportSubject(path);
        Assert.True(new LabService().LoadFile(path).IsTrainerCopy);
        var subject = TempFile(LabService.FileExtension);
        other.ExportSubject(subject);
        var distributed = new LabService().LoadFile(subject);
        Assert.False(distributed.IsTrainerCopy);
        Assert.All(distributed.Questions, q => Assert.False(q.HasAnswerKey));
    }

    [Fact]
    public async Task Submissions_AreGradedByHand_WithACommentKeptBesideTheCopy_AndGetAPdfReport()
    {
        var vm = await FromRecipe("ACC-01");
        vm.IsExam = true;
        vm.AddQuestionCommand.Execute(null);
        var written = vm.SelectedQuestion!;
        written.Kind = LabQuestionKind.Text;
        written.Points = 3;
        written.Title = "Expliquez le risque.";
        vm.Save(TempFile(LabService.FileExtension));

        var subject = new LabService().Parse(new LabService().SerializeSubject(vm.Lab!));
        var session = LabSession.Begin(subject, Now);
        var path = TempFile(LabSubmissionService.FileExtension);
        new LabSubmissionService().SaveFile(session.ToSubmission("Lou", session.NewWorkingCopy(), [], Now.AddMinutes(20),
            new Dictionary<string, LabAnswer> { ["q1"] = new() { Text = "Un accès sans mot de passe." } }), path);
        vm.AddSubmissions([path]);

        var row = vm.Submissions.Single();
        Assert.Equal(1, row.ToReview);
        Assert.EndsWith("*", row.Grade);
        vm.SelectedSubmission = row;
        var answer = vm.SelectedAnswers.Single();
        Assert.Equal("Un accès sans mot de passe.", answer.Answer);
        Assert.True(answer.NeedsReview);

        answer.Points = 2.5;
        vm.Comment = "Risque bien vu ; pensez à la correction.";
        vm.SaveMarksCommand.Execute(null);

        Assert.True(File.Exists(SubmissionMarksService.MarksPath(path)));
        var regraded = vm.Submissions.Single();
        Assert.Equal(0, regraded.ToReview);
        Assert.Equal(2.5, regraded.Graded.Question(vm.Lab!.Questions[0])!.EarnedPoints);
        Assert.Same(regraded, vm.SelectedSubmission);
        Assert.Equal(2.5, vm.SelectedAnswers.Single().Points);
        Assert.Equal("Risque bien vu ; pensez à la correction.", vm.Comment);

        var folder = Path.Combine(Path.GetTempPath(), "smadx-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        Assert.Equal(1, vm.ExportReports(folder));
        Assert.Single(Directory.GetFiles(folder, "*.pdf"));
    }

    [Fact]
    public async Task ProposeSolution_ForALabFromADomain_IsValidatedAndKeptInTheLabFolder()
    {
        // Un exercice fait à partir d'un domaine (pas d'une recommandation) : pas de domaine corrigé au départ
        var (vm, dialogs) = NewStudio();
        await vm.NewFromDomainFileAsync(Sample("fr", "08-owner"));
        Assert.False(vm.HasFixedDomain);

        vm.ProposeSolutionCommand.Execute(null);

        Assert.True(vm.HasFixedDomain);
        Assert.Contains("proposé par Studio", vm.SolutionLabel);
        Assert.Contains(vm.Issues, i => i.Issue.Level == LabIssueLevel.Ok && i.Issue.Message.Contains("tous les points"));
        Assert.True(vm.HasUnsavedChanges);
        Assert.Contains("## Domaine corrigé", vm.SheetPreview);

        var path = vm.SaveToFolder(TempDir());
        Assert.True(File.Exists(LabProject.Of(path)!.SolutionPath));
        Assert.False(vm.HasUnsavedChanges);
    }

    [Fact]
    public async Task HandMadeSolution_IsBroughtBackToTheStart_AndSavedWithTheLab()
    {
        var vm = await FromRecipe("KRB-05");
        var path = vm.SaveToFolder(TempDir());
        // Le formateur ouvre l'exercice dans SMAD-X un mois plus tard, corrige et enregistre son domaine
        var session = LabSession.Begin(vm.Lab!, Now.AddMonths(1));
        var work = session.NewWorkingCopy();
        Flatten(work.Domain).Single(o => o.Name == "krbtgt").PwdLastSet = Now.AddMonths(1).AddDays(-1);

        vm.ChooseSolution(work, "corrige.smad-x.json");
        Assert.True(vm.HasUnsavedChanges);
        vm.ValidateCommand.Execute(null);
        Assert.Contains(vm.Issues, i => i.Issue.Level == LabIssueLevel.Ok && i.Issue.Message.Contains("tous les points"));
        vm.Save(path);

        // Rouvert, il est relu avec l'exercice, aux dates du départ
        var (other, dialogs) = NewStudio();
        dialogs.OpenPath = path;
        await other.OpenCommand.ExecuteAsync(null);
        Assert.True(other.HasFixedDomain);
        Assert.False(other.HasUnsavedChanges);
        var saved = await new ADDataService().LoadDocumentAsync(LabProject.Of(path)!.SolutionPath);
        var start = Flatten(vm.Lab!.Start.Domain).ToDictionary(o => o.DistinguishedName, StringComparer.OrdinalIgnoreCase);
        Assert.All(Flatten(saved.Document!.Domain), o => Assert.Equal(start[o.DistinguishedName].CreatedDate, o.CreatedDate));
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

    /// <summary>Les exemplaires écrits sous un dossier : un dossier d'exercice chacun, rangé par rôle.</summary>
    private static string[] TrainerCopies(string dir) =>
        Directory.GetFiles(dir, "*" + LabService.FileExtension, SearchOption.AllDirectories).Where(f => !LabProject.IsHandOut(f)).Order().ToArray();

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

        var labs = TrainerCopies(dir);
        Assert.Equal(2, labs.Length);
        foreach (var lab in labs)
        {
            // Un dossier par exercice, à son nom, rangé par rôle
            var project = LabProject.Of(lab)!;
            Assert.Equal(Path.Combine(dir, Path.GetFileName(lab).Split('.')[0]), project.Root);
            Assert.Equal("1-trainer", Path.GetFileName(project.TrainerFolder));
            foreach (var file in new[] { project.NotesPath, project.SolutionPath, project.SheetPath, project.SubjectPath(lab), project.ReadMePath })
                Assert.True(File.Exists(file), file);
            Assert.True(Directory.Exists(project.SubmissionsFolder));
            Assert.True(Directory.Exists(project.ResultsFolder));
        }
        var loaded = new LabService().LoadFile(labs[0]);
        Assert.Equal("en-US", loaded.Language);
        Assert.Equal("cli.example.lab", loaded.Start.Domain.Name);

        // Le dossier : les domaines corrigés ne sont pas pris pour des exercices, mais servent à la validation
        var (validate, report, _) = Run("validate", dir, "--verbose", "--lang", "en");
        Assert.True(validate == 0, report);
        Assert.Equal(2, report.Split('\n').Count(l => l.Contains("The solution gets all the objectives' points")));
    }

    [Fact]
    public void Cli_RecipeCombine_WritesOneLab_OrExplainsTheConflict()
    {
        var dir = TempDir();
        var (code, output, error) = Run("recipe", "PWD-03", "PRIV-04", "--combine", "--lang", "fr", "--out", dir, "--date", "2026-10-22");
        Assert.True(code == 0, output + error);
        Assert.Equal(Path.Combine(dir, "anssi-pwd-03-priv-04", "1-formateur", "anssi-pwd-03-priv-04" + LabService.FileExtension), TrainerCopies(dir).Single());

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
            return File.ReadAllText(TrainerCopies(dir).Single());
        }
        Assert.Equal(Generate(), Generate());
    }

    [Fact]
    public void Cli_Convert_AcceptsAFolder()
    {
        var dir = TempDir();
        var (code, output, error) = Run("convert", Path.GetDirectoryName(Sample("fr", "x"))!, "--lang", "fr", "--out", dir, "--date", "2026-10-22");
        Assert.True(code == 0, output + error);

        var samples = Directory.GetFiles(Path.GetDirectoryName(Sample("fr", "x"))!, "*.smad-x.json");
        var labs = TrainerCopies(dir);
        Assert.Equal(samples.Length, labs.Length);
        Assert.All(labs, l => Assert.DoesNotContain("## Le corriger", File.ReadAllText(l)));
    }

    [Fact]
    public void Cli_Validate_FailsOnABrokenLab()
    {
        var dir = TempDir();
        Run("recipe", "ACC-02", "--lang", "fr", "--out", dir);
        var path = TrainerCopies(dir).Single();
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

    // ─── Ouverture au lancement, mode examen, copies d'une classe ────────────

    /// <summary>Exercice ACC-01 (compte Invité activé) enregistré, et sa correction : désactiver l'Invité.</summary>
    private static string SavedRecipeLab(string ruleId = "ACC-01", bool exam = false)
    {
        var (lab, _) = LabRecipes.CreateLab(LabRecipes.All.Single(r => r.RuleId == ruleId), "fr-FR", "classe.local", Now.Date);
        lab.Exam = exam;
        var path = Path.Combine(TempDir(), lab.Id + LabService.FileExtension);
        new LabService().SaveFile(lab, path);
        return path;
    }

    private static void DisableGuest(ADObject domain) =>
        Flatten(domain).Single(o => o.Name == "Guest").AccountFlags |= ADAccountFlags.AccountDisabled;

    /// <summary>Copie d'un apprenant sur l'exercice du fichier, enregistrée dans <paramref name="folder"/>.</summary>
    private static string Submission(string labPath, string student, Action<ADObject>? work = null, string? folder = null)
    {
        var session = LabSession.Begin(new LabService().LoadFile(labPath), Now);
        var document = session.NewWorkingCopy();
        work?.Invoke(document.Domain);
        var path = Path.Combine(folder ?? TempDir(), LabSubmissionService.SuggestedFileName(session.Lab.Id, student));
        new LabSubmissionService().SaveFile(session.ToSubmission(student, document, [], Now.AddMinutes(30)), path);
        return path;
    }

    [Fact]
    public async Task OpenPath_ADomainFromSmadX_StartsANewLab()
    {
        var (vm, _) = NewStudio();
        var path = Path.Combine(TempDir(), "contoso.smad-x.json");
        File.Copy(Sample("fr", "01-kerberoasting"), path);

        await vm.OpenPathAsync(path);

        Assert.True(vm.HasLab, vm.Status);
        Assert.Equal("contoso", vm.LabId);
        Assert.Null(vm.FilePath); // à enregistrer ailleurs que dans le dossier temporaire
    }

    [Fact]
    public async Task OpenPath_ALabOpensIt_ASubmissionExplainsWhatToDo()
    {
        var labPath = SavedRecipeLab();
        var (vm, _) = NewStudio();

        await vm.OpenPathAsync(Submission(labPath, "Camille"));
        Assert.False(vm.HasLab);
        Assert.Equal(StudioText.Instance["Submissions.OpenLabFirst"], vm.Status);

        await vm.OpenPathAsync(labPath);
        Assert.True(vm.HasLab);
        Assert.Equal(labPath, vm.FilePath);
    }

    [Fact]
    public async Task ExamMode_IsSavedInTheLab()
    {
        var vm = await FromRecipe("ACC-01");
        Assert.False(vm.IsExam);

        vm.IsExam = true;
        var path = TempFile(LabService.FileExtension);
        vm.Save(path);

        Assert.True(new LabService().LoadFile(path).Exam);
    }

    [Fact]
    public async Task Submissions_AreGradedWithTheOpenLab_AndExported()
    {
        var labPath = SavedRecipeLab();
        var (vm, dialogs) = NewStudio();
        await vm.OpenPathAsync(labPath);
        var max = vm.TotalPoints;

        dialogs.OpenPaths.AddRange([Submission(labPath, "Lucas"), Submission(labPath, "Camille", DisableGuest)]);
        await vm.AddSubmissionsCommand.ExecuteAsync(null);

        Assert.Equal(["Camille", "Lucas"], vm.Submissions.Select(s => s.Student));
        Assert.Equal($"{max} / {max}", vm.Submissions[0].Grade);
        Assert.Equal("100 %", vm.Submissions[0].Percent);
        Assert.All(vm.Submissions[0].Marks, m => Assert.True(m.Passed));
        Assert.Contains(vm.Submissions[1].Marks, m => !m.Passed);
        Assert.Equal(vm.Objectives.Count, vm.Submissions[1].Marks.Count);
        Assert.Equal(vm.Objectives.Count, vm.ObjectiveRates.Count);
        Assert.Contains("2 copie(s) corrigée(s) sur 2", vm.ClassSummary);

        var csv = TempFile(".csv");
        dialogs.SavePath = csv;
        await vm.ExportGradesCommand.ExecuteAsync(null);
        Assert.StartsWith("Apprenant;", File.ReadAllText(csv).TrimStart('﻿'));

        var report = TempFile(".md");
        dialogs.SavePath = report;
        await vm.ExportClassReportCommand.ExecuteAsync(null);
        Assert.Contains("| Camille |", File.ReadAllText(report));

        // Un autre exercice : la liste est vidée
        await vm.OpenPathAsync(SavedRecipeLab("PWD-03"));
        Assert.Empty(vm.Submissions);
        Assert.False(vm.ExportGradesCommand.CanExecute(null));
    }

    [Fact]
    public async Task Submissions_FromAFolder_AndRegradedAfterChangingPoints()
    {
        var labPath = SavedRecipeLab();
        var folder = TempDir();
        Submission(labPath, "Camille", DisableGuest, folder);
        Submission(labPath, "Lucas", folder: folder);
        var (vm, dialogs) = NewStudio();
        await vm.OpenPathAsync(labPath);

        dialogs.Folder = folder;
        await vm.AddSubmissionFolderCommand.ExecuteAsync(null);
        Assert.Equal(2, vm.Submissions.Count);

        vm.Objectives[0].Points += 5;
        vm.RegradeSubmissionsCommand.Execute(null);
        Assert.Equal($"{vm.TotalPoints} / {vm.TotalPoints}", vm.Submissions.Single(s => s.Student == "Camille").Grade);
    }

    [Fact]
    public void Cli_Grade_PrintsTheGrades_AndWritesTheFiles()
    {
        var labPath = SavedRecipeLab();
        var folder = TempDir();
        Submission(labPath, "Camille", DisableGuest, folder);
        Submission(labPath, "Lucas", folder: folder);
        var csv = Path.Combine(TempDir(), "notes.csv");
        var report = Path.Combine(TempDir(), "bilan.md");

        var (code, output, error) = Run("grade", labPath, folder, "--lang", "fr", "--csv", csv, "--report", report);

        Assert.True(code == 0, output + error);
        Assert.Contains("Camille", output);
        Assert.Contains("2/2 graded", output);
        Assert.True(File.Exists(csv));
        Assert.Contains("Réussite par objectif", File.ReadAllText(report));

        // Une copie d'un autre exercice ne peut pas être corrigée
        var (wrong, wrongOutput, _) = Run("grade", labPath, Submission(SavedRecipeLab("PWD-03"), "Inès"));
        Assert.Equal(1, wrong);
        Assert.Contains("error", wrongOutput);
    }

    [Fact]
    public void Cli_Exam_MarksTheGeneratedLab()
    {
        var dir = TempDir();
        var (code, output, error) = Run("recipe", "ACC-01", "--exam", "--lang", "fr", "--out", dir);
        Assert.True(code == 0, output + error);

        // L'exemplaire du formateur, avec ses indices, et le sujet à distribuer, sans eux : même nom, autre dossier
        var trainer = new LabService().LoadFile(Path.Combine(dir, "anssi-acc-01", "1-formateur", "anssi-acc-01" + LabService.FileExtension));
        Assert.True(trainer.Exam);
        Assert.True(trainer.IsTrainerCopy);
        var subject = new LabService().LoadFile(Path.Combine(dir, "anssi-acc-01", "2-a-distribuer", "anssi-acc-01" + LabService.FileExtension));
        Assert.True(subject.Exam);
        Assert.False(subject.IsTrainerCopy);
    }

    [Fact]
    public void Cli_ExportSubject_AndGradeALabFolder_WriteEverythingInItsFolders()
    {
        var dir = TempDir();
        Assert.Equal(0, Run("recipe", "ACC-01", "--lang", "en", "--out", dir, "--date", "2026-10-22").Code);
        var project = LabProject.OfRoot(Path.Combine(dir, "anssi-acc-01"))!;
        var labPath = project.FindLab()!;
        var labs = new LabService();
        var lab = labs.LoadFile(labPath);
        lab.Exam = true;
        lab.Questions = [new LabQuestion { Id = "q1", Kind = LabQuestionKind.TrueFalse, Points = 2, Title = LocalizedText.Of("Invité désactivé ?", "Guest disabled?"),
                                           Choices = [new() { Id = "yes", Text = LocalizedText.Of("Oui", "Yes"), Correct = true }, new() { Id = "no", Text = LocalizedText.Of("Non", "No") }] }];
        labs.SaveFile(lab, labPath);

        var (code, output, _) = Run("export-subject", labPath, "--lang", "en");
        Assert.Equal(0, code);
        var subjectPath = project.SubjectPath(labPath);
        Assert.Contains(subjectPath, output);
        var subject = labs.LoadFile(subjectPath);
        Assert.False(subject.Questions.Single().HasAnswerKey);

        // Une copie déposée dans 3-submissions ; « grade <dossier> » écrit tous les résultats dans 4-results
        var session = LabSession.Begin(subject, new DateTime(2026, 10, 23));
        var submission = Path.Combine(project.SubmissionsFolder, "lou.smadx-submission.json");
        new LabSubmissionService().SaveFile(session.ToSubmission("Lou", session.NewWorkingCopy(), [], new DateTime(2026, 10, 23, 10, 0, 0),
            new Dictionary<string, LabAnswer> { ["q1"] = new() { Choices = ["yes"] } }, "lou@example.org"), submission);

        var graded = Run("grade", project.Root, "--scale", "10", "--lang", "en");
        Assert.Contains("lou@example.org,Lou,", File.ReadAllText(project.MoodlePath));
        Assert.True(File.Exists(project.GradesPath));
        Assert.True(File.Exists(project.ClassReportPath));
        Assert.Single(Directory.GetFiles(project.ReportsFolder, "*.pdf"));
        Assert.Contains(" +", graded.Out); // la question, juste

        // Des fichiers donnés à la main gardent la main
        var moodle = Path.Combine(dir, "moodle.csv");
        Run("grade", labPath, project.SubmissionsFolder, "--moodle", moodle, "--lang", "en");
        Assert.Contains("lou@example.org,Lou,", File.ReadAllText(moodle));
    }

    [Fact]
    public async Task StudioSavesALabFolder_ByRole_AndReopensItWithItsSubmissions()
    {
        var vm = await FromRecipe("ACC-01");
        vm.IsExam = true;
        var parent = TempDir();
        File.WriteAllText(Path.Combine(parent, "autre.txt"), "un dossier déjà occupé");

        var path = vm.SaveToFolder(parent);

        // Le dossier choisi n'est pas vide : l'exercice a son propre dossier, à son nom
        var project = LabProject.Of(path)!;
        Assert.Equal(Path.Combine(parent, vm.LabId), project.Root);
        Assert.Equal(project.Root, vm.Project!.Root);
        foreach (var file in new[] { project.NotesPath, project.SolutionPath, project.SheetPath, project.SubjectPath(path), project.ReadMePath })
            Assert.True(File.Exists(file), file);
        Assert.False(new LabService().LoadFile(project.SubjectPath(path)).IsTrainerCopy);
        Assert.Contains("2-a-distribuer", File.ReadAllText(project.ReadMePath));
        Assert.False(vm.HasUnsavedChanges);

        // Une copie rendue, déposée dans 3-copies-rendues : corrigée dès l'ouverture
        var subject = new LabService().LoadFile(project.SubjectPath(path));
        var session = LabSession.Begin(subject, Now);
        new LabSubmissionService().SaveFile(session.ToSubmission("Lou", session.NewWorkingCopy(), [], Now.AddMinutes(10)),
            Path.Combine(project.SubmissionsFolder, LabSubmissionService.SuggestedFileName(vm.LabId, "Lou")));
        var (other, dialogs) = NewStudio();
        dialogs.OpenPath = path;
        await other.OpenCommand.ExecuteAsync(null);
        Assert.True(other.HasFixedDomain);
        Assert.Single(other.Submissions);

        // Les exports vont dans 4-resultats, sans rien demander
        await other.ExportGradesCommand.ExecuteAsync(null);
        await other.ExportReportsCommand.ExecuteAsync(null);
        Assert.True(File.Exists(project.GradesPath));
        Assert.Single(Directory.GetFiles(project.ReportsFolder, "*.pdf"));

        // Une nouvelle copie déposée depuis : « Recorriger » la prend
        var second = LabSession.Begin(subject, Now);
        new LabSubmissionService().SaveFile(second.ToSubmission("Sam", second.NewWorkingCopy(), [], Now.AddMinutes(12)),
            Path.Combine(project.SubmissionsFolder, LabSubmissionService.SuggestedFileName(vm.LabId, "Sam")));
        other.RegradeSubmissionsCommand.Execute(null);
        Assert.Equal(2, other.Submissions.Count);

        // Ouvrir le sujet par erreur : Studio le signale
        var (third, thirdDialogs) = NewStudio();
        thirdDialogs.OpenPath = project.SubjectPath(path);
        await third.OpenCommand.ExecuteAsync(null);
        Assert.Equal(StudioText.Instance["Status.OpenedSubject"], third.Status);

        // Enregistrer de nouveau dans le même dossier : pas de sous-dossier en plus
        Assert.Equal(path, other.SaveToFolder(project.Root));
    }
}
