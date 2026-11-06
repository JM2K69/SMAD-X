using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SMARX.Models;
using SMARX.Services.Linux;

namespace SMARX.ViewModels
{
    /// <summary>Couleurs de l'interface : accordé, refusé, non atteint ou neutre, et une couleur par compte.</summary>
    public static class Palette
    {
        public static readonly IBrush Allowed = new SolidColorBrush(Color.Parse("#2EA043"));
        public static readonly IBrush Denied = new SolidColorBrush(Color.Parse("#E5534B"));
        public static readonly IBrush NotReached = new SolidColorBrush(Color.Parse("#768390"));
        public static readonly IBrush AllowedBack = new SolidColorBrush(Color.Parse("#2EA043"), 0.16);
        public static readonly IBrush DeniedBack = new SolidColorBrush(Color.Parse("#E5534B"), 0.16);
        public static readonly IBrush NotReachedBack = new SolidColorBrush(Color.Parse("#768390"), 0.14);

        private static readonly IBrush[] Accounts =
            new[] { "#D29922", "#539BF5", "#B083F0", "#57AB5A", "#E0823D", "#DB61A2", "#39C5CF" }.Select(c => (IBrush)new SolidColorBrush(Color.Parse(c))).ToArray();

        public static IBrush Account(int index) => Accounts[index % Accounts.Length];
    }

    /// <summary>Un compte tel que l'affichent le testeur, la matrice et la carte des comptes.</summary>
    public interface IAccountItem
    {
        string Name { get; }
        string Initial { get; }
        IBrush Brush { get; }
    }

    /// <summary>Lecture, écriture, exécution sous forme de trois cases ; prévient quand l'utilisateur en coche une.</summary>
    public sealed partial class PermBits : ObservableObject
    {
        private bool _loading;

        public event Action? Changed;

        [ObservableProperty] private bool _r;
        [ObservableProperty] private bool _w;
        [ObservableProperty] private bool _x;

        public Perm Value
        {
            get => (R ? Perm.Read : 0) | (W ? Perm.Write : 0) | (X ? Perm.Execute : 0);
            set
            {
                _loading = true;
                R = value.HasFlag(Perm.Read);
                W = value.HasFlag(Perm.Write);
                X = value.HasFlag(Perm.Execute);
                _loading = false;
                OnPropertyChanged(nameof(Text));
            }
        }

        public string Text => Perms.Format(Value);

        partial void OnRChanged(bool value) => Notify();
        partial void OnWChanged(bool value) => Notify();
        partial void OnXChanged(bool value) => Notify();

        private void Notify()
        {
            OnPropertyChanged(nameof(Text));
            if (!_loading) Changed?.Invoke();
        }
    }

    /// <summary>Un fichier ou un dossier Linux de l'arborescence, avec ses droits tels que les montre ls -l.</summary>
    public sealed partial class NodeItem : ObservableObject
    {
        public NodeItem(LinuxNode node, string path)
        {
            Node = node;
            Path = path;
            foreach (var child in node.Children.OrderBy(c => !c.IsDirectory).ThenBy(c => c.Name, StringComparer.Ordinal))
                Children.Add(new NodeItem(child, LinuxHost.Combine(path, child.Name)));
        }

        public LinuxNode Node { get; }
        public string Path { get; }
        public ObservableCollection<NodeItem> Children { get; } = new();
        public string Name => Path == "/" ? "/" : Node.Name;
        public bool IsDirectory => Node.IsDirectory;
        public string Mode => LinuxListing.Mode(Node);
        public string Ownership => $"{Node.Owner}:{Node.Group}";

        [ObservableProperty] private bool _isExpanded = true;

        public void Refresh()
        {
            OnPropertyChanged(nameof(Mode));
            OnPropertyChanged(nameof(Ownership));
        }
    }

    /// <summary>Un compte Linux, avec sa pastille et sa ligne « id ».</summary>
    public sealed class UserItem : IAccountItem
    {
        public UserItem(LinuxHost host, LinuxUser user, int index)
        {
            User = user;
            Brush = user.IsRoot ? Palette.Denied : Palette.Account(index);
            string Group(string name) => $"{host.Group(name)?.Gid}({name})";
            IdLine = $"uid={user.Uid}({user.Name}) gid={Group(user.PrimaryGroup)} groups={string.Join(',', new[] { user.PrimaryGroup }.Concat(user.Groups).Distinct().Select(Group))}";
            Groups = string.Join(", ", user.Groups);
        }

        public LinuxUser User { get; }
        public string Name => User.Name;
        public string Initial => User.Name.Length > 0 ? User.Name[..1].ToUpperInvariant() : "?";
        public IBrush Brush { get; }
        public string IdLine { get; }
        public string Groups { get; }
        public string Umask => "umask " + User.Umask;
        public override string ToString() => Name;
    }

    /// <summary>Une opération proposée au testeur (Linux ou Windows), avec son libellé dans la langue de l'interface.</summary>
    public sealed class OperationItem(object operation, string label)
    {
        public object Operation { get; } = operation;
        public string Label { get; } = label;
        public override string ToString() => Label;
    }

    /// <summary>Une étape vérifiée, numérotée : accordée, refusée, ou neutre (information, ACE sans effet).</summary>
    /// <summary>Une route vers l'objet testé sous Linux : en local (pas de partage), ou par un partage Samba.</summary>
    public sealed class SambaRouteItem(SambaShare? share, string label, string title, string detail)
    {
        public SambaShare? Share { get; } = share;
        public string Label { get; } = label;
        /// <summary>« Sur le serveur » ou « Depuis le réseau » ; <see cref="Detail"/> : la session, ou le partage.</summary>
        public string Title { get; } = title;
        public string Detail { get; } = detail;
        public bool IsLocal => Share == null;
        public bool IsNetwork => Share != null;
        public override string ToString() => Label;
    }

    public sealed class StepItem(int number, bool? passed, string title, string detail)
    {
        public int Number { get; } = number;
        public bool? Passed { get; } = passed;
        public string Title { get; } = title;
        public string Detail { get; } = detail;
        public IBrush Brush => Passed switch { true => Palette.Allowed, false => Palette.Denied, null => Palette.NotReached };
        public string Symbol => Passed switch { true => "✓", false => "✕", null => "" };
    }

    /// <summary>Un dossier du chemin parcouru, coloré selon qu'il a été traversé, qu'il a bloqué ou qu'il n'a pas été atteint.</summary>
    public sealed class PathSegment(string name, IBrush brush, IBrush background, bool last)
    {
        public string Name { get; } = name;
        public IBrush Brush { get; } = brush;
        public IBrush Background { get; } = background;
        public bool ShowSeparator { get; } = !last;
    }

    public sealed partial class MatrixCell(object operation, bool allowed, IRelayCommand<MatrixCell> select) : ObservableObject
    {
        public object Operation { get; } = operation;
        public bool Allowed { get; } = allowed;
        public string Symbol => Allowed ? "✓" : "✕";
        public IBrush Brush => Allowed ? Palette.Allowed : Palette.Denied;
        public IBrush Background => Allowed ? Palette.AllowedBack : Palette.DeniedBack;
        public IRelayCommand<MatrixCell> Select { get; } = select;
        public IAccountItem? User { get; set; }

        [ObservableProperty] private bool _isSelected;
    }

    public sealed class MatrixRow(IAccountItem user, ObservableCollection<MatrixCell> cells)
    {
        public IAccountItem User { get; } = user;
        public ObservableCollection<MatrixCell> Cells { get; } = cells;
    }

    /// <summary>Une entrée nommée de l'ACL POSIX : utilisateur ou groupe, ses droits et ce qu'il en reste après le masque.</summary>
    public sealed partial class AclEntryItem : ObservableObject
    {
        public AclEntryItem(bool isUser, string name, Perm perm, Action<AclEntryItem> remove)
        {
            IsUser = isUser;
            Name = name;
            Bits.Value = perm;
            Remove = new RelayCommand(() => remove(this));
        }

        public bool IsUser { get; }
        public string Name { get; }
        public string Tag => IsUser ? $"user:{Name}" : $"group:{Name}";
        public PermBits Bits { get; } = new();
        public IRelayCommand Remove { get; }

        /// <summary>« effectif : r-x » quand le masque retire des droits.</summary>
        [ObservableProperty] private string? _effective;
    }

    /// <summary>Un groupe proposé à un compte, coché s'il en est membre.</summary>
    public sealed partial class MembershipItem : ObservableObject
    {
        private readonly Action<MembershipItem> _changed;

        public MembershipItem(string group, bool member, Action<MembershipItem> changed)
        {
            Group = group;
            _isMember = member;
            _changed = changed;
        }

        public string Group { get; }
        public string Display { get; init; } = "";

        /// <summary>Portée du groupe, sous Windows : « global », « domaine local », « local ».</summary>
        public string ScopeTag { get; init; } = "";
        public bool HasScopeTag => ScopeTag.Length > 0;

        /// <summary>Une adhésion impossible (un groupe de domaine local dans un groupe global…) se lit, grisée, avec sa raison.</summary>
        public bool IsEnabled { get; init; } = true;
        public string? Reason { get; init; }

        [ObservableProperty] private bool _isMember;

        partial void OnIsMemberChanged(bool value) => _changed(this);
    }
}
