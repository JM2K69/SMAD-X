using SMADX.Models;
using SMADX.Services;
using SMADX.Services.Labs;
using SMADX.ViewModels;
using static SMADX.Tests.TestHelpers;

namespace SMADX.Tests;

public class LabViewModelTests
{
    private readonly FakeDialogService _dialogs = new();
    private readonly MainWindowViewModel _main;

    public LabViewModelTests()
    {
        TierConfigurationService.Instance.ResetToDefaults();
        _main = new MainWindowViewModel(_dialogs);
    }

    private LabViewModel Open(string id)
    {
        _main.OpenBuiltInLabCommand.Execute(_main.BuiltInLabs.Single(l => l.Lab.Id == id));
        return _dialogs.LastLab!;
    }

    [Fact]
    public void BuiltInLabs_AreListedForTheMenu()
    {
        Assert.Equal(5, _main.BuiltInLabs.Count);
        Assert.All(_main.BuiltInLabs, l => Assert.Contains(" — ", l.Title));
    }

    [Fact]
    public void OpeningALab_LoadsItsStartDomain()
    {
        var lab = Open("02-kerberoasting");

        Assert.Equal("contoso.local", _main.RootObject!.Name);
        Assert.Contains(Flatten(_main.RootObject), o => o.Name == "svc-sql");
        Assert.Equal(5, lab.Objectives.Count);
        Assert.Equal(9, lab.MaxPoints); // la garde « svc-sql existe » ne compte pas
    }

    [Fact]
    public void Check_GradesTheCurrentDomain_AndRestartResetsIt()
    {
        var lab = Open("05-hygiene-comptes");
        lab.CheckCommand.Execute(null);
        Assert.Equal(0, lab.Points);
        Assert.All(lab.Objectives, o => Assert.False(o.Passed));

        // L'apprenant désactive le compte Invité dans la fenêtre principale
        Flatten(_main.RootObject!).Single(o => o.Name == "Guest").AccountFlags |= ADAccountFlags.AccountDisabled;
        lab.CheckCommand.Execute(null);
        Assert.Equal(2, lab.Points);
        Assert.True(lab.Objectives.Single(o => o.Objective.Id == "o1").Passed);

        lab.RestartCommand.Execute(null);
        Assert.False(Flatten(_main.RootObject!).Single(o => o.Name == "Guest").AccountFlags.HasFlag(ADAccountFlags.AccountDisabled));
        Assert.Equal(0, lab.Points);
        Assert.All(lab.Objectives, o => Assert.Null(o.Passed));
    }

    [Fact]
    public async Task ExportResult_WritesAReportForTheTeacher()
    {
        var lab = Open("01-premier-audit");
        lab.StudentName = "Camille Dupont";
        lab.Objectives[0].ToggleHintCommand.Execute(null);
        var path = TempFile(".md");
        _dialogs.SavePath = path;

        await lab.ExportResultCommand.ExecuteAsync(null);

        var report = await File.ReadAllTextAsync(path);
        Assert.Contains("Camille Dupont", report);
        Assert.Contains("`01-premier-audit`", report);
        Assert.Contains("0 / 9", report);             // la garde, acquise au départ, ne rapporte rien
        Assert.Contains("✘", report);
        Assert.Contains(_dialogs.Opened, o => o.StartsWith("Save:01-premier-audit_Camille Dupont_"));
    }

    // ─── Questions et exemplaire du formateur ─────────────────────────────────

    private static LabDefinition HygieneWithQuestions(bool exam)
    {
        var lab = new LabService().BuiltInLabs().Single(l => l.Id == "05-hygiene-comptes");
        lab.Exam = exam;
        LabChoice Choice(string id, bool correct = false) => new() { Id = id, Text = LocalizedText.Of($"Choix {id}", $"Choice {id}"), Correct = correct };
        lab.Questions =
        [
            new() { Id = "q1", Kind = LabQuestionKind.SingleChoice, Points = 2, Title = LocalizedText.Of("Question 1", "Question 1"),
                    Choices = [Choice("a", correct: true), Choice("b")] },
            new() { Id = "q2", Kind = LabQuestionKind.Matching, Points = 2, Title = LocalizedText.Of("Question 2", "Question 2"),
                    Prompts = [new() { Id = "p1", Text = LocalizedText.Of("Un", "One"), Match = "a" }, new() { Id = "p2", Text = LocalizedText.Of("Deux", "Two"), Match = "b" }],
                    Choices = [Choice("a"), Choice("b")] },
            new() { Id = "q3", Kind = LabQuestionKind.Text, Points = 1, Title = LocalizedText.Of("Question 3", "Question 3"), AcceptedAnswers = ["Kerberoasting"] }
        ];
        return lab;
    }

    [Fact]
    public async Task TrainerCopyOfAnExam_IsRefused_ButItsSubjectOpens()
    {
        var service = new LabService();
        var lab = HygieneWithQuestions(exam: true);
        var trainer = TempFile(LabService.FileExtension);
        service.SaveFile(lab, trainer); // indices et corrigé compris

        await _main.OpenStartupFileAsync(trainer);
        Assert.Null(_dialogs.LastLab);
        Assert.Equal([LocalizationService.Instance["Lab.TrainerCopy.Message"]], _dialogs.Messages);

        var subject = TempFile(LabService.FileExtension);
        service.SaveSubject(lab, subject);
        await _main.OpenStartupFileAsync(subject);
        Assert.NotNull(_dialogs.LastLab);
        Assert.Equal(3, _dialogs.LastLab!.Questions.Count);
    }

    [Fact]
    public async Task Answers_AreSavedInTheSubmission_CountAsUnsavedWork_AndAreResumed()
    {
        var file = TempFile(LabService.FileExtension);
        new LabService().SaveSubject(HygieneWithQuestions(exam: true), file);
        await _main.OpenStartupFileAsync(file);
        var lab = _dialogs.LastLab!;
        lab.StudentName = "Lou";
        lab.StudentId = "e42";
        var path = TempFile(LabSubmissionService.FileExtension);
        _dialogs.SavePath = path;
        await lab.SaveSubmissionCommand.ExecuteAsync(null);
        Assert.False(_main.HasUnsavedChanges);

        // Choix unique : cocher un choix décoche l'autre
        lab.Questions[0].Choices[1].IsSelected = true;
        lab.Questions[0].Choices[0].IsSelected = true;
        Assert.False(lab.Questions[0].Choices[1].IsSelected);
        lab.Questions[1].Prompts[0].Selected = lab.Questions[1].Prompts[0].Options.Single(o => o.Id == "b");
        lab.Questions[2].Text = " Kerberoasting ";
        Assert.True(_main.HasUnsavedChanges);

        await lab.SaveSubmissionCommand.ExecuteAsync(null);
        var saved = new LabSubmissionService().LoadFile(path);
        Assert.Equal(["a"], saved.Answers["q1"].Choices);
        Assert.Equal("b", saved.Answers["q2"].Matches!["p1"]);
        Assert.Equal("Kerberoasting", saved.Answers["q3"].Text);
        Assert.Equal("e42", saved.StudentId);
        Assert.DoesNotContain(saved.Lab.Questions, q => q.HasAnswerKey); // le sujet, sans corrigé

        var dialogs = new FakeDialogService();
        var next = new MainWindowViewModel(dialogs) { RecoveryFolder = _main.RecoveryFolder };
        await next.OpenStartupFileAsync(path);
        var resumed = dialogs.LastLab!;
        Assert.True(resumed.Questions[0].Choices[0].IsSelected);
        Assert.Equal("b", resumed.Questions[1].Prompts[0].Selected!.Id);
        Assert.Equal("Kerberoasting", resumed.Questions[2].Text);
        Assert.Equal("e42", resumed.StudentId);
        Assert.False(next.HasUnsavedChanges);
    }

    [Fact]
    public async Task Check_GradesTheQuestions_OutsideAnExamOnly()
    {
        var file = TempFile(LabService.FileExtension);
        new LabService().SaveFile(HygieneWithQuestions(exam: false), file);
        await _main.OpenStartupFileAsync(file);
        var lab = _dialogs.LastLab!;
        lab.Questions[0].Choices[0].IsSelected = true;
        lab.Questions[1].Prompts[0].Selected = lab.Questions[1].Prompts[0].Options.Single(o => o.Id == "a");
        lab.Questions[2].Text = "autre chose";

        lab.CheckCommand.Execute(null);

        Assert.True(lab.Questions[0].Passed);
        Assert.False(lab.Questions[1].Passed);
        Assert.False(lab.Questions[2].Passed);
        Assert.Equal(2, lab.Points); // q1 seulement : aucun objectif atteint
        Assert.Equal(lab.Lab.ObjectivePoints + 5, lab.MaxPoints);
    }

    [Fact]
    public async Task OpeningAnInvalidLabFile_ExplainsWhy()
    {
        var path = TempFile(LabService.FileExtension);
        await File.WriteAllTextAsync(path, """{ "Version": 2, "Domain": {} }""");
        _dialogs.OpenPath = path;

        await _main.OpenLabFileCommand.ExecuteAsync(null);

        Assert.Contains("smadx-lab", _main.StatusMessage);
        Assert.Null(_dialogs.LastLab);
    }

    // ─── Mode exercice et mode examen ────────────────────────────────────────

    private static string LabFile(string id, bool exam = false, Action<LabDefinition>? settings = null)
    {
        var service = new LabService();
        var lab = service.BuiltInLabs().Single(l => l.Id == id);
        lab.Exam = exam;
        settings?.Invoke(lab);
        var path = TempFile(LabService.FileExtension);
        service.SaveSubject(lab, path); // le fichier distribué
        return path;
    }

    [Fact]
    public void BuiltInLab_FromTheMenu_StaysInFreeMode()
    {
        Open("02-kerberoasting");

        Assert.False(_main.IsLabMode);
        Assert.True(_main.LoadFromFileCommand.CanExecute(null));
        Assert.Equal("SMAD-X", _main.WindowTitle);
        Assert.True(_main.HasCurrentLab);
    }

    [Fact]
    public async Task StartupLabFile_OpensLabMode_WhichLocksTheDomain()
    {
        await _main.OpenStartupFileAsync(LabFile("05-hygiene-comptes"));

        Assert.True(_main.IsLabMode);
        Assert.NotNull(_dialogs.LastLab);
        Assert.Contains(_dialogs.LastLab!.Title, _main.WindowTitle);
        foreach (var command in new System.Windows.Input.ICommand[] { _main.CreateNewDomainCommand, _main.InitializeSampleDataCommand,
                     _main.LoadFromFileCommand, _main.OpenLabFileCommand, _main.OpenBuiltInLabCommand })
            Assert.False(command.CanExecute(null));
        Assert.True(_main.SaveSubmissionCommand.CanExecute(null));

        Assert.True(_main.LeaveLabModeCommand.CanExecute(null));
        _main.LeaveLabModeCommand.Execute(null);
        Assert.False(_main.IsLabMode);
        Assert.True(_main.LoadFromFileCommand.CanExecute(null));
    }

    [Fact]
    public async Task ExamLab_HasNoHintsNoCheckingNoRestart_AndCannotBeLeft()
    {
        _dialogs.OpenPath = LabFile("05-hygiene-comptes", exam: true);
        await _main.OpenLabFileCommand.ExecuteAsync(null); // même ouvert depuis le menu

        var lab = _dialogs.LastLab!;
        Assert.True(_main.IsLabMode);
        Assert.True(lab.IsExam);
        Assert.Equal(string.Format(LocalizationService.Instance["Lab.Mode.ExamTitle"], lab.Title), _main.WindowTitle);
        Assert.False(lab.CheckCommand.CanExecute(null));
        Assert.False(lab.RestartCommand.CanExecute(null));
        Assert.False(lab.ExportResultCommand.CanExecute(null));
        Assert.All(lab.Objectives, o => Assert.False(o.CanShowHint));
        lab.Objectives[0].ToggleHintCommand.Execute(null);
        Assert.Equal(0, lab.HintsUsed);
        Assert.False(_main.CanLeaveLabMode);
        Assert.False(_main.LeaveLabModeCommand.CanExecute(null));
        Assert.False(_main.LoadFromFileCommand.CanExecute(null));
        // Tous les outils restent ouverts par défaut
        Assert.True(_main.ShowAuditCommand.CanExecute(null));
        Assert.True(_main.ShowAttackPathsCommand.CanExecute(null));
        Assert.True(_main.ShowComparisonCommand.CanExecute(null));
    }

    // ─── Travail non enregistré et copie de secours ─────────────────────────

    private static void DisableGuest(MainWindowViewModel main) =>
        Flatten(main.RootObject!).Single(o => o.Name == "Guest").AccountFlags |= ADAccountFlags.AccountDisabled;

    [Fact]
    public async Task LabMode_AutoSave_WritesARecoveryCopy_OfferedAtTheNextOpening()
    {
        var file = LabFile("05-hygiene-comptes", exam: true);
        await _main.OpenStartupFileAsync(file);
        _dialogs.LastLab!.StudentName = "Lou";
        DisableGuest(_main);
        Assert.True(_main.HasUnsavedChanges);

        _main.AutoSave();
        Assert.Single(Directory.GetFiles(_main.RecoveryFolder));

        // SMAD-X s'est mal fermé : la réouverture propose de reprendre le travail
        var dialogs = new FakeDialogService { ConfirmAnswer = true };
        var next = new MainWindowViewModel(dialogs) { RecoveryFolder = _main.RecoveryFolder };
        await next.OpenStartupFileAsync(file);

        Assert.Single(dialogs.Confirmations);
        Assert.True(Flatten(next.RootObject!).Single(o => o.Name == "Guest").AccountFlags.HasFlag(ADAccountFlags.AccountDisabled));
        Assert.Equal("Lou", dialogs.LastLab!.StudentName);
        Assert.Equal(_dialogs.LastLab.SessionId, dialogs.LastLab.SessionId);
        Assert.True(next.HasUnsavedChanges); // pas encore dans la copie de l'apprenant
    }

    [Fact]
    public async Task ARecoveryCopy_IsDeleted_WhenDeclinedOrOnceTheSubmissionIsSaved()
    {
        var file = LabFile("05-hygiene-comptes");
        await _main.OpenStartupFileAsync(file);
        DisableGuest(_main);
        _main.AutoSave();

        var dialogs = new FakeDialogService { ConfirmAnswer = false };
        var declined = new MainWindowViewModel(dialogs) { RecoveryFolder = _main.RecoveryFolder };
        await declined.OpenStartupFileAsync(file);
        Assert.Empty(Directory.GetFiles(_main.RecoveryFolder));

        DisableGuest(declined);
        declined.AutoSave();
        Assert.Single(Directory.GetFiles(_main.RecoveryFolder));
        dialogs.LastLab!.StudentName = "Lou";
        dialogs.SavePath = TempFile(LabSubmissionService.FileExtension);
        await dialogs.LastLab.SaveSubmissionCommand.ExecuteAsync(null);

        Assert.Empty(Directory.GetFiles(_main.RecoveryFolder));
        Assert.False(declined.HasUnsavedChanges);
    }

    [Fact]
    public async Task ClosingWithAnUnsavedSubmission_AsksForTheName_ThenSaves()
    {
        await _main.OpenStartupFileAsync(LabFile("05-hygiene-comptes"));
        DisableGuest(_main);
        _dialogs.UnsavedAnswer = UnsavedChoice.Save;

        Assert.False(await _main.ConfirmCloseAsync()); // pas encore de nom : la fenêtre de l'exercice s'ouvre
        Assert.Equal(LocalizationService.Instance["Lab.Submission.NeedName"], _main.StatusMessage);

        _dialogs.LastLab!.StudentName = "Lou";
        _dialogs.SavePath = TempFile(LabSubmissionService.FileExtension);
        Assert.True(await _main.ConfirmCloseAsync());
        Assert.True(File.Exists(_dialogs.SavePath));
    }

    [Fact]
    public async Task ExamTools_ClosedByTheTrainer_CannotBeOpened()
    {
        await _main.OpenStartupFileAsync(LabFile("05-hygiene-comptes", exam: true,
            l => l.ExamClosedTools = LabTools.AttackPaths | LabTools.Comparison | LabTools.FixGuidance));

        Assert.True(_main.ShowAuditCommand.CanExecute(null));
        Assert.False(_main.ShowAttackPathsCommand.CanExecute(null));
        Assert.False(_main.ShowComparisonCommand.CanExecute(null));
        Assert.False(_main.MarkBaselineCommand.CanExecute(null));

        // L'audit reste ouvert, mais ses fiches n'ont plus de section « Comment corriger »
        _main.ShowAuditCommand.Execute(null);
        var sheet = _dialogs.LastAudit!.Findings.First().Documentation;
        Assert.DoesNotContain("## Comment corriger", sheet);
        Assert.DoesNotContain("## How to fix", sheet);
        Assert.Contains("## ", sheet);
    }

    [Fact]
    public async Task ExamCheck_ShowsTheObjectivesMet_WithoutScoreNorExplanation()
    {
        await _main.OpenStartupFileAsync(LabFile("05-hygiene-comptes", exam: true, l => l.ExamCheck = true));
        var lab = _dialogs.LastLab!;

        Assert.True(lab.CanCheck);
        Assert.False(lab.ShowGrading);
        lab.CheckCommand.Execute(null);

        Assert.All(lab.Objectives, o => Assert.NotNull(o.Passed));
        Assert.All(lab.Objectives, o => Assert.Empty(o.Feedback));
    }

    [Fact]
    public void ElapsedTime_IsShownToTheLearner()
    {
        var lab = Open("02-kerberoasting");

        Assert.Equal(string.Format(LocalizationService.Instance["Lab.Elapsed"], lab.ElapsedMinutes, lab.Lab.DurationMinutes), lab.ElapsedLabel);
        Assert.False(lab.IsOverTime);
    }

    [Fact]
    public async Task SaveSubmission_NeedsAName_ThenRewritesTheSameFile()
    {
        var lab = Open("05-hygiene-comptes");
        Assert.False(lab.SaveSubmissionCommand.CanExecute(null));

        lab.StudentName = "Camille Dupont";
        var path = TempFile(LabSubmissionService.FileExtension);
        _dialogs.SavePath = path;
        await lab.SaveSubmissionCommand.ExecuteAsync(null);
        Assert.True(File.Exists(path));
        Assert.Contains(_dialogs.Opened, o => o == "Save:05-hygiene-comptes_Camille Dupont.smadx-submission.json");
        Assert.Contains(path, lab.SubmissionStatus);

        Flatten(_main.RootObject!).Single(o => o.Name == "Guest").AccountFlags |= ADAccountFlags.AccountDisabled;
        _dialogs.SavePath = null; // le deuxième enregistrement ne redemande pas le fichier
        await lab.SaveSubmissionCommand.ExecuteAsync(null);

        var submission = new LabSubmissionService().LoadFile(path);
        Assert.Equal("Camille Dupont", submission.Student);
        Assert.True(Flatten(submission.Work.Domain).Single(o => o.Name == "Guest").AccountFlags.HasFlag(ADAccountFlags.AccountDisabled));
    }

    [Fact]
    public async Task StartupSubmission_ResumesTheWork_TheNameAndTheHints()
    {
        var lab = Open("05-hygiene-comptes");
        lab.StudentName = "Camille Dupont";
        lab.Objectives[1].ToggleHintCommand.Execute(null);
        lab.Objectives[1].ToggleHintCommand.Execute(null); // masqué ensuite : il compte quand même
        Assert.Equal(1, lab.HintsUsed);
        Flatten(_main.RootObject!).Single(o => o.Name == "Guest").AccountFlags |= ADAccountFlags.AccountDisabled;
        var path = TempFile(LabSubmissionService.FileExtension);
        _dialogs.SavePath = path;
        await lab.SaveSubmissionCommand.ExecuteAsync(null);

        var dialogs = new FakeDialogService();
        var other = new MainWindowViewModel(dialogs);
        await other.OpenStartupFileAsync(path);

        var resumed = dialogs.LastLab!;
        Assert.True(other.IsLabMode);
        Assert.Equal("Camille Dupont", resumed.StudentName);
        Assert.Equal(1, resumed.HintsUsed);
        Assert.True(Flatten(other.RootObject!).Single(o => o.Name == "Guest").AccountFlags.HasFlag(ADAccountFlags.AccountDisabled));
        resumed.CheckCommand.Execute(null);
        Assert.Equal(2, resumed.Points);
        Assert.Contains("Camille Dupont", other.StatusMessage);

        // « Enregistrer ma copie » réécrit le fichier repris, sans redemander, scellé et dans la même session
        var session = new LabSubmissionService().LoadFile(path).SessionId;
        Assert.NotEqual(Guid.Empty, session);
        dialogs.SavePath = null;
        await resumed.SaveSubmissionCommand.ExecuteAsync(null);
        Assert.Contains(path, resumed.SubmissionStatus);
        var resaved = new LabSubmissionService().LoadFile(path);
        Assert.Equal(SubmissionSeal.Valid, resaved.SealCheck);
        Assert.Equal(session, resaved.SessionId);
        Assert.Empty(SubmissionGrader.Grade(new LabService().BuiltInLabs().Single(l => l.Id == "05-hygiene-comptes"), path, "fr-FR").Issues);
    }

    [Fact]
    public async Task StartupDomainFile_OpensItInFreeMode()
    {
        var path = TempFile(".smad-x.json");
        await new ADDataService().SaveDocumentAsync(new ADRootDocument { Version = ADRootDocument.CurrentVersion, Domain = BuildSmallDomain() }, path);

        await _main.OpenStartupFileAsync(path);

        Assert.False(_main.IsLabMode);
        Assert.Equal(BuildSmallDomain().Name, _main.RootObject!.Name);
    }
}

public class DelegationEditingTests
{
    [Fact]
    public void AddAndRemoveDelegation_OnTheSelectedOu()
    {
        var domain = BuildSmallDomain();
        var vm = new DelegationsViewModel();
        vm.LoadFromTree(domain);
        var corp = Find(domain, "Corp");

        Assert.False(vm.AddDelegationCommand.CanExecute(null)); // ni OU ni mandataire
        vm.SelectedOUNode = corp;
        vm.NewTrustee = "Helpdesk";
        vm.NewCategory = RightCategory.PasswordReset;
        vm.AddDelegationCommand.Execute(null);

        var delegation = Assert.Single(corp.Delegations);
        Assert.Equal("ResetPassword", delegation.Right);
        Assert.Equal(TrusteeType.Group, delegation.TrusteeType);
        Assert.Equal(corp.DistinguishedName, delegation.TargetDN);
        Assert.Contains(delegation, vm.FilteredDelegations);
        Assert.Contains("Helpdesk", vm.Principals);

        vm.SelectedDelegation = delegation;
        vm.RemoveDelegationCommand.Execute(null);
        Assert.Empty(corp.Delegations);
        Assert.DoesNotContain(delegation, vm.AllDelegations);
    }

    [Fact]
    public void NewCategories_HaveTheirImportName_AndOldImportsAreCountedAndFilteredByTheirEffectiveCategory()
    {
        var domain = BuildSmallDomain();
        var vm = new DelegationsViewModel();
        vm.LoadFromTree(domain);
        vm.SelectedOUNode = domain;
        vm.NewTrustee = "Helpdesk";
        vm.NewCategory = RightCategory.Replication;
        vm.AddDelegationCommand.Execute(null);
        Assert.Equal("DS-Replication-Get-Changes-All", Assert.Single(domain.Delegations).Right);

        // Importé avant la 0.11 : WriteDacl rangé sous le contrôle total
        Find(domain, "Corp").Delegations.Add(new ADDelegation { TrusteeName = "jdoe", Right = "WriteDacl", RightCategory = RightCategory.FullControl });
        vm.LoadFromTree(domain);
        Assert.Equal(2, vm.CountSensitive);
        Assert.Equal(0, vm.CountFullControl);
        vm.FilterCategory = nameof(RightCategory.WriteDacl);
        vm.ApplyFiltersCommand.Execute(null);
        Assert.Equal("jdoe", Assert.Single(vm.FilteredDelegations).TrusteeName);

        // Chaque catégorie a son libellé, dans les deux langues
        var loc = LocalizationService.Instance;
        var previous = loc.CurrentCulture.Name;
        try
        {
            foreach (var culture in new[] { "fr-FR", "en-US" })
            {
                loc.SetLanguage(culture);
                Assert.All(Enum.GetNames<RightCategory>(), c => Assert.DoesNotContain("[Delegation", loc[$"Delegation.Category.{c}"]));
            }
        }
        finally
        {
            loc.SetLanguage(previous);
        }
    }

    [Fact]
    public void InheritedDelegation_IsNotRemovedHere()
    {
        var domain = BuildSmallDomain();
        var corp = Find(domain, "Corp");
        var inherited = new ADDelegation { TrusteeName = "Helpdesk", TargetDN = corp.DistinguishedName, Right = "GenericAll", RightCategory = RightCategory.FullControl, IsInherited = true };
        corp.Delegations.Add(inherited);
        var vm = new DelegationsViewModel();
        vm.LoadFromTree(domain);

        vm.SelectedDelegation = inherited;
        vm.RemoveDelegationCommand.Execute(null);

        Assert.Single(corp.Delegations);
    }
}
