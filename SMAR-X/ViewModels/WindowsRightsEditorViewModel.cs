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
    /// Autorisations NTFS du fichier ou du dossier choisi, modifiables comme dans l'onglet Sécurité de Windows :
    /// propriétaire, héritage, ACE explicites (les héritées se lisent seulement). Chaque modification s'applique tout de
    /// suite au serveur simulé (<see cref="Changed"/>).
    /// </summary>
    public sealed partial class WindowsRightsEditorViewModel : ObservableObject
    {
        private static SmarxText Text => SmarxText.Instance;
        private WindowsHost? _host;
        private NtfsItem? _item;
        private bool _loading;

        public event Action? Changed;

        public ObservableCollection<AceItem> Aces { get; } = new();
        public ObservableCollection<Choice> Principals { get; } = new();
        public ObservableCollection<Choice> Owners { get; } = new();
        public ObservableCollection<Choice> NewRightsChoices { get; } = new();
        public ObservableCollection<Choice> NewAppliesChoices { get; } = new();

        [ObservableProperty] private Choice? _owner;
        [ObservableProperty] private bool _inheritanceEnabled;
        [ObservableProperty] private bool _isFolder;
        [ObservableProperty] private Choice? _newTrustee;
        [ObservableProperty] private bool _newIsDeny;
        [ObservableProperty] private Choice? _newRights;
        [ObservableProperty] private Choice? _newApplies;
        [ObservableProperty] private string _icacls = "";
        [ObservableProperty] private string _commands = "";

        public bool IsProtected => !InheritanceEnabled;

        /// <summary>« S'applique à » d'une ACE posée sur un dossier, dans l'ordre de Windows.</summary>
        public static ObservableCollection<Choice> AppliesChoices() => new(new[]
        {
            AceInheritance.ThisFolderSubfoldersAndFiles, AceInheritance.None, AceInheritance.ContainerInherit, AceInheritance.ObjectInherit,
            AceInheritance.ThisFolderSubfoldersAndFiles | AceInheritance.InheritOnly, AceInheritance.ContainerInherit | AceInheritance.InheritOnly,
            AceInheritance.ObjectInherit | AceInheritance.InheritOnly
        }.Select(a => new Choice(a, WindowsExplainer.AppliesTo(a, true, Text.Culture))));

        public void Load(WindowsHost host, NtfsItem? item)
        {
            _host = host;
            _item = item;
            _loading = true;
            var french = Text.Culture == "fr-FR";
            Principals.Clear();
            Owners.Clear();
            foreach (var u in host.Users) { Principals.Add(new Choice(u.Name, u.Name)); Owners.Add(new Choice(u.Name, u.Name)); }
            foreach (var g in host.Groups) { Principals.Add(new Choice(g.Name, WellKnown.Display(g.Name, french))); Owners.Add(new Choice(g.Name, WellKnown.Display(g.Name, french))); }
            foreach (var w in new[] { WellKnown.Everyone, WellKnown.AuthenticatedUsers, WellKnown.CreatorOwner, WellKnown.System, WellKnown.Interactive, WellKnown.OwnerRights })
                Principals.Add(new Choice(w, WellKnown.Display(w, french)));
            NewRightsChoices.Clear();
            foreach (var s in Ntfs.Simple) NewRightsChoices.Add(new Choice(s.Rights, WindowsExplainer.Rights(s.Rights, Text.Culture)));
            NewAppliesChoices.Clear();
            if (item != null)
            {
                IsFolder = item.IsDirectory;
                foreach (var c in item.IsDirectory ? AppliesChoices() : [new Choice(AceInheritance.None, Text.T("Ce fichier", "This file"))])
                    NewAppliesChoices.Add(c);
                Owner = Owners.FirstOrDefault(o => string.Equals((string)o.Value, item.Node.Owner, StringComparison.OrdinalIgnoreCase));
                InheritanceEnabled = item.Node.InheritanceEnabled;
            }
            // Les choix viennent d'être recréés : on retrouve le précédent par sa valeur
            var trustee = NewTrustee?.Value as string ?? "dave";
            NewTrustee = Principals.FirstOrDefault(p => string.Equals((string)p.Value, trustee, StringComparison.OrdinalIgnoreCase)) ?? Principals.FirstOrDefault();
            NewRights = NewRightsChoices.FirstOrDefault(c => Equals(c.Value, NtfsRights.Modify));
            NewApplies = NewAppliesChoices.FirstOrDefault();
            _loading = false;
            Describe();
        }

        /// <summary>La DACL effective, les ACE explicites modifiables, la sortie d'icacls et les commandes équivalentes.</summary>
        public void Describe()
        {
            Aces.Clear();
            if (_host == null || _item == null) return;
            var node = _item.Node;
            var dacl = NtfsInheritance.Dacl(_host, _item.Path);
            var explicitAces = node.Aces.OrderBy(a => a.Type == AceType.Deny ? 0 : 1).ToList();
            for (var i = 0; i < dacl.Count; i++)
            {
                var source = !dacl[i].Inherited && i < explicitAces.Count ? explicitAces[i] : null;
                Aces.Add(new AceItem(dacl[i], i + 1, source, node.IsDirectory, source == null ? null : Edit, source == null ? null : RemoveAce));
            }
            Icacls = string.Join("\n", WindowsListing.Icacls(_host, _item.Path));
            Commands = string.Join("\n", WindowsListing.Commands(_host, node, _item.Path));
            OnPropertyChanged(nameof(IsProtected));
        }

        partial void OnOwnerChanged(Choice? value)
        {
            if (_loading || _item == null || value == null) return;
            _item.Node.Owner = (string)value.Value;
            Apply();
        }

        /// <summary>Une ACE explicite modifiée : type, droits, portée. Un refus omet Synchroniser, comme l'onglet Sécurité.</summary>
        private void Edit(AceItem item)
        {
            if (item.Source is not { } ace) return;
            ace.Type = item.IsDeny ? AceType.Deny : AceType.Allow;
            ace.Rights = Ntfs.Format(Mask((NtfsRights)item.Rights.Value, ace.Type));
            ace.Inheritance = (AceInheritance)item.Applies.Value;
            Apply();
        }

        private static NtfsRights Mask(NtfsRights rights, AceType type) =>
            type == AceType.Deny && Ntfs.SimpleOf(rights) != null ? rights & ~NtfsRights.Synchronize : rights;

        private void RemoveAce(AceItem item)
        {
            if (_item == null || item.Source == null) return;
            _item.Node.Aces.Remove(item.Source);
            Apply();
        }

        [RelayCommand]
        private void AddAce()
        {
            if (_item == null || NewTrustee == null || NewRights == null || NewApplies == null) return;
            var type = NewIsDeny ? AceType.Deny : AceType.Allow;
            _item.Node.Aces.Add(new NtfsAce
            {
                Type = type, Trustee = (string)NewTrustee.Value,
                Rights = Ntfs.Format(Mask((NtfsRights)NewRights.Value, type)),
                Inheritance = (AceInheritance)NewApplies.Value
            });
            Apply();
        }

        /// <summary>Désactiver l'héritage en copiant les ACE héritées en ACE explicites (icacls /inheritance:d).</summary>
        [RelayCommand]
        private void DisableInheritanceCopy()
        {
            if (_host == null || _item == null || !_item.Node.InheritanceEnabled) return;
            var inherited = NtfsInheritance.Dacl(_host, _item.Path).Where(a => a.Inherited).ToList();
            _item.Node.InheritanceEnabled = false;
            foreach (var a in inherited)
                _item.Node.Aces.Add(new NtfsAce { Type = a.Type, Trustee = a.Trustee, Rights = Ntfs.Format(a.Mask), Inheritance = a.Inheritance });
            Apply();
        }

        /// <summary>Désactiver l'héritage en retirant les ACE héritées (icacls /inheritance:r).</summary>
        [RelayCommand]
        private void DisableInheritanceRemove()
        {
            if (_item == null) return;
            _item.Node.InheritanceEnabled = false;
            Apply();
        }

        /// <summary>Réactiver l'héritage (icacls /inheritance:e) : les ACE du parent reviennent après les explicites.</summary>
        [RelayCommand]
        private void EnableInheritance()
        {
            if (_item == null) return;
            _item.Node.InheritanceEnabled = true;
            Apply();
        }

        private void Apply()
        {
            if (_item != null)
            {
                _loading = true;
                InheritanceEnabled = _item.Node.InheritanceEnabled;
                _loading = false;
                _item.Refresh();
            }
            Describe();
            Changed?.Invoke();
        }
    }
}
