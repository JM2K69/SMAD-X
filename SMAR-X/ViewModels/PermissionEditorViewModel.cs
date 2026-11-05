using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SMARX.Models;
using SMARX.Services.Linux;

namespace SMARX.ViewModels
{
    /// <summary>
    /// Droits du fichier ou du dossier choisi, modifiables comme avec chown, chmod et setfacl. Chaque modification
    /// s'applique tout de suite au serveur simulé (<see cref="Changed"/>) : le testeur et la matrice se recalculent.
    /// </summary>
    public sealed partial class PermissionEditorViewModel : ObservableObject
    {
        private LinuxHost? _host;
        private NodeItem? _item;
        private bool _loading;

        public PermissionEditorViewModel()
        {
            foreach (var bits in new[] { OwnerBits, GroupBits, OtherBits })
                bits.Changed += ApplyMode;
            GroupObjBits.Changed += RecalculateMask;
        }

        /// <summary>Le serveur simulé a été modifié.</summary>
        public event Action? Changed;

        public PermBits OwnerBits { get; } = new();
        /// <summary>Droits du groupe du mode : le masque quand il y a une ACL.</summary>
        public PermBits GroupBits { get; } = new();
        public PermBits OtherBits { get; } = new();
        public PermBits GroupObjBits { get; } = new();
        public ObservableCollection<AclEntryItem> AclEntries { get; } = new();
        public ObservableCollection<string> Users { get; } = new();
        public ObservableCollection<string> Groups { get; } = new();

        [ObservableProperty] private string? _owner;
        [ObservableProperty] private string? _group;
        [ObservableProperty] private bool _setUid;
        [ObservableProperty] private bool _setGid;
        [ObservableProperty] private bool _sticky;
        [ObservableProperty] private bool _hasAcl;
        [ObservableProperty] private bool _isDirectory;
        [ObservableProperty] private string _octal = "";
        [ObservableProperty] private string _lsLine = "";
        [ObservableProperty] private string _commands = "";
        [ObservableProperty] private string _getfacl = "";
        [ObservableProperty] private string? _defaultAcl;
        [ObservableProperty] private string? _userToAdd;
        [ObservableProperty] private string? _groupToAdd;

        public string GroupRowLabel => HasAcl ? SmarxText.Instance["Rights.Mask"] : SmarxText.Instance["Rights.Group"];

        public void Load(LinuxHost host, NodeItem? item)
        {
            _host = host;
            _item = item;
            _loading = true;
            Users.Clear();
            foreach (var u in host.Users) Users.Add(u.Name);
            Groups.Clear();
            foreach (var g in host.Groups) Groups.Add(g.Name);
            UserToAdd = host.Users.FirstOrDefault(u => !u.IsRoot)?.Name;
            GroupToAdd = host.Groups.FirstOrDefault(g => g.Name != "root")?.Name;
            if (item != null)
            {
                var node = item.Node;
                Owner = node.Owner;
                Group = node.Group;
                SetUid = node.SetUid;
                SetGid = node.SetGid;
                Sticky = node.Sticky;
                IsDirectory = node.IsDirectory;
                OwnerBits.Value = node.OwnerPerm;
                GroupBits.Value = node.GroupPerm;
                OtherBits.Value = node.OtherPerm;
                LoadAcl(node);
            }
            _loading = false;
            Describe();
        }

        private void LoadAcl(LinuxNode node)
        {
            AclEntries.Clear();
            HasAcl = node.Acl != null;
            if (node.Acl is { } acl)
            {
                GroupObjBits.Value = Perms.Parse(acl.GroupObj);
                foreach (var e in acl.Users) AclEntries.Add(Entry(true, e));
                foreach (var e in acl.Groups) AclEntries.Add(Entry(false, e));
            }
            OnPropertyChanged(nameof(GroupRowLabel));
        }

        private AclEntryItem Entry(bool isUser, AclEntry entry)
        {
            var item = new AclEntryItem(isUser, entry.Name, Perms.Parse(entry.Perm), RemoveEntry);
            item.Bits.Changed += RecalculateMask;
            return item;
        }

        partial void OnOwnerChanged(string? value) { if (!_loading && _item != null && value != null) { _item.Node.Owner = value; Apply(); } }
        partial void OnGroupChanged(string? value) { if (!_loading && _item != null && value != null) { _item.Node.Group = value; Apply(); } }
        partial void OnSetUidChanged(bool value) => ApplyMode();
        partial void OnSetGidChanged(bool value) => ApplyMode();
        partial void OnStickyChanged(bool value) => ApplyMode();

        /// <summary>chmod : le mode complet, droits spéciaux compris ; avec une ACL, la ligne du groupe est le masque.</summary>
        private void ApplyMode()
        {
            if (_loading || _item == null) return;
            _item.Node.ModeBits = (SetUid ? 0x800 : 0) | (SetGid ? 0x400 : 0) | (Sticky ? 0x200 : 0)
                                  | ((int)OwnerBits.Value << 6) | ((int)GroupBits.Value << 3) | (int)OtherBits.Value;
            Apply();
        }

        /// <summary>setfacl -m : écrit l'ACL et recalcule le masque (l'union du groupe propriétaire et des entrées nommées).</summary>
        private void RecalculateMask()
        {
            if (_loading || _item == null) return;
            var node = _item.Node;
            node.Acl = new PosixAcl
            {
                Users = AclEntries.Where(e => e.IsUser).Select(e => AclEntry.Of(e.Name, e.Bits.Text)).ToList(),
                GroupObj = GroupObjBits.Text,
                Groups = AclEntries.Where(e => !e.IsUser).Select(e => AclEntry.Of(e.Name, e.Bits.Text)).ToList()
            };
            var mask = AclEntries.Aggregate(GroupObjBits.Value, (m, e) => m | e.Bits.Value);
            node.ModeBits = (node.ModeBits & ~0x38) | ((int)mask << 3);
            _loading = true;
            GroupBits.Value = mask;
            HasAcl = true;
            _loading = false;
            OnPropertyChanged(nameof(GroupRowLabel));
            Apply();
        }

        [RelayCommand]
        private void AddUser() => Add(true, UserToAdd);

        [RelayCommand]
        private void AddGroup() => Add(false, GroupToAdd);

        private void Add(bool isUser, string? name)
        {
            if (_item == null || string.IsNullOrEmpty(name) || AclEntries.Any(e => e.IsUser == isUser && e.Name == name)) return;
            if (!HasAcl)
            {
                // Première entrée : les droits du groupe du mode deviennent ceux du groupe propriétaire dans l'ACL
                _loading = true;
                GroupObjBits.Value = GroupBits.Value;
                _loading = false;
            }
            AclEntries.Add(Entry(isUser, AclEntry.Of(name, "r--")));
            RecalculateMask();
        }

        private void RemoveEntry(AclEntryItem entry)
        {
            AclEntries.Remove(entry);
            RecalculateMask();
        }

        /// <summary>setfacl -b : plus d'ACL ; les droits du groupe propriétaire reviennent dans le mode.</summary>
        [RelayCommand]
        private void RemoveAcl()
        {
            if (_item == null || _item.Node.Acl is not { } acl) return;
            _item.Node.Acl = null;
            _item.Node.ModeBits = (_item.Node.ModeBits & ~0x38) | ((int)Perms.Parse(acl.GroupObj) << 3);
            _loading = true;
            GroupBits.Value = _item.Node.GroupPerm;
            LoadAcl(_item.Node);
            _loading = false;
            Apply();
        }

        private void Apply()
        {
            _item?.Refresh();
            Describe();
            Changed?.Invoke();
        }

        /// <summary>Ligne ls -l, mode octal, effectif de chaque entrée, getfacl et commandes équivalentes.</summary>
        public void Describe()
        {
            if (_item == null) return;
            var node = _item.Node;
            Octal = node.Mode;
            LsLine = $"{LinuxListing.Mode(node)}  {node.Owner}  {node.Group}  {_item.Name}";
            foreach (var e in AclEntries)
            {
                var effective = e.Bits.Value & node.GroupPerm;
                e.Effective = effective != e.Bits.Value ? SmarxText.Instance.T($"effectif : {Perms.Format(effective)}", $"effective: {Perms.Format(effective)}") : null;
            }
            Getfacl = string.Join("\n", LinuxListing.Getfacl(node, _item.Path).Where(l => !l.StartsWith("default:")));
            var defaults = LinuxListing.Getfacl(node, _item.Path).Where(l => l.StartsWith("default:")).ToList();
            DefaultAcl = defaults.Count > 0 ? string.Join("\n", defaults) : null;
            Commands = string.Join("\n", LinuxScripts.Commands(node, _item.Path));
        }

        /// <summary>La langue a changé : le libellé de la ligne du groupe et les « effectif ».</summary>
        public void Relabel()
        {
            OnPropertyChanged(nameof(GroupRowLabel));
            Describe();
        }
    }
}
