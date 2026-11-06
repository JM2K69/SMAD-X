// Captures d'écran de SMAD-X Studio pour le site de documentation, en français et en anglais.
// Les fenêtres sont rendues hors écran (Avalonia Headless avec le moteur Skia) : rien ne s'affiche sur le bureau.
// L'exercice montré est créé ici à partir d'une recommandation, sur un domaine propre à la documentation ;
// la fiche formateur et les notes (la solution) ne sont jamais capturées. Les copies de l'onglet « Copies »
// sont celles d'apprenants fictifs, fabriquées ici ; la capture ne montre que les notes, pas les corrections.
// Usage : dotnet run --project tools/StudioDocGenerator -- <racine du dépôt>
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SMADX.Models;
using SMADX.Services;
using SMADX.Services.Labs;
using SMADX.Studio;
using SMADX.Studio.ViewModels;
using SMADX.Studio.Views;
using SMARX.Models;
using SMARX.Samples;
using SMARX.Services.Labs;

var repo = Path.GetFullPath(args.Length > 0 ? args[0] : ".");

AppBuilder.Configure<App>()
    .UseSkia()
    .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
    .SetupWithoutStarting();

// Les captures ne montrent ni ne modifient les exercices récents du formateur qui les génère
StudioSettings.DefaultPath = Path.Combine(Path.GetTempPath(), "smadx-studio-captures", "settings.json");
if (File.Exists(StudioSettings.DefaultPath)) File.Delete(StudioSettings.DefaultPath);

foreach (var (culture, lang) in new[] { ("fr-FR", "fr"), ("en-US", "en") })
{
    StudioText.Instance.Culture = culture;
    LocalizationService.Instance.SetLanguage(culture);
    TierConfigurationService.Instance.ResetToDefaults();
    var outDir = Path.Combine(repo, "docs", lang, "assets", "screens");
    Directory.CreateDirectory(outDir);
    string Out(string name) => Path.Combine(outDir, name + ".png");

    // ─── Accueil et choix de la recommandation ─────────────────────────────────
    var dialogs = new CaptureDialogs();
    var vm = new StudioViewModel(dialogs, () => new DateTime(2026, 10, 22, 9, 0, 0));
    var main = new MainWindow { DataContext = vm, Width = 1400, Height = 880 };
    main.Show();
    Capture(main, Out("studio-home"));

    // Trois recommandations combinées dans le même exercice
    var picker = new RecipePickerViewModel(new DateTime(2026, 10, 22));
    foreach (var id in new[] { "KRB-01", "DLG-02", "ACL-01" })
        picker.Recipes.First(r => r.RuleId == id).IsChecked = true;
    picker.Selected = picker.Recipes.First(r => r.RuleId == "KRB-01");
    picker.DomainName = T(culture, "demo-studio.local", "studio-demo.lab");
    Capture(new RecipeWindow { DataContext = picker, Width = 1000, Height = 640 }, Out("studio-recipe"));

    // ─── Exercice créé : énoncé, objectifs et propositions, validation ─────────
    dialogs.Recipe = picker.Request();
    Wait(vm.NewFromRecipeCommand.ExecuteAsync(null));
    vm.Status = StudioText.Instance["Status.Created"];

    Tab(main, 0);
    Capture(main, Out("studio-lab"));
    Tab(main, 1);
    Capture(main, Out("studio-statement"));
    Tab(main, 2);
    vm.SelectedObjective = vm.Objectives.FirstOrDefault(o => o.CheckType == LabCheckType.RuleClear) ?? vm.Objectives[0];
    Capture(main, Out("studio-objectives"));
    // Une garde retirée revient dans les propositions
    vm.SelectedObjective = vm.Objectives[^1];
    vm.RemoveObjectiveCommand.Execute(null);
    vm.SuggestCommand.Execute(null);
    Capture(main, Out("studio-suggestions"));
    vm.CloseSuggestionsCommand.Execute(null);

    // Deux questions de compréhension : un oui / non et une association
    Tab(main, 3);
    vm.AddQuestionCommand.Execute(null);
    var yesNo = vm.SelectedQuestion!;
    yesNo.Kind = LabQuestionKind.TrueFalse;
    yesNo.Title = T(culture, "Un compte de service peut rester membre de Domain Admins s'il a un mot de passe long.",
                             "A service account may stay a member of Domain Admins if its password is long.");
    yesNo.Choices[1].Correct = true;
    vm.AddQuestionCommand.Execute(null);
    var matching = vm.SelectedQuestion!;
    matching.Kind = LabQuestionKind.Matching;
    matching.Points = 3;
    matching.Partial = true;
    matching.Title = T(culture, "Associez chaque faiblesse à ce qu'elle permet.", "Match each weakness with what it allows.");
    foreach (var (left, right) in new[]
             {
                 (T(culture, "SPN sur un compte privilégié", "SPN on a privileged account"), T(culture, "Casser son mot de passe hors ligne", "Crack its password offline")),
                 (T(culture, "Délégation vers les services d'un DC", "Delegation to a DC's services"), T(culture, "Se faire passer pour un administrateur auprès du DC", "Impersonate an administrator to the DC")),
                 (T(culture, "Propriétaire de Domain Admins", "Owner of Domain Admins"), T(culture, "Modifier les membres du groupe", "Change the group's members"))
             })
    {
        matching.AddPairCommand.Execute(null);
        matching.Pairs[^1].Left = left;
        matching.Pairs[^1].Right = right;
    }
    matching.AddChoiceCommand.Execute(null);
    matching.Choices[^1].Text = T(culture, "Désactiver l'audit", "Disable auditing");
    Capture(main, Out("studio-questions"));
    // Passer d'une question à l'autre ne doit rien changer à leur forme (la liste « Forme » est partagée)
    foreach (var q in new[] { yesNo, matching, yesNo, matching })
    {
        vm.SelectedQuestion = q;
        Pump(100);
    }
    if (yesNo.Kind != LabQuestionKind.TrueFalse || matching.Kind != LabQuestionKind.Matching || matching.Model.Choices.Count != 4)
        throw new Exception("Changer de question a modifié la forme d'une question.");

    Tab(main, 4);
    vm.ValidateCommand.Execute(null);
    Capture(main, Out("studio-validation"));

    // ─── Copies d'une classe fictive ──────────────────────────────────────────
    vm.AddSubmissions(ClassSubmissions(vm.Lab!, culture));
    vm.SelectedSubmission = vm.Submissions.FirstOrDefault(s => s.Graded.Graded && s.Graded.Result!.Questions.Count > 0);
    Tab(main, 6);
    Capture(main, Out("studio-submissions"));

    // ─── Un exercice SMAR-X de démonstration, propre à la documentation ────────
    var demo = DemoSmarxLab(culture);
    vm.LoadSmarx(demo, null);
    var smarx = vm.Smarx!;
    smarx.Solution = DemoSmarxSolution(demo);
    smarx.SolutionLabel = demo.Id + SmarxStudioViewModel.SolutionExtension;
    vm.Status = StudioText.Instance["Status.SmarxCreated"];
    Tab(main, 2);
    smarx.SelectedObjective = smarx.Objectives.First(o => o.Model.Check.Type == SmarxCheckType.ExactAccess);
    Capture(main, Out("studio-smarx-objectives"));
    Tab(main, 4);
    smarx.ValidateCommand.Execute(null);
    Capture(main, Out("studio-smarx-validation"));
    smarx.AddSubmissions(SmarxClassSubmissions(demo));
    Tab(main, 5);
    Capture(main, Out("studio-smarx-submissions"));
    main.Close();

    // La fenêtre des recettes SMAR-X : l'arborescence d'une entreprise, sur Windows et Linux
    var smarxPicker = new SmarxRecipePickerViewModel
    {
        Company = T(culture, "Atelier Martin", "Martin Works"),
        Services = T(culture, "Comptabilité, Production, Ventes, Direction", "Accounting, Production, Sales, Direction")
    };
    smarxPicker.Platform = smarxPicker.Platforms.Last();
    Capture(new SmarxRecipeWindow { DataContext = smarxPicker, Width = 1000, Height = 640 }, Out("studio-smarx-recipe"));

    Console.WriteLine($"{lang} : captures de Studio dans {outDir}");
}

static string T(string culture, string fr, string en) => culture == "fr-FR" ? fr : en;

// Exercice SMAR-X de démonstration : dave, stagiaire, lit budget.ods et rien d'autre de la comptabilité. Il n'est
// pas fourni avec SMAR-X ; sa solution n'apparaît dans aucune capture.
static SmarxLab DemoSmarxLab(string culture)
{
    const string Compta = "/srv/compta", Budget = "/srv/compta/budget.ods", Notes = "/srv/compta/notes.txt";
    var linux = SmarxSystem.Linux;
    return new SmarxLab
    {
        Id = "demo-smarx-compta",
        Level = LabLevel.Beginner,
        DurationMinutes = 15,
        Title = LocalizedText.Of("Démonstration : un stagiaire à la comptabilité", "Demo: an intern in accounting"),
        Statement = LocalizedText.Of(
            "# Un stagiaire à la comptabilité\n\ndave doit lire `budget.ods`, et rien d'autre de `/srv/compta`.\n\n*Exercice de démonstration de la documentation.*",
            "# An intern in accounting\n\ndave must read `budget.ods`, and nothing else in `/srv/compta`.\n\n*Documentation demo lab.*"),
        Start = new SmarxDocument { Linux = [LinuxSamples.FileServer()] },
        Objectives =
        [
            new SmarxObjective { Id = "o1", Points = 2, Title = LocalizedText.Of("dave lit budget.ods", "dave reads budget.ods"),
                Check = new SmarxCheck { Type = SmarxCheckType.AccessAllowed, System = linux, Account = "dave", Operation = "Read", Path = Budget } },
            new SmarxObjective { Id = "o2", Points = 3, Title = LocalizedText.Of("Rien d'autre pour dave", "Nothing else for dave"),
                Check = new SmarxCheck
                {
                    Type = SmarxCheckType.ExactAccess, System = linux, Accounts = ["dave"], Paths = [Compta, Budget, Notes],
                    Operations = ["List", "Enter", "Read", "Write"], Needs = [$"dave Enter {Compta}", $"dave Read {Budget}"]
                } },
            new SmarxObjective { Id = "o3", Guard = true, Points = 0, Title = LocalizedText.Of("Les comptes ne changent pas", "The accounts do not change"),
                Check = new SmarxCheck { Type = SmarxCheckType.AccountUnchanged, System = linux, Account = "*" } }
        ]
    };
}

static SmarxDocument DemoSmarxSolution(SmarxLab lab)
{
    var work = SmarxLabGrader.Clone(lab.Start);
    var host = work.Linux[0];
    host.Find("/srv/compta")!.Acl = new PosixAcl { Users = [AclEntry.Of("dave", "--x")], GroupObj = "rwx" };
    host.Find("/srv/compta/budget.ods")!.Acl = new PosixAcl { Users = [AclEntry.Of("dave", "r--")], GroupObj = "r--" };
    return work;
}

// Copies d'apprenants fictifs : une solution, un départ inchangé, une copie qui donne trop (dave écrit aussi budget.ods)
static IReadOnlyList<string> SmarxClassSubmissions(SmarxLab lab)
{
    var folder = Path.Combine(Path.GetTempPath(), "smadx-studio-captures", "smarx-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(folder);
    var half = SmarxLabGrader.Clone(lab.Start);
    half.Linux[0].Find("/srv/compta")!.Acl = new PosixAcl { Users = [AclEntry.Of("dave", "--x")], GroupObj = "rwx" };
    var budget = half.Linux[0].Find("/srv/compta/budget.ods")!;
    budget.Acl = new PosixAcl { Users = [AclEntry.Of("dave", "rw-")], GroupObj = "r--" };
    budget.ModeBits = Convert.ToInt32("0660", 8);
    var copies = new[] { ("Camille Martin", DemoSmarxSolution(lab), 18), ("Hugo Petit", SmarxLabGrader.Clone(lab.Start), 25), ("Inès Moreau", half, 22) };
    return copies.Select(c =>
    {
        var path = Path.Combine(folder, SmarxLabService.SubmissionFileName(lab.Id, c.Item1));
        File.WriteAllText(path, SmarxLabService.SerializeSubmission(new SmarxSubmission
        {
            Student = c.Item1, LabId = lab.Id, SessionId = Guid.NewGuid(), StartedOn = new DateTime(2026, 11, 6, 9, 0, 0),
            SavedAt = new DateTime(2026, 11, 6, 9, c.Item3, 0), ElapsedMinutes = c.Item3, Lab = lab, Work = c.Item2
        }));
        return path;
    }).ToList();
}

static IEnumerable<ADObject> Flatten(ADObject o) => new[] { o }.Concat(o.Children.SelectMany(Flatten));

// Quatre apprenants fictifs sur l'exercice de démonstration (KRB-01, DLG-02 et ACL-01 combinés)
static IReadOnlyList<string> ClassSubmissions(LabDefinition lab, string culture)
{
    var folder = Path.Combine(Path.GetTempPath(), "smadx-doc-studio", culture);
    if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
    Directory.CreateDirectory(folder);
    var started = new DateTime(2026, 10, 27, 9, 0, 0);

    void Kerberoast(ADObject d) => Flatten(d).Single(o => o.Name == "svc-app").MemberOf.Remove("Domain Admins");
    void Delegation(ADObject d)
    {
        var server = Flatten(d).Single(o => o.Name == "SRV-APP01");
        server.AccountFlags &= ~ADAccountFlags.TrustedToAuthForDelegation;
        server.AllowedToDelegateTo.Clear();
    }
    void Owner(ADObject d) => Flatten(d).Single(o => o.Name == "Domain Admins" && o.Type == ADObjectType.Group).Owner = "Domain Admins";
    void DeleteAccount(ADObject d)
    {
        var svc = Flatten(d).Single(o => o.Name == "svc-app");
        svc.Parent!.Children.Remove(svc);
    }

    // Réponses aux deux questions : oui / non (q1), puis les associations (q2) ; les propositions sont celles saisies plus haut
    var matching = lab.Questions.Single(q => q.Kind == LabQuestionKind.Matching);
    var pairs = matching.Prompts!.Select(p => p.Match!).ToList();
    Dictionary<string, LabAnswer> Answers(string yesNo, params int[] order) => new()
    {
        ["q1"] = new LabAnswer { Choices = [yesNo] },
        ["q2"] = new LabAnswer { Matches = matching.Prompts!.Select((p, i) => (p.Id, pairs[order[i]])).ToDictionary(x => x.Id, x => x.Item2) }
    };
    var students = new (string Fr, string En, Action<ADObject>[] Work, string[] Hints, int Minutes, Dictionary<string, LabAnswer> Answers)[]
    {
        ("Camille Martin", "Alex Morgan", [Kerberoast, Delegation, Owner], [], 38, Answers("no", 0, 1, 2)),
        ("Lucas Bernard", "Sam Taylor", [Kerberoast, Owner], ["o5", "o6"], 52, Answers("no", 1, 0, 2)),
        ("Inès Moreau", "Jordan Lee", [DeleteAccount, Owner], ["o6"], 45, Answers("yes", 0, 1, 2)),
        ("Hugo Petit", "Chris Walker", [], ["o5"], 30, Answers("yes", 2, 0, 1)),
    };
    var paths = new List<string>();
    foreach (var (fr, en, work, hints, minutes, answers) in students)
    {
        var session = LabSession.Begin(lab, started);
        var document = session.NewWorkingCopy();
        foreach (var step in work) step(document.Domain);
        var name = T(culture, fr, en);
        var path = Path.Combine(folder, LabSubmissionService.SuggestedFileName(lab.Id, name));
        new LabSubmissionService().SaveFile(session.ToSubmission(name, document, hints, started.AddMinutes(minutes), answers), path);
        paths.Add(path);
    }

    // Un cinquième a rouvert la copie de la première et l'a enregistrée sous son nom : même session, que Studio signale
    var original = new LabSubmissionService().LoadFile(paths[0]);
    var copier = T(culture, "Nathan Roux", "Taylor Reed");
    var copied = Path.Combine(folder, LabSubmissionService.SuggestedFileName(lab.Id, copier));
    new LabSubmissionService().SaveFile(LabSession.Resume(original).ToSubmission(copier, original.Work, [], started.AddMinutes(41)), copied);
    paths.Add(copied);
    return paths;
}

static void Tab(Window window, int index)
{
    // Les onglets de l'exercice affiché : ceux d'un exercice SMAD-X ou d'un exercice SMAR-X (mis en page d'abord)
    Pump();
    window.GetVisualDescendants().OfType<TabControl>().First(t => t.IsEffectivelyVisible).SelectedIndex = index;
    Pump();
}

// Attendre une commande asynchrone en faisant tourner le dispatcher : sa suite s'y exécute
static void Wait(Task task)
{
    while (!task.IsCompleted)
        Pump(50);
    task.GetAwaiter().GetResult();
}

static void Pump(int milliseconds = 300)
{
    var end = DateTime.UtcNow.AddMilliseconds(milliseconds);
    do
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Thread.Sleep(16);
    } while (DateTime.UtcNow < end);
}

static void Capture(Window window, string path)
{
    if (!window.IsVisible) window.Show();
    Pump();
    var frame = window.CaptureRenderedFrame() ?? throw new Exception("Aucune image rendue pour " + path);
    frame.Save(path);
    Console.WriteLine("  " + Path.GetFileName(path));
    if (window is not MainWindow) window.Close();
}

/// <summary>Dialogues de capture : la recette est fournie, aucun fichier n'est ouvert ni enregistré.</summary>
sealed class CaptureDialogs : IStudioDialogs
{
    public Task<UnsavedChoice> AskUnsavedAsync() => Task.FromResult(UnsavedChoice.Cancel);
    public RecipeRequest? Recipe { get; set; }
    public Task<string?> OpenFileAsync(string title, string filterName, string pattern) => Task.FromResult<string?>(null);
    public Task<IReadOnlyList<string>> OpenFilesAsync(string title, string filterName, string pattern) => Task.FromResult<IReadOnlyList<string>>([]);
    public Task<string?> PickFolderAsync(string title, string? startFolder = null) => Task.FromResult<string?>(null);
    public Task ShowFolderAsync(string folder) => Task.CompletedTask;
    public Task<string?> SaveFileAsync(string title, string suggestedName, string filterName, string pattern) => Task.FromResult<string?>(null);
    public Task<RecipeRequest?> ChooseRecipeAsync() => Task.FromResult(Recipe);
    public Task<SmarxRecipeRequest?> ChooseSmarxRecipeAsync() => Task.FromResult<SmarxRecipeRequest?>(null);
}
