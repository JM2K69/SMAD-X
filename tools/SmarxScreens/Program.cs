// Captures d'écran de SMAR-X : la fenêtre est rendue hors écran (Avalonia Headless avec le moteur Skia), sur le
// serveur d'exemple, pour quelques accès qui montrent chacun une règle.
// Usage : dotnet run --project tools/SmarxScreens -- <dossier de sortie>
using Avalonia;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.Threading;
using SMARX;
using SMARX.Services.Linux;
using SMARX.ViewModels;
using SMARX.Views;

var outDir = Path.GetFullPath(args.Length > 0 ? args[0] : "smarx-screens");
Directory.CreateDirectory(outDir);

AppBuilder.Configure<App>()
    .UseSkia()
    .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
    .WithInterFont()
    .SetupWithoutStarting();

Capture("fr-FR", ThemeVariant.Dark, "bob", "/srv/compta/budget.ods", LinuxOperation.Read, "01-bob-lit-budget");
Capture("fr-FR", ThemeVariant.Dark, "carol", "/srv/compta/budget.ods", LinuxOperation.Read, "02-carol-bloquee-au-dossier");
Capture("fr-FR", ThemeVariant.Dark, "alice", "/srv/compta/notes.txt", LinuxOperation.Write, "03-proprietaire-limitee");
Capture("fr-FR", ThemeVariant.Dark, "bob", "/srv/partage", LinuxOperation.Create, "04-acl-et-masque");
Capture("fr-FR", ThemeVariant.Light, "bob", "/tmp/alice.txt", LinuxOperation.Delete, "05-sticky-bit-clair");
Capture("en-US", ThemeVariant.Dark, "alice", "/srv/projets", LinuxOperation.Create, "06-default-acl-english");
Capture("fr-FR", ThemeVariant.Dark, "alice", "/srv/projets", LinuxOperation.Create, "07-colonne-droite-complete", 1900);
Capture("fr-FR", ThemeVariant.Dark, "bob", "/srv/compta/budget.ods", LinuxOperation.Read, "08-bascule-en-anglais", switchTo: "en-US");

void Capture(string culture, ThemeVariant theme, string user, string path, LinuxOperation operation, string name, int height = 1000, string? switchTo = null)
{
    SmarxText.Instance.SetLanguage(culture);
    Application.Current!.RequestedThemeVariant = theme;
    var vm = new MainWindowViewModel();
    vm.SelectedNode = Find(vm.Tree[0], path);
    vm.SelectedUser = vm.Users.First(u => u.Name == user);
    vm.SelectedOperation = vm.Operations.First(o => o.Operation == operation);
    var window = new MainWindow { DataContext = vm, Width = 1520, Height = height };
    window.Show();
    Pump(600);
    if (switchTo != null)
    {
        // Changement de langue fenêtre ouverte, comme le bouton de l'en-tête
        SmarxText.Instance.SetLanguage(switchTo);
        Pump(600);
    }
    var frame = window.CaptureRenderedFrame() ?? throw new Exception("Aucune image rendue pour " + name);
    var file = Path.Combine(outDir, name + ".png");
    frame.Save(file);
    Console.WriteLine(file);
    window.Close();
}

static NodeItem Find(NodeItem item, string path) =>
    item.Path == path ? item : item.Children.Select(c => FindOrNull(c, path)).First(f => f != null)!;

static NodeItem? FindOrNull(NodeItem item, string path) =>
    item.Path == path ? item : item.Children.Select(c => FindOrNull(c, path)).FirstOrDefault(f => f != null);

static void Pump(int milliseconds)
{
    var end = DateTime.UtcNow.AddMilliseconds(milliseconds);
    do
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Thread.Sleep(16);
    } while (DateTime.UtcNow < end);
}
