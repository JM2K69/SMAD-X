// Captures d'écran de SMAD-X Studio pour le site de documentation, en français et en anglais.
// Les fenêtres sont rendues hors écran (Avalonia Headless avec le moteur Skia) : rien ne s'affiche sur le bureau.
// L'exercice montré est créé ici à partir d'une recommandation, sur un domaine propre à la documentation ;
// la fiche formateur et les notes (la solution) ne sont jamais capturées.
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
    main.Close();

    Console.WriteLine($"{lang} : captures de Studio dans {outDir}");
}

static string T(string culture, string fr, string en) => culture == "fr-FR" ? fr : en;

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
    public Task<string?> SaveFileAsync(string title, string suggestedName, string filterName, string pattern) => Task.FromResult<string?>(null);
    public Task<RecipeRequest?> ChooseRecipeAsync() => Task.FromResult(Recipe);
}
