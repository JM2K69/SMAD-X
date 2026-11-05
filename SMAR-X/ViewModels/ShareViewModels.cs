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
    /// <summary>Une ligne des autorisations du partage : Contrôle total, Modifier ou Lecture, à autoriser ou à refuser.</summary>
    public sealed partial class SharePermissionRow : ObservableObject
    {
        private readonly Action<ShareRight, AceType, bool> _toggle;
        private readonly bool _loading;

        public SharePermissionRow(ShareRight right, string label, bool allow, bool deny, Action<ShareRight, AceType, bool> toggle)
        {
            _toggle = toggle;
            Right = right;
            Label = label;
            _loading = true;
            AllowChecked = allow;
            DenyChecked = deny;
            _loading = false;
        }

        public ShareRight Right { get; }
        public string Label { get; }

        [ObservableProperty] private bool _allowChecked;
        [ObservableProperty] private bool _denyChecked;

        partial void OnAllowCheckedChanged(bool value) { if (!_loading) _toggle(Right, AceType.Allow, value); }
        partial void OnDenyCheckedChanged(bool value) { if (!_loading) _toggle(Right, AceType.Deny, value); }
    }

    /// <summary>
    /// Le Partage avancé d'un dossier : le partager ou non, ses noms de partage (un dossier peut en avoir plusieurs),
    /// les commentaires, l'énumération basée sur l'accès et les autorisations du partage. Les changements s'appliquent
    /// tout de suite au serveur simulé ; Annuler remet les partages comme avant.
    /// </summary>
    public sealed partial class ShareSettingsViewModel : ObservableObject
    {
        private static SmarxText Text => SmarxText.Instance;
        private readonly WindowsHost _host;
        private readonly NtfsItem _item;
        private readonly List<SmbShare> _snapshot;
        private bool _loading;

        public ShareSettingsViewModel(WindowsHost host, NtfsItem item)
        {
            _host = host;
            _item = item;
            _snapshot = host.Shares.Select(Copy).ToList();
            Reload(null);
        }

        private static SmbShare Copy(SmbShare s) => new()
        {
            Name = s.Name, Path = s.Path, Description = s.Description, AccessBasedEnumeration = s.AccessBasedEnumeration,
            Permissions = s.Permissions.Select(a => new ShareAce { Type = a.Type, Trustee = a.Trustee, Right = a.Right }).ToList()
        };

        public ObservableCollection<SmbShare> Shares { get; } = new();
        public ObservableCollection<PrincipalItem> Principals { get; } = new();
        public ObservableCollection<PrincipalItem> Candidates { get; } = new();
        public ObservableCollection<SharePermissionRow> Rows { get; } = new();

        [ObservableProperty] private SmbShare? _selectedShare;
        [ObservableProperty] private PrincipalItem? _selectedPrincipal;
        [ObservableProperty] private PrincipalItem? _candidate;
        [ObservableProperty] private string _newName = "";
        [ObservableProperty] private string _message = "";
        [ObservableProperty] private string _commands = "";

        public string Title => Text.T($"Partage avancé de {_item.Name}", $"Advanced Sharing of {_item.Name}");
        public string ObjectName => _item.Path;
        public bool HasMessage => Message.Length > 0;
        public bool HasShare => SelectedShare != null;
        public string NetworkPath => SelectedShare == null ? "" : $@"\\{_host.Name}\{SelectedShare.Name}";

        public bool IsShared
        {
            get => Shares.Count > 0;
            set
            {
                if (value == IsShared) return;
                if (value) Create(UniqueName(_item.Node.Name));
                else
                {
                    _host.Shares.RemoveAll(s => SamePath(s.Path));
                    Reload(null);
                }
                OnPropertyChanged();
            }
        }

        public string Comment
        {
            get => SelectedShare?.Description ?? "";
            set { if (SelectedShare != null) { SelectedShare.Description = value.Length == 0 ? null : value; Describe(); } }
        }

        public bool AccessBasedEnumeration
        {
            get => SelectedShare?.AccessBasedEnumeration ?? false;
            set { if (SelectedShare != null) { SelectedShare.AccessBasedEnumeration = value; Describe(); } }
        }

        private bool SamePath(string path) => string.Equals(path.TrimEnd('\\'), _item.Path.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);

        private string UniqueName(string name)
        {
            var candidate = name;
            for (var n = 2; _host.Share(candidate) != null; n++) candidate = $"{name}{n}";
            return candidate;
        }

        /// <summary>Un nouveau partage du dossier : Lecture pour Tout le monde, comme Windows le propose.</summary>
        private void Create(string name)
        {
            _host.Shares.Add(new SmbShare { Name = name, Path = _item.Path.TrimEnd('\\'), Permissions = [ShareAce.Allow(WellKnown.Everyone, ShareRight.Read)] });
            Reload(name);
        }

        private void Reload(string? select)
        {
            var selected = select ?? SelectedShare?.Name;
            _loading = true;
            Shares.Clear();
            foreach (var share in _host.Shares.Where(s => SamePath(s.Path))) Shares.Add(share);
            SelectedShare = Shares.FirstOrDefault(s => s.Name == selected) ?? Shares.FirstOrDefault();
            _loading = false;
            OnPropertyChanged(nameof(IsShared));
            ShareChanged();
        }

        partial void OnSelectedShareChanged(SmbShare? value)
        {
            if (!_loading) ShareChanged();
        }

        private void ShareChanged()
        {
            OnPropertyChanged(nameof(HasShare));
            OnPropertyChanged(nameof(Comment));
            OnPropertyChanged(nameof(AccessBasedEnumeration));
            OnPropertyChanged(nameof(NetworkPath));
            RebuildPrincipals(null);
        }

        private void RebuildPrincipals(string? select)
        {
            var selected = select ?? SelectedPrincipal?.Trustee;
            Principals.Clear();
            Candidates.Clear();
            if (SelectedShare is { } share)
            {
                foreach (var trustee in SharePermissions.Principals(_host, share))
                    Principals.Add(PrincipalItem.Of(_host, trustee));
                var listed = Principals.Select(p => p.Trustee).ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (var name in _host.Users.Select(u => u.Name).Concat(_host.Groups.Select(g => g.Name))
                             .Concat([WellKnown.Everyone, WellKnown.AuthenticatedUsers, WellKnown.Users, WellKnown.Administrators])
                             .Distinct(StringComparer.OrdinalIgnoreCase).Where(n => !listed.Contains(n)))
                    Candidates.Add(PrincipalItem.Of(_host, name));
            }
            Candidate = Candidates.FirstOrDefault();
            SelectedPrincipal = Principals.FirstOrDefault(p => p.Trustee == selected) ?? Principals.FirstOrDefault();
            BuildRows();
        }

        partial void OnSelectedPrincipalChanged(PrincipalItem? value) => BuildRows();
        partial void OnMessageChanged(string value) => OnPropertyChanged(nameof(HasMessage));

        private void BuildRows()
        {
            Rows.Clear();
            if (SelectedShare is { } share && SelectedPrincipal is { } principal)
                foreach (var right in SharePermissions.Rows)
                    Rows.Add(new SharePermissionRow(right, Label(right),
                        SharePermissions.IsChecked(share, principal.Trustee, right, AceType.Allow),
                        SharePermissions.IsChecked(share, principal.Trustee, right, AceType.Deny), Toggle));
            Describe();
        }

        private void Describe() => Commands = SelectedShare == null ? "" : string.Join("\n", SharePermissions.Commands(_host, SelectedShare));

        public static string Label(ShareRight right) => right switch
        {
            ShareRight.FullControl => Text.T("Contrôle total", "Full Control"),
            ShareRight.Change => Text.T("Modifier", "Change"),
            _ => Text.T("Lecture", "Read")
        };

        private void Toggle(ShareRight right, AceType type, bool on)
        {
            if (SelectedShare is not { } share || SelectedPrincipal is not { } principal) return;
            SharePermissions.Set(share, principal.Trustee, right, type, on);
            Message = "";
            RebuildPrincipals(principal.Trustee);
        }

        /// <summary>Un autre nom de partage pour le même dossier.</summary>
        [RelayCommand]
        private void AddShare()
        {
            var name = NewName.Trim();
            if (name.Length == 0 || name.IndexOfAny(['\\', '/', ':', '*', '?', '"', '<', '>', '|']) >= 0)
                Message = Text.T("Nom de partage invalide.", "Invalid share name.");
            else if (_host.Share(name) != null)
                Message = Text.T($"Le partage {name} existe déjà sur ce serveur.", $"The share {name} already exists on this server.");
            else
            {
                Message = "";
                NewName = "";
                Create(name);
            }
        }

        [RelayCommand]
        private void RemoveShare()
        {
            if (SelectedShare is not { } share) return;
            _host.Shares.Remove(share);
            Reload(null);
        }

        [RelayCommand]
        private void AddPrincipal()
        {
            if (SelectedShare is not { } share || Candidate is not { } candidate) return;
            SharePermissions.Add(share, candidate.Trustee);
            RebuildPrincipals(candidate.Trustee);
        }

        [RelayCommand]
        private void RemovePrincipal()
        {
            if (SelectedShare is not { } share || SelectedPrincipal is not { } principal) return;
            SharePermissions.Remove(share, principal.Trustee);
            RebuildPrincipals(null);
        }

        /// <summary>Annuler : les partages du serveur reviennent à l'état d'avant la fenêtre.</summary>
        public void Cancel()
        {
            _host.Shares.Clear();
            _host.Shares.AddRange(_snapshot);
        }
    }
}
