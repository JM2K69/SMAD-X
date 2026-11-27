using System.Collections.ObjectModel;
using System.Net.Sockets;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SMAGX.Forge;
using SMAGX.Labs;

namespace SMAGX.ViewModels;

/// <summary>Une commande tapée dans le terminal : l'invite, la ligne, et ce qu'elle a écrit (avec les couleurs de Git).</summary>
public sealed record TerminalBlock(string Prompt, string Command, string Output);

/// <summary>Une entrée d'une des trois zones : la lettre d'état (M, A, D, ??, UU) et le chemin.</summary>
public sealed record ZoneItem(string Code, string Path, bool Staged, bool Conflict);

/// <summary>Un projet de la forge : son chemin, son adresse SSH à cloner, et ses branches (nom et dernier commit).</summary>
public sealed record ForgeProject(string Path, string Url, string Branches);

/// <summary>Ce que l'application demande à l'élève dans une fenêtre : une identité, un texte pour Git, le travail à deux, les clés.</summary>
public interface IDialogs
{
    /// <summary>L'identité d'un poste ; <paramref name="another"/> pour un poste de plus. Les noms d'utilisateur pris sont refusés.</summary>
    Task<SmagxSettings?> AskIdentityAsync(SmagxSettings current, IReadOnlyCollection<string> takenLogins, bool another);

    /// <summary>L'éditeur que Git ouvre : le texte proposé, rendu modifié, ou null si l'élève abandonne.</summary>
    Task<string?> EditAsync(string text);

    /// <summary>La fenêtre « Travail à deux » : servir ses forges ou rejoindre celles d'un camarade.</summary>
    Task CollaborateAsync(MainWindowViewModel vm);

    /// <summary>La page « Clés SSH » d'un compte de la forge.</summary>
    Task ManageKeysAsync(KeysViewModel keys);

    /// <summary>Le parcours des scénarios : celui choisi et où il se joue, ou null.</summary>
    Task<(SmagxLab Lab, ScenarioMode Mode)?> ChooseScenarioAsync(bool joined);
}

/// <summary>
/// La fenêtre de SMAG-X : les postes de la salle (un par élève, ou deux rôles pour un même élève) et les forges qu'ils
/// partagent (une façon GitLab, une façon GitHub), celles de ce SMAG-X ou celles d'un camarade jointes par le réseau.
/// </summary>
public partial class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly SmagxSettings _settings;
    private readonly IDialogs _dialogs;
    private List<SMAGX.Forge.Forge> _forges = [];
    private ForgeServer? _server;
    private IReadOnlyList<RemoteForge>? _joined;
    private DispatcherTimer? _poll;
    private bool _refreshing, _refreshAgain;
    private readonly SortedSet<string> _visitors = new(StringComparer.Ordinal);

    public MainWindowViewModel(SmagxSettings settings, IDialogs dialogs)
    {
        _settings = settings;
        _dialogs = dialogs;
        NewForges();
        var first = new StationViewModel(Primary(), ActiveForges, dialogs);
        Add(first);
        Current = first;
    }

    public ObservableCollection<StationViewModel> Stations { get; } = [];

    /// <summary>Le poste affiché : son terminal, son historique, ses zones et ses fichiers.</summary>
    [ObservableProperty] private StationViewModel _current = null!;

    partial void OnCurrentChanged(StationViewModel value)
    {
        // La liste des onglets se vide quand un scénario remplace les postes
        if (value is null) return;
        Scenario?.ShowRole(value.RoleId);
        _ = RefreshForgeAsync();
    }

    public bool HasIdentity => _settings.HasIdentity;

    private static SmagxText Text => SmagxText.Instance;

    /// <summary>L'identité de l'élève (celle du premier poste), ou un compte « eleve » tant qu'il ne l'a pas choisie.</summary>
    private SmagxSettings Primary() => _settings.HasIdentity
        ? new SmagxSettings { Name = _settings.Name, Email = _settings.Email, Login = _settings.Login }
        : new SmagxSettings { Name = "", Email = "", Login = "eleve" };

    private void Add(StationViewModel station)
    {
        station.CommandRan += () => _ = RefreshForgeAsync();
        station.RemoveRequested += RemoveStation;
        Stations.Add(station);
        OnPropertyChanged(nameof(SeveralStations));
    }

    public bool SeveralStations => Stations.Count > 1;

    // ── Identité et postes ──────────────────────────────────────────────────

    /// <summary>Au premier lancement : l'identité de l'élève, gardée pour les fois suivantes, puis un poste neuf.</summary>
    public async Task AskIdentityAsync()
    {
        var station = Stations[0];
        if (await _dialogs.AskIdentityAsync(_settings, OtherLogins(station), false) is not { } chosen) return;
        SaveIdentity(chosen);
        station.Reset(ActiveForges, chosen);
        Current = station;
        OnPropertyChanged(nameof(HasIdentity));
        await RefreshForgeAsync();
    }

    /// <summary>Change l'identité du poste affiché, qui repart à neuf (celle du premier poste est gardée pour les fois suivantes).</summary>
    [RelayCommand]
    private async Task ChangeIdentity()
    {
        if (InScenario) return;
        var station = Current;
        if (await _dialogs.AskIdentityAsync(station.Identity, OtherLogins(station), station != Stations[0]) is not { } chosen) return;
        if (station == Stations[0]) SaveIdentity(chosen);
        station.Reset(ActiveForges, chosen);
        OnPropertyChanged(nameof(HasIdentity));
        await RefreshForgeAsync();
    }

    private void SaveIdentity(SmagxSettings chosen)
    {
        _settings.Name = chosen.Name;
        _settings.Email = chosen.Email;
        _settings.Login = chosen.Login;
        _settings.Save();
    }

    private List<string> OtherLogins(StationViewModel except) =>
        Stations.Where(s => s != except).Select(s => s.Login).ToList();

    /// <summary>Un poste de plus dans la salle : un autre élève (ou le même dans un autre rôle), sur les mêmes forges.</summary>
    [RelayCommand]
    private async Task AddStation()
    {
        if (InScenario) return;
        if (await _dialogs.AskIdentityAsync(new SmagxSettings(), Stations.Select(s => s.Login).ToList(), true) is not { } chosen) return;
        var station = new StationViewModel(chosen, ActiveForges, _dialogs) { Removable = true };
        Add(station);
        Current = station;
    }

    private void RemoveStation(StationViewModel station)
    {
        if (!station.Removable || !Stations.Contains(station)) return;
        var index = Stations.IndexOf(station);
        if (Current == station) Current = Stations[index > 0 ? index - 1 : 1];
        Stations.Remove(station);
        OnPropertyChanged(nameof(SeveralStations));
    }

    /// <summary>
    /// Tous les postes repartent à neuf, et les forges de ce SMAG-X aussi, sauf si elles sont servies à un camarade
    /// (elles sont alors les siennes aussi).
    /// </summary>
    [RelayCommand]
    private async Task StartSession()
    {
        if (InScenario)
        {
            await QuitScenario();
            return;
        }
        if (_server is null) NewForges();
        foreach (var station in Stations) station.Reset(ActiveForges);
        _ = RefreshForgeAsync();
    }

    [RelayCommand]
    private void ToggleLanguage()
    {
        Text.SetLanguage(Text.French ? "en-US" : "fr-FR");
        foreach (var station in Stations) station.LanguageChanged();
        NetworkChanged();
        if (Scenario is { } scenario)
        {
            scenario.Relabel();
            scenario.ShowRole(Current.RoleId);
        }
        _ = RefreshForgeAsync();
    }

    // ── Forges ──────────────────────────────────────────────────────────────

    public ObservableCollection<ForgeProject> Projects { get; } = [];

    [ObservableProperty] private string _newProjectName = "";

    /// <summary>Vide, ou « la forge du camarade ne répond pas ».</summary>
    [ObservableProperty] private string _forgeMessage = "";

    /// <summary>« Compte alice · 1 clé SSH » : le compte du poste affiché sur la forge choisie.</summary>
    [ObservableProperty] private string _accountText = "";

    /// <summary>Faux : GitLab (gitlab.example) ; vrai : GitHub (github.example).</summary>
    [ObservableProperty] private bool _gitHubShown;

    partial void OnGitHubShownChanged(bool value)
    {
        OnPropertyChanged(nameof(ForgeHost));
        OnPropertyChanged(nameof(ForgeIntro));
        _ = RefreshForgeAsync();
    }

    public bool NoProject => Projects.Count == 0 && ForgeMessage.Length == 0;

    /// <summary>Les forges de ce SMAG-X : GitLab, puis GitHub.</summary>
    public IReadOnlyList<SMAGX.Forge.Forge> Forges => _forges;

    /// <summary>Les forges que les postes joignent : celles de ce SMAG-X, ou celles du camarade rejoint.</summary>
    public IReadOnlyList<IForge> ActiveForges => (IReadOnlyList<IForge>?)_joined ?? _forges;

    /// <summary>La forge montrée dans la carte Forge.</summary>
    public IForge ShownForge => ActiveForges.FirstOrDefault(f => f.Kind == (GitHubShown ? ForgeKind.GitHub : ForgeKind.GitLab)) ?? ActiveForges[0];

    public string ForgeHost => ShownForge.Host;

    public string ForgeIntro => GitHubShown ? Text["Forge.IntroGitHub"] : Text["Forge.Intro"];

    [RelayCommand]
    private void ShowGitLab() => GitHubShown = false;

    [RelayCommand]
    private void ShowGitHub() => GitHubShown = true;

    private void NewForges()
    {
        foreach (var forge in _forges) forge.Changed -= OnForgeChanged;
        _forges = [new SMAGX.Forge.Forge("gitlab.example"), new SMAGX.Forge.Forge("github.example", ForgeKind.GitHub)];
        foreach (var forge in _forges) forge.Changed += OnForgeChanged;
    }

    // Un poste, ou le camarade par le réseau, a changé une forge : depuis un autre fil
    private void OnForgeChanged() => Dispatcher.UIThread.Post(() => _ = RefreshForgeAsync());

    /// <summary>Relit les projets et les clés de la forge montrée (par le réseau si elle est chez un camarade), sans bloquer l'interface.</summary>
    public async Task RefreshForgeAsync()
    {
        if (_refreshing)
        {
            _refreshAgain = true;
            return;
        }
        _refreshing = true;
        try
        {
            do
            {
                _refreshAgain = false;
                if (Current is null) return;
                var forge = ShownForge;
                var account = Current.Login;
                IReadOnlyList<ProjectInfo> list = [];
                var keys = 0;
                var message = "";
                try
                {
                    (list, keys) = await Task.Run(() => (forge.Projects(), forge.Keys(account).Count));
                }
                catch (ForgeUnavailableException)
                {
                    message = Text["Forge.Unreachable"];
                }
                var projects = list.Select(p => new ForgeProject(p.Path, $"git@{forge.Host}:{p.Path}.git",
                    p.Branches.Count == 0 ? Text.T("vide : aucune branche", "empty: no branch") : string.Join(" · ", p.Branches.Select(b => $"{b.Branch} {b.Short}")))).ToList();
                if (!projects.SequenceEqual(Projects))
                {
                    Projects.Clear();
                    foreach (var project in projects) Projects.Add(project);
                }
                ForgeMessage = message;
                AccountText = Text.T($"Compte {account} · ", $"Account {account} · ")
                              + (keys == 0 ? Text.T("aucune clé SSH", "no SSH key") : keys == 1 ? Text.T("1 clé SSH", "1 SSH key") : Text.T($"{keys} clés SSH", $"{keys} SSH keys"));
                OnPropertyChanged(nameof(NoProject));
                OnPropertyChanged(nameof(ForgeHost));
            } while (_refreshAgain);
        }
        finally
        {
            _refreshing = false;
        }
    }

    /// <summary>« Nouveau projet » sur la forge montrée : un dépôt vide (branche main).</summary>
    [RelayCommand]
    private async Task CreateProject()
    {
        var name = NewProjectName.Trim().Trim('/').Replace('\\', '/');
        if (name.EndsWith(".git")) name = name[..^4];
        if (name.Length == 0 || name.Contains("..") || name.Contains(' ')) return;
        if (!name.Contains('/')) name = Current.Login + "/" + name;
        var forge = ShownForge;
        try
        {
            await Task.Run(() => forge.Create(name));
            NewProjectName = "";
        }
        catch (ForgeUnavailableException)
        {
            ForgeMessage = Text["Forge.Unreachable"];
        }
        await RefreshForgeAsync();
    }

    /// <summary>La page « Clés SSH » du compte du poste affiché, sur la forge montrée.</summary>
    [RelayCommand]
    private async Task ManageKeys()
    {
        var keys = new KeysViewModel(ShownForge, Current.Login);
        await keys.RefreshAsync();
        await _dialogs.ManageKeysAsync(keys);
        await RefreshForgeAsync();
    }

    // ── Travail à deux ──────────────────────────────────────────────────────

    [RelayCommand]
    private Task Collaborate() => _dialogs.CollaborateAsync(this);

    [ObservableProperty] private bool _serving;
    /// <summary>L'adresse à donner au camarade : celle de la carte réseau qui a une passerelle (le réseau de la salle).</summary>
    [ObservableProperty] private string _serverAddress = "";

    /// <summary>Les autres adresses du poste (cartes virtuelles, VPN), au cas où le camarade serait sur l'une d'elles.</summary>
    [ObservableProperty] private string _serverOtherAddresses = "";

    public bool HasServerOtherAddresses => ServerOtherAddresses.Length > 0;
    partial void OnServerOtherAddressesChanged(string value) => OnPropertyChanged(nameof(HasServerOtherAddresses));
    [ObservableProperty] private string _serverCode = "";
    [ObservableProperty] private string _serverVisitors = "";
    [ObservableProperty] private string _serverError = "";
    [ObservableProperty] private string _joinAddress = "";
    [ObservableProperty] private string _joinCode = "";
    [ObservableProperty] private string _joinError = "";
    [ObservableProperty] private bool _joining;

    public bool Joined => _joined is not null;
    public bool CanServe => !Joined;
    public bool CanJoin => !Serving && !Joined && !Joining;

    private string JoinedAt => _joined is [var j, ..] ? (j.Port == ForgeServer.DefaultPort ? j.Address : $"{j.Address}:{j.Port}") : "";

    /// <summary>D'où viennent les forges, sous le titre de la carte : vide (celles de ce SMAG-X), servies, ou celles du camarade.</summary>
    public string ForgeOrigin => Joined ? Text.T($"Celle du SMAG-X de {JoinedAt}", $"The one on {JoinedAt}'s SMAG-X")
        : Serving ? Text.T("Partagée par le réseau, code " + ServerCode, "Shared over the network, code " + ServerCode) : "";

    public bool HasForgeOrigin => ForgeOrigin.Length > 0;

    /// <summary>L'état du travail à deux dans l'en-tête, en deux mots ; le détail est dans l'infobulle.</summary>
    public string NetworkStatus => Joined ? Text.T("Connecté", "Connected") : Serving ? Text.T("Serveur actif", "Server on") : "";

    public bool HasNetworkStatus => NetworkStatus.Length > 0;

    /// <summary>L'infobulle de l'en-tête : adresses, code, camarades connectés (ou la forge rejointe).</summary>
    public string NetworkDetails
    {
        get
        {
            var open = Text.T("Cliquez pour ouvrir « Travail à deux ».", "Click to open \"Pair work\".");
            if (Joined) return Text.T($"Vos postes utilisent les forges du SMAG-X de {JoinedAt}.", $"Your workstations use the forges of {JoinedAt}'s SMAG-X.") + "\n" + open;
            if (!Serving) return "";
            var lines = new List<string>
            {
                Text.T("Ce SMAG-X sert ses forges (GitLab et GitHub).", "This SMAG-X serves its forges (GitLab and GitHub)."),
                Text.T($"Adresse : {ServerAddress}", $"Address: {ServerAddress}"),
                Text.T($"Code : {ServerCode}", $"Code: {ServerCode}")
            };
            if (HasServerOtherAddresses) lines.Add(Text.T($"Autres adresses : {ServerOtherAddresses}", $"Other addresses: {ServerOtherAddresses}"));
            lines.Add(_visitors.Count == 0 ? Text.T("Personne ne s'est encore connecté.", "Nobody has connected yet.")
                : Text.T("Connecté : ", "Connected: ") + string.Join(", ", _visitors));
            lines.Add(open);
            return string.Join("\n", lines);
        }
    }

    public string JoinedText => Joined
        ? Text.T($"Vos postes utilisent les forges du SMAG-X de {JoinedAt}.", $"Your workstations use the forges of {JoinedAt}'s SMAG-X.") : "";

    private void NetworkChanged()
    {
        foreach (var name in new[] { nameof(Joined), nameof(CanServe), nameof(CanJoin), nameof(ForgeOrigin), nameof(HasForgeOrigin),
                     nameof(NetworkStatus), nameof(HasNetworkStatus), nameof(NetworkDetails), nameof(JoinedText), nameof(ForgeIntro) })
            OnPropertyChanged(name);
        if (Serving) ServerVisitors = _visitors.Count == 0 ? Text.T("Personne ne s'est encore connecté.", "Nobody has connected yet.")
            : Text.T("Connecté : ", "Connected: ") + string.Join(", ", _visitors);
    }

    partial void OnServingChanged(bool value) => NetworkChanged();
    partial void OnJoiningChanged(bool value) => OnPropertyChanged(nameof(CanJoin));

    /// <summary>Sert les forges de ce SMAG-X sur le réseau de la salle, protégées par un code à quatre chiffres.</summary>
    [RelayCommand]
    private void StartServer()
    {
        if (_server is not null || Joined) return;
        ServerError = "";
        try
        {
            var server = new ForgeServer(_forges, Random.Shared.Next(1000, 10000).ToString());
            server.Start();
            server.Visited += address => Dispatcher.UIThread.Post(() =>
            {
                if (_visitors.Add(address)) NetworkChanged();
            });
            _server = server;
        }
        catch (SocketException e)
        {
            ServerError = Text.T("Le serveur n'a pas pu démarrer : ", "The server could not start: ") + e.Message;
            return;
        }
        var addresses = ForgeServer.LocalAddresses();
        var port = _server.Port == ForgeServer.DefaultPort ? "" : ":" + _server.Port;
        ServerAddress = addresses.Count == 0 ? Text.T("aucune adresse réseau", "no network address") : addresses[0] + port;
        ServerOtherAddresses = string.Join("  ·  ", addresses.Skip(1).Select(a => a + port));
        ServerCode = _server.Code;
        _visitors.Clear();
        Serving = true;
    }

    [RelayCommand]
    private void StopServer()
    {
        _server?.Dispose();
        _server = null;
        Serving = false;
        ServerVisitors = "";
    }

    /// <summary>Rejoint les forges d'un camarade : son adresse (et son port, s'il n'est pas 7878) et le code affiché chez lui.</summary>
    [RelayCommand]
    private async Task Join()
    {
        JoinError = "";
        var text = JoinAddress.Trim();
        var port = ForgeServer.DefaultPort;
        if (text.LastIndexOf(':') is var colon and > 0 && int.TryParse(text[(colon + 1)..], out var p))
        {
            port = p;
            text = text[..colon];
        }
        var code = JoinCode.Trim();
        if (text.Length == 0 || code.Length == 0)
        {
            JoinError = Text.T("Tapez l'adresse et le code affichés chez votre camarade.", "Type the address and the code shown on your classmate's screen.");
            return;
        }
        Joining = true;
        IReadOnlyList<RemoteForge> remotes;
        try
        {
            remotes = await Task.Run(() => RemoteForge.Connect(text, port, code));
        }
        catch (ForgeRefusedException)
        {
            JoinError = Text.T("Code refusé : vérifiez le code affiché chez votre camarade.", "Code refused: check the code shown on your classmate's screen.");
            return;
        }
        catch (ForgeUnavailableException)
        {
            JoinError = Text.T("Aucun SMAG-X ne répond à cette adresse : vérifiez-la, et que votre camarade a démarré le serveur.",
                "No SMAG-X answers at this address: check it, and that your classmate has started the server.");
            return;
        }
        finally
        {
            Joining = false;
        }
        if (remotes.Count == 0) return;
        _joined = remotes;
        foreach (var station in Stations) station.UseForges(remotes);
        // Les forges du camarade changent sans prévenir : la carte est relue régulièrement
        _poll = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _poll.Tick += (_, _) => _ = RefreshForgeAsync();
        _poll.Start();
        NetworkChanged();
        await RefreshForgeAsync();
    }

    /// <summary>Revient aux forges de ce SMAG-X.</summary>
    [RelayCommand]
    private async Task Leave()
    {
        _poll?.Stop();
        _poll = null;
        _joined = null;
        foreach (var station in Stations) station.UseForges(_forges);
        NetworkChanged();
        await RefreshForgeAsync();
    }

    // ── Scénarios ───────────────────────────────────────────────────────────

    /// <summary>Le scénario en cours (son panneau à gauche), ou null en travail libre.</summary>
    [ObservableProperty] private ScenarioViewModel? _scenario;

    public bool InScenario => Scenario is not null;

    partial void OnScenarioChanged(ScenarioViewModel? value) => OnPropertyChanged(nameof(InScenario));

    private bool _checking, _checkAgain;

    [RelayCommand]
    private async Task ChooseScenario()
    {
        if (await _dialogs.ChooseScenarioAsync(Joined) is not { } choice) return;
        await StartScenarioAsync(choice.Lab, choice.Mode);
    }

    /// <summary>
    /// Lance un scénario : des forges neuves (sauf pour celui qui rejoint celles d'un camarade, et son serveur démarré
    /// pour celui qui les sert), un poste par rôle joué ici, puis le départ du scénario.
    /// </summary>
    public async Task StartScenarioAsync(SmagxLab lab, ScenarioMode mode)
    {
        if (mode == ScenarioMode.Join && !Joined) return;
        if (mode != ScenarioMode.Join)
        {
            StopServer();
            if (Joined) await Leave();
            NewForges();
            if (mode == ScenarioMode.Serve) StartServer();
        }
        var roles = mode switch
        {
            ScenarioMode.Here => lab.Roles,
            ScenarioMode.Serve => lab.Roles.Where(r => r.Serves).ToList(),
            _ => lab.Roles.Where(r => !r.Serves).ToList()
        };
        Scenario = null;
        Stations.Clear();
        foreach (var role in roles)
        {
            var identity = role.Login is null ? Primary() : new SmagxSettings { Name = role.Name, Email = role.Email, Login = role.Login };
            Add(new StationViewModel(identity, ActiveForges, _dialogs) { RoleId = role.Id });
        }
        var stations = Stations.ToDictionary(s => s.RoleId!, s => s.Workstation);
        var forges = mode == ScenarioMode.Join ? null : _forges;
        await Task.Run(() => SmagxLabSetup.Apply(lab, stations, forges, DateTimeOffset.Now.AddHours(-2)));
        foreach (var station in Stations) station.Refresh();
        // La carte Forge montre celle du scénario
        GitHubShown = lab.Setup.Contains("github.example") || lab.Objectives.Any(o => o.Check.Host == "github.example");
        Scenario = new ScenarioViewModel(lab, mode);
        Current = Stations[0];
        Scenario.ShowRole(Current.RoleId);
        await RefreshForgeAsync();
    }

    [RelayCommand]
    private Task RestartScenario() => Scenario is { } s ? StartScenarioAsync(s.Lab, s.Mode) : Task.CompletedTask;

    /// <summary>Quitte le scénario : retour au poste de l'élève, en travail libre (le travail à deux continue).</summary>
    [RelayCommand]
    private async Task QuitScenario()
    {
        if (Scenario is null) return;
        Scenario = null;
        Stations.Clear();
        var first = new StationViewModel(Primary(), ActiveForges, _dialogs);
        Add(first);
        Current = first;
        await RefreshForgeAsync();
    }

    [RelayCommand]
    private Task CheckNow() => CheckScenarioAsync();

    /// <summary>
    /// « Vérifier » : les objectifs du scénario sur les postes et la forge, hors du fil de l'interface (la forge peut
    /// être chez un camarade). Comme dans SMAD-X, chaque objectif manqué dit ce qui manque, sans dire comment faire ;
    /// en examen, « Vérifier » n'existe que si le formateur l'a permis, et ne dit que ce qui est atteint.
    /// </summary>
    public async Task CheckScenarioAsync()
    {
        if (Scenario is not { } scenario || !scenario.CanCheck) return;
        if (_checking)
        {
            _checkAgain = true;
            return;
        }
        _checking = true;
        try
        {
            do
            {
                _checkAgain = false;
                var stations = Stations.Where(s => s.RoleId is not null).ToDictionary(s => s.RoleId!, s => s.Workstation);
                var forges = ActiveForges;
                var culture = Text.Culture;
                try
                {
                    var result = await Task.Run(() => SmagxLabGrader.Grade(scenario.Lab, stations, forges, null, culture));
                    if (Scenario == scenario) scenario.Apply(result);
                }
                catch (InvalidOperationException)
                {
                    // Un poste changeait pendant la vérification : elle sera refaite à la prochaine commande
                }
            } while (_checkAgain);
        }
        finally
        {
            _checking = false;
        }
    }

    public void Dispose()
    {
        _poll?.Stop();
        _server?.Dispose();
        GC.SuppressFinalize(this);
    }
}
