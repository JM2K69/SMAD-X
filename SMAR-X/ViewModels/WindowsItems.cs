using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SMARX.Models;
using SMARX.Services.Windows;

namespace SMARX.ViewModels
{
    /// <summary>Un choix d'une liste déroulante : une valeur et son libellé dans la langue de l'interface.</summary>
    /// <summary>Une route vers l'objet testé : en local (pas de partage), ou par un partage.</summary>
    public sealed class RouteItem(SmbShare? share, string label, string title, string detail)
    {
        public SmbShare? Share { get; } = share;
        public string Label { get; } = label;
        /// <summary>« Sur le serveur » ou « Depuis le réseau » ; <see cref="Detail"/> : la session, ou le partage.</summary>
        public string Title { get; } = title;
        public string Detail { get; } = detail;
        public bool IsLocal => Share == null;
        public bool IsNetwork => Share != null;
        public override string ToString() => Label;
    }

    public sealed class Choice(object value, string label)
    {
        public object Value { get; } = value;
        public string Label { get; } = label;
        public override string ToString() => Label;
    }

    /// <summary>Un fichier ou un dossier NTFS de l'arborescence, avec son propriétaire et l'état de l'héritage.</summary>
    public sealed partial class NtfsItem : ObservableObject
    {
        public NtfsItem(NtfsNode node, string path)
        {
            Node = node;
            Path = path;
            foreach (var child in node.Children.OrderBy(c => !c.IsDirectory).ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase))
                Children.Add(new NtfsItem(child, WindowsHost.Combine(path, child.Name)));
        }

        public NtfsNode Node { get; }
        public string Path { get; }
        public ObservableCollection<NtfsItem> Children { get; } = new();
        public string Name => WindowsHost.Parent(Path) == null ? Path : Node.Name;
        public bool IsDirectory => Node.IsDirectory;
        public string Owner => SmarxText.Instance.T("propriétaire : ", "owner: ") + WellKnown.Display(Node.Owner, SmarxText.Instance.Culture == "fr-FR");
        public bool IsProtected => !Node.InheritanceEnabled;
        public string ProtectedLabel => SmarxText.Instance.T("héritage désactivé", "inheritance disabled");

        [ObservableProperty] private bool _isExpanded = true;

        public void Refresh()
        {
            OnPropertyChanged(nameof(Owner));
            OnPropertyChanged(nameof(IsProtected));
            OnPropertyChanged(nameof(ProtectedLabel));
        }
    }

    /// <summary>Un compte Windows, avec sa pastille et ses groupes.</summary>
    public sealed class WindowsUserItem : IAccountItem
    {
        public WindowsUserItem(WindowsHost host, WindowsUser user, int index)
        {
            User = user;
            Brush = user.IsAdministrator ? Palette.Denied : Palette.Account(index);
            var french = SmarxText.Instance.Culture == "fr-FR";
            Groups = string.Join(", ", user.Groups.Select(g => WellKnown.Display(g, french)));
            IdLine = $"{host.Name}\\{user.Name} : " + string.Join(", ", user.Groups.Append(WellKnown.Users).Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(g => WellKnown.Display(g, french)));
        }

        public WindowsUser User { get; }
        public string Name => User.Name;
        public string Initial => User.Name.Length > 0 ? User.Name[..1].ToUpperInvariant() : "?";
        public IBrush Brush { get; }
        public string Groups { get; }
        public string IdLine { get; }
        public bool IsAdministrator => User.IsAdministrator;
        public override string ToString() => Name;
    }

    /// <summary>
    /// Une ACE de la DACL effective. Une ACE explicite se modifie (type, droits, portée) ou se retire ; une ACE héritée se
    /// lit seulement, avec le dossier d'où elle vient.
    /// </summary>
    public sealed partial class AceItem : ObservableObject
    {
        private readonly Action<AceItem>? _changed;
        private readonly bool _loading = true;

        public AceItem(EffectiveAce ace, int index, NtfsAce? source, bool folder, Action<AceItem>? changed, Action<AceItem>? remove)
        {
            var text = SmarxText.Instance;
            var french = text.Culture == "fr-FR";
            Ace = ace;
            Index = index;
            Source = source;
            _changed = changed;
            Trustee = WellKnown.Display(ace.Trustee, french);
            Origin = ace.Inherited ? text.T($"héritée de {ace.InheritedFrom}", $"inherited from {ace.InheritedFrom}") : text.T("explicite", "explicit");
            AppliesTo = WindowsExplainer.AppliesTo(ace.Inheritance, folder, text.Culture);
            RightsChoices = new ObservableCollection<Choice>(Ntfs.Simple.Select(s => new Choice(s.Rights, WindowsExplainer.Rights(s.Rights, text.Culture))));
            if (Ntfs.SimpleOf(ace.Mask) is null)
                RightsChoices.Add(new Choice(ace.Mask, text.T("Spécial : ", "Special: ") + Ntfs.Format(ace.Mask)));
            AppliesChoices = folder ? WindowsRightsEditorViewModel.AppliesChoices() : [new Choice(AceInheritance.None, text.T("Ce fichier", "This file"))];
            _isDeny = ace.Type == AceType.Deny;
            _rights = RightsChoices.First(c => Equals(c.Value, Ntfs.SimpleOf(ace.Mask) ?? ace.Mask));
            _applies = AppliesChoices.FirstOrDefault(c => Equals(c.Value, folder ? source?.Inheritance ?? ace.Inheritance : AceInheritance.None)) ?? AppliesChoices[0];
            Remove = new RelayCommand(() => remove?.Invoke(this), () => remove != null);
            _loading = false;
        }

        public EffectiveAce Ace { get; }
        public int Index { get; }
        public NtfsAce? Source { get; }
        public bool IsExplicit => Source != null;
        public bool IsInherited => Source == null;
        public string Trustee { get; }
        public string Origin { get; }
        public string AppliesTo { get; }
        public ObservableCollection<Choice> RightsChoices { get; }
        public ObservableCollection<Choice> AppliesChoices { get; }
        public IRelayCommand Remove { get; }
        public string TypeLabel => IsDeny ? SmarxText.Instance.T("Refuser", "Deny") : SmarxText.Instance.T("Autoriser", "Allow");
        public IBrush TypeBrush => IsDeny ? Palette.Denied : Palette.Allowed;
        public IBrush TypeBackground => IsDeny ? Palette.DeniedBack : Palette.AllowedBack;
        public string RightsLabel => Rights.Label;

        [ObservableProperty] private bool _isDeny;
        [ObservableProperty] private Choice _rights;
        [ObservableProperty] private Choice _applies;

        partial void OnIsDenyChanged(bool value)
        {
            OnPropertyChanged(nameof(TypeLabel));
            OnPropertyChanged(nameof(TypeBrush));
            OnPropertyChanged(nameof(TypeBackground));
            if (!_loading) _changed?.Invoke(this);
        }

        partial void OnRightsChanged(Choice value) { if (!_loading) _changed?.Invoke(this); }
        partial void OnAppliesChanged(Choice value) { if (!_loading) _changed?.Invoke(this); }
    }

    /// <summary>Un droit spécial et s'il est accordé au compte testé : l'onglet « Accès effectif » de Windows.</summary>
    public sealed class EffectiveRightItem(string label, bool granted)
    {
        public string Label { get; } = label;
        public bool Granted { get; } = granted;
        public string Symbol => Granted ? "✓" : "✕";
        public IBrush Brush => Granted ? Palette.Allowed : Palette.Denied;
    }
}
