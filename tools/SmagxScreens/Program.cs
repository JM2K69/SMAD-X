// Captures d'écran et icône de SMAG-X, rendues hors écran (Avalonia Headless avec le moteur Skia) : rien ne s'affiche.
// La capture joue une vraie session d'élève : dépôt, branches, fusion, puis des fichiers en cours dans les deux zones.
// Usage : dotnet run --project tools/SmagxScreens -- <dossier de sortie> [fr|en]
//         dotnet run --project tools/SmagxScreens -- --icon <fichier .ico>   (l'icône de l'application, depuis le logo)
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using SMADX.Services;
using SMAGX;
using SMAGX.Labs;
using SMAGX.ViewModels;
using SMAGX.Views;

AppBuilder.Configure<App>()
    .UseSkia()
    .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
    .WithInterFont()
    .WithEmojiFont()
    .SetupWithoutStarting();

if (args is ["--icon", var icoPath])
{
    Icon(Path.GetFullPath(icoPath));
    return;
}

var outDir = Path.GetFullPath(args.Length > 0 ? args[0] : "smagx-screens");
var language = args.Length > 1 ? args[1] : "fr";
Directory.CreateDirectory(outDir);
SmagxSettings.DefaultPath = Path.Combine(Path.GetTempPath(), "smagx-screens-settings.json");
SmagxText.Instance.SetLanguage(language == "en" ? "en-US" : "fr-FR");
Session(outDir);

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
    Pump(600);
    var frame = window.CaptureRenderedFrame() ?? throw new Exception("Aucune image rendue pour " + path);
    frame.Save(path);
    Console.WriteLine(path);
}

// Une session d'Alice : présentation à Git, premier commit, une branche contact, un commit sur main, la fusion ;
// sa clé SSH (ssh-keygen, ajoutée sur la forge : smagx-cles.png), le test de connexion pendant la question sur la clé
// du serveur (smagx-ssh.png), le premier envoi, puis une page en préparation (index) et une feuille de style modifiée
// (smagx-main.png). Puis le poste de Bob, sur la même forge : sa clé, le clone, une branche poussée (smagx-postes.png) ;
// Alice sur GitHub (smagx-github.png), et la fenêtre « Travail à deux », forges servies (smagx-travail-a-deux.png).
static void Session(string outDir)
{
    var settings = new SmagxSettings { Name = "Alice Martin", Email = "alice@lycee.example", Login = "alice" };
    var window = new MainWindow { Width = 1640, Height = 940, WindowState = WindowState.Normal };
    var vm = new MainWindowViewModel(settings, new AcceptingDialogs());
    window.DataContext = vm;
    var start = new DateTimeOffset(2026, 11, 27, 9, 0, 0, TimeSpan.FromHours(1));
    var minute = 0;
    void Station()
    {
        vm.Current.Workstation.Clock = () => start.AddMinutes(minute);
        vm.Current.Workstation.Random = new Random(vm.Current.Login.Length);
    }
    Station();
    window.Show();
    Pump();

    void Wait(Task task)
    {
        while (!task.IsCompleted) Pump(30);
        task.GetAwaiter().GetResult();
    }
    // Une commande ; ses questions (ssh, ssh-keygen) reçoivent les réponses dans l'ordre, la capture se fait avant la dernière
    void Type(string line, string? capture = null, params string[] answers)
    {
        minute += 3;
        vm.Current.Input = line;
        var run = vm.Current.RunAsync();
        var queue = new Queue<string>(answers);
        while (!run.IsCompleted)
        {
            Pump(30);
            if (!vm.Current.Answering || queue.Count == 0) continue;
            vm.Current.Input = queue.Dequeue();
            if (queue.Count == 0 && capture is not null) Capture(window, Path.Combine(outDir, capture));
            Wait(vm.Current.RunAsync());
        }
        run.GetAwaiter().GetResult();
    }
    void Write(string file, string text) => vm.Current.Workstation.WriteText(vm.Current.Workstation.Cwd + "/" + file, text);
    // La clé publique du poste ajoutée à son compte, dans la page « Clés SSH » de la forge montrée
    KeysViewModel AddKey(string title)
    {
        var keys = new KeysViewModel(vm.ShownForge, vm.Current.Login);
        keys.KeyText = vm.Current.Workstation.ReadText(vm.Current.Workstation.Home + "/.ssh/id_ed25519.pub");
        keys.Title = title;
        Wait(keys.AddCommand.ExecuteAsync(null));
        Wait(vm.RefreshForgeAsync());
        return keys;
    }
    void CreateProject(string name)
    {
        vm.NewProjectName = name;
        Wait(vm.CreateProjectCommand.ExecuteAsync(null));
    }

    Type("git config --global user.name \"Alice Martin\"");
    Type("git config --global user.email alice@lycee.example");
    Type("git config --global init.defaultBranch main");
    Type("git init");
    Write("index.html", "<h1>Agence Méridien</h1>\n<p>Bienvenue</p>\n");
    Write("style.css", "body {\n  margin: 0;\n}\n");
    Type("git add .");
    Type("git commit -m \"Page d'accueil\"");
    Type("git switch -c contact");
    Write("contact.html", "<p>Contact : 01 02 03 04 05</p>\n");
    Type("git add contact.html");
    Type("git commit -m \"Page contact\"");
    Type("git switch main");
    Write("index.html", "<h1>Agence Méridien, voyages</h1>\n<p>Bienvenue</p>\n");
    Type("git commit -am \"Titre de l'agence\"");
    Type("git merge contact");

    // La clé SSH d'Alice, puis le projet partagé sur la forge et le premier envoi
    Type("clear");
    Type("ssh-keygen -t ed25519 -C \"alice@lycee.example\"", null, "", "", "");
    // La clé publique, trouvée par tabulation : une tabulation complète « id_ed25519 », deux affichent les choix
    var input = window.GetVisualDescendants().OfType<TextBox>().First(t => t.Classes.Contains("terminal"));
    void Keys(string typed)
    {
        vm.Current.Input = typed;
        input.Focus();
        input.CaretIndex = typed.Length;
    }
    void PressTab()
    {
        window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
        Pump(60);
    }
    Keys("cat ~/.ssh/i");
    PressTab();
    if (vm.Current.Input != "cat ~/.ssh/id_ed25519") throw new Exception("Tabulation : " + vm.Current.Input);
    PressTab();
    PressTab();
    Capture(window, Path.Combine(outDir, "smagx-tab.png"));
    Keys(vm.Current.Input + ".");
    PressTab();
    if (vm.Current.Input != "cat ~/.ssh/id_ed25519.pub ") throw new Exception("Tabulation : " + vm.Current.Input);
    Type(vm.Current.Input);
    var keys = AddKey("Poste du lycée");
    var keysWindow = new KeysWindow { DataContext = keys };
    keysWindow.Show();
    Capture(keysWindow, Path.Combine(outDir, "smagx-cles.png"));
    keysWindow.Close();
    Type("ssh -T git@gitlab.example", "smagx-ssh.png", "yes");
    CreateProject("classe/agence");
    Type("git remote add origin git@gitlab.example:classe/agence.git");
    Type("git push -u origin main");
    Type("git log --oneline");
    Write("services.html", "<ul><li>Séjours</li></ul>\n");
    Type("git add services.html");
    Write("style.css", "body {\n  margin: 0;\n  color: navy;\n}\n");
    // Un dossier vide, et un brouillon que .gitignore cache à Git : le panneau Fichiers les signale
    Type("mkdir images");
    Write("notes/brouillon.txt", "idées en vrac\n");
    Write(".gitignore", "notes/\n");
    Type("git status");
    vm.Current.SelectedFile = "style.css";
    Capture(window, Path.Combine(outDir, "smagx-main.png"));
    // Le panneau Fichiers en entier : dossiers, dossier vide, dossier ignoré par .gitignore
    vm.Current.SelectedFile = null;
    window.GetVisualDescendants().OfType<ListBox>().First(l => ReferenceEquals(l.ItemsSource, vm.Current.Entries)).ScrollIntoView(0);
    Capture(window, Path.Combine(outDir, "smagx-fichiers.png"));

    // Le poste de Bob (la fenêtre d'identité des sessions jouées le donne), sur la même forge
    Wait(vm.AddStationCommand.ExecuteAsync(null));
    Station();
    Type("git config --global user.name \"Bob Durand\"");
    Type("git config --global user.email bob@lycee.example");
    Type("ssh-keygen -t ed25519 -C \"bob@lycee.example\"", null, "", "", "");
    AddKey("Poste de Bob");
    Type("clear");
    Type("git clone git@gitlab.example:classe/agence.git", null, "yes");
    Type("cd agence");
    Type("git switch -c tarifs");
    Write("tarifs.html", "<h2>Tarifs</h2>\n<p>Séjour d'une semaine : 690 €</p>\n");
    Type("git add tarifs.html");
    Type("git commit -m \"Page des tarifs\"");
    Type("git push -u origin tarifs");
    vm.Current.SelectedFile = "tarifs.html";
    Capture(window, Path.Combine(outDir, "smagx-postes.png"));

    // Alice publie aussi le projet sur GitHub : un autre serveur, une autre clé de serveur à accepter
    vm.Current = vm.Stations[0];
    vm.GitHubShown = true;
    Wait(vm.RefreshForgeAsync());
    AddKey("Poste du lycée");
    CreateProject("agence");
    Type("clear");
    Type("git remote add github git@github.example:alice/agence.git");
    Type("git push github main", null, "yes");
    Type("ssh -T git@github.example");
    Type("git remote -v");
    Capture(window, Path.Combine(outDir, "smagx-github.png"));

    // Le travail à deux : les forges servies (adresse et code d'exemple, sans ouvrir de vrai serveur)
    vm.ServerAddress = "192.168.1.20";
    vm.ServerOtherAddresses = "192.168.56.1";
    vm.ServerCode = "4821";
    vm.Serving = true;
    vm.ServerVisitors = SmagxText.Instance.T("Connecté : 192.168.1.21", "Connected: 192.168.1.21");
    Capture(window, Path.Combine(outDir, "smagx-serveur-actif.png"));
    var collaborate = new CollaborateWindow { DataContext = vm, Width = 820 };
    collaborate.Show();
    Capture(collaborate, Path.Combine(outDir, "smagx-travail-a-deux.png"));
    collaborate.Close();

    // Les scénarios : le parcours (un scénario à deux choisi), un scénario seul en cours, un scénario à deux en cours
    var picker = new ScenarioPickerWindow { DataContext = new ScenarioPickerViewModel(SmagxBuiltInLabs.All(), false) };
    picker.Show();
    Pump();
    picker.Select(pair: true, 0);
    Capture(picker, Path.Combine(outDir, "smagx-parcours.png"));
    picker.Close();

    window.Width = 1920;
    window.Height = 1000;
    Wait(vm.StartScenarioAsync(SmagxBuiltInLabs.ThreeAreas(), ScenarioMode.Here));
    Station();
    Type("git status");
    Type("git add style.css");
    Type("git commit -m \"Nouvelle couleur\"");
    Type("git restore index.html");
    Type("git status");
    Wait(vm.CheckNowCommand.ExecuteAsync(null));
    Capture(window, Path.Combine(outDir, "smagx-scenario.png"));

    Wait(vm.StartScenarioAsync(SmagxBuiltInLabs.PairWork(), ScenarioMode.Here));
    Station();
    Type("git clone git@gitlab.example:classe/agence.git");
    Type("cd agence");
    Write("equipe.html", "<h2>L'équipe</h2>\n");
    Type("git add equipe.html");
    Type("git commit -m \"Page équipe\"");
    Type("git push");
    vm.Current = vm.Stations[1];
    Station();
    Type("git clone git@gitlab.example:classe/agence.git");
    Type("cd agence");
    Write("tarifs.html", "<h2>Tarifs</h2>\n");
    Type("git add tarifs.html");
    Type("git commit -m \"Page tarifs\"");
    Type("git push");
    Wait(vm.CheckNowCommand.ExecuteAsync(null));
    Capture(window, Path.Combine(outDir, "smagx-scenario-deux.png"));
    window.Close();
}

// L'icône : le logo de l'en-tête (le graphe de commits sur le dégradé orange), en PNG à plusieurs tailles dans un .ico
static void Icon(string path)
{
    var sizes = new[] { 256, 64, 48, 32, 24, 16 };
    var images = new List<byte[]>();
    foreach (var size in sizes)
    {
        var logo = new Border
        {
            Width = size, Height = size, CornerRadius = new CornerRadius(size * 0.22),
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops = { new GradientStop(Color.Parse("#F05033"), 0), new GradientStop(Color.Parse("#F2994A"), 1) }
            },
            Child = new PathIcon
            {
                Data = (Geometry)Application.Current!.Resources["GitIcon"]!, Foreground = Brushes.White,
                Width = size * 0.6, Height = size * 0.6, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            }
        };
        var host = new Window { Width = size, Height = size, Background = Brushes.Transparent, Content = logo };
        host.Show();
        Pump(100);
        using var bitmap = new RenderTargetBitmap(new PixelSize(size, size));
        bitmap.Render(logo);
        using var png = new MemoryStream();
        bitmap.Save(png);
        images.Add(png.ToArray());
        host.Close();
    }
    using var ico = new BinaryWriter(File.Create(path));
    ico.Write((short)0);
    ico.Write((short)1);
    ico.Write((short)sizes.Length);
    var offset = 6 + 16 * sizes.Length;
    for (var i = 0; i < sizes.Length; i++)
    {
        ico.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
        ico.Write((byte)(sizes[i] >= 256 ? 0 : sizes[i]));
        ico.Write((byte)0);
        ico.Write((byte)0);
        ico.Write((short)1);
        ico.Write((short)32);
        ico.Write(images[i].Length);
        ico.Write(offset);
        offset += images[i].Length;
    }
    foreach (var image in images) ico.Write(image);
    Console.WriteLine(path);
}

/// <summary>Les fenêtres de la session jouée : l'éditeur de Git garde le texte proposé, comme si l'élève l'enregistrait.</summary>
sealed class AcceptingDialogs : IDialogs
{
    public Task<SmagxSettings?> AskIdentityAsync(SmagxSettings current, IReadOnlyCollection<string> takenLogins, bool another) =>
        Task.FromResult(another ? new SmagxSettings { Name = "Bob Durand", Email = "bob@lycee.example", Login = "bob" } : null);
    public Task<string?> EditAsync(string text) => Task.FromResult<string?>(text);
    public Task CollaborateAsync(MainWindowViewModel vm) => Task.CompletedTask;
    public Task ManageKeysAsync(KeysViewModel keys) => Task.CompletedTask;
    public Task<(SmagxLab Lab, ScenarioMode Mode)?> ChooseScenarioAsync(bool joined) => Task.FromResult<(SmagxLab, ScenarioMode)?>(null);
}
