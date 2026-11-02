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

    /// <summary>
    /// Choix d'objets dans l'annuaire (comme « Sélectionner des utilisateurs, des ordinateurs ou des groupes ») : une
    /// recherche sur le nom ou l'emplacement, une case par objet.
    /// </summary>
    public sealed partial class ObjectPickerViewModel : ViewModelBase
    {
        private readonly IReadOnlyList<PickerEntry> _entries;

        public ObjectPickerViewModel(IEnumerable<ADObject> candidates, string title)
        {
            Title = title;
            _entries = candidates.OrderBy(o => o.Name, StringComparer.CurrentCultureIgnoreCase).Select(o => new PickerEntry(o)).ToList();
        }

        public string Title { get; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Entries))]
        private string _filter = string.Empty;

        /// <summary>Les objets dont le nom ou l'emplacement contient la recherche ; les cochés restent cochés quand elle change.</summary>
        public IEnumerable<PickerEntry> Entries => string.IsNullOrWhiteSpace(Filter)
            ? _entries
            : _entries.Where(e => e.Name.Contains(Filter.Trim(), StringComparison.CurrentCultureIgnoreCase)
                                  || e.Location.Contains(Filter.Trim(), StringComparison.CurrentCultureIgnoreCase));

        public IReadOnlyList<string> SelectedNames => _entries.Where(e => e.IsSelected).Select(e => e.Name).ToList();
    }
}
