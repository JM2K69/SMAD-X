using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SMARX.Models;
using SMARX.Services.Linux;

namespace SMARX.ViewModels
{
    /// <summary>
    /// Espace Linux : l'arborescence du serveur, le testeur « qui fait quoi » et ses étapes, la matrice d'accès, les
    /// droits modifiables et les comptes. Tout se recalcule à chaque choix et à chaque modification.
    /// </summary>
    public sealed partial class LinuxWorkspaceViewModel : ObservableObject
    {
        private static SmarxText Text => SmarxText.Instance;
        private bool _loadingAccount;

        public LinuxWorkspaceViewModel(LinuxHost host)
        {
            SelectCellCommand = new RelayCommand<MatrixCell>(SelectCell);
            Editor.Changed += Recompute;
            Load(host);
        }

        public LinuxHost Host { get; private set; } = new();
        public ObservableCollection<NodeItem> Tree { get; } = new();
        public ObservableCollection<UserItem> Users { get; } = new();
        public ObservableCollection<OperationItem> Operations { get; } = new();
        public ObservableCollection<StepItem> Steps { get; } = new();
        public ObservableCollection<PathSegment> PathSegments { get; } = new();
        public ObservableCollection<string> MatrixColumns { get; } = new();
        public ObservableCollection<MatrixRow> MatrixRows { get; } = new();
        public ObservableCollection<string> CreationReasons { get; } = new();
        public ObservableCollection<MembershipItem> Memberships { get; } = new();
        public ObservableCollection<string> GroupNames { get; } = new();
        public PermissionEditorViewModel Editor { get; } = new();
        public IRelayCommand<MatrixCell> SelectCellCommand { get; }
        public IReadOnlyList<string> Umasks { get; } = ["022", "027", "077", "002"];

        [ObservableProperty] private NodeItem? _selectedNode;
        [ObservableProperty] private UserItem? _selectedUser;
        [ObservableProperty] private OperationItem? _selectedOperation;
        [ObservableProperty] private bool _hasResult;
        [ObservableProperty] private bool _allowed;
        [ObservableProperty] private string _verdict = "";
        [ObservableProperty] private string? _problem;
        [ObservableProperty] private string? _newFile;
        [ObservableProperty] private string? _newDirectory;
        [ObservableProperty] private UserItem? _selectedAccount;
        [ObservableProperty] private string? _primaryGroup;
        [ObservableProperty] private string? _umask;
        [ObservableProperty] private string _newUserName = "";
        [ObservableProperty] private string _newGroupName = "";
        [ObservableProperty] private string _accountCommands = "";

        public string Title => $"{Host.Name} · Linux";
        public string VerdictTitle => Allowed ? Text["Tester.Allowed"] : Text["Tester.Denied"];
        public string VerdictSymbol => Allowed ? "✓" : "✕";
        public Avalonia.Media.IBrush VerdictBrush => Allowed ? Palette.Allowed : Palette.Denied;
        public Avalonia.Media.IBrush VerdictBackground => Allowed ? Palette.AllowedBack : Palette.DeniedBack;
        public bool ShowCreation => SelectedNode?.IsDirectory == true && SelectedUser != null;

        /// <summary>Charge un serveur : l'arborescence, les comptes, et un premier test (bob lit le budget, s'il existe).</summary>
        public void Load(LinuxHost host)
        {
            Host = host;
            OnPropertyChanged(nameof(Title));
            Tree.Clear();
            Tree.Add(new NodeItem(host.Root, "/"));
            RebuildUsers("bob");
            SelectedNode = Find(Tree[0], "/srv/compta/budget.ods") ?? Tree[0];
        }

        private static NodeItem? Find(NodeItem item, string path) =>
            item.Path == path ? item : item.Children.Select(c => Find(c, path)).FirstOrDefault(f => f != null);

        /// <summary>Les comptes ont changé : leurs pastilles et lignes « id », en gardant les comptes choisis.</summary>
        private void RebuildUsers(string? preferred = null)
        {
            var tested = SelectedUser?.Name ?? preferred;
            var edited = SelectedAccount?.Name ?? preferred;
            Users.Clear();
            for (var i = 0; i < Host.Users.Count; i++)
                Users.Add(new UserItem(Host, Host.Users[i], i));
            GroupNames.Clear();
            foreach (var g in Host.Groups) GroupNames.Add(g.Name);
            SelectedUser = Users.FirstOrDefault(u => u.Name == tested) ?? Users.FirstOrDefault();
            SelectedAccount = Users.FirstOrDefault(u => u.Name == edited) ?? Users.FirstOrDefault();
        }

        partial void OnSelectedNodeChanged(NodeItem? value)
        {
            var previous = SelectedOperation?.Operation;
            Operations.Clear();
            if (value != null)
                foreach (var op in Enum.GetValues<LinuxOperation>().Where(o => LinuxAccess.Applies(o, value.Node, value.Path)))
                    Operations.Add(new OperationItem(op, LinuxExplainer.Operation(op, Text.Culture)));
            SelectedOperation = Operations.FirstOrDefault(o => Equals(o.Operation, previous)) ?? Operations.FirstOrDefault();
            Editor.Load(Host, value);
            OnPropertyChanged(nameof(ShowCreation));
            Recompute();
        }

        partial void OnSelectedUserChanged(UserItem? value)
        {
            OnPropertyChanged(nameof(ShowCreation));
            Recompute();
        }

        partial void OnSelectedOperationChanged(OperationItem? value) => Recompute();

        private void SelectCell(MatrixCell? cell)
        {
            if (cell?.User is not UserItem user) return;
            SelectedUser = user;
            SelectedOperation = Operations.FirstOrDefault(o => Equals(o.Operation, cell.Operation)) ?? SelectedOperation;
        }

        /// <summary>Le testeur, le chemin parcouru, la matrice et ce que recevrait un nouvel élément.</summary>
        public void Recompute()
        {
            Steps.Clear();
            PathSegments.Clear();
            if (SelectedNode is not { } node || SelectedUser is not { } user || SelectedOperation is not { Operation: LinuxOperation operation })
            {
                HasResult = false;
                return;
            }

            var result = LinuxAccess.Check(Host, user.User, node.Path, operation);
            HasResult = true;
            Allowed = result.Allowed;
            Problem = LinuxExplainer.Problem(result, Text.Culture);
            Verdict = LinuxExplainer.Verdict(result, Text.Culture);
            var number = 1;
            foreach (var step in LinuxExplainer.Steps(result, Text.Culture))
                Steps.Add(new StepItem(number++, step.Step.Passed, step.Title, step.Detail));
            BuildPath(result, node.Path);
            OnPropertyChanged(nameof(VerdictTitle));
            OnPropertyChanged(nameof(VerdictSymbol));
            OnPropertyChanged(nameof(VerdictBrush));
            OnPropertyChanged(nameof(VerdictBackground));

            BuildMatrix(node);
            BuildCreation(node, user);
        }

        /// <summary>Chaque dossier du chemin : vert s'il a été traversé, rouge s'il a bloqué, gris s'il n'a pas été atteint.</summary>
        private void BuildPath(AccessResult result, string path)
        {
            var traversed = result.Steps.Where(s => s.Kind == AccessStepKind.Traverse).ToDictionary(s => s.Path, s => s.Passed);
            var blocked = traversed.ContainsValue(false);
            var paths = new List<string> { "/" };
            foreach (var name in LinuxHost.Split(path))
                paths.Add(LinuxHost.Combine(paths[^1], name));
            for (var i = 0; i < paths.Count; i++)
            {
                var p = paths[i];
                var name = p == "/" ? "/" : LinuxHost.Split(p)[^1];
                bool? state = p == path ? (blocked ? null : result.Allowed)
                    : traversed.TryGetValue(p, out var passed) ? passed : null;
                PathSegments.Add(state switch
                {
                    true => new PathSegment(name, Palette.Allowed, Palette.AllowedBack, i == paths.Count - 1),
                    false => new PathSegment(name, Palette.Denied, Palette.DeniedBack, i == paths.Count - 1),
                    null => new PathSegment(name, Palette.NotReached, Palette.NotReachedBack, i == paths.Count - 1)
                });
            }
        }

        private void BuildMatrix(NodeItem node)
        {
            var operations = (node.IsDirectory ? LinuxAccess.DirectoryOperations : LinuxAccess.FileOperations)
                .Where(o => LinuxAccess.Applies(o, node.Node, node.Path)).ToList();
            MatrixColumns.Clear();
            foreach (var op in operations)
                MatrixColumns.Add(ShortLabel(op));
            MatrixRows.Clear();
            foreach (var row in LinuxAccess.Matrix(Host, node.Path))
            {
                var user = Users.First(u => u.User == row.User);
                var cells = new ObservableCollection<MatrixCell>(operations.Select(op => new MatrixCell(op, row.Results[op].Allowed, SelectCellCommand)
                {
                    User = user,
                    IsSelected = user == SelectedUser && Equals(op, SelectedOperation?.Operation)
                }));
                MatrixRows.Add(new MatrixRow(user, cells));
            }
        }

        /// <summary>Libellé court d'une colonne de la matrice.</summary>
        private static string ShortLabel(LinuxOperation operation) => operation switch
        {
            LinuxOperation.Read => Text.T("Lire", "Read"),
            LinuxOperation.Write => Text.T("Écrire", "Write"),
            LinuxOperation.Execute => Text.T("Exécuter", "Execute"),
            LinuxOperation.List => Text.T("Lister", "List"),
            LinuxOperation.Enter => Text.T("Entrer", "Enter"),
            LinuxOperation.Create => Text.T("Créer dedans", "Create inside"),
            LinuxOperation.Delete => Text.T("Supprimer", "Delete"),
            LinuxOperation.ChangePermissions => Text.T("Changer les droits", "Change permissions"),
            _ => LinuxExplainer.Operation(operation, Text.Culture)
        };

        /// <summary>Ce que recevraient un fichier et un dossier créés par le compte choisi dans le dossier choisi, et pourquoi.</summary>
        private void BuildCreation(NodeItem node, UserItem user)
        {
            CreationReasons.Clear();
            NewFile = NewDirectory = null;
            if (!node.IsDirectory) return;

            var file = LinuxCreation.Create(Host, user.User, node.Path, "nouveau.txt", LinuxNodeKind.File);
            if (file.Node == null)
            {
                NewFile = Text.T($"{user.Name} ne peut pas créer dans {node.Path}.", $"{user.Name} cannot create in {node.Path}.");
                return;
            }
            var directory = LinuxCreation.Create(Host, user.User, node.Path, "nouveau", LinuxNodeKind.Directory).Node!;
            NewFile = $"{LinuxListing.Mode(file.Node)}  {file.Node.Owner}  {file.Node.Group}  {Text.T("fichier", "file")}";
            NewDirectory = $"{LinuxListing.Mode(directory)}  {directory.Owner}  {directory.Group}  {Text.T("dossier", "directory")}";
            CreationReasons.Add(Text.T($"Propriétaire : {user.Name}, le compte qui crée.", $"Owner: {user.Name}, the account that creates."));
            CreationReasons.Add(file.GroupFromSetGid
                ? Text.T($"Groupe : {node.Node.Group}, celui du dossier, qui a le bit setgid ; un sous-dossier reçoit aussi setgid.",
                         $"Group: {node.Node.Group}, the directory's, which has the setgid bit; a subdirectory gets setgid too.")
                : Text.T($"Groupe : {user.User.PrimaryGroup}, le groupe principal de {user.Name}.", $"Group: {user.User.PrimaryGroup}, {user.Name}'s primary group."));
            CreationReasons.Add(file.FromDefaultAcl
                ? Text.T("Droits : l'ACL par défaut du dossier, limitée par 666 (fichier) ou 777 (dossier) ; l'umask ne compte pas.",
                         "Permissions: the directory's default ACL, limited by 666 (file) or 777 (directory); the umask does not count.")
                : Text.T($"Droits : 666 (fichier) ou 777 (dossier), moins l'umask {user.User.Umask} de {user.Name}.",
                         $"Permissions: 666 (file) or 777 (directory), minus {user.Name}'s umask {user.User.Umask}."));
        }

        // ─── Comptes et groupes ─────────────────────────────────────────────────────

        partial void OnSelectedAccountChanged(UserItem? value)
        {
            _loadingAccount = true;
            Memberships.Clear();
            if (value != null)
            {
                foreach (var g in Host.Groups.Where(g => g.Name != value.User.PrimaryGroup))
                    Memberships.Add(new MembershipItem(g.Name, value.User.Groups.Contains(g.Name), OnMembershipChanged) { Display = g.Name });
                PrimaryGroup = value.User.PrimaryGroup;
                Umask = value.User.Umask;
            }
            _loadingAccount = false;
            DescribeAccount();
        }

        private void OnMembershipChanged(MembershipItem item)
        {
            if (_loadingAccount || SelectedAccount?.User is not { } user) return;
            user.Groups = Memberships.Where(m => m.IsMember).Select(m => m.Group).ToList();
            AccountChanged();
        }

        partial void OnPrimaryGroupChanged(string? value)
        {
            if (_loadingAccount || value == null || SelectedAccount?.User is not { } user || user.PrimaryGroup == value) return;
            user.PrimaryGroup = value;
            user.Groups.Remove(value);
            AccountChanged();
            OnSelectedAccountChanged(SelectedAccount);
        }

        partial void OnUmaskChanged(string? value)
        {
            if (_loadingAccount || value == null || SelectedAccount?.User is not { } user) return;
            user.Umask = value;
            AccountChanged();
        }

        /// <summary>useradd : un compte, avec son groupe personnel (comme Debian), et le premier UID libre à partir de 1001.</summary>
        [RelayCommand]
        private void AddUser()
        {
            var name = NewUserName.Trim();
            if (name.Length == 0 || Host.User(name) != null) return;
            if (Host.Group(name) == null)
                Host.Groups.Add(new LinuxGroup { Name = name, Gid = NextId(Host.Groups.Select(g => g.Gid), 1001) });
            Host.Users.Add(new LinuxUser { Name = name, Uid = NextId(Host.Users.Select(u => u.Uid), 1001), PrimaryGroup = name });
            NewUserName = "";
            RebuildUsers();
            SelectedAccount = Users.First(u => u.Name == name);
            AccountChanged();
        }

        /// <summary>groupadd : un groupe de service, avec le premier GID libre à partir de 2001.</summary>
        [RelayCommand]
        private void AddGroup()
        {
            var name = NewGroupName.Trim();
            if (name.Length == 0 || Host.Group(name) != null) return;
            Host.Groups.Add(new LinuxGroup { Name = name, Gid = NextId(Host.Groups.Select(g => g.Gid), 2001) });
            NewGroupName = "";
            RebuildUsers();
            AccountChanged();
        }

        private static int NextId(IEnumerable<int> used, int from)
        {
            var taken = used.ToHashSet();
            var id = from;
            while (taken.Contains(id)) id++;
            return id;
        }

        /// <summary>Un compte ou un groupe a changé : les lignes « id », le testeur, la matrice et la commande équivalente.</summary>
        private void AccountChanged()
        {
            var account = SelectedAccount?.Name;
            RebuildUsers();
            SelectedAccount = Users.FirstOrDefault(u => u.Name == account) ?? SelectedAccount;
            Editor.Load(Host, SelectedNode);
            Recompute();
            DescribeAccount();
        }

        private void DescribeAccount()
        {
            if (SelectedAccount?.User is not { } user)
            {
                AccountCommands = "";
                return;
            }
            var groups = user.Groups.Count > 0 ? string.Join(',', user.Groups) : "\"\"";
            AccountCommands = $"usermod -g {user.PrimaryGroup} -G {groups} {user.Name}\n"
                              + Text.T($"# umask {user.Umask} : dans ~/.profile de {user.Name}", $"# umask {user.Umask}: in {user.Name}'s ~/.profile")
                              + Text.T("\n# reconnexion nécessaire pour que les groupes comptent", "\n# log in again for the groups to apply");
        }

        /// <summary>La langue a changé : opérations, explications, éditeur.</summary>
        public void Relabel()
        {
            var selected = SelectedOperation?.Operation;
            var items = Operations.Select(o => new OperationItem(o.Operation, LinuxExplainer.Operation((LinuxOperation)o.Operation, Text.Culture))).ToList();
            Operations.Clear();
            foreach (var item in items) Operations.Add(item);
            SelectedOperation = Operations.FirstOrDefault(o => Equals(o.Operation, selected));
            Editor.Relabel();
            Recompute();
            DescribeAccount();
        }
    }
}
