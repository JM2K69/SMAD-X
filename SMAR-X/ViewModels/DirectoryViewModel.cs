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
    public enum DirectoryKind { Domain, Server, Container, User, Group }

    /// <summary>Un nœud de l'arbre « Comptes et groupes » : le domaine, le serveur, un dossier, un compte ou un groupe.</summary>
    public sealed partial class DirectoryNode : ObservableObject
    {
        public DirectoryNode(DirectoryKind kind, string display, string? name = null, string tag = "")
        {
            Kind = kind;
            Display = display;
            Name = name;
            Tag = tag;
        }

        public DirectoryKind Kind { get; }
        public string Display { get; }
        /// <summary>Le nom du compte ou du groupe ; null pour le domaine, le serveur ou un dossier.</summary>
        public string? Name { get; }
        /// <summary>« global », « domaine local »… : la portée d'un groupe, à côté de son nom.</summary>
        public string Tag { get; }
        public bool HasTag => Tag.Length > 0;
        public ObservableCollection<DirectoryNode> Children { get; } = new();
        [ObservableProperty] private bool _isExpanded = true;

        public bool IsDomain => Kind == DirectoryKind.Domain;
        public bool IsServer => Kind == DirectoryKind.Server;
        public bool IsContainer => Kind == DirectoryKind.Container;
        public bool IsUser => Kind == DirectoryKind.User;
        public bool IsGroup => Kind == DirectoryKind.Group;
        public override string ToString() => Display;
    }

    /// <summary>
    /// Un nœud de l'onglet « Imbrication » : le compte (ou le groupe) choisi, puis ses groupes, puis les groupes de ces
    /// groupes, de proche en proche : ce que whoami aplatit en une liste.
    /// </summary>
    public sealed class NestingNode(string display, string detail, bool isUser)
    {
        public string Display { get; } = display;
        public string Detail { get; } = detail;
        public bool IsUser { get; } = isUser;
        public bool IsGroup => !IsUser;
        public bool IsExpanded { get; set; } = true;
        public ObservableCollection<NestingNode> Children { get; } = new();
    }

    /// <summary>Une ligne des onglets « Membre de » et « Membres » : un compte ou un groupe, et d'où il vient.</summary>
    public sealed record DirectoryEntry(string Name, string Display, string Detail, bool IsUser)
    {
        public bool IsGroup => !IsUser;
    }

    /// <summary>Un compte ou un groupe proposé par « Ajouter… » : grisé, avec sa raison, si Active Directory le refuserait.</summary>
    public sealed partial class PickerCandidate : ObservableObject
    {
        public PickerCandidate(string name, string display, string detail, bool isUser, string? reason)
        {
            Name = name;
            Display = display;
            Detail = detail;
            IsUser = isUser;
            Reason = reason;
        }

        public string Name { get; }
        public string Display { get; }
        public string Detail { get; }
        public bool IsUser { get; }
        public bool IsGroup => !IsUser;
        public string? Reason { get; }
        public bool IsEnabled => Reason == null;
        [ObservableProperty] private bool _isChecked;
    }

    /// <summary>
    /// « Sélectionner des utilisateurs ou des groupes » : les comptes et les groupes qui peuvent être ajoutés, un filtre,
    /// et ceux que l'on coche.
    /// </summary>
    public sealed partial class DirectoryPickerViewModel : ObservableObject
    {
        private readonly IReadOnlyList<PickerCandidate> _all;

        public DirectoryPickerViewModel(string title, IReadOnlyList<PickerCandidate> candidates)
        {
            Title = title;
            _all = candidates;
            foreach (var c in candidates) Items.Add(c);
        }

        public string Title { get; }
        public ObservableCollection<PickerCandidate> Items { get; } = new();
        [ObservableProperty] private string _filter = "";

        partial void OnFilterChanged(string value)
        {
            Items.Clear();
            foreach (var c in _all.Where(c => value.Length == 0 || c.Display.Contains(value, StringComparison.OrdinalIgnoreCase)
                                              || c.Detail.Contains(value, StringComparison.OrdinalIgnoreCase)))
                Items.Add(c);
        }

        public IReadOnlyList<string> Chosen => _all.Where(c => c.IsChecked && c.IsEnabled).Select(c => c.Name).ToList();
    }

    /// <summary>
    /// « Comptes et groupes », comme la console Utilisateurs et ordinateurs Active Directory : à gauche le domaine (ses
    /// utilisateurs, ses groupes globaux et de domaine local) et le serveur (ses comptes et ses groupes locaux) ; à droite
    /// l'objet choisi, ses groupes (Membre de) et, pour un groupe, ses membres, avec Ajouter… et Supprimer. Chaque
    /// changement passe aussitôt dans le testeur, la matrice et whoami.
    /// </summary>
    public sealed partial class DirectoryViewModel : ObservableObject, IDisposable
    {
        private static SmarxText Text => SmarxText.Instance;
        private readonly WindowsWorkspaceViewModel _workspace;
        private bool _rebuilding;

        public DirectoryViewModel(WindowsWorkspaceViewModel workspace, string? select = null)
        {
            _workspace = workspace;
            _workspace.AccountsChanged += Rebuild;
            Kinds =
            [
                new Choice(CreatableKind.DomainUser, Text.T("utilisateur du domaine", "domain user")),
                new Choice(CreatableKind.GlobalGroup, Text.T("groupe global (G_)", "global group (G_)")),
                new Choice(CreatableKind.DomainLocalGroup, Text.T("groupe de domaine local (DL_)", "domain local group (DL_)")),
                new Choice(CreatableKind.LocalUser, Text.T("utilisateur local du serveur", "local user of the server")),
                new Choice(CreatableKind.LocalGroup, Text.T("groupe local du serveur", "local group of the server"))
            ];
            if (Host.Domain == null) Kinds = Kinds.Where(k => k.Value is CreatableKind.LocalUser or CreatableKind.LocalGroup).ToList();
            _newKind = Kinds[0];
            Rebuild();
            SelectedNode = Find(select) ?? Tree.SelectMany(All).FirstOrDefault(n => n.IsUser || n.IsGroup);
        }

        public enum CreatableKind { DomainUser, GlobalGroup, DomainLocalGroup, LocalUser, LocalGroup }

        private WindowsHost Host => _workspace.Host;

        public string WindowTitle => Text.T($"Comptes et groupes \u2014 {Host.Name}", $"Accounts and groups \u2014 {Host.Name}");

        public ObservableCollection<DirectoryNode> Tree { get; } = new();
        public ObservableCollection<DirectoryEntry> MemberOf { get; } = new();
        public ObservableCollection<DirectoryEntry> Members { get; } = new();
        /// <summary>Les groupes reçus par imbrication : ceux des groupes de l'objet, de proche en proche.</summary>
        public ObservableCollection<DirectoryEntry> Nested { get; } = new();
        /// <summary>L'arbre de l'imbrication de l'objet choisi.</summary>
        public ObservableCollection<NestingNode> Nesting { get; } = new();
        public IReadOnlyList<Choice> Kinds { get; }

        [ObservableProperty] private DirectoryNode? _selectedNode;
        [ObservableProperty] private string _title = "";
        [ObservableProperty] private string _qualified = "";
        [ObservableProperty] private string _kindLabel = "";
        [ObservableProperty] private string _description = "";
        [ObservableProperty] private string _commands = "";
        [ObservableProperty] private string _membersNote = "";
        [ObservableProperty] private string _nestingPaths = "";
        [ObservableProperty] private bool _hasObject;
        [ObservableProperty] private bool _isUser;
        [ObservableProperty] private bool _isGroup;
        [ObservableProperty] private bool _canEditMembers;
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RemoveMemberOfCommand))]
        private DirectoryEntry? _selectedMemberOf;
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RemoveMemberCommand))]
        private DirectoryEntry? _selectedMember;
        [ObservableProperty] private string _newName = "";
        [ObservableProperty] private Choice _newKind;
        [ObservableProperty] private string _message = "";

        public bool HasNested => Nested.Count > 0;
        public bool HasMembersNote => MembersNote.Length > 0;
        public bool HasMessage => Message.Length > 0;
        partial void OnMembersNoteChanged(string value) => OnPropertyChanged(nameof(HasMembersNote));
        partial void OnMessageChanged(string value) => OnPropertyChanged(nameof(HasMessage));

        partial void OnSelectedNodeChanged(DirectoryNode? value)
        {
            if (!_rebuilding) Describe();
        }

        // ─── L'arbre ─────────────────────────────────────────────────────────────

        private void Rebuild()
        {
            var selected = SelectedNode?.Name;
            _rebuilding = true;
            Tree.Clear();
            var culture = Text.Culture;
            var french = culture == "fr-FR";
            if (Host.Domain is { } domain)
            {
                var root = new DirectoryNode(DirectoryKind.Domain, Text.T($"{domain} (domaine)", $"{domain} (domain)"));
                root.Children.Add(Container(Text.T("Utilisateurs", "Users"),
                    Host.Users.Where(u => u.Domain).Select(u => new DirectoryNode(DirectoryKind.User, u.Name, u.Name))));
                root.Children.Add(Container(Text.T("Groupes globaux (G_)", "Global groups (G_)"),
                    Groups(WindowsGroupScope.Global)));
                root.Children.Add(Container(Text.T("Groupes de domaine local (DL_)", "Domain local groups (DL_)"),
                    Groups(WindowsGroupScope.DomainLocal)));
                Tree.Add(root);
            }
            var server = new DirectoryNode(DirectoryKind.Server, Text.T($"{Host.Name} (serveur)", $"{Host.Name} (server)"));
            server.Children.Add(Container(Text.T("Utilisateurs locaux", "Local users"),
                Host.Users.Where(u => !u.Domain).Select(u => new DirectoryNode(DirectoryKind.User, u.Name, u.Name))));
            server.Children.Add(Container(Text.T("Groupes locaux", "Local groups"), Groups(WindowsGroupScope.Local)));
            Tree.Add(server);
            _rebuilding = false;
            SelectedNode = Find(selected) ?? SelectedNode;
            Describe();

            IEnumerable<DirectoryNode> Groups(WindowsGroupScope scope) =>
                Host.Groups.Where(g => g.Scope == scope)
                    .Select(g => new DirectoryNode(DirectoryKind.Group, WellKnown.Display(g.Name, french), g.Name, WindowsGroups.ScopeLabel(scope, culture)));
        }

        private static DirectoryNode Container(string display, IEnumerable<DirectoryNode> children)
        {
            var node = new DirectoryNode(DirectoryKind.Container, display);
            foreach (var child in children) node.Children.Add(child);
            return node;
        }

        /// <summary>Choisit le compte ou le groupe de ce nom dans l'arbre ; faux s'il n'existe pas.</summary>
        public bool Select(string name)
        {
            if (Find(name) is not { } node) return false;
            SelectedNode = node;
            return true;
        }

        private static IEnumerable<DirectoryNode> All(DirectoryNode node) => new[] { node }.Concat(node.Children.SelectMany(All));

        private DirectoryNode? Find(string? name) =>
            name == null ? null : Tree.SelectMany(All).FirstOrDefault(n => string.Equals(n.Name, name, StringComparison.OrdinalIgnoreCase));

        // ─── L'objet choisi ──────────────────────────────────────────────────────

        private void Describe()
        {
            MemberOf.Clear();
            Members.Clear();
            Nested.Clear();
            Nesting.Clear();
            NestingPaths = "";
            MembersNote = "";
            var name = SelectedNode?.Name;
            var user = name == null ? null : Host.User(name);
            var group = name == null ? null : Host.Group(name);
            HasObject = user != null || group != null;
            IsUser = user != null;
            IsGroup = group != null;
            if (!HasObject)
            {
                Title = SelectedNode?.Display ?? "";
                Qualified = KindLabel = Description = Commands = "";
                CanEditMembers = false;
                OnPropertyChanged(nameof(HasNested));
                return;
            }
            var french = Text.Culture == "fr-FR";
            Title = WellKnown.Display(name!, french);
            Qualified = name is WellKnown.Administrators or WellKnown.Users ? @"BUILTIN\" + name : Host.Qualified(name!);
            KindLabel = Kind(user, group);
            Description = group?.Description ?? "";

            var memberOf = user?.Groups ?? group!.Groups;
            foreach (var g in memberOf) MemberOf.Add(Entry(g));
            // Utilisateurs : tous les comptes en sont membres, sans qu'on l'ajoute
            if (user != null) MemberOf.Add(new DirectoryEntry(WellKnown.Users, WellKnown.Display(WellKnown.Users, french),
                Text.T("tous les comptes, sans qu'on les ajoute", "every account, without being added"), false));

            var holder = user ?? new WindowsUser { Name = group!.Name, Groups = group.Groups };
            foreach (var (g, via) in WindowsGroups.EffectiveWithOrigin(Host, holder).Where(o => o.Via != null))
                Nested.Add(new DirectoryEntry(g, WellKnown.Display(g, french), Text.T($"par {WellKnown.Display(via!, french)}", $"through {via}"), false));
            OnPropertyChanged(nameof(HasNested));

            // L'imbrication en arbre : l'objet, ses groupes, les groupes de ses groupes
            var root = new NestingNode(Title, KindLabel, user != null);
            foreach (var g in memberOf) root.Children.Add(NestingOf(g, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { name! }));
            if (user != null) root.Children.Add(new NestingNode(WellKnown.Display(WellKnown.Users, french), Text.T("tous les comptes", "every account"), false));
            Nesting.Add(root);
            NestingPaths = string.Join("\n", Paths(root, []));

            CanEditMembers = group != null && group.Name != WellKnown.Users;
            if (group != null)
            {
                if (group.Name == WellKnown.Users)
                    MembersNote = Text.T("Tous les comptes du serveur et du domaine en sont membres : on n'ajoute personne à Utilisateurs.",
                                         "Every account of the server and the domain is a member: nobody is added to Users.");
                foreach (var m in WindowsGroups.Members(Host, group.Name)) Members.Add(Entry(m));
            }
            Commands = string.Join("\n", (group != null ? [WindowsGroups.CreateCommand(Host, group)] : Array.Empty<string>())
                .Concat(group != null ? Members.Select(m => WindowsGroups.AddMemberCommand(Host, m.Name, group.Name)) : [])
                .Concat(memberOf.Select(g => WindowsGroups.AddMemberCommand(Host, name!, g))));
        }

        /// <summary>Chaque chemin de l'objet à un groupe qui ne mène plus à aucun autre : ce que l'arbre montre, en une ligne.</summary>
        private static IEnumerable<string> Paths(NestingNode node, List<string> before)
        {
            var path = new List<string>(before) { node.Display };
            if (node.Children.Count == 0)
            {
                if (path.Count > 1) yield return string.Join(" \u2192 ", path);
                yield break;
            }
            foreach (var child in node.Children)
            foreach (var line in Paths(child, path))
                yield return line;
        }

        /// <summary>Un groupe et, sous lui, les groupes dont il est membre ; une boucle s'arrête au groupe déjà vu.</summary>
        private NestingNode NestingOf(string group, HashSet<string> seen)
        {
            var entry = Entry(group);
            var node = new NestingNode(entry.Display, entry.Detail, false);
            if (!seen.Add(group)) return node;
            foreach (var parent in Host.Group(group)?.Groups ?? [])
                node.Children.Add(NestingOf(parent, new HashSet<string>(seen, StringComparer.OrdinalIgnoreCase)));
            return node;
        }

        private string Kind(WindowsUser? user, WindowsGroup? group)
        {
            if (user != null)
                return user.Domain ? Text.T("Utilisateur du domaine", "Domain user") : Text.T("Utilisateur local du serveur", "Local user of the server");
            return group!.Scope switch
            {
                WindowsGroupScope.Global => Text.T("Groupe de sécurité global", "Global security group"),
                WindowsGroupScope.DomainLocal => Text.T("Groupe de sécurité de domaine local", "Domain local security group"),
                _ when group.Name is WellKnown.Administrators or WellKnown.Users => Text.T("Groupe intégré du serveur", "Built-in group of the server"),
                _ => Text.T("Groupe local du serveur", "Local group of the server")
            };
        }

        /// <summary>Un compte ou un groupe dans une liste : son nom, et ce qu'il est (« groupe global », « compte local »).</summary>
        private DirectoryEntry Entry(string name)
        {
            var french = Text.Culture == "fr-FR";
            if (Host.User(name) is { } u)
                return new DirectoryEntry(name, Host.Qualified(name),
                    u.Domain ? Text.T("utilisateur du domaine", "domain user") : Text.T("utilisateur local", "local user"), true);
            var detail = WindowsGroups.ScopeOf(Host, name) switch
            {
                WindowsGroupScope.Global => Text.T("groupe global", "global group"),
                WindowsGroupScope.DomainLocal => Text.T("groupe de domaine local", "domain local group"),
                _ => Text.T("groupe local", "local group")
            };
            return new DirectoryEntry(name, WellKnown.Display(name, french), detail, false);
        }

        // ─── Ajouter, supprimer ──────────────────────────────────────────────────

        /// <summary>Les groupes que l'objet choisi peut rejoindre (« Membre de » → Ajouter…).</summary>
        public IReadOnlyList<PickerCandidate> MemberOfCandidates()
        {
            if (SelectedNode?.Name is not { } name) return [];
            var current = Host.User(name)?.Groups ?? Host.Group(name)?.Groups ?? [];
            return Host.Groups.Where(g => g.Name != WellKnown.Users && !Same(g.Name, name))
                .Select(g => Candidate(g.Name, current.Contains(g.Name, StringComparer.OrdinalIgnoreCase)
                    ? Text.T("Déjà membre.", "Already a member.") : WindowsGroups.CannotJoin(Host, name, g.Name, Text.Culture)))
                .ToList();
        }

        /// <summary>Les comptes et les groupes qui peuvent entrer dans le groupe choisi (« Membres » → Ajouter…).</summary>
        public IReadOnlyList<PickerCandidate> MemberCandidates()
        {
            if (SelectedNode?.Name is not { } group || Host.Group(group) == null) return [];
            var members = WindowsGroups.Members(Host, group);
            return Host.Users.Select(u => u.Name)
                .Concat(Host.Groups.Where(g => g.Name is not (WellKnown.Users or WellKnown.Administrators) && !Same(g.Name, group)).Select(g => g.Name))
                .Select(m => Candidate(m, members.Contains(m, StringComparer.OrdinalIgnoreCase)
                    ? Text.T("Déjà membre.", "Already a member.") : WindowsGroups.CannotJoin(Host, m, group, Text.Culture)))
                .ToList();
        }

        /// <summary>« Membre de » → Ajouter… : les groupes que l'objet choisi peut rejoindre.</summary>
        public DirectoryPickerViewModel MemberOfPicker() =>
            new(Text.T($"Ajouter {Title} à des groupes", $"Add {Title} to groups"), MemberOfCandidates());

        /// <summary>« Membres » → Ajouter… : les comptes et les groupes qui peuvent entrer dans le groupe choisi.</summary>
        public DirectoryPickerViewModel MembersPicker() =>
            new(Text.T($"Ajouter des membres à {Title}", $"Add members to {Title}"), MemberCandidates());

        private PickerCandidate Candidate(string name, string? reason)
        {
            var entry = Entry(name);
            return new PickerCandidate(name, entry.Display, entry.Detail, entry.IsUser, reason);
        }

        /// <summary>L'objet choisi rejoint ces groupes.</summary>
        public void AddMemberOf(IEnumerable<string> groups)
        {
            if (SelectedNode?.Name is not { } name || (Host.User(name)?.Groups ?? Host.Group(name)?.Groups) is not { } list) return;
            foreach (var g in groups.Where(g => WindowsGroups.CannotJoin(Host, name, g, Text.Culture) == null && !list.Contains(g, StringComparer.OrdinalIgnoreCase)))
                list.Add(g);
            Changed();
        }

        /// <summary>Ces comptes et ces groupes entrent dans le groupe choisi.</summary>
        public void AddMembers(IEnumerable<string> members)
        {
            if (SelectedNode?.Name is not { } group) return;
            foreach (var m in members.Where(m => WindowsGroups.CannotJoin(Host, m, group, Text.Culture) == null))
                if ((Host.User(m)?.Groups ?? Host.Group(m)?.Groups) is { } list && !list.Contains(group, StringComparer.OrdinalIgnoreCase))
                    list.Add(group);
            Changed();
        }

        [RelayCommand(CanExecute = nameof(CanRemoveMemberOf))]
        private void RemoveMemberOf()
        {
            if (SelectedNode?.Name is not { } name || SelectedMemberOf is not { } entry) return;
            (Host.User(name)?.Groups ?? Host.Group(name)?.Groups)?.RemoveAll(g => Same(g, entry.Name));
            Changed();
        }

        private bool CanRemoveMemberOf => SelectedMemberOf is { } e && e.Name != WellKnown.Users;

        [RelayCommand(CanExecute = nameof(CanRemoveMember))]
        private void RemoveMember()
        {
            if (SelectedNode?.Name is not { } group || SelectedMember is not { } entry) return;
            (Host.User(entry.Name)?.Groups ?? Host.Group(entry.Name)?.Groups)?.RemoveAll(g => Same(g, group));
            Changed();
        }

        private bool CanRemoveMember => SelectedMember != null && CanEditMembers;

        /// <summary>Nouveau compte ou nouveau groupe, de la sorte choisie ; il est aussitôt sélectionné.</summary>
        [RelayCommand]
        private void Create()
        {
            var name = NewName.Trim();
            if (name.Length == 0 || name.IndexOfAny(['\\', '/', ':', '*', '?', '"', '<', '>', '|', ' ']) >= 0)
            {
                Message = Text.T("Nom invalide : lettres, chiffres, tiret ou tiret bas, sans espace.", "Invalid name: letters, digits, hyphen or underscore, no space.");
                return;
            }
            if (Host.User(name) != null || Host.Group(name) != null)
            {
                Message = Text.T($"{name} existe déjà.", $"{name} already exists.");
                return;
            }
            Message = "";
            switch (NewKind.Value)
            {
                case CreatableKind.DomainUser: Host.Users.Add(new WindowsUser { Name = name, Domain = true }); break;
                case CreatableKind.LocalUser: Host.Users.Add(new WindowsUser { Name = name }); break;
                case CreatableKind.GlobalGroup: Host.Groups.Add(new WindowsGroup { Name = name, Scope = WindowsGroupScope.Global }); break;
                case CreatableKind.DomainLocalGroup: Host.Groups.Add(new WindowsGroup { Name = name, Scope = WindowsGroupScope.DomainLocal }); break;
                default: Host.Groups.Add(new WindowsGroup { Name = name }); break;
            }
            NewName = "";
            Changed();
            SelectedNode = Find(name) ?? SelectedNode;
        }

        private void Changed() => _workspace.NotifyAccountsChanged();

        private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        public void Dispose() => _workspace.AccountsChanged -= Rebuild;
    }
}
