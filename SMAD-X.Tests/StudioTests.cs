using SMADX.Models;
using SMADX.Services;
using SMADX.Services.Labs;
using SMADX.Studio;
using SMADX.Studio.ViewModels;
using static SMADX.Tests.TestHelpers;

namespace SMADX.Tests;

/// <summary>SMAD-X Studio : la fenêtre d'édition (ViewModel) et la ligne de commande.</summary>
[Collection(StudioTextCollection.Name)]
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
        public Task<string?> PickFolderAsync(string title, string? startFolder = null)
        {
            StartFolders.Add(startFolder);
            return Task.FromResult(Folder);
        }
        public List<string?> StartFolders { get; } = new();
        public List<string> ShownFolders { get; } = new();
        public Task ShowFolderAsync(string folder) { ShownFolders.Add(folder); return Task.CompletedTask; }
        public Task<string?> SaveFileAsync(string title, string suggestedName, string filterName, string pattern) => Task.FromResult(SavePath);
        public Task<RecipeRequest?> ChooseRecipeAsync() => Task.FromResult(Recipe);
        public Task<SmarxRecipeRequest?> ChooseSmarxRecipeAsync() => Task.FromResult<SmarxRecipeRequest?>(null);
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

        // Le domaine de l'exercice est celui d'une version de Windows Server, 2022 sauf choix contraire
        Assert.Equal(WindowsServerProfile.DefaultVersion, picker.Request()!.Version);
        picker.Version = picker.Versions.Single(v => v.Value == WindowsServerVersion.Server2019);
        Assert.Equal(WindowsServerVersion.Server2019, picker.Request()!.Version);
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
        var (code, output, error) = Run("recipe", "KRB-01", "DLG-02", "--lang", "en", "--domain", "cli.example.lab", "--server", "2025", "--out", dir, "--date", "2026-10-22");
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
        Assert.Equal(WindowsServerVersion.Server2025, loaded.Start.Domain.ServerVersion);

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
    [InlineData("recipe", "KRB-01", "--server", "2016")]
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

        // La copie d'un exercice du formateur (pas fourni avec SMAD-X) : la barre d'état dit lequel ouvrir
        await vm.OpenPathAsync(Submission(labPath, "Camille"));
        Assert.False(vm.HasLab);
        Assert.Equal(string.Format(StudioText.Instance["Submissions.UnknownLab"], new LabService().LoadFile(labPath).Id), vm.Status);

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

    [Fact]
    public async Task ANewLab_IsLaidOutByRoleAtOnce_AndEveryStepWritesInItsFolder()
    {
        var settings = StudioSettings.DefaultPath;
        StudioSettings.DefaultPath = Path.Combine(TempDir(), "settings.json");
        try
        {
            var parent = TempDir();
            File.WriteAllText(Path.Combine(parent, "autres-cours.txt"), "un dossier d'exercices déjà occupé");
            var (vm, dialogs) = NewStudio();
            dialogs.Folder = parent;
            dialogs.Recipe = new RecipeRequest([LabRecipes.All.Single(r => r.RuleId == "ACC-01")], "fr-FR", "studio.local");

            // Créer : le dossier de l'exercice, ses quatre dossiers par rôle et tous ses fichiers, sans « Enregistrer »
            await vm.NewFromRecipeCommand.ExecuteAsync(null);
            var project = vm.Project!;
            Assert.Equal(Path.Combine(parent, vm.LabId), project.Root);
            Assert.Equal(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), dialogs.StartFolders[0]);
            foreach (var folder in new[] { project.TrainerFolder, project.HandOutFolder, project.SubmissionsFolder, project.ResultsFolder })
                Assert.True(Directory.Exists(folder), folder);
            foreach (var file in new[] { vm.FilePath!, project.SolutionPath, project.SheetPath, project.SubjectPath(vm.FilePath!), project.ReadMePath })
                Assert.True(File.Exists(file), file);
            Assert.False(vm.HasUnsavedChanges);
            Assert.Equal(string.Format(StudioText.Instance["Status.CreatedProject"], project.Root), vm.Status);

            // Le sujet et la fiche vont à leur place, sans fenêtre d'enregistrement
            vm.IsExam = true;
            await vm.ExportSubjectCommand.ExecuteAsync(null);
            Assert.False(new LabService().LoadFile(project.SubjectPath(vm.FilePath!)).IsTrainerCopy);
            Assert.True(new LabService().LoadFile(vm.FilePath!).Exam); // l'exemplaire, enregistré avec
            vm.Notes = "Désactiver Guest.";
            await vm.ExportSheetCommand.ExecuteAsync(null);
            Assert.Contains("Désactiver Guest.", File.ReadAllText(project.SheetPath));
            Assert.False(vm.HasUnsavedChanges);

            // Les copies reçues d'ailleurs sont rangées dans 3-copies-rendues, une seule fois ; les notes à la main à côté
            var usb = TempDir();
            var lou = Submission(project.SubjectPath(vm.FilePath!), "Lou", folder: usb);
            dialogs.OpenPaths.Add(lou);
            await vm.AddSubmissionsCommand.ExecuteAsync(null);
            await vm.AddSubmissionsCommand.ExecuteAsync(null);
            var filed = Path.Combine(project.SubmissionsFolder, Path.GetFileName(lou));
            Assert.Equal(filed, Assert.Single(vm.Submissions).Graded.Path);
            Assert.Single(project.SubmissionFiles());
            vm.SelectedSubmission = vm.Submissions[0];
            vm.Comment = "Bien.";
            vm.SaveMarksCommand.Execute(null);
            Assert.True(File.Exists(SubmissionMarksService.MarksPath(filed)));

            // Un téléchargement Moodle : un dossier par apprenant, des copies du même nom
            var moodle = TempDir();
            foreach (var student in new[] { "Sam", "Noa" })
            {
                var copy = Submission(project.SubjectPath(vm.FilePath!), student, folder: Directory.CreateDirectory(Path.Combine(moodle, student)).FullName);
                File.Move(copy, Path.Combine(Path.GetDirectoryName(copy)!, "copie" + LabSubmissionService.FileExtension));
            }
            dialogs.Folder = moodle;
            await vm.AddSubmissionFolderCommand.ExecuteAsync(null);
            Assert.Equal(3, project.SubmissionFiles().Count);
            Assert.True(File.Exists(Path.Combine(project.SubmissionsFolder, "Noa", "copie" + LabSubmissionService.FileExtension)));
            Assert.Equal(3, vm.Submissions.Count);

            // Les résultats vont dans 4-resultats
            await vm.ExportGradesCommand.ExecuteAsync(null);
            Assert.True(File.Exists(project.GradesPath));

            // Renommé, l'exercice change de nom de fichier et de dossier ; ses copies le suivent
            vm.LabId = "revue-comptes";
            await vm.SaveCommand.ExecuteAsync(null);
            var renamed = vm.Project!;
            Assert.Equal(Path.Combine(parent, "revue-comptes"), renamed.Root);
            Assert.False(Directory.Exists(project.Root));
            Assert.Equal(renamed.LabPath("revue-comptes"), vm.FilePath);
            Assert.Single(Directory.GetFiles(renamed.TrainerFolder, "*" + LabService.FileExtension));
            Assert.Single(Directory.GetFiles(renamed.HandOutFolder, "*" + LabService.FileExtension));
            Assert.Equal(3, vm.Submissions.Count);
            Assert.All(vm.Submissions, s => Assert.StartsWith(renamed.Root, s.Graded.Path));

            // Le dossier de l'exercice s'ouvre dans l'explorateur
            await vm.ShowProjectFolderCommand.ExecuteAsync(null);
            Assert.Equal(renamed.Root, Assert.Single(dialogs.ShownFolders));

            // L'exercice suivant est proposé au même endroit, sans écraser un exercice du même nom
            dialogs.Folder = parent;
            var (next, nextDialogs) = NewStudio();
            nextDialogs.Folder = parent;
            nextDialogs.Recipe = dialogs.Recipe;
            await next.NewFromRecipeCommand.ExecuteAsync(null);
            Assert.Equal(parent, nextDialogs.StartFolders[0]);
            Assert.Equal(Path.Combine(parent, next.LabId), next.Project!.Root);
            var (third, thirdDialogs) = NewStudio();
            thirdDialogs.Folder = parent;
            thirdDialogs.Recipe = dialogs.Recipe;
            await third.NewFromRecipeCommand.ExecuteAsync(null);
            Assert.Equal(Path.Combine(parent, third.LabId + "-2"), third.Project!.Root);

            // Sans dossier choisi, l'exercice reste ouvert, à enregistrer plus tard : « Enregistrer » demande où
            var (later, laterDialogs) = NewStudio();
            laterDialogs.Recipe = dialogs.Recipe;
            await later.NewFromRecipeCommand.ExecuteAsync(null);
            Assert.Null(later.FilePath);
            Assert.Equal(StudioText.Instance["Status.CreatedNotSaved"], later.Status);
            laterDialogs.Folder = TempDir();
            await later.SaveCommand.ExecuteAsync(null);
            Assert.Equal(laterDialogs.Folder, later.Project!.Root);
        }
        finally
        {
            StudioSettings.DefaultPath = settings;
        }
    }

    [Fact]
    public async Task ALabFolder_OpensWithEverything_AndCopiesDroppedOnStudioAreFiledAndGraded()
    {
        var settings = StudioSettings.DefaultPath;
        StudioSettings.DefaultPath = Path.Combine(TempDir(), "settings.json");
        try
        {
            // Un exercice créé, rangé dans son dossier
            var (author, authorDialogs) = NewStudio();
            authorDialogs.Folder = TempDir();
            authorDialogs.Recipe = new RecipeRequest([LabRecipes.All.Single(r => r.RuleId == "ACC-01")], "fr-FR", "studio.local");
            await author.NewFromRecipeCommand.ExecuteAsync(null);
            var project = author.Project!;
            var subject = project.SubjectPath(author.FilePath!);

            // Le rouvrir par son dossier, ou par l'un de ses dossiers par rôle : l'exemplaire, la solution, les copies
            File.Copy(Submission(subject, "Lou"), Path.Combine(project.SubmissionsFolder, "lou" + LabSubmissionService.FileExtension));
            var (vm, dialogs) = NewStudio();
            dialogs.Folder = project.SubmissionsFolder;
            await vm.OpenFolderCommand.ExecuteAsync(null);
            Assert.Equal(author.FilePath, vm.FilePath);
            Assert.False(LabProject.IsHandOut(vm.FilePath!)); // l'exemplaire, pas le sujet
            Assert.True(vm.HasFixedDomain);
            Assert.Single(vm.Submissions);

            // Les copies déposées dans l'explorateur pendant que Studio est ouvert : corrigées au retour dans Studio
            File.Copy(Submission(subject, "Sam"), Path.Combine(project.SubmissionsFolder, "sam" + LabSubmissionService.FileExtension));
            vm.PickUpNewSubmissions();
            Assert.Equal(2, vm.Submissions.Count);

            // Des copies et un dossier de copies glissés sur la fenêtre : rangés dans 3-copies-rendues et corrigés
            var usb = TempDir();
            var noa = Submission(subject, "Noa", folder: usb);
            var moodle = Directory.CreateDirectory(Path.Combine(TempDir(), "devoir")).FullName;
            Submission(subject, "Eli", folder: Directory.CreateDirectory(Path.Combine(moodle, "Eli")).FullName);
            await vm.DropAsync([noa, moodle]);
            Assert.Equal(4, vm.Submissions.Count);
            Assert.Equal(4, project.SubmissionFiles().Count);
            Assert.True(File.Exists(Path.Combine(project.SubmissionsFolder, Path.GetFileName(noa))));

            // Un dossier sans exercice ni copie : Studio le dit
            var empty = TempDir();
            await vm.DropAsync([empty]);
            Assert.Equal(string.Format(StudioText.Instance["Status.NotALabFolder"], empty), vm.Status);
            await vm.OpenFolderPathAsync(empty);
            Assert.Equal(author.FilePath, vm.FilePath);

            // L'exercice est dans les récents de l'écran d'accueil : un clic le rouvre
            var (other, _) = NewStudio();
            var recent = Assert.Single(other.RecentLabs);
            Assert.Equal(project.Root, recent.Path);
            Assert.Equal(author.LabTitle, recent.Title);
            await other.OpenRecentCommand.ExecuteAsync(recent);
            Assert.Equal(author.FilePath, other.FilePath);

            // Le dossier glissé sur une fenêtre vide ouvre l'exercice ; avec un exercice modifié, Studio demande d'abord
            var (dropped, droppedDialogs) = NewStudio();
            await dropped.DropAsync([project.Root]);
            Assert.Equal(author.FilePath, dropped.FilePath);
            Assert.Equal(4, dropped.Submissions.Count);
            dropped.LabTitle = "Modifié";
            droppedDialogs.UnsavedAnswer = UnsavedChoice.Cancel;
            await dropped.OpenFolderPathAsync(project.Root);
            Assert.Equal("Modifié", dropped.LabTitle);
            droppedDialogs.UnsavedAnswer = UnsavedChoice.Discard;
            await dropped.OpenFolderPathAsync(project.Root);
            Assert.NotEqual("Modifié", dropped.LabTitle);
        }
        finally
        {
            StudioSettings.DefaultPath = settings;
        }
    }

    [Fact]
    public void LabProject_ImportSubmission_FilesOnce_AndNumbersOtherFilesOfTheSameName()
    {
        var project = LabProject.Create(TempDir(), "fr-FR");
        var a = Path.Combine(TempDir(), "copie" + LabSubmissionService.FileExtension);
        var b = Path.Combine(TempDir(), "copie" + LabSubmissionService.FileExtension);
        File.WriteAllText(a, "{\"a\":1}");
        File.WriteAllText(b, "{\"b\":2}");
        File.WriteAllText(SubmissionMarksService.MarksPath(b), "{}");

        var first = project.ImportSubmission(a);
        Assert.Equal(first, project.ImportSubmission(a));
        var second = project.ImportSubmission(b);
        Assert.Equal(Path.Combine(project.SubmissionsFolder, "copie (2)" + LabSubmissionService.FileExtension), second);
        Assert.True(File.Exists(SubmissionMarksService.MarksPath(second)));
        Assert.Equal(second, project.ImportSubmission(second)); // déjà rangée
        Assert.Equal(first, project.ImportSubmission(a, Path.Combine("..", "..", "ailleurs.json"))); // jamais hors du dossier
    }

    // ─── Exercices fournis avec SMAD-X : corriger leurs copies sans le dépôt ─

    /// <summary>
    /// Le fichier de l'exercice fourni dans le dépôt, celui que la ligne de commande corrige. Le dépôt se trouve depuis
    /// ce fichier source : les binaires des tests peuvent être compilés ailleurs.
    /// </summary>
    private static string RepoLab(string id, [System.Runtime.CompilerServices.CallerFilePath] string source = "")
    {
        for (var dir = new DirectoryInfo(Path.GetDirectoryName(source)!); dir != null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "SMAD-X.slnx")))
                return Path.Combine(dir.FullName, "SMAD-X.Core", "Labs", id + LabService.FileExtension);
        throw new DirectoryNotFoundException("SMAD-X.slnx introuvable au-dessus de " + source);
    }

    /// <summary>Copie d'un apprenant sur un exercice fourni, ouvert dans SMAD-X par son menu Exercices.</summary>
    private static string BuiltInSubmission(string labId, string student, string folder, Action<ADObject>? work = null, string? studentId = null, int minutes = 25)
    {
        var session = LabSession.Begin(new LabService().BuiltInLab(labId)!, Now);
        var document = session.NewWorkingCopy();
        work?.Invoke(document.Domain);
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, LabSubmissionService.SuggestedFileName(labId, student));
        new LabSubmissionService().SaveFile(session.ToSubmission(student, document, [], Now.AddMinutes(minutes), studentId: studentId), path);
        return path;
    }

    /// <summary>Une partie du travail : 03, la délégation du changement de mot de passe ; chemin-04, tout le chemin coupé.</summary>
    private static void PartOfTheWork(string labId, ADObject domain)
    {
        if (labId == "03-delegation-support")
        {
            var users = Flatten(domain).Single(o => o.Name == "Utilisateurs");
            users.Delegations.Add(new ADDelegation { TrusteeName = "GG-Helpdesk", TargetDN = users.DistinguishedName, Right = "ResetPassword", RightCategory = RightCategory.PasswordReset });
            return;
        }
        foreach (var d in domain.Delegations.Where(x => !x.IsInherited && x.RightCategory == RightCategory.FullControl && x.TrusteeName.StartsWith("GG-")).ToList())
            domain.Delegations.Remove(d);
    }

    [Fact]
    public void BuiltInLab_IsReadAfreshByItsId_AsCopiesNameIt()
    {
        var service = new LabService();
        foreach (var lab in service.BuiltInLabs())
        {
            var read = service.BuiltInLab(lab.Id);
            Assert.NotNull(read);
            Assert.Equal(lab.Id, read.Id);
            Assert.NotSame(read, service.BuiltInLab(lab.Id)); // modifiable sans toucher au suivant
        }
        Assert.Equal("chemin-04-write-members", service.BuiltInLab("CHEMIN-04-WRITE-MEMBERS")?.Id);
        Assert.Null(service.BuiltInLab("exercice-du-formateur"));
        Assert.Null(service.BuiltInLab(""));

        var folder = TempDir();
        Assert.Equal("03-delegation-support", LabSubmissionService.ReadLabId(BuiltInSubmission("03-delegation-support", "Lou", folder)));
        var notACopy = Path.Combine(folder, "page" + LabSubmissionService.FileExtension);
        File.WriteAllText(notACopy, "<html></html>");
        Assert.Null(LabSubmissionService.ReadLabId(notACopy));
        File.WriteAllText(notACopy, "{\"Format\":\"smadx-lab\",\"LabId\":\"03-delegation-support\"}");
        Assert.Null(LabSubmissionService.ReadLabId(notACopy));
        Assert.Null(LabSubmissionService.ReadLabId(Path.Combine(folder, "absente" + LabSubmissionService.FileExtension)));
    }

    [Fact]
    public async Task BuiltInLabs_AreListedLikeTheLabsMenuOfSmadX_AndOpenWithoutAFile()
    {
        var (vm, dialogs) = NewStudio();

        // « identifiant · titre », triés par identifiant : le début du nom des copies (03-delegation-support_Lou.smadx-submission.json)
        var general = vm.BuiltInLabs.Single(l => l.Id == "03-delegation-support");
        Assert.Equal("03-delegation-support · " + new LabService().BuiltInLab(general.Id)!.Title.Resolve("fr-FR"), general.Title);
        Assert.Equal(["01-premier-audit", "02-kerberoasting", "03-delegation-support", "04-cloisonnement", "05-hygiene-comptes"], vm.BuiltInLabs.Select(l => l.Id));
        Assert.Equal(Enumerable.Range(1, 15).Select(i => $"chemin-{i:00}-"), vm.FrenchAttackPathLabs.Select(l => l.Id[..10]));
        Assert.Equal(Enumerable.Range(1, 15).Select(i => $"path-{i:00}-"), vm.EnglishAttackPathLabs.Select(l => l.Id[..8]));

        await vm.OpenBuiltInLabCommand.ExecuteAsync(vm.FrenchAttackPathLabs.Single(l => l.Id == "chemin-04-write-members"));

        Assert.True(vm.HasLab, vm.Status);
        Assert.Equal("chemin-04-write-members", vm.LabId);
        Assert.Equal("fr-FR", vm.EditCulture);
        Assert.Null(vm.FilePath);
        Assert.False(vm.HasUnsavedChanges); // tel qu'il est fourni : fermer Studio ne demande rien
        Assert.Equal(string.Format(StudioText.Instance["Status.OpenedBuiltIn"], vm.LabTitle), vm.Status);
        Assert.True(vm.AddSubmissionsCommand.CanExecute(null));
        Assert.True(vm.AddSubmissionFolderCommand.CanExecute(null));

        // Modifié dans Studio, il ne change pas l'exercice fourni : le rouvrir rend l'original
        var points = vm.Objectives[0].Points;
        vm.Objectives[0].Points += 5;
        Assert.True(vm.HasUnsavedChanges);
        dialogs.UnsavedAnswer = UnsavedChoice.Discard;
        Assert.True(await vm.OpenBuiltInLabAsync("chemin-04-write-members"));
        Assert.Equal(points, vm.Objectives[0].Points);

        // Interface en anglais : les deux séries restent proposées (les copies sont dans la langue des apprenants),
        // et les exercices bilingues prennent leur titre anglais
        vm.SetLanguageCommand.Execute("en-US");
        Assert.Equal(15, vm.FrenchAttackPathLabs.Count);
        Assert.Equal(15, vm.EnglishAttackPathLabs.Count);
        Assert.Equal("03-delegation-support · " + new LabService().BuiltInLab("03-delegation-support")!.Title.Resolve("en-US"),
            vm.BuiltInLabs.Single(l => l.Id == "03-delegation-support").Title);
    }

    /// <summary>
    /// L'exercice fourni ouvert dans Studio corrige les copies comme <c>SMAD-X.Studio grade</c> avec le fichier du dépôt :
    /// même tableau des notes, case pour case.
    /// </summary>
    [Theory]
    [InlineData("03-delegation-support")]
    [InlineData("chemin-04-write-members")]
    public async Task BuiltInLab_GradesTheCopies_AsTheCommandLineDoesWithTheRepositoryFile(string id)
    {
        var folder = TempDir();
        BuiltInSubmission(id, "Camille", folder, d => PartOfTheWork(id, d));
        BuiltInSubmission(id, "Lucas", folder);
        var cli = Path.Combine(TempDir(), "cli.csv");
        var (_, output, error) = Run("grade", RepoLab(id), folder, "--lang", "fr", "--csv", cli);
        Assert.True(File.Exists(cli), output + error);

        var (vm, dialogs) = NewStudio();
        Assert.True(await vm.OpenBuiltInLabAsync(id), vm.Status);
        dialogs.Folder = folder;
        await vm.AddSubmissionFolderCommand.ExecuteAsync(null);
        var studio = Path.Combine(TempDir(), "studio.csv");
        dialogs.SavePath = studio;
        await vm.ExportGradesCommand.ExecuteAsync(null);

        Assert.Equal(File.ReadAllText(cli), File.ReadAllText(studio));
        Assert.Equal(2, vm.Submissions.Count(s => s.Graded.Graded));
        Assert.True(vm.Submissions.Single(s => s.Student == "Camille").Graded.Points > vm.Submissions.Single(s => s.Student == "Lucas").Graded.Points);
        Assert.Contains("Camille", output);
    }

    [Fact]
    public async Task CopiesOfABuiltInLab_DroppedOnAnEmptyStudio_OpenTheLab_AndAreGraded()
    {
        var folder = TempDir();
        var camille = BuiltInSubmission("chemin-04-write-members", "Camille", folder, d => PartOfTheWork("chemin-04-write-members", d));
        var lucas = BuiltInSubmission("chemin-04-write-members", "Lucas", folder);

        var (vm, _) = NewStudio();
        await vm.DropAsync([camille, lucas]);

        Assert.Equal("chemin-04-write-members", vm.LabId);
        Assert.Null(vm.FilePath);
        Assert.Equal(["Camille", "Lucas"], vm.Submissions.Select(s => s.Student));
        Assert.Equal($"{vm.TotalPoints} / {vm.TotalPoints}", vm.Submissions[0].Grade);
        Assert.Equal(camille, vm.Submissions[0].Graded.Path); // sans dossier d'exercice, les copies restent où elles sont
        Assert.Equal(string.Format(StudioText.Instance["Submissions.OpenedBuiltIn"], vm.LabTitle) + " " + string.Format(StudioText.Instance["Submissions.Graded"], 2), vm.Status);

        // Un dossier de copies, l'exercice d'une autre langue : il est ouvert aussi
        var english = TempDir();
        BuiltInSubmission("path-04-write-members", "Sam", english);
        var (other, _) = NewStudio();
        await other.DropAsync([english]);
        Assert.Equal("path-04-write-members", other.LabId);
        Assert.Single(other.Submissions);

        // Une copie donnée au lancement (SMAD-X.Studio copie.smadx-submission.json)
        var (started, _) = NewStudio();
        await started.OpenPathAsync(lucas);
        Assert.Equal("chemin-04-write-members", started.LabId);
        Assert.Single(started.Submissions);

        Assert.Equal(StudioViewModel.SubmissionsTab, started.SelectedTab); // déposer des copies montre l'onglet Copies
        Assert.False(started.HasBatch); // un seul exercice : pas de lot
    }

    [Fact]
    public async Task CopiesOfAnotherLab_DroppedOnAnEmptyStudio_SayWhichLabToOpen()
    {
        var labPath = SavedRecipeLab();
        var copy = Submission(labPath, "Camille");
        var (vm, _) = NewStudio();

        await vm.DropAsync([copy]);

        Assert.False(vm.HasLab);
        Assert.Equal(string.Format(StudioText.Instance["Submissions.UnknownLab"], new LabService().LoadFile(labPath).Id), vm.Status);

        // Un fichier qui n'est pas une copie : ouvrir d'abord l'exercice
        var page = Path.Combine(TempDir(), "page" + LabSubmissionService.FileExtension);
        File.WriteAllText(page, "<html></html>");
        await vm.DropAsync([page]);
        Assert.False(vm.HasLab);
        Assert.Equal(StudioText.Instance["Submissions.OpenLabFirst"], vm.Status);

        // Un exercice ouvert : les copies déposées s'y ajoutent, rien d'autre ne s'ouvre
        await vm.OpenPathAsync(labPath);
        await vm.DropAsync([BuiltInSubmission("chemin-04-write-members", "Lucas", TempDir())]);
        Assert.Equal(labPath, vm.FilePath);
        Assert.Contains("chemin-04-write-members", Assert.Single(vm.Submissions).Remarks);
    }

    /// <summary>
    /// Un dossier de devoirs : un sous-dossier par apprenant, une copie par exercice fourni, des comptes rendus .md et des
    /// captures à côté ; le nom saisi varie d'une copie à l'autre, l'identifiant beaucoup moins.
    /// </summary>
    private static string Homework()
    {
        var devoirs = TempDir();
        var andrea = Path.Combine(devoirs, "Andrea", "td4", "exercice");
        BuiltInSubmission("03-delegation-support", "Andrea Laurent", andrea, d => PartOfTheWork("03-delegation-support", d), "a.laurent");
        BuiltInSubmission("chemin-04-write-members", "Andrea laurent", andrea, d => PartOfTheWork("chemin-04-write-members", d), "a.laurent");
        var dimitri = Path.Combine(devoirs, "dimitri", "td4", "Exo");
        BuiltInSubmission("03-delegation-support", "Dimitri.JUNIQUE", dimitri, studentId: "d.junique");
        BuiltInSubmission("chemin-04-write-members", "Dimitri.JUNIQUE", dimitri, studentId: "D.junique");
        BuiltInSubmission("chemin-05-gpo", "Dimitri.JUNQIUE", dimitri, studentId: "D.junique");
        File.WriteAllText(Path.Combine(dimitri, "chemin-05-gpo_Dimitri.JUNQIUE_20261122_1301.md"), "# Compte rendu");
        Directory.CreateDirectory(Path.Combine(devoirs, "dimitri", "td4", "Images"));
        File.WriteAllBytes(Path.Combine(devoirs, "dimitri", "td4", "Images", "capture.png"), [0x89, 0x50, 0x4E, 0x47]);
        return devoirs;
    }

    [Fact]
    public async Task AHomeworkFolder_WithSeveralLabs_IsSortedByLab_GradedLabByLab_AndSummedUpPerLearner()
    {
        var devoirs = Homework();
        var (vm, dialogs) = NewStudio();

        await vm.DropAsync([devoirs]);

        // Rangées par exercice ; le premier exercice du lot s'ouvre avec ses seules copies, sous l'onglet Copies
        Assert.True(vm.HasBatch);
        Assert.Equal(["03-delegation-support (2)", "chemin-04-write-members (2)", "chemin-05-gpo (1)"], vm.BatchLabs.Select(l => l.Chip));
        Assert.Equal("03-delegation-support", vm.LabId);
        Assert.True(vm.BatchLabs[0].IsOpen);
        Assert.Equal(["Andrea Laurent", "Dimitri.JUNIQUE"], vm.Submissions.Select(s => s.Student));
        Assert.DoesNotContain(vm.Submissions, s => s.Remarks.Contains("chemin-")); // pas de « copie d'un autre exercice »
        Assert.Equal(StudioViewModel.SubmissionsTab, vm.SelectedTab);
        Assert.Equal(string.Format(StudioText.Instance["Batch.Opened"], 5, 3, "03-delegation-support", 2), vm.Status);

        // Un clic : l'exercice suivant, avec ses copies
        await vm.OpenBatchLabCommand.ExecuteAsync(vm.BatchLabs[1]);
        Assert.Equal("chemin-04-write-members", vm.LabId);
        Assert.Equal(2, vm.Submissions.Count);
        Assert.Equal($"{vm.TotalPoints} / {vm.TotalPoints}", vm.Submissions.Single(s => s.Student == "Andrea laurent").Grade);
        Assert.True(vm.BatchLabs[1].IsOpen);
        Assert.False(vm.BatchLabs[1].CanOpen);

        // Ouvert par le menu, un exercice du lot reçoit aussi ses copies
        Assert.True(await vm.OpenBuiltInLabAsync("chemin-05-gpo"));
        Assert.Equal("Dimitri.JUNQIUE", Assert.Single(vm.Submissions).Student);

        // Le bilan : une ligne par apprenant (son identifiant les réunit), une note par exercice, les deux moyennes
        var lines = vm.BatchCsv().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("Apprenant;Identifiant;03-delegation-support (sur 20);chemin-04-write-members (sur 20);chemin-05-gpo (sur 20);Exercices rendus;" +
                     "Moyenne des exercices rendus (sur 20);Moyenne sur les 3 exercices (sur 20);Remarques", lines[0]);
        Assert.Equal(3, lines.Length);
        var grades = new Dictionary<string, double>();
        foreach (var id in new[] { "03-delegation-support", "chemin-04-write-members", "chemin-05-gpo" })
            foreach (var row in SubmissionGrader.GradeAll(new LabService().BuiltInLab(id)!, Directory.GetFiles(devoirs, id + "_*" + LabSubmissionService.FileExtension, SearchOption.AllDirectories), "fr-FR"))
                grades[$"{row.StudentId.ToLowerInvariant()}|{id}"] = row.Scaled(20);
        string F(double value) => SubmissionGrader.Format(Math.Round(value, 2), System.Globalization.CultureInfo.GetCultureInfo("fr-FR"));
        var a = (grades["a.laurent|03-delegation-support"], grades["a.laurent|chemin-04-write-members"]);
        Assert.Equal($"Andrea Laurent;a.laurent;{F(a.Item1)};{F(a.Item2)};;2;{F((a.Item1 + a.Item2) / 2)};{F((a.Item1 + a.Item2) / 3)};", lines[1]);
        Assert.Equal(20, a.Item2);
        var d = new[] { "03-delegation-support", "chemin-04-write-members", "chemin-05-gpo" }.Select(id => grades["d.junique|" + id]).ToList();
        Assert.StartsWith($"Dimitri.JUNIQUE;d.junique;{F(d[0])};{F(d[1])};{F(d[2])};3;{F(d.Average())};{F(d.Sum() / 3)};", lines[2]);

        var csv = Path.Combine(TempDir(), "bilan.csv");
        dialogs.SavePath = csv;
        await vm.ExportBatchCommand.ExecuteAsync(null);
        Assert.Equal(vm.BatchCsv(), File.ReadAllText(csv).TrimStart('﻿'));

        // Un nouveau dépôt d'un seul exercice remplace le lot
        await vm.DropAsync([BuiltInSubmission("chemin-05-gpo", "Noa", TempDir())]);
        Assert.False(vm.HasBatch);
        Assert.Empty(vm.BatchLabs);
    }

    [Fact]
    public void SubmissionBatch_KeepsTheLatestCopy_AndSaysWhatToCheck()
    {
        var devoirs = TempDir();
        BuiltInSubmission("chemin-04-write-members", "Lou", Path.Combine(devoirs, "v1"), studentId: "lou", minutes: 10);
        BuiltInSubmission("chemin-04-write-members", "Lou", Path.Combine(devoirs, "v2"), d => PartOfTheWork("chemin-04-write-members", d), "LOU", minutes: 40);
        BuiltInSubmission("03-delegation-support", "Lou", devoirs, studentId: "lou");
        File.WriteAllText(Path.Combine(devoirs, "page" + LabSubmissionService.FileExtension), "<html></html>");

        var batch = new SubmissionBatch(Directory.GetFiles(devoirs, "*" + LabSubmissionService.FileExtension, SearchOption.AllDirectories));
        Assert.Equal(["03-delegation-support", "chemin-04-write-members"], batch.Labs.Select(l => l.LabId));
        Assert.Single(batch.Unreadable);
        Assert.Equal(3, batch.Count);

        var labs = batch.Labs.Select(l => new LabService().BuiltInLab(l.LabId)!)
            .Select(lab => new GradedBatchLab(lab, SubmissionGrader.GradeAll(lab, batch.Find(lab.Id)!.Paths, "en-US"))).ToList();
        var lines = SubmissionBatch.Csv(labs, "en-US").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.StartsWith("Learner,ID,03-delegation-support (out of 20),chemin-04-write-members (out of 20),Labs handed in,", lines[0]);
        var lou = Assert.Single(lines.Skip(1));
        Assert.Contains(",20,2,", lou); // la copie la plus récente (tout le chemin coupé) compte
        Assert.EndsWith("\"chemin-04-write-members: 2 submissions, the latest counts\"", lou); // entre guillemets : la virgule sépare les colonnes
    }

    // ─── Un domaine qui ne peut pas être lu : le message dit lequel, et pourquoi ─

    [Fact]
    public async Task AMissingDomain_GivesItsFullPath()
    {
        var (vm, _) = NewStudio();
        var path = Path.Combine(TempDir(), "absent.smad-x.json");
        await vm.NewFromDomainFileAsync(path);
        Assert.Equal($"Fichier introuvable : {path}. A-t-il été déplacé, renommé ou supprimé ?", vm.Status);
        Assert.Null(vm.Lab);
    }

    [Fact]
    public async Task AFileThatIsNotADomain_SaysSo_WithTheTechnicalDetail()
    {
        var (vm, _) = NewStudio();
        var path = Path.Combine(TempDir(), "page.smad-x.json");
        File.WriteAllText(path, "<html></html>");
        await vm.NewFromDomainFileAsync(path);
        Assert.StartsWith("page.smad-x.json n'est pas un domaine SMAD-X : choisissez un fichier .smad-x.json enregistré par SMAD-X. Détail : ", vm.Status);
        Assert.Null(vm.Lab);
    }

    /// <summary>« dotnet run .\SMAD-X.Studio.csproj » passe le projet à Studio : il n'est pas lu comme un domaine.</summary>
    [Fact]
    public async Task AStartupFileThatIsNeitherALabNorADomain_IsNotOpened()
    {
        var (vm, _) = NewStudio();
        var path = Path.Combine(TempDir(), "SMAD-X.Studio.csproj");
        File.WriteAllText(path, "<Project Sdk=\"Microsoft.NET.Sdk\"></Project>");
        await vm.OpenPathAsync(path);
        Assert.Equal($"Studio a été lancé avec « {path} », qui n'est ni un exercice, ni un dossier d'exercice, ni un domaine .smad-x.json : rien n'est ouvert.", vm.Status);
        Assert.Null(vm.Lab);
    }

    [Fact]
    public void LoadFailures_AreWordedInEnglish_ForTheCommandLine()
    {
        var path = Path.Combine("labs", "domain.smad-x.json");
        Assert.Equal($"File not found: {path}. Was it moved, renamed or deleted?",
            StudioViewModel.LoadFailureMessage(DocumentLoadResult.Failed(DocumentLoadStatus.NotFound), path, french: false));
        Assert.StartsWith("domain.smad-x.json comes from a newer version of SMAD-X (format v99;",
            StudioViewModel.LoadFailureMessage(DocumentLoadResult.Failed(DocumentLoadStatus.TooNew, 99), path, french: false));

        var dir = TempDir();
        var missing = Path.Combine(dir, "absent.smad-x.json");
        var (code, _, error) = Run("convert", missing, "--lang", "en", "--out", dir);
        Assert.Equal(1, code);
        Assert.Contains($"File not found: {missing}.", error);
    }
}
