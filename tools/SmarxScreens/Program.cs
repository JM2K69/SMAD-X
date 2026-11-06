// Captures d'écran de SMAR-X : la fenêtre est rendue hors écran (Avalonia Headless avec le moteur Skia), sur les
// serveurs d'exemple, pour quelques accès qui montrent chacun une règle.
// Usage : dotnet run --project tools/SmarxScreens -- <dossier de sortie>
//         dotnet run --project tools/SmarxScreens -- --docs <racine du dépôt>   (captures de la documentation, en français et en anglais)
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SMARX;
using SMARX.Labs;
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
        Security(culture, ThemeVariant.Dark, @"D:\Partages\Compta\Archives", "DL_Compta_LM", "smarx-security");
        Dialog(culture, @"D:\Partages\Compta\Archives", "DL_Archives_Refuser", false, "smarx-edit");
        Dialog(culture, @"D:\Partages\Compta\Archives", null, true, "smarx-advanced");
        // Le même accès, sur le serveur puis depuis le réseau : NTFS laisse dave modifier planning.xlsx,
        // mais par le partage Projets (Lecture), il ne l'écrit plus
        Network(culture, "dave", @"D:\Partages\Projets\planning.xlsx", WindowsOperation.Write, null, "smarx-network-local");
        Network(culture, "dave", @"D:\Partages\Projets\planning.xlsx", WindowsOperation.Write, "Projets", "smarx-network");
        Sharing(culture, @"D:\Partages", "smarx-share");
        // Le même accès, sur le serveur puis depuis le réseau : le noyau laisse carol écrire doc.txt,
        // mais par le partage partage (lecture seule), elle ne l'écrit plus
        LinuxNetwork(culture, "carol", "/srv/partage/doc.txt", LinuxOperation.Write, null, "smarx-samba-local");
        LinuxNetwork(culture, "carol", "/srv/partage/doc.txt", LinuxOperation.Write, "partage", "smarx-samba");
        SambaWindow(culture, "/srv/projets", "smarx-samba-share");
        // « Comptes et groupes » : les membres d'un groupe de domaine local, l'imbrication de bob, et le sélecteur
        // d'Ajouter… qui grise ce qu'Active Directory refuserait
        DirectoryWindowCapture(culture, "DL_Compta_LM", 1, "smarx-directory");
        DirectoryWindowCapture(culture, "bob", 2, "smarx-nesting");
        PickerCapture(culture, "G_RH", "smarx-picker");
        // whoami de dave, juste après l'avoir mis dans G_RH : la session ouverte garde l'ancien jeton
        Whoami(culture, "dave", "G_RH", "smarx-whoami");
        // L'ACL par défaut de /srv/projets : ce que recevra ce qui y sera créé
        LinuxCard(culture, "/srv/projets", "Default.Title", "smarx-default-acl");
        // Un exercice fourni, vérifié au départ : rien n'est encore fait, les gardes tiennent ; aucun indice ouvert
        LabWindow(culture, "smarx-lab");
    }
    return;
}

// L'espace Linux, le testeur sur le serveur (share null) ou passant par un partage Samba
void LinuxNetwork(string culture, string user, string path, LinuxOperation operation, string? share, string name)
{
    var shell = Prepare(culture, ThemeVariant.Dark);
    var vm = shell.Linux;
    vm.SelectedNode = Find(vm.Tree[0], path, n => n.Path, n => n.Children);
    vm.SelectedUser = vm.Users.First(u => u.Name == user);
    vm.SelectedRoute = vm.Routes.First(r => r.Share?.Name == share);
    vm.SelectedOperation = vm.Operations.First(o => Equals(o.Operation, operation));
    vm.SelectedAccount = vm.Users.First(u => u.Name == user);
    Capture(shell, name, 1000);
}

// La fenêtre d'un exercice fourni, après « Vérifier » sur les serveurs de départ
void LabWindow(string culture, string name)
{
    var shell = Prepare(culture, ThemeVariant.Dark);
    shell.OpenLab(SmarxBuiltInLabs.LinuxTeamFolder());
    var window = new SmarxLabWindow { DataContext = shell.Lab, Width = 1000, Height = 820 };
    window.Show();
    shell.Lab!.CheckCommand.Execute(null);
    Pump(700);
    var frame = window.CaptureRenderedFrame() ?? throw new Exception("Aucune image rendue pour " + name);
    var file = Path.Combine(outDir, name + ".png");
    frame.Save(file);
    Console.WriteLine(file);
    window.Close();
}

// La fenêtre whoami d'un compte qu'on vient de mettre dans un groupe : ce qui change à la prochaine session
void Whoami(string culture, string account, string group, string name)
{
    var shell = Prepare(culture, ThemeVariant.Dark);
    var vm = shell.Windows;
    var whoami = new WhoamiViewModel(vm, account, network: false, elevated: false);
    using (var directory = new DirectoryViewModel(vm, group)) directory.AddMembers([account]);
    var window = new WhoamiWindow { DataContext = whoami, Width = 1240, Height = 900 };
    window.Show();
    Pump(700);
    var frame = window.CaptureRenderedFrame() ?? throw new Exception("Aucune image rendue pour " + name);
    var file = Path.Combine(outDir, name + ".png");
    frame.Save(file);
    Console.WriteLine(file);
    window.Close();
}

// « Comptes et groupes », ouverte sur un compte ou un groupe, à l'onglet donné (0 Membre de, 1 Membres, 2 Imbrication)
void DirectoryWindowCapture(string culture, string select, int tab, string name)
{
    var shell = Prepare(culture, ThemeVariant.Dark);
    var window = new DirectoryWindow { DataContext = new DirectoryViewModel(shell.Windows, select), Width = 1240, Height = 760 };
    window.Show();
    Pump(300);
    window.GetVisualDescendants().OfType<TabControl>().First().SelectedIndex = tab;
    Pump(700);
    var frame = window.CaptureRenderedFrame() ?? throw new Exception("Aucune image rendue pour " + name);
    var file = Path.Combine(outDir, name + ".png");
    frame.Save(file);
    Console.WriteLine(file);
    window.Close();
}

// « Sélectionner des utilisateurs ou des groupes », ouvert par Ajouter… de l'onglet Membres d'un groupe
void PickerCapture(string culture, string group, string name)
{
    var shell = Prepare(culture, ThemeVariant.Dark);
    using var directory = new DirectoryViewModel(shell.Windows, group);
    var window = new DirectoryPickerWindow { DataContext = directory.MembersPicker(), Width = 680, Height = 620 };
    window.Show();
    Pump(700);
    var frame = window.CaptureRenderedFrame() ?? throw new Exception("Aucune image rendue pour " + name);
    var file = Path.Combine(outDir, name + ".png");
    frame.Save(file);
    Console.WriteLine(file);
    window.Close();
}

// Une carte de la colonne de droite de l'espace Linux, seule : la fenêtre est rendue assez haute pour tout montrer
void LinuxCard(string culture, string path, string titleKey, string name)
{
    var shell = Prepare(culture, ThemeVariant.Dark);
    var vm = shell.Linux;
    vm.SelectedNode = Find(vm.Tree[0], path, n => n.Path, n => n.Children);
    var window = new MainWindow { DataContext = shell, Width = 1600, Height = 2600 };
    window.Show();
    Pump(700);
    var title = SmarxText.Instance[titleKey];
    var card = window.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == title && t.IsEffectivelyVisible)
        .GetVisualAncestors().OfType<Border>().First(b => b.Classes.Contains("card"));
    using var bitmap = new RenderTargetBitmap(new PixelSize((int)card.Bounds.Width, (int)card.Bounds.Height));
    bitmap.Render(card);
    var file = Path.Combine(outDir, name + ".png");
    bitmap.Save(file);
    Console.WriteLine(file);
    window.Close();
}

// La fenêtre Partage Samba d'un dossier
void SambaWindow(string culture, string folder, string name)
{
    var shell = Prepare(culture, ThemeVariant.Dark);
    var vm = shell.Linux;
    var window = new SambaShareWindow { DataContext = new SambaShareSettingsViewModel(vm.Host, Find(vm.Tree[0], folder, n => n.Path, n => n.Children)) };
    window.Show();
    Pump(700);
    var frame = window.CaptureRenderedFrame() ?? throw new Exception("Aucune image rendue pour " + name);
    var file = Path.Combine(outDir, name + ".png");
    frame.Save(file);
    Console.WriteLine(file);
    window.Close();
}

// L'espace Windows, le testeur sur le serveur (share null) ou passant par un partage
void Network(string culture, string user, string path, WindowsOperation operation, string? share, string name)
{
    var shell = Prepare(culture, ThemeVariant.Dark);
    shell.ShowWindowsCommand.Execute(null);
    var vm = shell.Windows;
    vm.SelectedNode = Find(vm.Tree[0], path, n => n.Path, n => n.Children);
    vm.SelectedUser = vm.Users.First(u => u.Name == user);
    vm.SelectedOperation = vm.Operations.First(o => Equals(o.Operation, operation));
    vm.SelectedRoute = vm.Routes.First(r => r.Share?.Name == share);
    vm.SelectedAccount = vm.Users.First(u => u.Name == user);
    Capture(shell, name, 1000);
}

// La fenêtre Partage avancé d'un dossier
void Sharing(string culture, string folder, string name)
{
    var shell = Prepare(culture, ThemeVariant.Dark);
    var vm = shell.Windows;
    var window = new ShareWindow { DataContext = new ShareSettingsViewModel(vm.Host, Find(vm.Tree[0], folder, n => n.Path, n => n.Children)) };
    window.Show();
    Pump(700);
    var frame = window.CaptureRenderedFrame() ?? throw new Exception("Aucune image rendue pour " + name);
    var file = Path.Combine(outDir, name + ".png");
    frame.Save(file);
    Console.WriteLine(file);
    window.Close();
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
Security("fr-FR", ThemeVariant.Dark, @"D:\Partages\Projets", "DL_Projets_Refuser", "12-onglet-securite");
Security("en-US", ThemeVariant.Light, @"D:\Partages\Projets\planning.xlsx", "dave", "13-security-tab-english");
Dialog("fr-FR", @"D:\Partages\Compta\Archives", "DL_Archives_Refuser", false, "14-fenetre-modifier");
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
