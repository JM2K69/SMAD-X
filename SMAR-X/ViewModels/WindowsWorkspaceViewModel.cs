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
    /// Espace Windows : l'arborescence NTFS, le testeur (avec la session élevée de l'UAC), ses étapes, la matrice, les
    /// autorisations modifiables, l'accès effectif et les comptes locaux.
    /// </summary>
    public sealed partial class WindowsWorkspaceViewModel : ObservableObject
    {
        private static SmarxText Text => SmarxText.Instance;
        private bool _loadingAccount;

        public WindowsWorkspaceViewModel(WindowsHost host)
        {
            SelectCellCommand = new RelayCommand<MatrixCell>(SelectCell);
            Editor.Changed += () =>
            {
                Security.Load(Host, SelectedNode);
                Recompute();
            };
            Load(host);
        }

        public WindowsHost Host { get; private set; } = new();
        public ObservableCollection<NtfsItem> Tree { get; } = new();
        public ObservableCollection<WindowsUserItem> Users { get; } = new();
        public ObservableCollection<OperationItem> Operations { get; } = new();
        public ObservableCollection<StepItem> Steps { get; } = new();
        public ObservableCollection<string> MatrixColumns { get; } = new();
        public ObservableCollection<MatrixRow> MatrixRows { get; } = new();
        public ObservableCollection<MembershipItem> Memberships { get; } = new();

        /// <summary>Les groupes créés (pas Administrators ni Users), pour les imbriquer : un groupe global dans un groupe de domaine local.</summary>
        public ObservableCollection<WindowsGroupItem> GroupItems { get; } = new();
        public ObservableCollection<MembershipItem> GroupMemberships { get; } = new();

        /// <summary>Les portées proposées pour un nouveau groupe : global et domaine local si le serveur est dans un domaine.</summary>
        public ObservableCollection<Choice> GroupScopes { get; } = new();
        public bool HasDomain => Host.Domain != null;

        public WindowsRightsEditorViewModel Editor { get; } = new();
        public SecurityTabViewModel Security { get; } = new();
        public IRelayCommand<MatrixCell> SelectCellCommand { get; }

        /// <summary>D'où vient l'accès testé : une session sur le serveur, ou un partage qui contient l'objet.</summary>
        public ObservableCollection<RouteItem> Routes { get; } = new();

        /// <summary>Le partage de l'objet choisi, s'il est partagé, et les partages plus larges qui y mènent.</summary>
        public ObservableCollection<string> ShareLines { get; } = new();

        [ObservableProperty] private NtfsItem? _selectedNode;
        [ObservableProperty] private WindowsUserItem? _selectedUser;
        [ObservableProperty] private OperationItem? _selectedOperation;
        [ObservableProperty] private bool _elevated;
        [ObservableProperty] private bool _hasResult;
        [ObservableProperty] private bool _allowed;
        [ObservableProperty] private string _verdict = "";
        [ObservableProperty] private string? _newItem;
        [ObservableProperty] private WindowsUserItem? _selectedAccount;
        [ObservableProperty] private string _newUserName = "";
        [ObservableProperty] private string _newGroupName = "";
        [ObservableProperty] private string _accountCommands = "";
        [ObservableProperty] private WindowsGroupItem? _selectedGroup;
        [ObservableProperty] private string _groupCommands = "";
        [ObservableProperty] private bool _newUserDomain = true;
        [ObservableProperty] private Choice? _newGroupScope;
        [ObservableProperty] private RouteItem? _selectedRoute;

        public string Title => $"{Host.Name} · Windows";
        public string VerdictTitle => Allowed ? Text["Tester.Allowed"] : Text["Tester.Denied"];
        public string VerdictSymbol => Allowed ? "✓" : "✕";
        public Avalonia.Media.IBrush VerdictBrush => Allowed ? Palette.Allowed : Palette.Denied;
        public Avalonia.Media.IBrush VerdictBackground => Allowed ? Palette.AllowedBack : Palette.DeniedBack;
        public bool CanElevate => SelectedUser is { IsAdministrator: true } u && !u.User.IsBuiltInAdministrator && !IsNetwork;
        public bool ShowCreation => SelectedNode?.IsDirectory == true && SelectedUser != null;
        public bool IsNetwork => SelectedRoute?.Share != null;
        public bool CanShare => SelectedNode?.IsDirectory == true;
        public string SharingTitle => Text.T($"Partage de {SelectedNode?.Name}", $"Sharing of {SelectedNode?.Name}");

        /// <summary>Le chemin de l'objet testé : sur le serveur, ou son chemin réseau par le partage choisi.</summary>
        public string TargetText => SelectedNode == null ? "" : SelectedRoute?.Share is { } share ? share.Unc(Host.Name, SelectedNode.Path) : SelectedNode.Path;

        public string AccessKind => IsNetwork
            ? Text.T("Accès distant : par un partage SMB, depuis un poste du réseau.", "Remote access: through an SMB share, from a network computer.")
            : Text["Tester.LocalWindows"];

        /// <summary>Charge un serveur : l'arborescence, les comptes, et un premier test (dave modifie planning.xlsx, s'il existe).</summary>
        public void Load(WindowsHost host)
        {
            Host = host;
            OnPropertyChanged(nameof(Title));
            Tree.Clear();
            Tree.Add(new NtfsItem(host.Root, host.Root.Name + "\\"));
            RebuildUsers("dave");
            SelectedNode = Find(Tree[0], @"D:\Partages\Projets\planning.xlsx") ?? Tree[0];
            SelectedOperation = Operations.FirstOrDefault(o => Equals(o.Operation, WindowsOperation.Write)) ?? SelectedOperation;
        }

        /// <summary>Choisit l'objet de ce chemin dans l'arbre, s'il existe (l'objet d'un exercice, à son ouverture).</summary>
        public void Select(string path)
        {
            if (Find(Tree[0], path) is { } item) SelectedNode = item;
        }

        private static NtfsItem? Find(NtfsItem item, string path) =>
            string.Equals(item.Path, path, StringComparison.OrdinalIgnoreCase) ? item : item.Children.Select(c => Find(c, path)).FirstOrDefault(f => f != null);

        private void RebuildUsers(string? preferred = null)
        {
            var tested = SelectedUser?.Name ?? preferred;
            var edited = SelectedAccount?.Name ?? preferred;
            Users.Clear();
            for (var i = 0; i < Host.Users.Count; i++)
                Users.Add(new WindowsUserItem(Host, Host.Users[i], i));
            SelectedUser = Users.FirstOrDefault(u => u.Name == tested) ?? Users.FirstOrDefault();
            SelectedAccount = Users.FirstOrDefault(u => u.Name == edited) ?? Users.FirstOrDefault();
            RebuildGroups();
        }

        private void RebuildGroups()
        {
            var edited = SelectedGroup?.Name;
            GroupItems.Clear();
            foreach (var g in Host.Groups.Where(g => g.Name is not (WellKnown.Administrators or WellKnown.Users)))
                GroupItems.Add(new WindowsGroupItem(Host, g));
            SelectedGroup = GroupItems.FirstOrDefault(g => g.Name == edited) ?? GroupItems.FirstOrDefault();

            var scope = NewGroupScope?.Value;
            GroupScopes.Clear();
            if (HasDomain)
            {
                GroupScopes.Add(new Choice(WindowsGroupScope.Global, Text.T("global (G_)", "global (G_)")));
                GroupScopes.Add(new Choice(WindowsGroupScope.DomainLocal, Text.T("domaine local (DL_)", "domain local (DL_)")));
            }
            GroupScopes.Add(new Choice(WindowsGroupScope.Local, Text.T("local du serveur", "local to the server")));
            NewGroupScope = GroupScopes.FirstOrDefault(c => Equals(c.Value, scope)) ?? GroupScopes.First();
            OnPropertyChanged(nameof(HasDomain));
        }

        partial void OnSelectedNodeChanged(NtfsItem? value)
        {
            // La route d'abord : changer l'opération recalcule, et l'ancienne route peut passer par un partage qui ne
            // contient pas le nouvel objet. Rien ne se calcule avant que l'objet, la route et l'opération correspondent.
            using (Rebuilding())
            {
                RebuildRoutes();
                var previous = SelectedOperation?.Operation;
                Operations.Clear();
                if (value != null)
                    foreach (var op in Enum.GetValues<WindowsOperation>().Where(o => WindowsAccess.Applies(o, value.Node, value.Path)))
                        Operations.Add(new OperationItem(op, WindowsExplainer.Operation(op, Text.Culture)));
                SelectedOperation = Operations.FirstOrDefault(o => Equals(o.Operation, previous)) ?? Operations.FirstOrDefault();
                Editor.Load(Host, value);
                Security.Load(Host, value);
                OnPropertyChanged(nameof(ShowCreation));
            }
            Recompute();
        }

        // Pendant une reconstruction, les liaisons de l'interface (vider une liste remet sa sélection à null) ne
        // déclenchent aucun calcul : un seul a lieu à la fin, sur un état cohérent.
        private int _rebuilding;

        private sealed class RebuildScope(WindowsWorkspaceViewModel owner) : IDisposable
        {
            public void Dispose() => owner._rebuilding--;
        }

        private RebuildScope Rebuilding()
        {
            _rebuilding++;
            return new RebuildScope(this);
        }

        /// <summary>Les routes vers l'objet choisi : en local, puis par chaque partage qui le contient (le plus proche d'abord).</summary>
        private void RebuildRoutes()
        {
            using var rebuilding = Rebuilding();
            var previous = SelectedRoute?.Share?.Name;
            Routes.Clear();
            Routes.Add(new RouteItem(null, Text.T("en local, sur le serveur", "locally, on the server"),
                Text.T("Sur le serveur", "On the server"), Text.T("session locale (console, RDP)", "local session (console, RDP)")));
            ShareLines.Clear();
            if (SelectedNode is { } node)
            {
                foreach (var share in SmbAccess.SharesOf(Host, node.Path))
                {
                    var unc = share.Unc(Host.Name, node.Path);
                    Routes.Add(new RouteItem(share, Text.T($"par le partage {unc}", $"through the share {unc}"),
                        Text.T("Depuis le réseau", "From the network"), $@"\\{Host.Name}\{share.Name}"));
                    ShareLines.Add(string.Equals(share.Path, node.Path, StringComparison.OrdinalIgnoreCase)
                        ? Text.T($"Partagé : {unc}", $"Shared: {unc}") + (share.IsHidden ? Text.T(" (caché)", " (hidden)") : "")
                        : Text.T($"Accessible par {unc}", $"Reachable through {unc}"));
                }
                if (ShareLines.Count == 0)
                    ShareLines.Add(Text.T("Non partagé : accessible en local seulement.", "Not shared: local access only."));
            }
            _loadingRoute = true;
            SelectedRoute = Routes.FirstOrDefault(r => previous != null && r.Share?.Name == previous) ?? Routes[0];
            _loadingRoute = false;
            OnPropertyChanged(nameof(CanShare));
            OnPropertyChanged(nameof(SharingTitle));
            RouteChanged();
        }

        private bool _loadingRoute;

        partial void OnSelectedRouteChanged(RouteItem? value)
        {
            if (_loadingRoute || _rebuilding > 0) return;
            RouteChanged();
            Recompute();
        }

        private void RouteChanged()
        {
            if (IsNetwork) Elevated = false;
            OnPropertyChanged(nameof(IsNetwork));
            OnPropertyChanged(nameof(CanElevate));
            OnPropertyChanged(nameof(TargetText));
            OnPropertyChanged(nameof(AccessKind));
        }

        partial void OnSelectedUserChanged(WindowsUserItem? value)
        {
            if (!CanElevate) Elevated = false;
            OnPropertyChanged(nameof(CanElevate));
            OnPropertyChanged(nameof(ShowCreation));
            Recompute();
        }

        partial void OnSelectedOperationChanged(OperationItem? value) => Recompute();
        partial void OnElevatedChanged(bool value) => Recompute();

        private void SelectCell(MatrixCell? cell)
        {
            if (cell?.User is not WindowsUserItem user) return;
            SelectedUser = user;
            SelectedOperation = Operations.FirstOrDefault(o => Equals(o.Operation, cell.Operation)) ?? SelectedOperation;
        }

        /// <summary>Après les fenêtres Modifier, Avancé ou Partage avancé : l'objet a pu changer, tout ce qui en dépend se recalcule.</summary>
        public void AfterSecurityWindow()
        {
            SelectedNode?.Refresh();
            Editor.Load(Host, SelectedNode);
            Security.Load(Host, SelectedNode);
            RebuildRoutes();
            Recompute();
        }

        /// <summary>Le testeur, la matrice, l'accès effectif et ce que recevrait un nouvel élément.</summary>
        public void Recompute()
        {
            if (_rebuilding > 0) return;
            Steps.Clear();
            if (SelectedNode is not { } node || SelectedUser is not { } user || SelectedOperation is not { Operation: WindowsOperation operation }
                || SelectedRoute?.Share is { } outside && !outside.Contains(node.Path))
            {
                HasResult = false;
                return;
            }

            HasResult = true;
            IReadOnlyList<WindowsExplainedStep> explained;
            if (SelectedRoute?.Share is { } share)
            {
                var remote = SmbAccess.Check(Host, user.User, share, node.Path, operation);
                Allowed = remote.Allowed;
                Verdict = WindowsExplainer.NetworkVerdict(remote, Text.Culture);
                explained = WindowsExplainer.NetworkSteps(Host, remote, Text.Culture);
            }
            else
            {
                var result = WindowsAccess.Check(Host, user.User, node.Path, operation, Elevated);
                Allowed = result.Allowed;
                Verdict = WindowsExplainer.Verdict(result, Text.Culture);
                explained = WindowsExplainer.Steps(Host, result, Text.Culture);
            }
            var number = 1;
            foreach (var step in explained)
                Steps.Add(new StepItem(number++, step.Passed, step.Title, step.Detail));
            OnPropertyChanged(nameof(VerdictTitle));
            OnPropertyChanged(nameof(VerdictSymbol));
            OnPropertyChanged(nameof(VerdictBrush));
            OnPropertyChanged(nameof(VerdictBackground));

            BuildMatrix(node);
            BuildCreation(node, user);
        }

        private void BuildMatrix(NtfsItem node)
        {
            var operations = (node.IsDirectory ? WindowsAccess.FolderOperations : WindowsAccess.FileOperations)
                .Where(o => WindowsAccess.Applies(o, node.Node, node.Path)).ToList();
            MatrixColumns.Clear();
            foreach (var op in operations)
                MatrixColumns.Add(ShortLabel(op));
            MatrixRows.Clear();
            var share = SelectedRoute?.Share;
            var local = share == null ? WindowsAccess.Matrix(Host, node.Path, Elevated).ToDictionary(r => r.User) : null;
            foreach (var user in Users)
            {
                // Par un partage : chaque compte depuis le réseau ; en local : la matrice de la session (élevée ou non)
                bool Granted(WindowsOperation op) => share != null
                    ? SmbAccess.Check(Host, user.User, share, node.Path, op).Allowed
                    : local![user.User].Results[op].Allowed;
                var cells = new ObservableCollection<MatrixCell>(operations.Select(op => new MatrixCell(op, Granted(op), SelectCellCommand)
                {
                    User = user,
                    IsSelected = user == SelectedUser && Equals(op, SelectedOperation?.Operation)
                }));
                MatrixRows.Add(new MatrixRow(user, cells));
            }
        }

        private static string ShortLabel(WindowsOperation operation) => operation switch
        {
            WindowsOperation.Read => Text.T("Lire", "Read"),
            WindowsOperation.Write => Text.T("Écrire", "Write"),
            WindowsOperation.Execute => Text.T("Exécuter", "Execute"),
            WindowsOperation.List => Text.T("Lister", "List"),
            WindowsOperation.CreateFile => Text.T("Créer un fichier", "Create a file"),
            WindowsOperation.CreateFolder => Text.T("Créer un dossier", "Create a folder"),
            WindowsOperation.Delete => Text.T("Supprimer", "Delete"),
            WindowsOperation.ChangePermissions => Text.T("Changer les autorisations", "Change permissions"),
            _ => WindowsExplainer.Operation(operation, Text.Culture)
        };

        /// <summary>Ce que recevrait un fichier créé par le compte testé dans le dossier choisi : son propriétaire et ses ACE.</summary>
        private void BuildCreation(NtfsItem node, WindowsUserItem user)
        {
            NewItem = null;
            if (!node.IsDirectory) return;
            var created = WindowsCreation.Create(Host, user.User, node.Path, Text.T("nouveau.txt", "new.txt"), NtfsNodeKind.File, Elevated);
            try
            {
                NewItem = created.Node == null
                    ? Text.T($"{user.Name} ne peut pas créer de fichier dans {node.Path} : il faut « Création de fichiers ».",
                             $"{user.Name} cannot create a file in {node.Path}: \"Create files\" is needed.")
                    : Text.T($"Propriétaire : {user.Name}, le créateur.\n", $"Owner: {user.Name}, the creator.\n")
                      + string.Join("\n", WindowsListing.Icacls(Host, created.Path));
            }
            finally
            {
                WindowsCreation.Undo(Host, created);
            }
        }

        // ─── Comptes et groupes locaux ──────────────────────────────────────────────

        partial void OnSelectedAccountChanged(WindowsUserItem? value)
        {
            _loadingAccount = true;
            Memberships.Clear();
            var french = Text.Culture == "fr-FR";
            if (value != null)
                foreach (var g in Host.Groups.Where(g => !string.Equals(g.Name, WellKnown.Users, StringComparison.OrdinalIgnoreCase)))
                    Memberships.Add(Membership(value.User.Name, g, value.User.Groups, OnMembershipChanged));
            _loadingAccount = false;
            DescribeAccount();
        }

        /// <summary>Une case « membre de » : grisée, avec sa raison, si Active Directory refuserait l'adhésion.</summary>
        private MembershipItem Membership(string member, WindowsGroup group, List<string> current, Action<MembershipItem> changed)
        {
            var isMember = current.Contains(group.Name, StringComparer.OrdinalIgnoreCase);
            var reason = WindowsGroups.CannotJoin(Host, member, group.Name, Text.Culture);
            return new MembershipItem(group.Name, isMember, changed)
            {
                Display = WellKnown.Display(group.Name, Text.Culture == "fr-FR"),
                ScopeTag = HasDomain ? WindowsGroups.ScopeLabel(group.Scope, Text.Culture) : "",
                // Une adhésion déjà là (venue d'un fichier) reste décochable
                IsEnabled = isMember || reason == null,
                Reason = reason
            };
        }

        partial void OnSelectedGroupChanged(WindowsGroupItem? value)
        {
            _loadingAccount = true;
            GroupMemberships.Clear();
            if (value != null)
                foreach (var g in Host.Groups.Where(g => !string.Equals(g.Name, WellKnown.Users, StringComparison.OrdinalIgnoreCase)
                                                         && !string.Equals(g.Name, value.Name, StringComparison.OrdinalIgnoreCase)))
                    GroupMemberships.Add(Membership(value.Name, g, value.Group.Groups, OnGroupMembershipChanged));
            _loadingAccount = false;
            DescribeGroup();
        }

        private void OnGroupMembershipChanged(MembershipItem item)
        {
            if (_loadingAccount || SelectedGroup?.Group is not { } group) return;
            group.Groups = GroupMemberships.Where(m => m.IsMember).Select(m => m.Group).ToList();
            AccountChanged();
        }

        private void OnMembershipChanged(MembershipItem item)
        {
            if (_loadingAccount || SelectedAccount?.User is not { } user) return;
            user.Groups = Memberships.Where(m => m.IsMember).Select(m => m.Group).ToList();
            AccountChanged();
        }

        /// <summary>net user /add : un compte local, membre de Users.</summary>
        [RelayCommand]
        private void AddUser()
        {
            var name = NewUserName.Trim();
            if (name.Length == 0 || Host.User(name) != null) return;
            Host.Users.Add(new WindowsUser { Name = name, Domain = HasDomain && NewUserDomain });
            NewUserName = "";
            RebuildUsers();
            SelectedAccount = Users.First(u => u.Name == name);
            AccountChanged();
        }

        /// <summary>Un groupe du domaine (New-ADGroup), global ou de domaine local, ou un groupe local du serveur (net localgroup).</summary>
        [RelayCommand]
        private void AddGroup()
        {
            var name = NewGroupName.Trim();
            if (name.Length == 0 || Host.Groups.Any(g => string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase))
                                 || Host.User(name) != null) return;
            Host.Groups.Add(new WindowsGroup { Name = name, Scope = NewGroupScope?.Value is WindowsGroupScope scope ? scope : WindowsGroupScope.Local });
            NewGroupName = "";
            AccountChanged();
            SelectedGroup = GroupItems.FirstOrDefault(g => g.Name == name) ?? SelectedGroup;
        }

        private void AccountChanged()
        {
            var account = SelectedAccount?.Name;
            RebuildUsers();
            SelectedAccount = Users.FirstOrDefault(u => u.Name == account) ?? SelectedAccount;
            OnSelectedAccountChanged(SelectedAccount);
            OnSelectedGroupChanged(SelectedGroup);
            Editor.Load(Host, SelectedNode);
            OnPropertyChanged(nameof(CanElevate));
            Recompute();
        }

        private void DescribeAccount()
        {
            if (SelectedAccount?.User is not { } user)
            {
                AccountCommands = "";
                return;
            }
            var lines = user.Groups.Select(g => WindowsGroups.AddMemberCommand(Host, user.Name, g)).ToList();
            lines.Add(Text.T($"# {user.Name} doit fermer et rouvrir sa session pour que ses groupes comptent",
                             $"# {user.Name} must sign out and in again for the groups to apply"));
            AccountCommands = string.Join("\n", lines);
        }

        private void DescribeGroup()
        {
            if (SelectedGroup?.Group is not { } group)
            {
                GroupCommands = "";
                return;
            }
            GroupCommands = string.Join("\n", group.Groups.Select(g => WindowsGroups.AddMemberCommand(Host, group.Name, g))
                .Prepend(WindowsGroups.CreateCommand(Host, group)));
        }

        public void Relabel()
        {
            var selected = SelectedOperation?.Operation;
            var items = Operations.Select(o => new OperationItem(o.Operation, WindowsExplainer.Operation((WindowsOperation)o.Operation, Text.Culture))).ToList();
            Operations.Clear();
            foreach (var item in items) Operations.Add(item);
            SelectedOperation = Operations.FirstOrDefault(o => Equals(o.Operation, selected));
            RebuildUsers();
            foreach (var item in All(Tree)) item.Refresh();
            Editor.Load(Host, SelectedNode);
            Security.Load(Host, SelectedNode);
            RebuildRoutes();
            Recompute();
            DescribeAccount();
            DescribeGroup();
        }

        private static IEnumerable<NtfsItem> All(IEnumerable<NtfsItem> items) => items.SelectMany(i => new[] { i }.Concat(All(i.Children)));
    }
}
