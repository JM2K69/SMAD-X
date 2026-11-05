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
    /// <summary>Un compte de la liste « Noms de groupes ou d'utilisateurs ».</summary>
    public sealed class PrincipalItem(string trustee, string display, bool isUser)
    {
        public string Trustee { get; } = trustee;
        public string Display { get; } = display;
        public bool IsUser { get; } = isUser;
        public bool IsGroup => !IsUser;
        public override string ToString() => Display;

        public static PrincipalItem Of(WindowsHost host, string trustee) =>
            new(trustee, NtfsSecurityTab.Display(host, trustee, SmarxText.Instance.Culture), host.User(trustee) != null);
    }

    /// <summary>Une ligne de l'onglet Sécurité : ses coches Autoriser et Refuser, noires (explicites) ou grises (héritées).</summary>
    public sealed class SecurityRowItem(SecurityRow row, string label)
    {
        public string Label { get; } = label;
        public string AllowMark => row.Allow == Mark.None ? "" : "✓";
        public string DenyMark => row.Deny == Mark.None ? "" : "✓";
        // « Autorisations spéciales » est toujours grise, comme dans l'Explorateur
        public bool AllowGray => row.Allow == Mark.Inherited || row.Permission == BasicPermission.Special;
        public bool DenyGray => row.Deny == Mark.Inherited || row.Permission == BasicPermission.Special;
    }

    /// <summary>
    /// L'onglet Sécurité des Propriétés de l'Explorateur, pour le fichier ou le dossier choisi : les comptes, puis les
    /// autorisations de base du compte choisi. Les boutons Modifier et Avancé ouvrent les mêmes fenêtres que Windows.
    /// </summary>
    public sealed partial class SecurityTabViewModel : ObservableObject
    {
        private static SmarxText Text => SmarxText.Instance;

        public WindowsHost? Host { get; private set; }
        public NtfsItem? Item { get; private set; }
        public ObservableCollection<PrincipalItem> Principals { get; } = new();
        public ObservableCollection<SecurityRowItem> Rows { get; } = new();

        [ObservableProperty] private PrincipalItem? _selectedPrincipal;

        public string Title => Text.T($"Propriétés de {Item?.Name} · Sécurité", $"{Item?.Name} Properties · Security");
        public string ObjectName => Item?.Path ?? "";
        public string PermissionsFor => Text.T($"Autorisations pour {SelectedPrincipal?.Display}", $"Permissions for {SelectedPrincipal?.Display}");

        public void Load(WindowsHost host, NtfsItem? item)
        {
            Host = host;
            Item = item;
            var selected = SelectedPrincipal?.Trustee;
            Principals.Clear();
            if (item != null)
                foreach (var trustee in NtfsSecurityTab.Principals(host, item.Path))
                    Principals.Add(PrincipalItem.Of(host, trustee));
            SelectedPrincipal = Principals.FirstOrDefault(p => p.Trustee == selected) ?? Principals.FirstOrDefault();
            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(ObjectName));
            BuildRows();
        }

        partial void OnSelectedPrincipalChanged(PrincipalItem? value) => BuildRows();

        private void BuildRows()
        {
            Rows.Clear();
            OnPropertyChanged(nameof(PermissionsFor));
            if (Host == null || Item == null || SelectedPrincipal == null) return;
            foreach (var row in NtfsSecurityTab.Permissions(Host, Item.Path, SelectedPrincipal.Trustee))
                Rows.Add(new SecurityRowItem(row, NtfsSecurityTab.Name(row.Permission, Text.Culture)));
        }
    }

    /// <summary>L'état d'un objet avant une fenêtre, pour Annuler : propriétaire, héritage, ACE explicites.</summary>
    internal sealed class NodeSnapshot(NtfsNode node)
    {
        private readonly string _owner = node.Owner;
        private readonly bool _inheritance = node.InheritanceEnabled;
        private readonly List<NtfsAce> _aces = node.Aces.Select(Copy).ToList();

        private static NtfsAce Copy(NtfsAce a) => new() { Type = a.Type, Trustee = a.Trustee, Rights = a.Rights, Inheritance = a.Inheritance };

        public void Restore()
        {
            node.Owner = _owner;
            node.InheritanceEnabled = _inheritance;
            node.Aces = _aces.Select(Copy).ToList();
        }
    }

    /// <summary>Une ligne de la fenêtre Modifier : deux cases, grisées quand seul l'héritage les coche.</summary>
    public sealed partial class EditRowItem : ObservableObject
    {
        private readonly Action<BasicPermission, AceType, bool> _toggle;
        private bool _loading;

        public EditRowItem(SecurityRow row, string label, bool allowLocked, bool denyLocked, Action<BasicPermission, AceType, bool> toggle)
        {
            _toggle = toggle;
            Permission = row.Permission;
            Label = label;
            _loading = true;
            AllowChecked = row.Allow != Mark.None;
            DenyChecked = row.Deny != Mark.None;
            _loading = false;
            // « Autorisations spéciales » ne se coche pas ici : il faut la fenêtre Avancé
            AllowEnabled = row.Permission != BasicPermission.Special && !allowLocked;
            DenyEnabled = row.Permission != BasicPermission.Special && !denyLocked;
        }

        public BasicPermission Permission { get; }
        public string Label { get; }
        public bool AllowEnabled { get; }
        public bool DenyEnabled { get; }

        [ObservableProperty] private bool _allowChecked;
        [ObservableProperty] private bool _denyChecked;

        partial void OnAllowCheckedChanged(bool value) { if (!_loading) _toggle(Permission, AceType.Allow, value); }
        partial void OnDenyCheckedChanged(bool value) { if (!_loading) _toggle(Permission, AceType.Deny, value); }
    }

    /// <summary>
    /// La fenêtre « Autorisations pour … » (bouton Modifier) : ajouter ou supprimer un compte, cocher ses autorisations de
    /// base. Chaque case s'applique tout de suite au serveur simulé, avec la commande icacls qui ferait de même ; Annuler
    /// remet l'objet comme il était.
    /// </summary>
    public sealed partial class PermissionsEditViewModel : ObservableObject
    {
        private static SmarxText Text => SmarxText.Instance;
        private readonly WindowsHost _host;
        private readonly NtfsItem _item;
        private readonly NodeSnapshot _snapshot;

        public PermissionsEditViewModel(WindowsHost host, NtfsItem item)
        {
            _host = host;
            _item = item;
            _snapshot = new NodeSnapshot(item.Node);
            Reload(null);
        }

        public ObservableCollection<PrincipalItem> Principals { get; } = new();
        public ObservableCollection<EditRowItem> Rows { get; } = new();
        public ObservableCollection<PrincipalItem> Candidates { get; } = new();

        [ObservableProperty] private PrincipalItem? _selectedPrincipal;
        [ObservableProperty] private PrincipalItem? _candidate;
        [ObservableProperty] private string _message = "";
        [ObservableProperty] private string _commands = "";

        public string Title => Text.T($"Autorisations pour {_item.Name}", $"Permissions for {_item.Name}");
        public string ObjectName => _item.Path;
        public string PermissionsFor => Text.T($"Autorisations pour {SelectedPrincipal?.Display}", $"Permissions for {SelectedPrincipal?.Display}");
        public bool HasMessage => Message.Length > 0;
        /// <summary>Rouge pour un refus de Windows, ambre pour son avertissement sur les refus.</summary>
        public Avalonia.Media.IBrush MessageBrush => _warning ? Warning : Palette.Denied;
        private static readonly Avalonia.Media.IBrush Warning = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#D29922"));
        private bool _warning;

        private void Reload(string? select)
        {
            var selected = select ?? SelectedPrincipal?.Trustee;
            Principals.Clear();
            foreach (var trustee in NtfsSecurityTab.Principals(_host, _item.Path))
                Principals.Add(PrincipalItem.Of(_host, trustee));
            SelectedPrincipal = Principals.FirstOrDefault(p => p.Trustee == selected) ?? Principals.FirstOrDefault();
            Candidates.Clear();
            var listed = Principals.Select(p => p.Trustee).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var name in _host.Users.Select(u => u.Name).Concat(_host.Groups.Select(g => g.Name))
                         .Concat([WellKnown.Everyone, WellKnown.AuthenticatedUsers, WellKnown.Users, WellKnown.Administrators, WellKnown.System,
                                  WellKnown.Interactive, WellKnown.Network, WellKnown.CreatorOwner, WellKnown.OwnerRights])
                         .Distinct(StringComparer.OrdinalIgnoreCase).Where(n => !listed.Contains(n)))
                Candidates.Add(PrincipalItem.Of(_host, name));
            Candidate = Candidates.FirstOrDefault();
            BuildRows();
        }

        partial void OnSelectedPrincipalChanged(PrincipalItem? value) => BuildRows();
        partial void OnMessageChanged(string value)
        {
            OnPropertyChanged(nameof(HasMessage));
            OnPropertyChanged(nameof(MessageBrush));
        }

        private void Say(string text, bool warning = false)
        {
            _warning = warning;
            Message = text;
            OnPropertyChanged(nameof(MessageBrush));
        }

        private void BuildRows()
        {
            Rows.Clear();
            OnPropertyChanged(nameof(PermissionsFor));
            if (SelectedPrincipal is not { } principal) { Commands = ""; return; }
            var rows = NtfsSecurityTab.Permissions(_host, _item.Path, principal.Trustee);
            foreach (var row in rows)
            {
                // Grisée : cochée par l'héritage seulement (une ACE explicite qui la donne aussi la rend modifiable)
                var allowLocked = row.Allow == Mark.Inherited && !NtfsSecurityTab.Explicitly(_host, _item.Path, principal.Trustee, row.Permission, AceType.Allow);
                var denyLocked = row.Deny == Mark.Inherited && !NtfsSecurityTab.Explicitly(_host, _item.Path, principal.Trustee, row.Permission, AceType.Deny);
                Rows.Add(new EditRowItem(row, NtfsSecurityTab.Name(row.Permission, Text.Culture), allowLocked, denyLocked, Toggle));
            }
            Commands = string.Join("\n", WindowsListing.TrusteeCommands(_host, _item.Node, _item.Path, principal.Trustee));
        }

        private void Toggle(BasicPermission permission, AceType type, bool on)
        {
            if (SelectedPrincipal is not { } principal) return;
            if (!NtfsSecurityTab.Set(_host, _item.Path, principal.Trustee, permission, type, on))
                Say(Text.T("Cette autorisation est héritée : elle se change sur le dossier parent, ou après avoir désactivé l'héritage.",
                           "This permission is inherited: change it on the parent folder, or after disabling inheritance."));
            else if (type == AceType.Deny && on)
                // L'avertissement de Windows, avec sa nuance : l'ordre de lecture des ACE
                Say(Text.T("Windows prévient : « Vous définissez une entrée d'autorisation de refus. Les entrées de refus sont prioritaires sur les entrées d'autorisation » : membre de deux groupes, l'un autorisé, l'autre refusé, le compte est refusé. Nuance : une autorisation explicite est lue avant un refus hérité, et l'emporte. Les cases Autoriser qui partagent des droits avec ce refus ont été décochées.",
                           "Windows warns: \"You are setting a deny permissions entry. Deny entries take precedence over allow entries\": a member of two groups, one allowed and one denied, is denied. Nuance: an explicit allow is read before an inherited deny, and wins. The Allow boxes that share rights with this deny were cleared."),
                    warning: true);
            else
                Say("");
            BuildRows();
        }

        [RelayCommand]
        private void Add()
        {
            if (Candidate is not { } candidate) return;
            NtfsSecurityTab.Add(_host, _item.Path, candidate.Trustee);
            Say("");
            Reload(candidate.Trustee);
        }

        [RelayCommand]
        private void Remove()
        {
            if (SelectedPrincipal is not { } principal) return;
            if (NtfsSecurityTab.Remove(_host, _item.Path, principal.Trustee))
            {
                Say("");
                Reload(null);
                Commands = $"icacls \"{_item.Path}\" /remove \"{WindowsListing.Account(_host, principal.Trustee)}\"";
            }
            else
                Say(Text.T($"Impossible de supprimer {principal.Display} : cet objet hérite de ses autorisations. Désactivez d'abord l'héritage (bouton Avancé).",
                                 $"You can't remove {principal.Display} because this object is inheriting permissions from its parent. Disable inheritance first (Advanced)."));
        }

        /// <summary>Annuler : l'objet revient à l'état d'avant la fenêtre.</summary>
        public void Cancel() => _snapshot.Restore();
    }

    /// <summary>Une entrée de « Paramètres de sécurité avancés ».</summary>
    public sealed class AdvancedEntryItem(string type, string principal, string access, string inheritedFrom, string appliesTo, bool deny)
    {
        public string Type { get; } = type;
        public string Principal { get; } = principal;
        public string Access { get; } = access;
        public string InheritedFrom { get; } = inheritedFrom;
        public string AppliesTo { get; } = appliesTo;
        public bool IsDeny { get; } = deny;
    }

    /// <summary>
    /// « Paramètres de sécurité avancés » : le propriétaire, les entrées (les ACE fusionnées comme Windows les présente),
    /// l'héritage (désactiver en convertissant ou en supprimant les héritées, ou le réactiver) et l'onglet Accès effectif.
    /// </summary>
    public sealed partial class AdvancedSecurityViewModel : ObservableObject
    {
        private static SmarxText Text => SmarxText.Instance;
        private readonly WindowsHost _host;
        private readonly NtfsItem _item;
        private readonly NodeSnapshot _snapshot;
        private bool _loading;

        public AdvancedSecurityViewModel(WindowsHost host, NtfsItem item, string? user)
        {
            _host = host;
            _item = item;
            _snapshot = new NodeSnapshot(item.Node);
            var french = Text.Culture == "fr-FR";
            _loading = true;
            foreach (var u in host.Users) Owners.Add(new Choice(u.Name, u.Name));
            foreach (var g in host.Groups) Owners.Add(new Choice(g.Name, WellKnown.Display(g.Name, french)));
            Owner = Owners.FirstOrDefault(o => string.Equals((string)o.Value, item.Node.Owner, StringComparison.OrdinalIgnoreCase));
            foreach (var u in host.Users) EffectiveUsers.Add(new Choice(u.Name, u.Name));
            EffectiveUser = EffectiveUsers.FirstOrDefault(u => (string)u.Value == user) ?? EffectiveUsers.FirstOrDefault();
            _loading = false;
            Describe();
        }

        public ObservableCollection<Choice> Owners { get; } = new();
        public ObservableCollection<AdvancedEntryItem> Entries { get; } = new();
        public ObservableCollection<Choice> EffectiveUsers { get; } = new();
        public ObservableCollection<EffectiveRightItem> EffectiveRights { get; } = new();

        [ObservableProperty] private Choice? _owner;
        [ObservableProperty] private bool _asking;
        [ObservableProperty] private string _commands = "";
        [ObservableProperty] private Choice? _effectiveUser;
        [ObservableProperty] private bool _elevated;

        public string Title => Text.T($"Paramètres de sécurité avancés pour {_item.Name}", $"Advanced Security Settings for {_item.Name}");
        public string ObjectName => _item.Path;
        public bool IsFolder => _item.IsDirectory;
        public bool InheritanceEnabled => _item.Node.InheritanceEnabled;
        public bool CanDisable => InheritanceEnabled && !Asking && WindowsHost.Parent(_item.Path) != null;
        public bool CanEnable => !InheritanceEnabled && WindowsHost.Parent(_item.Path) != null;

        private void Describe()
        {
            Entries.Clear();
            var culture = Text.Culture;
            foreach (var e in NtfsSecurityTab.Advanced(_host, _item.Path))
                Entries.Add(new AdvancedEntryItem(e.Type == AceType.Deny ? Text.T("Refuser", "Deny") : Text.T("Autoriser", "Allow"),
                    NtfsSecurityTab.Display(_host, e.Trustee, culture), NtfsSecurityTab.Access(e, _item.IsDirectory, culture),
                    NtfsSecurityTab.InheritedFrom(e, culture), NtfsSecurityTab.AppliesTo(e, _item.IsDirectory, culture), e.Type == AceType.Deny));
            OnPropertyChanged(nameof(InheritanceEnabled));
            OnPropertyChanged(nameof(CanDisable));
            OnPropertyChanged(nameof(CanEnable));
            BuildEffective();
        }

        partial void OnOwnerChanged(Choice? value)
        {
            if (_loading || value == null) return;
            _item.Node.Owner = (string)value.Value;
            Commands = $"icacls \"{_item.Path}\" /setowner \"{WindowsListing.Account(_host, _item.Node.Owner)}\"";
            Describe();
        }

        partial void OnAskingChanged(bool value) => OnPropertyChanged(nameof(CanDisable));
        partial void OnEffectiveUserChanged(Choice? value) => BuildEffective();
        partial void OnElevatedChanged(bool value) => BuildEffective();

        [RelayCommand] private void Disable() => Asking = true;
        [RelayCommand] private void KeepInheritance() => Asking = false;

        /// <summary>« Convertir les autorisations héritées en autorisations explicites sur cet objet » (icacls /inheritance:d).</summary>
        [RelayCommand]
        private void Convert()
        {
            NtfsSecurityTab.DisableInheritance(_host, _item.Path, convert: true);
            Commands = $"icacls \"{_item.Path}\" /inheritance:d";
            Asking = false;
            Describe();
        }

        /// <summary>« Supprimer toutes les autorisations héritées de cet objet » (icacls /inheritance:r).</summary>
        [RelayCommand]
        private void RemoveInherited()
        {
            NtfsSecurityTab.DisableInheritance(_host, _item.Path, convert: false);
            Commands = $"icacls \"{_item.Path}\" /inheritance:r";
            Asking = false;
            Describe();
        }

        [RelayCommand]
        private void Enable()
        {
            _item.Node.InheritanceEnabled = true;
            Commands = $"icacls \"{_item.Path}\" /inheritance:e";
            Describe();
        }

        /// <summary>L'onglet Accès effectif : chaque droit spécial, accordé ou non au compte choisi.</summary>
        private void BuildEffective()
        {
            EffectiveRights.Clear();
            if (EffectiveUser == null || _host.User((string)EffectiveUser.Value) is not { } user) return;
            var token = WindowsToken.Local(user, Elevated);
            foreach (var (_, right) in Ntfs.Special)
            {
                var grant = WindowsAccess.Open(_host, token, _item.Path, right);
                var label = WindowsExplainer.Rights(right, Text.Culture);
                if (grant == OpenGrant.ByParent)
                    label += Text.T(" (par le dossier parent)", " (from the parent folder)");
                EffectiveRights.Add(new EffectiveRightItem(label, grant != OpenGrant.Denied));
            }
        }

        /// <summary>Annuler : l'objet revient à l'état d'avant la fenêtre.</summary>
        public void Cancel() => _snapshot.Restore();
    }
}
