using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SMARX.Models;
using SMARX.Services.Windows;

namespace SMARX.ViewModels
{
    /// <summary>
    /// Une ligne du jeton dans la fenêtre whoami : ce qu'écrit Windows, ce que cela veut dire, et ce qui a changé depuis
    /// l'ouverture de la session (« + » ajouté, « − » retiré, « ~ » attributs changés).
    /// </summary>
    public sealed record WhoamiRow(string Name, string Type, string Sid, string Attributes, string Meaning, string Change, string Color, string Background)
    {
        public bool IsChanged => Change.Length > 0;
    }

    /// <summary>
    /// La fenêtre whoami d'un compte : le jeton de sa session, sur le serveur ou par un partage, élevée ou non, avec chaque
    /// identité expliquée. Windows lit les groupes à l'ouverture de session : si les groupes du compte ont changé depuis,
    /// la fenêtre montre le jeton de la prochaine session et ce qui change, et « Fermer et rouvrir la session » l'applique.
    /// </summary>
    public sealed partial class WhoamiViewModel : ObservableObject, IDisposable
    {
        private static SmarxText Text => SmarxText.Instance;
        private readonly WindowsWorkspaceViewModel _workspace;

        public WhoamiViewModel(WindowsWorkspaceViewModel workspace, string account, bool network, bool elevated)
        {
            _workspace = workspace;
            _account = account;
            _network = network;
            _elevated = elevated;
            _workspace.AccountsChanged += Refresh;
            Refresh();
        }

        public ObservableCollection<string> Accounts { get; } = new();
        public ObservableCollection<WhoamiRow> Rows { get; } = new();

        [ObservableProperty] private string _account;
        [ObservableProperty] private bool _network;
        [ObservableProperty] private bool _elevated;
        [ObservableProperty] private string _title = "";
        [ObservableProperty] private string _sessionText = "";
        [ObservableProperty] private string _tableTitle = "";
        [ObservableProperty] private bool _hasChanges;
        [ObservableProperty] private string _changes = "";
        [ObservableProperty] private bool _canElevate;

        public bool Local
        {
            get => !Network;
            set => Network = !value;
        }

        partial void OnAccountChanged(string value) => Refresh();
        partial void OnNetworkChanged(bool value)
        {
            OnPropertyChanged(nameof(Local));
            Refresh();
        }
        partial void OnElevatedChanged(bool value) => Refresh();

        /// <summary>Le compte ferme et rouvre sa session (ou se reconnecte au partage) : son jeton prend ses groupes d'aujourd'hui.</summary>
        [RelayCommand]
        private void Relogon() => _workspace.Relogon(Account);

        private bool _refreshing;

        private void Refresh()
        {
            if (_refreshing) return;
            _refreshing = true;
            try
            {
                var host = _workspace.Host;
                var names = host.Users.Select(u => u.Name).ToList();
                if (!Accounts.SequenceEqual(names))
                {
                    Accounts.Clear();
                    foreach (var name in names) Accounts.Add(name);
                }
                if (host.User(Account) is not { } user)
                {
                    Rows.Clear();
                    return;
                }
                Title = $"whoami — {host.Qualified(user.Name)}";
                CanElevate = !Network && WindowsGroups.IsAdministrator(host, user);
                var elevated = Elevated && CanElevate;

                // Le jeton d'après la reconnexion, et celui de la session ouverte (ce que whoami affiche maintenant)
                var next = WindowsWhoami.Of(host, user, Network, elevated);
                var sessionHost = _workspace.SessionOf(user.Name);
                var session = sessionHost?.User(user.Name) is { } before ? WindowsWhoami.Of(sessionHost, before, Network, elevated) : next;
                SessionText = session.Text();

                Rows.Clear();
                var old = session.Groups.ToDictionary(g => g.Key, StringComparer.OrdinalIgnoreCase);
                foreach (var g in next.Groups)
                {
                    var change = !old.TryGetValue(g.Key, out var was) ? "+" : was.Attributes != g.Attributes ? "~" : "";
                    Rows.Add(Row(g, change));
                }
                foreach (var g in session.Groups.Where(g => next.Groups.All(n => !string.Equals(n.Key, g.Key, StringComparison.OrdinalIgnoreCase))))
                    Rows.Add(Row(g, "−"));

                var count = Rows.Count(r => r.IsChanged);
                HasChanges = count > 0;
                TableTitle = HasChanges
                    ? Text.T("Le jeton de la prochaine session, et ce qui change", "The token of the next session, and what changes")
                    : Text.T("Le jeton de la session", "The session's token");
                Changes = Text.T(
                    $"{count} différence(s) avec la session ouverte de {user.Name} : Windows lit les groupes à l'ouverture de session, et whoami y montre encore l'ancien jeton. Il faut fermer et rouvrir la session{(Network ? " (par un partage : se reconnecter)" : "")}. Le testeur et la matrice de SMAR-X calculent déjà avec les nouveaux groupes.",
                    $"{count} difference(s) with {user.Name}'s open session: Windows reads the groups when the session opens, and whoami there still shows the old token. The session must be closed and opened again{(Network ? " (through a share: reconnect)" : "")}. SMAR-X's tester and matrix already use the new groups.");
            }
            finally
            {
                _refreshing = false;
            }
        }

        private WhoamiRow Row(WhoamiEntry g, string change) => change switch
        {
            "+" => new(g.Name, g.Type, g.Sid, g.Attributes, Meaning(g), change, "#3FB950", "#263FB950"),
            "−" => new(g.Name, g.Type, g.Sid, g.Attributes, Meaning(g), change, "#F85149", "#26F85149"),
            "~" => new(g.Name, g.Type, g.Sid, g.Attributes, Meaning(g), change, "#D29922", "#26D29922"),
            _ => new(g.Name, g.Type, g.Sid, g.Attributes, Meaning(g), change, "Transparent", "Transparent")
        };

        /// <summary>Ce que veut dire une identité du jeton, et d'où elle vient.</summary>
        private string Meaning(WhoamiEntry g)
        {
            var french = Text.Culture == "fr-FR";
            return g.Origin switch
            {
                WhoamiOrigin.Everyone => Text.T("toute identité", "any identity"),
                WhoamiOrigin.AllAccounts => Text.T("tous les comptes", "every account"),
                WhoamiOrigin.Session => Network ? Text.T("session réseau, par un partage", "network session, through a share")
                                                : Text.T("session ouverte sur le serveur", "session open on the server"),
                WhoamiOrigin.Authenticated => Text.T("tout compte authentifié", "any authenticated account"),
                WhoamiOrigin.Label => g.Sid == "S-1-16-12288" ? Text.T("niveau d'intégrité élevé : session élevée", "high integrity level: elevated session")
                                                              : Text.T("niveau d'intégrité moyen", "medium integrity level"),
                _ => (g.Via is { } via ? Text.T($"par {WellKnown.Display(via, french)}", $"through {via}") : Text.T("membre direct", "direct member"))
                     + (g.DenyOnly ? Text.T(" ; sans élévation, refus seulement (UAC)", "; without elevation, deny only (UAC)") : "")
            };
        }

        public void Dispose() => _workspace.AccountsChanged -= Refresh;
    }
}
