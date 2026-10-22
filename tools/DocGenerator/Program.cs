// Contenu généré du site de documentation, en français et en anglais :
// - les captures d'écran : les fenêtres de SMAD-X sont rendues hors écran (Avalonia Headless avec le moteur
//   Skia), rien ne s'affiche sur le bureau et aucune touche n'est simulée ;
// - les pages tirées du code : règles d'audit et catalogue des exercices, qui ne peuvent donc pas diverger.
// Usage : dotnet run --project tools/DocGenerator -- <racine du dépôt>
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SMADX;
using SMADX.Graph;
using SMADX.Models;
using SMADX.Services;
using SMADX.Services.AttackPaths;
using SMADX.Services.Audit;
using SMADX.Services.Labs;
using SMADX.ViewModels;
using SMADX.Views;

var repo = Path.GetFullPath(args.Length > 0 ? args[0] : ".");

AppBuilder.Configure<App>()
    .UseSkia()
    .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
    .SetupWithoutStarting();

foreach (var (culture, lang) in new[] { ("fr-FR", "fr"), ("en-US", "en") })
{
    LocalizationService.Instance.SetLanguage(culture);
    TierConfigurationService.Instance.ResetToDefaults();
    var outDir = Path.Combine(repo, "docs", lang, "assets", "screens");
    Directory.CreateDirectory(outDir);
    string Out(string name) => Path.Combine(outDir, name + ".png");

    // ─── Fenêtre principale : domaine d'exemple, un compte sélectionné ──────────
    var (main, vm, dialogs) = OpenMain();
    Select(vm, "Administrator");
    Capture(main, Out("main"));

    // ─── Graphe des relations ──────────────────────────────────────────────────
    vm.ShowGraphViewCommand.Execute(null);
    CaptureGraph(dialogs.Graph!, Out("graph"));

    // ─── Délégations et sites ──────────────────────────────────────────────────
    vm.ShowDelegationsCommand.Execute(null);
    Pump();
    Capture(new DelegationsWindow(vm.RootObject!) { Width = 1400, Height = 820 }, Out("delegations"));
    Capture(new SitesWindow(vm.SitesTopology!) { Width = 1300, Height = 800 }, Out("sites"));
    main.Close();

    // Aucune capture ne montre un exercice ou un scénario fourni : la documentation ne donne pas les solutions.
    // Les démonstrations utilisent le domaine d'exemple et une faiblesse propre à la documentation (DemoWeakness).

    // ─── Panneau Sécurité : un compte de service du domaine d'exemple ─────────
    (main, vm, dialogs) = OpenMain();
    Select(vm, "svc-webapp"); // le gMSA du domaine d'exemple
    ScrollDetailsToEnd(main);
    Capture(main, Out("security-panel"));
    main.Close();

    // ─── Audit, chemins d'attaque et avant / après sur la démonstration ─────────
    (main, vm, dialogs) = OpenMain();
    var demoGroup = DemoWeakness(vm.RootObject!, culture);
    vm.MarkBaselineCommand.Execute(null);
    vm.ShowAuditCommand.Execute(null);
    var audit = dialogs.Audit!;
    audit.SelectedFinding = audit.Findings.First(f => f.Finding.Rule.Id == "ACL-01" && f.Finding.Object == demoGroup);
    Capture(new AuditWindow { DataContext = audit, Width = 1400, Height = 860 }, Out("audit"));

    vm.ShowAttackPathsCommand.Execute(null);
    var paths = dialogs.AttackPaths!;
    paths.SelectedPath = paths.Paths.First(p => p.Label == "GG-HelpDesk");
    Capture(new AttackPathsWindow { DataContext = paths, Width = 1400, Height = 760 }, Out("attack-paths"));
    paths.ShowInGraphCommand.Execute(null);
    CaptureGraph(dialogs.Graph!, Out("graph-attack-path"));

    demoGroup.Owner = "Domain Admins";
    vm.ShowComparisonCommand.Execute(null);
    Capture(new ComparisonWindow { DataContext = dialogs.Comparison, Width = 1300, Height = 760 }, Out("comparison"));
    main.Close();

    // ─── Exercice de démonstration, construit ici et jamais embarqué dans l'application ───
    (main, vm, dialogs) = OpenMain();
    vm.OpenBuiltInLabCommand.Execute(new LabMenuItem(DemoLab(culture), ""));
    var lab = dialogs.Lab!;
    Capture(new LabWindow { DataContext = lab, Width = 1400, Height = 900 }, Out("lab-statement"));
    lab.CheckCommand.Execute(null);
    Capture(new LabWindow { DataContext = lab, Width = 1400, Height = 900 }, Out("lab-checked"));
    main.Close();

    WriteRulesPage(repo, culture, lang);
    WriteLabCatalog(repo, culture, lang);
    Console.WriteLine($"{lang} : captures dans {outDir}");
}

// ─── Démonstration propre à la documentation ────────────────────────────────

// Lors d'une migration, la propriété du groupe Schema Admins a été donnée au helpdesk, et personne ne l'a reprise.
// Aucun exercice fourni n'utilise cette faiblesse.
static ADObject DemoWeakness(ADObject domain, string culture)
{
    var group = Flatten(domain).First(o => o.Type == ADObjectType.Group && o.Name == "Schema Admins");
    group.Owner = "GG-HelpDesk";
    return group;
}

static LabDefinition DemoLab(string culture)
{
    var start = new ADRootDocument { Version = ADRootDocument.CurrentVersion, Domain = new ADDataService().CreateSampleStructure() };
    ADDataService.RestoreParentReferences(start.Domain, null);
    var group = DemoWeakness(start.Domain, culture);
    LocalizedText Text(string text) => new() { [culture] = text };
    return new LabDefinition
    {
        Id = "demo-documentation", Language = culture, Level = LabLevel.Beginner, DurationMinutes = 10, ReferenceDate = DateTime.Today,
        Title = Text(T(culture, "Démonstration : la migration oubliée", "Demo: the forgotten migration")),
        Statement = Text(T(culture,
            """
            # Démonstration : la migration oubliée

            ## Contexte
            Chez **Contoso**, l'annuaire a été migré il y a deux ans par l'équipe du helpdesk. Vous préparez la revue annuelle des droits.

            ## Votre mission
            - Vérifiez que le helpdesk ne peut pas atteindre le contrôle du domaine.
            - Le helpdesk garde son groupe et ses délégations.

            > Exercice de démonstration de la documentation : il n'est pas fourni avec l'application.
            """,
            """
            # Demo: the forgotten migration

            ## Context
            At **Contoso**, the directory was migrated two years ago by the help desk team. You are preparing the yearly access review.

            ## Your mission
            - Check that the help desk cannot reach domain control.
            - The help desk keeps its group and its delegations.

            > Documentation demo lab: it is not shipped with the application.
            """)),
        Start = start,
        Objectives =
        [
            new() { Id = "o1", Points = 4, Title = Text(T(culture, "Le chemin depuis « GG-HelpDesk » est coupé", "The path from \"GG-HelpDesk\" is cut")),
                    Hint = Text(T(culture, "Regardez les propriétaires des groupes privilégiés.", "Look at the owners of privileged groups.")),
                    Check = new LabCheck { Type = LabCheckType.NoAttackPath, Object = "GG-HelpDesk" } },
            new() { Id = "o2", Points = 2, Title = Text(T(culture, "Les groupes privilégiés ont un propriétaire administratif", "Privileged groups have an administrative owner")),
                    Hint = Text(T(culture, "Fiche ACL-01.", "Rule sheet ACL-01.")),
                    Check = new LabCheck { Type = LabCheckType.RuleClear, RuleId = "ACL-01", Object = group.Name } },
            new() { Id = "o3", Points = 1, Title = Text(T(culture, "Le groupe GG-HelpDesk existe toujours", "Group GG-HelpDesk still exists")),
                    Hint = Text(T(culture, "Le helpdesk a besoin de son groupe.", "The help desk needs its group.")),
                    Check = new LabCheck { Type = LabCheckType.ObjectExists, Object = "GG-HelpDesk", ObjectType = ADObjectType.Group } }
        ]
    };
}

// ─── Pages générées ─────────────────────────────────────────────────────────

static string T(string culture, string fr, string en) => culture == "fr-FR" ? fr : en;

static string SeverityLabel(string culture, AuditSeverity severity) =>
    LocalizationService.Instance.Translate($"Audit.Severity.{severity}", culture);

static void WriteRulesPage(string repo, string culture, string lang)
{
    var loc = LocalizationService.Instance;
    string Title(AuditRule rule) => loc.Translate($"Audit.{rule.Id}", culture).Split('\n')[0].TrimStart('#', ' ').Trim();
    var families = new (string Prefix, string Fr, string En)[]
    {
        ("KRB", "Kerberos", "Kerberos"), ("DLG", "Délégation", "Delegation"), ("PWD", "Mots de passe", "Passwords"),
        ("ACC", "Comptes", "Accounts"), ("PRIV", "Groupes privilégiés", "Privileged groups"),
        ("ACL", "Propriétaires et permissions", "Owners and permissions"), ("TIER", "Cloisonnement (tiering)", "Tiering"),
        ("GRP", "Groupes", "Groups")
    };

    var sb = new System.Text.StringBuilder();
    sb.AppendLine(T(culture, "# Règles d'audit", "# Audit rules")).AppendLine();
    sb.AppendLine(T(culture,
        $"!!! info \"Page générée\"\n    Cette page est produite à partir du code de SMAD-X (`tools/DocGenerator`) : elle liste exactement les {AuditRules.All.Count} règles évaluées par l'application.",
        $"!!! info \"Generated page\"\n    This page is produced from the SMAD-X code (`tools/DocGenerator`): it lists exactly the {AuditRules.All.Count} rules the application evaluates.")).AppendLine();
    sb.AppendLine(T(culture,
        "Chaque règle en échec retire des points au score, une seule fois quel que soit le nombre d'objets concernés : **12** pour une règle critique, **6** élevée, **3** moyenne, **1** faible. Le niveau ANSSI estimé est celui du point de contrôle le plus critique en échec (5 si aucun) ; ce n'est pas le calcul officiel de l'outil ADS de l'ANSSI.",
        "Each failed rule takes points off the score, once whatever the number of objects involved: **12** for a critical rule, **6** high, **3** medium, **1** low. The estimated ANSSI level is the level of the most critical failed control point (5 if none); it is not the official computation of ANSSI's ADS tool.")).AppendLine();

    foreach (var (prefix, fr, en) in families)
    {
        var rules = AuditRules.All.Where(r => r.Id.StartsWith(prefix + "-")).ToList();
        if (rules.Count == 0) continue;
        sb.AppendLine($"## {T(culture, fr, en)}").AppendLine();
        sb.AppendLine(T(culture, "| Règle | Gravité | Niveau ANSSI | Références |", "| Rule | Severity | ANSSI level | References |"));
        sb.AppendLine("|---|---|---|---|");
        foreach (var rule in rules)
        {
            var refs = string.Join("<br>", rule.References.Select(r => $"[{r.FrameworkLabel} · {r.Id}]({r.Url})"));
            sb.AppendLine($"| **{rule.Id}** {Title(rule)} | {SeverityLabel(culture, rule.Severity)} | {rule.AnssiLevel?.ToString() ?? "—"} | {refs} |");
        }
        sb.AppendLine();
    }
    var path = Path.Combine(repo, "docs", lang, "reference", T(culture, "regles-audit.md", "audit-rules.md"));
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    File.WriteAllText(path, sb.ToString().ReplaceLineEndings("\n"));
}

static void WriteLabCatalog(string repo, string culture, string lang)
{
    var loc = LocalizationService.Instance;
    var labs = new LabService().BuiltInLabs().Where(l => l.Language is null || l.Language == culture).ToList();
    var sb = new System.Text.StringBuilder();
    sb.AppendLine(T(culture, "# Catalogue des exercices", "# Lab catalog")).AppendLine();
    sb.AppendLine(T(culture,
        "!!! info \"Page générée\"\n    Liste produite à partir des exercices embarqués dans SMAD-X (`tools/DocGenerator`).",
        "!!! info \"Generated page\"\n    List produced from the labs embedded in SMAD-X (`tools/DocGenerator`).")).AppendLine();

    void Table(string heading, string menu, IEnumerable<LabDefinition> items)
    {
        sb.AppendLine($"## {heading}").AppendLine();
        sb.AppendLine(menu).AppendLine();
        sb.AppendLine(T(culture, "| Exercice | Niveau | Durée | Domaine | Objectifs |", "| Lab | Level | Duration | Domain | Objectives |"));
        sb.AppendLine("|---|---|---|---|---|");
        foreach (var lab in items)
            sb.AppendLine($"| {lab.Title.Resolve(culture)} | {loc.Translate($"Lab.Level.{lab.Level}", culture)} | {lab.DurationMinutes} min | `{lab.Start.Domain.Name}` | {lab.Objectives.Count} ({lab.MaxPoints} pts) |");
        sb.AppendLine();
    }

    Table(T(culture, "Exercices généraux", "General labs"),
        T(culture, "Menu **Exercices > Exercices fournis**. Ces exercices sont bilingues.", "**Labs > Built-in labs** menu. These labs are bilingual."),
        labs.Where(l => l.Category == null));
    Table(T(culture, "Chemins d'attaque", "Attack paths"),
        T(culture, "Menu **Exercices > Chemins d'attaque**. Chaque exercice a son organisation et son domaine ; la version anglaise utilise d'autres domaines.",
                   "**Labs > Attack paths** menu. Each lab has its own organisation and domain; the French version uses other domains."),
        labs.Where(l => l.Category == MainWindowViewModel.AttackPathCategory).OrderBy(l => l.Id));

    var path = Path.Combine(repo, "docs", lang, T(culture, "exercices", "labs"), T(culture, "catalogue.md", "catalog.md"));
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    File.WriteAllText(path, sb.ToString().ReplaceLineEndings("\n"));
}

static IEnumerable<ADObject> Flatten(ADObject o) => new[] { o }.Concat(o.Children.SelectMany(Flatten));

// Fenêtre principale avec son vrai ViewModel ; le service de dialogues récupère les fenêtres demandées
static (MainWindow, MainWindowViewModel, CaptureDialogs) OpenMain(string? file = null)
{
    var dialogs = new CaptureDialogs();
    var window = new MainWindow { Width = 1500, Height = 950 };
    var vm = new MainWindowViewModel(dialogs);
    window.DataContext = vm;
    window.Show();
    if (file != null)
    {
        dialogs.OpenPath = file;
        // Attendre en faisant tourner le dispatcher : la suite de la commande s'y exécute
        var loading = vm.LoadFromFileCommand.ExecuteAsync(null);
        while (!loading.IsCompleted)
            Pump(50);
        loading.GetAwaiter().GetResult();
        // La barre d'état cite le chemin complet du fichier : seul son nom a sa place dans la documentation
        vm.StatusMessage = vm.StatusMessage.Replace(Path.GetDirectoryName(file) + Path.DirectorySeparatorChar, "");
    }
    Pump();
    return (window, vm, dialogs);
}

// Comme « Afficher dans l'arbre » : déplie les parents et sélectionne (la sélection est réappliquée par le dispatcher)
static void Select(MainWindowViewModel vm, string name)
{
    vm.SelectObject(Flatten(vm.RootObject!).First(o => o.Name == name));
    Pump();
}

// Le panneau de détails défile : on descend jusqu'à la section Sécurité
static void ScrollDetailsToEnd(MainWindow window)
{
    var details = window.GetVisualDescendants().OfType<ScrollViewer>()
        .Where(v => v.Extent.Height > v.Viewport.Height)
        .OrderByDescending(v => v.Extent.Height)
        .First();
    details.Offset = new Vector(0, details.Extent.Height);
    Pump();
}

// Laisse tourner le dispatcher (mise en page, liaisons, minuteries comme celle du graphe)
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

// Le graphe se place par simulation physique : on la laisse converger, puis on recadre
static void CaptureGraph(GraphViewModel graph, string path)
{
    var window = new GraphWindow(graph) { Width = 1400, Height = 860 };
    window.Show();
    Pump(6000);
    window.GetVisualDescendants().OfType<GraphCanvas>().Single().FitView();
    Capture(window, path);
}

/// <summary>Service de dialogues de capture : répond aux sélections de fichier et garde les fenêtres demandées.</summary>
sealed class CaptureDialogs : IDialogService
{
    public string? OpenPath { get; set; }
    public GraphViewModel? Graph { get; private set; }
    public AuditViewModel? Audit { get; private set; }
    public LabViewModel? Lab { get; private set; }
    public ComparisonViewModel? Comparison { get; private set; }
    public AttackPathsViewModel? AttackPaths { get; private set; }

    public Task<string?> PickSaveFileAsync(string title, string suggestedFileName, string defaultExtension, params FileFilter[] filters) => Task.FromResult<string?>(null);
    public Task<string?> PickOpenFileAsync(string title, params FileFilter[] filters) => Task.FromResult(OpenPath);
    public Task<NewDomainRequest?> ShowNewDomainAsync() => Task.FromResult<NewDomainRequest?>(null);
    public Task ShowRelationsAsync(RelationsViewModel viewModel) => Task.CompletedTask;
    public void ShowGraph(GraphViewModel viewModel) => Graph = viewModel;
    public void ShowAudit(AuditViewModel viewModel) => Audit = viewModel;
    public void ShowLab(LabViewModel viewModel) => Lab = viewModel;
    public void ShowComparison(ComparisonViewModel viewModel) => Comparison = viewModel;
    public void ShowAttackPaths(AttackPathsViewModel viewModel) => AttackPaths = viewModel;
    public Task ShowTierConfigurationAsync() => Task.CompletedTask;
    public Task ShowTimelineAsync() => Task.CompletedTask;
    public Task ShowDelegationsAsync(ADObject? root) => Task.CompletedTask;
    public Task ShowSitesAsync(ADSitesTopology? topology) => Task.CompletedTask;
    public Task<string?> ShowExportReportAsync(ADRootDocument document) => Task.FromResult<string?>(null);
    public Task ShowAboutAsync() => Task.CompletedTask;
    public Task ShowShortcutsAsync() => Task.CompletedTask;
}
