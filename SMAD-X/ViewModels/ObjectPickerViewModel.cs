using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using SMADX.Models;

namespace SMADX.ViewModels
{
    /// <summary>Objet de l'annuaire proposé au choix, avec son emplacement.</summary>
    public sealed partial class PickerEntry : ObservableObject
    {
        public PickerEntry(ADObject obj)
        {
            Object = obj;
            Location = string.Join("/", Ancestors(obj).Reverse().Select(a => a.Name));
        }

        public ADObject Object { get; }
        public string Name => Object.Name;
        public string Icon => Object.Icon;
        public string Location { get; }

        [ObservableProperty] private bool _isSelected;

        private static IEnumerable<ADObject> Ancestors(ADObject o)
        {
            for (var p = o.Parent; p != null && p.Type != ADObjectType.Domain; p = p.Parent) yield return p;
        }
    }

    /// <summary>Type d'objet proposé au choix (utilisateurs, groupes…), que l'on affiche ou masque d'une case.</summary>
    public sealed partial class PickerKind : ObservableObject
    {
        private readonly Func<ADObject, bool> _matches;

        public PickerKind(string label, Func<ADObject, bool> matches)
        {
            Label = label;
            _matches = matches;
        }

        public string Label { get; }

        [ObservableProperty] private bool _isChecked = true;

        public bool Matches(ADObject o) => _matches(o);
    }

    /// <summary>
    /// Choix d'objets dans l'annuaire (comme « Sélectionner des utilisateurs, des ordinateurs ou des groupes ») : les types
    /// d'objets proposés, une recherche sur le nom ou l'emplacement, une case par objet.
    /// </summary>
    public sealed partial class ObjectPickerViewModel : ViewModelBase
    {
        private readonly IReadOnlyList<PickerEntry> _entries;

        /// <param name="kinds">Types d'objets proposés : un objet n'est proposé que si l'un d'eux le reconnaît.</param>
        public ObjectPickerViewModel(IEnumerable<ADObject> candidates, string title, IReadOnlyList<PickerKind> kinds)
        {
            Title = title;
            Kinds = kinds;
            _entries = candidates.Where(o => kinds.Any(k => k.Matches(o)))
                .OrderBy(o => o.Name, StringComparer.CurrentCultureIgnoreCase).Select(o => new PickerEntry(o)).ToList();
            foreach (var kind in kinds)
                kind.PropertyChanged += (_, _) => OnPropertyChanged(nameof(Entries));
        }

        public string Title { get; }

        public IReadOnlyList<PickerKind> Kinds { get; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Entries))]
        private string _filter = string.Empty;

        /// <summary>
        /// Les objets d'un type coché dont le nom ou l'emplacement contient la recherche ; les cochés restent cochés quand
        /// l'une ou l'autre change.
        /// </summary>
        public IEnumerable<PickerEntry> Entries => _entries.Where(e => Kinds.Any(k => k.IsChecked && k.Matches(e.Object))
            && (string.IsNullOrWhiteSpace(Filter)
                || e.Name.Contains(Filter.Trim(), StringComparison.CurrentCultureIgnoreCase)
                || e.Location.Contains(Filter.Trim(), StringComparison.CurrentCultureIgnoreCase)));

        public IReadOnlyList<string> SelectedNames => _entries.Where(e => e.IsSelected).Select(e => e.Name).ToList();
    }
}
