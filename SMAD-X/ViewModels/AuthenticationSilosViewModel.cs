using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SMADX.Models;
using SMADX.Services;

namespace SMADX.ViewModels
{
    /// <summary>Silo en cours d'édition ; les listes s'écrivent un nom par ligne.</summary>
    public sealed partial class SiloItem : ObservableObject
    {
        [ObservableProperty] private string _name = string.Empty;
        [ObservableProperty] private string _description = string.Empty;
        [ObservableProperty] private bool _enforced;
        [ObservableProperty] private string _membersText = string.Empty;
        [ObservableProperty] private string _allowedFromText = string.Empty;

        public static SiloItem From(ADAuthenticationSilo silo) => new()
        {
            Name = silo.Name, Description = silo.Description, Enforced = silo.Enforced,
            MembersText = string.Join(Environment.NewLine, silo.Members), AllowedFromText = string.Join(Environment.NewLine, silo.AllowedFrom)
        };

        public ADAuthenticationSilo ToSilo() => new()
        {
            Name = Name.Trim(), Description = Description.Trim(), Enforced = Enforced,
            Members = new ObservableCollection<string>(Lines(MembersText)), AllowedFrom = new ObservableCollection<string>(Lines(AllowedFromText))
        };

        private static IEnumerable<string> Lines(string text) =>
            text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Fenêtre « Silos d'authentification » : les silos du domaine, leur mode (audit ou appliqué), leurs membres et les
    /// ordinateurs autorisés en plus. Les modifications ne touchent le domaine qu'à la validation.
    /// </summary>
    public sealed partial class AuthenticationSilosViewModel : ViewModelBase
    {
        private static LocalizationService Loc => LocalizationService.Instance;

        public AuthenticationSilosViewModel(IEnumerable<ADAuthenticationSilo>? silos)
        {
            foreach (var silo in silos ?? []) Silos.Add(SiloItem.From(silo));
            Selected = Silos.FirstOrDefault();
        }

        public ObservableCollection<SiloItem> Silos { get; } = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasSelection))]
        [NotifyCanExecuteChangedFor(nameof(RemoveCommand))]
        private SiloItem? _selected;

        public bool HasSelection => Selected != null;

        [RelayCommand]
        private void Add()
        {
            var name = "Tier0";
            for (var i = 2; Silos.Any(s => string.Equals(s.Name.Trim(), name, StringComparison.OrdinalIgnoreCase)); i++)
                name = $"Silo{i}";
            var silo = new SiloItem { Name = name };
            Silos.Add(silo);
            Selected = silo;
        }

        [RelayCommand(CanExecute = nameof(HasSelection))]
        private void Remove()
        {
            if (Selected == null) return;
            var index = Silos.IndexOf(Selected);
            Silos.Remove(Selected);
            Selected = Silos.Count == 0 ? null : Silos[Math.Min(index, Silos.Count - 1)];
        }

        /// <summary>Ce qui empêche de valider : un silo sans nom, ou deux silos du même nom ; null si tout va bien.</summary>
        public string? Error =>
            Silos.Any(s => s.Name.Trim().Length == 0) ? Loc["Dialog.Silos.ErrorName"]
            : Silos.GroupBy(s => s.Name.Trim(), StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1) is { } twice
                ? string.Format(Loc["Dialog.Silos.ErrorDuplicate"], twice.Key)
                : null;

        /// <summary>Les silos tels qu'édités, à poser sur le domaine.</summary>
        public List<ADAuthenticationSilo> Result() => Silos.Select(s => s.ToSilo()).ToList();
    }
}
