using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SMAGX.Explain;
using SMAGX.Forge;
using SMAGX.Git;
using SMAGX.Machine;

namespace SMAGX.ViewModels;

/// <summary>
/// Une ligne du panneau Fichiers : un dossier ou un fichier, son chemin depuis la racine montrée, sa profondeur (le
/// retrait), pour un dossier s'il est vide (Git ne suit pas les dossiers vides), et s'il est ignoré par un .gitignore
/// (seulement s'il n'est pas suivi : un fichier déjà validé reste suivi, comme dans Git).
/// </summary>
public sealed record FileEntry(string Path, string Name, int Depth, bool IsFolder, bool IsEmpty, bool IsIgnored = false)
{
    public string Label => IsFolder ? Name + "/" : Name;
    public Avalonia.Thickness Indent => new(Depth * 16, 0, 0, 0);
    public bool ShowEmpty => IsEmpty && !IsIgnored;
    public double Opacity => IsIgnored ? 0.5 : 1;
}

/// <summary>
/// Un poste de la salle : le compte d'un élève (son nom d'utilisateur, son nom complet, son adresse), son terminal, son
/// historique, ses trois zones et ses fichiers. Plusieurs postes partagent la même forge : un élève peut jouer deux
/// rôles, ou deux élèves travailler sur le même SMAG-X.
/// </summary>
public partial class StationViewModel : ObservableObject
{
    private readonly IDialogs _dialogs;
    private readonly List<string> _history = [];
    private int _historyIndex;
    private Shell _shell = null!;

    private TaskCompletionSource<string?>? _answer;
    private string _runningPrompt = "", _runningLine = "";

    public StationViewModel(SmagxSettings identity, IReadOnlyList<IForge> forges, IDialogs dialogs)
    {
        _dialogs = dialogs;
        Identity = identity;
        Reset(forges);
    }

    public SmagxSettings Identity { get; private set; }

    public string Login => Identity.Login ?? "eleve";
    public string FullName => Identity.Name ?? "";
    public string IdentityText => $"{Identity.Name} <{Identity.Email}>";

    public Workstation Workstation { get; private set; } = null!;

    /// <summary>Après chaque commande : la forge a pu changer (carte Forge à rafraîchir).</summary>
    public event Action? CommandRan;

    /// <summary>Faux pour le premier poste (celui de l'élève) ; vrai pour les postes ajoutés, qu'on peut retirer.</summary>
    public bool Removable { get; init; }

    /// <summary>Dans un scénario, le rôle joué sur ce poste (« moi », « alice », « bob ») ; null en travail libre.</summary>
    public string? RoleId { get; init; }

    public event Action<StationViewModel>? RemoveRequested;

    [RelayCommand]
    private void Remove() => RemoveRequested?.Invoke(this);

    public ObservableCollection<TerminalBlock> Blocks { get; } = [];
    public ObservableCollection<ZoneItem> WorkItems { get; } = [];
    public ObservableCollection<ZoneItem> IndexItems { get; } = [];
    /// <summary>Le panneau Fichiers : dossiers et fichiers de la racine montrée, en arborescence (dossiers d'abord).</summary>
    public ObservableCollection<FileEntry> Entries { get; } = [];

    [ObservableProperty] private FileEntry? _selectedEntry;

    /// <summary>La racine montrée dans le panneau Fichiers : « ~/projet », ou le dépôt où l'on est.</summary>
    [ObservableProperty] private string _filesRootLabel = "";

    private bool _syncingSelection;

    [ObservableProperty] private string _prompt = "";
    [ObservableProperty] private string _input = "";
    [ObservableProperty] private bool _busy;

    /// <summary>La commande en cours qui attend une réponse (ssh, ssh-keygen) : ce qu'elle a écrit, question comprise.</summary>
    [ObservableProperty] private TerminalBlock? _pending;

    /// <summary>Vrai pendant qu'une commande attend la réponse de l'élève : la saisie lui répond.</summary>
    [ObservableProperty] private bool _answering;

    /// <summary>Le caractère qui cache une phrase de passe tapée ('\0' : saisie visible).</summary>
    [ObservableProperty] private char _mask;

    public bool CanType => !Busy || Answering;

    partial void OnBusyChanged(bool value) => OnPropertyChanged(nameof(CanType));
    partial void OnAnsweringChanged(bool value) => OnPropertyChanged(nameof(CanType));
    [ObservableProperty] private CommitGraph? _graph;
    [ObservableProperty] private string _graphMessage = "";
    [ObservableProperty] private string _repositorySummary = "";
    [ObservableProperty] private string _explanation = "";
    [ObservableProperty] private string? _selectedFile;
    [ObservableProperty] private string _editorText = "";
    [ObservableProperty] private string _newFileName = "";

    public bool WorkClean => WorkItems.Count == 0;
    public bool IndexEmpty => IndexItems.Count == 0;
    public bool HasSelectedFile => SelectedFile is not null;

    private static SmagxText Text => SmagxText.Instance;

    /// <summary>
    /// Un poste neuf : le compte de l'élève, son dossier personnel et un dossier ~/projet vide. Git n'est pas configuré :
    /// l'élève s'y présente lui-même avec git config.
    /// </summary>
    public void Reset(IReadOnlyList<IForge> forges, SmagxSettings? identity = null)
    {
        if (identity is not null) Identity = identity;
        Workstation = new Workstation(Login, "poste") { Editor = Edit, Ask = AskInTerminal, FullName = Identity.Name, Email = Identity.Email };
        Workstation.Network.AddRange(forges);
        Workstation.EnsureDirectory(Workstation.Home + "/projet");
        Workstation.Cwd = Workstation.Home + "/projet";
        _shell = new Shell(Workstation);
        _history.Clear();
        _historyIndex = 0;
        Blocks.Clear();
        SelectedFile = null;
        Explanation = Text["Explain.Start"];
        OnPropertyChanged(nameof(Login));
        OnPropertyChanged(nameof(FullName));
        OnPropertyChanged(nameof(IdentityText));
        Refresh();
    }

    /// <summary>Les forges que joint le poste (GitLab et GitHub) : celles de ce SMAG-X, ou celles d'un camarade par le réseau.</summary>
    public void UseForges(IReadOnlyList<IForge> forges)
    {
        Workstation.Network.Clear();
        Workstation.Network.AddRange(forges);
    }

    public void LanguageChanged()
    {
        if (Blocks.Count == 0) Explanation = Text["Explain.Start"];
        Refresh();
    }

    // ── Terminal ────────────────────────────────────────────────────────────

    /// <summary>Lance la ligne tapée, hors du fil de l'interface (l'éditeur de Git peut alors ouvrir sa fenêtre).</summary>
    [RelayCommand]
    public async Task RunAsync()
    {
        if (Answering)
        {
            Answer(Input);
            return;
        }
        var line = Input.Trim();
        Input = "";
        if (Busy) return;
        if (line.Length > 0 && (_history.Count == 0 || _history[^1] != line)) _history.Add(line);
        _historyIndex = _history.Count;
        if (line is "clear")
        {
            Blocks.Clear();
            return;
        }
        var prompt = Prompt;
        if (line.Length == 0)
        {
            Blocks.Add(new TerminalBlock(prompt, "", ""));
            return;
        }
        Busy = true;
        (_runningPrompt, _runningLine) = (prompt, line);
        try
        {
            var result = await Task.Run(() => _shell.Run(line));
            Pending = null;
            Blocks.Add(new TerminalBlock(prompt, line, result.Display));
            if (Explainer.Explain(Workstation, line, result, Text.Culture) is { } explanation)
                Explanation = explanation;
        }
        finally
        {
            Busy = false;
        }
        Refresh();
        CommandRan?.Invoke();
    }

    /// <summary>Flèche haut (-1) ou bas (+1) : la commande précédente ou suivante de l'historique.</summary>
    public void History(int direction)
    {
        if (_history.Count == 0) return;
        _historyIndex = Math.Clamp(_historyIndex + direction, 0, _history.Count);
        Input = _historyIndex < _history.Count ? _history[_historyIndex] : "";
    }

    public void Clear() => Blocks.Clear();

    // La ligne d'une tabulation sans résultat net : à la suivante, sur la même ligne, les choix s'affichent
    private string? _ambiguous;

    /// <summary>
    /// Tabulation : complète le mot sous le curseur (nom de commande, sous-commande de git, branche, fichier…) et rend
    /// la nouvelle position du curseur. Plusieurs choix sans début commun à ajouter : une deuxième tabulation les
    /// affiche sous la ligne, comme bash.
    /// </summary>
    public int Complete(int caret)
    {
        if (Busy) return caret;
        var completion = Completer.Complete(Workstation, Input, caret);
        if (completion.Line != Input)
        {
            _ambiguous = null;
            Input = completion.Line;
            return completion.Caret;
        }
        if (!completion.Ambiguous) return caret;
        if (_ambiguous == Input)
        {
            Blocks.Add(new TerminalBlock(Prompt, Input, string.Join("  ", completion.Candidates) + "\n"));
            _ambiguous = null;
        }
        else _ambiguous = Input;
        return caret;
    }

    /// <summary>
    /// Une question de la commande en cours (depuis son fil) : le terminal montre ce qu'elle a écrit, la saisie de l'élève
    /// devient la réponse. Une phrase de passe se tape masquée.
    /// </summary>
    private string? AskInTerminal(string shown, bool secret)
    {
        var answer = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        Dispatcher.UIThread.Post(() =>
        {
            _answer = answer;
            Pending = new TerminalBlock(_runningPrompt, _runningLine, shown);
            Mask = secret ? '•' : '\0';
            Input = "";
            Answering = true;
        });
        return answer.Task.GetAwaiter().GetResult();
    }

    /// <summary>La réponse tapée (Entrée), ou null (Ctrl+C : la commande abandonne).</summary>
    public void Answer(string? text)
    {
        if (!Answering || _answer is not { } answer) return;
        _answer = null;
        Input = "";
        Mask = '\0';
        Answering = false;
        answer.TrySetResult(text);
    }

    /// <summary>L'éditeur de Git, appelé depuis le fil de la commande : la fenêtre s'ouvre sur l'interface, la commande attend.</summary>
    private string? Edit(string text) =>
        Dispatcher.UIThread.InvokeAsync(() => _dialogs.EditAsync(text)).GetAwaiter().GetResult();

    // ── État affiché ────────────────────────────────────────────────────────

    /// <summary>Recalcule l'invite, le graphe, les trois zones et la liste des fichiers.</summary>
    public void Refresh()
    {
        Prompt = _shell.Prompt();
        var repo = Workstation.FindRepository(Workstation.Cwd);
        WorkItems.Clear();
        IndexItems.Clear();
        if (repo is null)
        {
            Graph = null;
            GraphMessage = Text["Graph.NoRepository"];
            RepositorySummary = "";
        }
        else
        {
            var graph = CommitGraph.Build(repo);
            Graph = graph.Rows.Count > 0 ? graph : null;
            GraphMessage = graph.Rows.Count > 0 ? "" : Text["Graph.Empty"];
            var status = Status.Compute(Workstation, repo);
            foreach (var c in status.Conflicts) WorkItems.Add(new ZoneItem(c.Short, Status.Show(Workstation, repo, c.Path), false, true));
            foreach (var c in status.Unstaged) WorkItems.Add(new ZoneItem(Code(c.Kind), Status.Show(Workstation, repo, c.Path), false, false));
            foreach (var path in status.Untracked) WorkItems.Add(new ZoneItem("??", Status.Show(Workstation, repo, path), false, false));
            foreach (var c in status.Staged) IndexItems.Add(new ZoneItem(Code(c.Kind), Status.Show(Workstation, repo, c.Path), true, false));
            RepositorySummary = Summary(repo, graph);
        }
        OnPropertyChanged(nameof(WorkClean));
        OnPropertyChanged(nameof(IndexEmpty));
        RefreshFiles();
    }

    private static string Code(ChangeKind kind) => kind switch { ChangeKind.Added => "A", ChangeKind.Modified => "M", _ => "D" };

    private static string Summary(Repository repo, CommitGraph graph)
    {
        var where = repo.CurrentBranch is { } branch ? $"{Text["Status.Branch"]} {branch}" : $"{Text["Status.Detached"]} ({repo.DetachedHead?.Short})";
        if (repo.Head is not { } head) return $"{where} · {Text.T("aucun commit", "no commits")}";
        var count = repo.Ancestors([head]).Count;
        var subject = repo.GetCommit(head).Subject;
        return $"{where} · {count} commit{(count > 1 ? "s" : "")} · HEAD {head.Short} « {subject} »"
               + (repo.MergeHead is null ? "" : Text.T(" · fusion en cours", " · merge in progress"));
    }

    // ── Fichiers ────────────────────────────────────────────────────────────

    /// <summary>Le dossier dont on montre les fichiers : la racine du dépôt, sinon le dossier courant.</summary>
    private string FilesRoot => Workstation.FindRepository(Workstation.Cwd)?.Root ?? Workstation.Cwd;

    private void RefreshFiles()
    {
        var root = FilesRoot;
        var home = Workstation.Home;
        FilesRootLabel = root == home ? "~" : root.StartsWith(home + "/") ? "~" + root[home.Length..] : root;
        var entries = Tree(root);
        if (!entries.SequenceEqual(Entries))
        {
            var selected = SelectedFile;
            var folder = SelectedEntry is { IsFolder: true } f ? f.Path : null;
            _syncingSelection = true;
            Entries.Clear();
            foreach (var entry in entries) Entries.Add(entry);
            _syncingSelection = false;
            SelectedFile = selected is not null && entries.Any(e => !e.IsFolder && e.Path == selected) ? selected : null;
            if (SelectedFile is null && folder is not null) SelectedEntry = Entries.FirstOrDefault(e => e.IsFolder && e.Path == folder);
        }
        if (SelectedFile is { } current && !Workstation.FileExists(Workstation.Combine(root, current))) SelectedFile = null;
        else if (SelectedFile is { } shown) EditorText = Workstation.ReadText(Workstation.Combine(root, shown));
    }

    // Les dossiers (d'abord, chacun suivi de son contenu en retrait) puis les fichiers, comme un explorateur ; sans .git.
    // À la racine d'un dépôt, les .gitignore marquent ce que Git ne voit pas.
    private List<FileEntry> Tree(string root)
    {
        var entries = new List<FileEntry>();
        var repo = Workstation.FindRepository(Workstation.Cwd);
        var ignore = repo is not null && repo.Root == root ? Ignore.Load(Status.WorkTree(Workstation, repo)) : null;
        bool Ignored(string relative, bool folder) => ignore is not null && (folder
            // Un dossier est ignoré si un fichier quelconque dedans le serait, et qu'aucun fichier dedans n'est suivi
            ? ignore.IsIgnored(relative + "/\u0001") && !repo!.Index.Entries.Any(e => e.Path.StartsWith(relative + "/", StringComparison.Ordinal))
            : !repo!.Index.Contains(relative) && ignore.IsIgnored(relative));
        void Walk(string dir, string relative, int depth)
        {
            var prefix = dir == "/" ? "/" : dir + "/";
            foreach (var name in Workstation.Directories.Where(d => d != dir && Workstation.Parent(d) == dir)
                         .Select(d => d[prefix.Length..]).Where(n => n != ".git").Order(StringComparer.Ordinal))
            {
                var full = prefix + name;
                var empty = !Workstation.Files.Keys.Any(f => f.StartsWith(full + "/", StringComparison.Ordinal));
                entries.Add(new FileEntry(relative + name, name, depth, true, empty, Ignored(relative + name, folder: true)));
                Walk(full, relative + name + "/", depth + 1);
            }
            foreach (var name in Workstation.Files.Keys.Where(f => Workstation.Parent(f) == dir)
                         .Select(f => f[prefix.Length..]).Order(StringComparer.Ordinal))
                entries.Add(new FileEntry(relative + name, name, depth, false, false, Ignored(relative + name, folder: false)));
        }
        Walk(root, "", 0);
        return entries;
    }

    // Un dossier choisi : pas d'éditeur ; un fichier choisi : son contenu dans l'éditeur
    partial void OnSelectedEntryChanged(FileEntry? value)
    {
        if (_syncingSelection) return;
        SelectedFile = value is { IsFolder: false } ? value.Path : null;
    }

    partial void OnSelectedFileChanged(string? value)
    {
        EditorText = value is null ? "" : Workstation.ReadText(Workstation.Combine(FilesRoot, value));
        OnPropertyChanged(nameof(HasSelectedFile));
        _syncingSelection = true;
        if (value is not null) SelectedEntry = Entries.FirstOrDefault(e => !e.IsFolder && e.Path == value);
        else if (SelectedEntry is { IsFolder: false }) SelectedEntry = null;
        _syncingSelection = false;
    }

    [RelayCommand]
    private void SaveFile()
    {
        if (SelectedFile is not { } file) return;
        Workstation.WriteText(Workstation.Combine(FilesRoot, file), EditorText.Replace("\r\n", "\n"));
        Refresh();
    }

    [RelayCommand]
    private void NewFile()
    {
        var name = NewFileName.Trim().Replace('\\', '/').Trim('/');
        if (name.Length == 0 || name.Contains("..") || name == ".git" || name.StartsWith(".git/")) return;
        var path = Workstation.Combine(FilesRoot, name);
        if (!Workstation.FileExists(path)) Workstation.WriteText(path, "");
        NewFileName = "";
        Refresh();
        SelectedFile = name;
    }

    [RelayCommand]
    private void DeleteFile()
    {
        if (SelectedFile is not { } file) return;
        Workstation.DeleteFile(Workstation.Combine(FilesRoot, file));
        SelectedFile = null;
        Refresh();
    }
}
