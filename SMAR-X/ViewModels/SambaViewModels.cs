using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SMARX.Models;

namespace SMARX.ViewModels
{
    /// <summary>
    /// Le partage Samba d'un dossier, comme une section de smb.conf : le partager ou non, ses noms, qui s'y connecte, qui y
    /// écrit, sous quelle identité, avec quels masques. La section s'affiche telle que Samba la lit. Les changements
    /// s'appliquent tout de suite au serveur simulé ; Annuler remet les partages comme avant.
    /// </summary>
    public sealed partial class SambaShareSettingsViewModel : ObservableObject
    {
        private static SmarxText Text => SmarxText.Instance;
        private readonly LinuxHost _host;
        private readonly NodeItem _item;
        private readonly List<SambaShare> _snapshot;
        private bool _loading;

        public SambaShareSettingsViewModel(LinuxHost host, NodeItem item)
        {
            _host = host;
            _item = item;
            _snapshot = host.Shares.Select(Copy).ToList();
            Reload(null);
        }

        private static SambaShare Copy(SambaShare s) => new()
        {
            Name = s.Name, Path = s.Path, Comment = s.Comment, ReadOnly = s.ReadOnly,
            ValidUsers = [.. s.ValidUsers], InvalidUsers = [.. s.InvalidUsers], ReadList = [.. s.ReadList], WriteList = [.. s.WriteList],
            ForceUser = s.ForceUser, ForceGroup = s.ForceGroup, CreateMask = s.CreateMask, ForceCreateMode = s.ForceCreateMode,
            DirectoryMask = s.DirectoryMask, ForceDirectoryMode = s.ForceDirectoryMode, HideUnreadable = s.HideUnreadable, Browseable = s.Browseable
        };

        public ObservableCollection<SambaShare> Shares { get; } = new();

        [ObservableProperty] private SambaShare? _selectedShare;
        [ObservableProperty] private string _newName = "";
        [ObservableProperty] private string _message = "";
        [ObservableProperty] private string _config = "";

        public string Title => Text.T($"Partage Samba de {_item.Path}", $"Samba sharing of {_item.Path}");
        public string ObjectName => _item.Path;
        public bool HasShare => SelectedShare != null;
        public bool HasMessage => Message.Length > 0;
        public string NetworkPath => SelectedShare == null ? "" : $@"\\{_host.Name}\{SelectedShare.Name}";

        public bool IsShared
        {
            get => Shares.Count > 0;
            set
            {
                if (value == IsShared) return;
                if (value) Create(UniqueName(_item.Node.Name.Length == 0 ? "racine" : _item.Node.Name));
                else
                {
                    _host.Shares.RemoveAll(s => s.Path == _item.Path);
                    Reload(null);
                }
                OnPropertyChanged();
            }
        }

        // Les réglages du partage choisi : chaque changement réécrit la section de smb.conf
        public string Comment { get => SelectedShare?.Comment ?? ""; set => Change(s => s.Comment = value.Length == 0 ? null : value); }
        public bool ReadOnly { get => SelectedShare?.ReadOnly ?? true; set => Change(s => s.ReadOnly = value); }
        public string ValidUsers { get => Join(SelectedShare?.ValidUsers); set => Change(s => s.ValidUsers = Split(value)); }
        public string InvalidUsers { get => Join(SelectedShare?.InvalidUsers); set => Change(s => s.InvalidUsers = Split(value)); }
        public string ReadList { get => Join(SelectedShare?.ReadList); set => Change(s => s.ReadList = Split(value)); }
        public string WriteList { get => Join(SelectedShare?.WriteList); set => Change(s => s.WriteList = Split(value)); }
        public string ForceUser { get => SelectedShare?.ForceUser ?? ""; set => Change(s => s.ForceUser = Account(value, user: true)); }
        public string ForceGroup { get => SelectedShare?.ForceGroup ?? ""; set => Change(s => s.ForceGroup = Account(value, user: false)); }
        public string CreateMask { get => SelectedShare?.CreateMask ?? ""; set => Change(s => s.CreateMask = Octal(value, s.CreateMask)); }
        public string ForceCreateMode { get => SelectedShare?.ForceCreateMode ?? ""; set => Change(s => s.ForceCreateMode = Octal(value, s.ForceCreateMode)); }
        public string DirectoryMask { get => SelectedShare?.DirectoryMask ?? ""; set => Change(s => s.DirectoryMask = Octal(value, s.DirectoryMask)); }
        public string ForceDirectoryMode { get => SelectedShare?.ForceDirectoryMode ?? ""; set => Change(s => s.ForceDirectoryMode = Octal(value, s.ForceDirectoryMode)); }
        public bool HideUnreadable { get => SelectedShare?.HideUnreadable ?? false; set => Change(s => s.HideUnreadable = value); }
        public bool Browseable { get => SelectedShare?.Browseable ?? true; set => Change(s => s.Browseable = value); }

        private static string Join(List<string>? list) => list == null ? "" : string.Join(' ', list);
        private static List<string> Split(string text) => text.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries).ToList();

        private void Change(Action<SambaShare> apply)
        {
            if (_loading || SelectedShare is not { } share) return;
            Message = "";
            apply(share);
            Describe();
        }

        /// <summary>Un compte (force user) ou un groupe (force group) du serveur ; vide pour aucun.</summary>
        private string? Account(string value, bool user)
        {
            var name = value.Trim();
            if (name.Length == 0) return null;
            if (user ? _host.User(name) != null : _host.Group(name) != null) return name;
            Message = Text.T($"{name} n'existe pas sur ce serveur.", $"{name} does not exist on this server.");
            return user ? SelectedShare?.ForceUser : SelectedShare?.ForceGroup;
        }

        /// <summary>Un mode octal (« 0660 », « 2770 ») ; une valeur invalide garde la précédente.</summary>
        private string Octal(string value, string previous)
        {
            var text = value.Trim();
            if (text.Length is >= 3 and <= 4 && text.All(c => c is >= '0' and <= '7')) return text.PadLeft(4, '0');
            Message = Text.T("Un mode s'écrit en octal, comme 0660 ou 2770.", "A mode is written in octal, like 0660 or 2770.");
            return previous;
        }

        private string UniqueName(string name)
        {
            var candidate = name;
            for (var n = 2; _host.Share(candidate) != null; n++) candidate = $"{name}{n}";
            return candidate;
        }

        /// <summary>Un nouveau partage du dossier : les valeurs par défaut de Samba (lecture seule, create mask 0744, directory mask 0755).</summary>
        private void Create(string name)
        {
            _host.Shares.Add(new SambaShare { Name = name, Path = _item.Path });
            Reload(name);
        }

        private void Reload(string? select)
        {
            var selected = select ?? SelectedShare?.Name;
            _loading = true;
            Shares.Clear();
            foreach (var share in _host.Shares.Where(s => s.Path == _item.Path)) Shares.Add(share);
            SelectedShare = Shares.FirstOrDefault(s => s.Name == selected) ?? Shares.FirstOrDefault();
            _loading = false;
            OnPropertyChanged(nameof(IsShared));
            ShareChanged();
        }

        partial void OnSelectedShareChanged(SambaShare? value)
        {
            if (!_loading) ShareChanged();
        }

        partial void OnMessageChanged(string value) => OnPropertyChanged(nameof(HasMessage));

        private void ShareChanged()
        {
            foreach (var property in new[] { nameof(HasShare), nameof(NetworkPath), nameof(Comment), nameof(ReadOnly), nameof(ValidUsers), nameof(InvalidUsers),
                         nameof(ReadList), nameof(WriteList), nameof(ForceUser), nameof(ForceGroup), nameof(CreateMask), nameof(ForceCreateMode),
                         nameof(DirectoryMask), nameof(ForceDirectoryMode), nameof(HideUnreadable), nameof(Browseable) })
                OnPropertyChanged(property);
            Describe();
        }

        private void Describe() => Config = SelectedShare == null ? "" : string.Join("\n", SelectedShare.Config());

        [RelayCommand]
        private void AddShare()
        {
            var name = NewName.Trim();
            if (name.Length == 0 || name.IndexOfAny(['[', ']', '/', '\\', ':', '*', '?', '"', '<', '>', '|', ' ']) >= 0)
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

        /// <summary>Annuler : les partages du serveur reviennent à l'état d'avant la fenêtre.</summary>
        public void Cancel()
        {
            _host.Shares.Clear();
            _host.Shares.AddRange(_snapshot);
        }
    }
}
