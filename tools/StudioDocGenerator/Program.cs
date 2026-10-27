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

var repo = Path.GetFullPath(args.Length > 0 ? args[0] : ".");

AppBuilder.Configure<App>()
    .UseSkia()
    .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
    .SetupWithoutStarting();

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
    Tab(main, 3);
    vm.ValidateCommand.Execute(null);
    Capture(main, Out("studio-validation"));

    // ─── Copies d'une classe fictive ──────────────────────────────────────────
    vm.AddSubmissions(ClassSubmissions(vm.Lab!, culture));
    Tab(main, 5);
    Capture(main, Out("studio-submissions"));
    main.Close();

    Console.WriteLine($"{lang} : captures de Studio dans {outDir}");
}

static string T(string culture, string fr, string en) => culture == "fr-FR" ? fr : en;

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

    var students = new (string Fr, string En, Action<ADObject>[] Work, string[] Hints, int Minutes)[]
    {
        ("Camille Martin", "Alex Morgan", [Kerberoast, Delegation, Owner], [], 38),
        ("Lucas Bernard", "Sam Taylor", [Kerberoast, Owner], ["o5", "o6"], 52),
        ("Inès Moreau", "Jordan Lee", [DeleteAccount, Owner], ["o6"], 45),
        ("Hugo Petit", "Chris Walker", [], ["o5"], 30),
    };
    var paths = new List<string>();
    foreach (var (fr, en, work, hints, minutes) in students)
    {
        var session = LabSession.Begin(lab, started);
        var document = session.NewWorkingCopy();
        foreach (var step in work) step(document.Domain);
        var name = T(culture, fr, en);
        var path = Path.Combine(folder, LabSubmissionService.SuggestedFileName(lab.Id, name));
        new LabSubmissionService().SaveFile(session.ToSubmission(name, document, hints, started.AddMinutes(minutes)), path);
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
    window.GetVisualDescendants().OfType<TabControl>().Single().SelectedIndex = index;
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
    public RecipeRequest? Recipe { get; set; }
    public Task<string?> OpenFileAsync(string title, string filterName, string pattern) => Task.FromResult<string?>(null);
    public Task<IReadOnlyList<string>> OpenFilesAsync(string title, string filterName, string pattern) => Task.FromResult<IReadOnlyList<string>>([]);
    public Task<string?> PickFolderAsync(string title) => Task.FromResult<string?>(null);
    public Task<string?> SaveFileAsync(string title, string suggestedName, string filterName, string pattern) => Task.FromResult<string?>(null);
    public Task<RecipeRequest?> ChooseRecipeAsync() => Task.FromResult(Recipe);
}
