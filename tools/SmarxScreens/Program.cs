// Captures d'écran de SMAR-X : la fenêtre est rendue hors écran (Avalonia Headless avec le moteur Skia), sur les
// serveurs d'exemple, pour quelques accès qui montrent chacun une règle.
// Usage : dotnet run --project tools/SmarxScreens -- <dossier de sortie>
//         dotnet run --project tools/SmarxScreens -- --docs <racine du dépôt>   (captures de la documentation, en français et en anglais)
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.Threading;
using SMARX;
using SMARX.Services.Linux;
using SMARX.Services.Windows;
using SMARX.ViewModels;
using SMARX.Views;

var docs = args.Length > 1 && args[0] == "--docs";
var outDir = Path.GetFullPath(docs ? args[1] : args.Length > 0 ? args[0] : "smarx-screens");
Directory.CreateDirectory(outDir);

AppBuilder.Configure<App>()
    .UseSkia()
    .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
    .WithInterFont()
    .SetupWithoutStarting();

if (docs)
{
    // Archives : Modification héritée de Compta (coches grises), Écriture refusée sur Archives même (coche noire)
    var root = outDir;
    foreach (var (culture, language) in new[] { ("fr-FR", "fr"), ("en-US", "en") })
    {
        outDir = Path.Combine(root, "docs", language, "assets", "screens");
        Linux(culture, ThemeVariant.Dark, "bob", "/srv/partage", LinuxOperation.Create, "smarx-linux");
        Security(culture, ThemeVariant.Dark, @"D:\Partages\Compta\Archives", "GL_Compta", "smarx-security");
        Dialog(culture, @"D:\Partages\Compta\Archives", "GL_Compta", false, "smarx-edit");
        Dialog(culture, @"D:\Partages\Compta\Archives", null, true, "smarx-advanced");
    }
    return;
}

Linux("fr-FR", ThemeVariant.Dark, "bob", "/srv/compta/budget.ods", LinuxOperation.Read, "01-linux-bob-lit-budget");
Linux("fr-FR", ThemeVariant.Dark, "carol", "/srv/compta/budget.ods", LinuxOperation.Read, "02-linux-carol-bloquee");
Linux("fr-FR", ThemeVariant.Dark, "bob", "/srv/partage", LinuxOperation.Create, "03-linux-acl-et-masque");
Linux("fr-FR", ThemeVariant.Dark, "dave", "/srv/compta", LinuxOperation.List, "04-linux-comptes", 1900);
Windows("fr-FR", ThemeVariant.Dark, "dave", @"D:\Partages\Projets\planning.xlsx", WindowsOperation.Write, false, "05-windows-explicite-avant-herite");
Windows("fr-FR", ThemeVariant.Dark, "dave", @"D:\Partages\Projets\notes.txt", WindowsOperation.Write, false, "06-windows-refus-herite");
Windows("fr-FR", ThemeVariant.Dark, "carol", @"D:\Admin", WindowsOperation.List, false, "07-windows-uac-non-eleve");
Windows("fr-FR", ThemeVariant.Light, "bob", @"D:\Partages\Commun", WindowsOperation.CreateFile, false, "08-windows-creator-owner-clair", 2100);
Windows("en-US", ThemeVariant.Dark, "alice", @"D:\Partages\RH\annonce-rh.txt", WindowsOperation.Read, false, "09-windows-traverse-english");
Glossary("fr-FR", 0, "10-aide-memoire-windows");
Security("fr-FR", ThemeVariant.Dark, @"D:\Partages\Projets", "GL_Stagiaires", "12-onglet-securite");
Security("en-US", ThemeVariant.Light, @"D:\Partages\Projets\planning.xlsx", "dave", "13-security-tab-english");
Dialog("fr-FR", @"D:\Partages\Compta\Archives", "GL_Compta", false, "14-fenetre-modifier");
Dialog("fr-FR", @"D:\Partages\Compta\Archives", null, true, "15-fenetre-avance");
Glossary("fr-FR", 1, "11-aide-memoire-linux");

// La colonne de droite de l'espace Windows, ouverte sur l'onglet Sécurité d'un objet et un compte
void Security(string culture, ThemeVariant theme, string path, string principal, string name)
{
    var shell = Prepare(culture, theme);
    shell.ShowWindowsCommand.Execute(null);
    var vm = shell.Windows;
    vm.SelectedNode = Find(vm.Tree[0], path, n => n.Path, n => n.Children);
    vm.Security.SelectedPrincipal = vm.Security.Principals.First(p => p.Trustee == principal);
    Capture(shell, name, 1000);
}

// Les fenêtres Modifier et Avancé, sur le serveur d'exemple
void Dialog(string culture, string path, string? principal, bool advanced, string name)
{
    var shell = Prepare(culture, ThemeVariant.Dark);
    var vm = shell.Windows;
    var node = Find(vm.Tree[0], path, n => n.Path, n => n.Children);
    Window window;
    if (advanced)
        window = new AdvancedSecurityWindow { DataContext = new AdvancedSecurityViewModel(vm.Host, node, "bob"), Width = 1100 };
    else
    {
        var edit = new PermissionsEditViewModel(vm.Host, node);
        edit.SelectedPrincipal = edit.Principals.First(p => p.Trustee == principal);
        window = new PermissionsWindow { DataContext = edit };
    }
    window.Show();
    Pump(700);
    var frame = window.CaptureRenderedFrame() ?? throw new Exception("Aucune image rendue pour " + name);
    var file = Path.Combine(outDir, name + ".png");
    frame.Save(file);
    Console.WriteLine(file);
    window.Close();
}

void Glossary(string culture, int tab, string name)
{
    SmarxText.Instance.SetLanguage(culture);
    Application.Current!.RequestedThemeVariant = ThemeVariant.Dark;
    var window = new GlossaryWindow { DataContext = new GlossaryViewModel(tab), Width = 1100, Height = 1500 };
    window.Show();
    Pump(700);
    var frame = window.CaptureRenderedFrame() ?? throw new Exception("Aucune image rendue pour " + name);
    var file = Path.Combine(outDir, name + ".png");
    frame.Save(file);
    Console.WriteLine(file);
    window.Close();
}

void Linux(string culture, ThemeVariant theme, string user, string path, LinuxOperation operation, string name, int height = 1000)
{
    var shell = Prepare(culture, theme);
    var vm = shell.Linux;
    vm.SelectedNode = Find(vm.Tree[0], path, n => n.Path, n => n.Children);
    vm.SelectedUser = vm.Users.First(u => u.Name == user);
    vm.SelectedOperation = vm.Operations.First(o => Equals(o.Operation, operation));
    vm.SelectedAccount = vm.Users.First(u => u.Name == user);
    Capture(shell, name, height);
}

void Windows(string culture, ThemeVariant theme, string user, string path, WindowsOperation operation, bool elevated, string name, int height = 1000)
{
    var shell = Prepare(culture, theme);
    shell.ShowWindowsCommand.Execute(null);
    var vm = shell.Windows;
    vm.SelectedNode = Find(vm.Tree[0], path, n => n.Path, n => n.Children);
    vm.SelectedUser = vm.Users.First(u => u.Name == user);
    vm.SelectedOperation = vm.Operations.First(o => Equals(o.Operation, operation));
    vm.Elevated = elevated;
    vm.SelectedAccount = vm.Users.First(u => u.Name == user);
    Capture(shell, name, height);
}

MainWindowViewModel Prepare(string culture, ThemeVariant theme)
{
    SmarxText.Instance.SetLanguage(culture);
    Application.Current!.RequestedThemeVariant = theme;
    return new MainWindowViewModel();
}

void Capture(MainWindowViewModel shell, string name, int height)
{
    var window = new MainWindow { DataContext = shell, Width = 1600, Height = height };
    window.Show();
    Pump(700);
    var frame = window.CaptureRenderedFrame() ?? throw new Exception("Aucune image rendue pour " + name);
    var file = Path.Combine(outDir, name + ".png");
    frame.Save(file);
    Console.WriteLine(file);
    window.Close();
}

static T Find<T>(T item, string path, Func<T, string> pathOf, Func<T, IEnumerable<T>> children) where T : class =>
    FindOrNull(item, path, pathOf, children) ?? throw new ArgumentException(path);

static T? FindOrNull<T>(T item, string path, Func<T, string> pathOf, Func<T, IEnumerable<T>> children) where T : class =>
    string.Equals(pathOf(item), path, StringComparison.OrdinalIgnoreCase) ? item
    : children(item).Select(c => FindOrNull(c, path, pathOf, children)).FirstOrDefault(f => f != null);

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
