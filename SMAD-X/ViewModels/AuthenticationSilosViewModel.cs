using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SMADX.Models;
using SMADX.Services;
using SMADX.Services.Audit;

namespace SMADX.ViewModels
{
    /// <summary>Silo en cours d'édition : ses identités et ses ordinateurs.</summary>
    public sealed partial class SiloItem : ObservableObject
    {
        [ObservableProperty] private string _name = string.Empty;
        [ObservableProperty] private string _description = string.Empty;
        [ObservableProperty] private bool _enforced;
        [ObservableProperty] private string? _selectedMember;
        [ObservableProperty] private string? _selectedComputer;

        public ObservableCollection<string> Members { get; } = new();
        public ObservableCollection<string> Computers { get; } = new();

        public static SiloItem From(ADAuthenticationSilo silo)
        {
            var item = new SiloItem { Name = silo.Name, Description = silo.Description, Enforced = silo.Enforced };
            foreach (var m in silo.Members) item.Members.Add(m);
            foreach (var c in silo.Computers) item.Computers.Add(c);
            return item;
        }

        public ADAuthenticationSilo ToSilo() => new()
        {
            Name = Name.Trim(), Description = Description.Trim(), Enforced = Enforced,
            Members = new ObservableCollection<string>(Members), Computers = new ObservableCollection<string>(Computers)
        };
    }

    /// <summary>
    /// Fenêtre « Silos d'authentification » : chaque silo associe des identités (utilisateurs, groupes, comptes de service)
    /// aux ordinateurs depuis lesquels elles s'authentifient, choisis dans l'annuaire. Les modifications ne touchent le
    /// domaine qu'à la validation.
    /// </summary>
    public sealed partial class AuthenticationSilosViewModel : ViewModelBase
    {
        private readonly AuditContext? _context;

        private static LocalizationService Loc => LocalizationService.Instance;

        /// <param name="domain">Domaine affiché : les objets à choisir, et l'appartenance à Protected Users.</param>
        public AuthenticationSilosViewModel(IEnumerable<ADAuthenticationSilo>? silos, ADObject? domain = null)
        {
            _context = domain != null ? new AuditContext(domain, DateTime.Now) : null;
            foreach (var silo in silos ?? []) Silos.Add(SiloItem.From(silo));
            Selected = Silos.FirstOrDefault();
        }

        public ObservableCollection<SiloItem> Silos { get; } = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasSelection), nameof(ProtectedUsersWarning), nameof(HasProtectedUsersWarning))]
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

        // ─── Identités et ordinateurs, choisis dans l'annuaire ────────────────────

        /// <summary>Utilisateurs, groupes et comptes de service qui ne sont pas encore dans le silo.</summary>
        public ObjectPickerViewModel PickMembers() =>
            new(Candidates(o => o.Type is ADObjectType.User or ADObjectType.Group or ADObjectType.GMSA, Selected?.Members), Loc["Dialog.Silos.PickMembers"]);

        /// <summary>Ordinateurs et groupes (d'ordinateurs) qui ne sont pas encore dans le silo.</summary>
        public ObjectPickerViewModel PickComputers() =>
            new(Candidates(o => o.Type is ADObjectType.Computer or ADObjectType.Group, Selected?.Computers), Loc["Dialog.Silos.PickComputers"]);

        public void AddMembers(IEnumerable<string> names) => AddTo(Selected?.Members, names);

        public void AddComputers(IEnumerable<string> names) => AddTo(Selected?.Computers, names);

        [RelayCommand]
        private void RemoveMember()
        {
            if (Selected?.SelectedMember is { } member) Selected.Members.Remove(member);
            Refresh();
        }

        [RelayCommand]
        private void RemoveComputer()
        {
            if (Selected?.SelectedComputer is { } computer) Selected.Computers.Remove(computer);
        }

        private IEnumerable<ADObject> Candidates(Func<ADObject, bool> kind, ICollection<string>? present) =>
            _context?.Objects.Where(o => kind(o) && !(present ?? []).Contains(o.Name, StringComparer.OrdinalIgnoreCase)) ?? [];

        private void AddTo(ObservableCollection<string>? list, IEnumerable<string> names)
        {
            if (list == null) return;
            foreach (var name in names.Where(n => !list.Contains(n, StringComparer.OrdinalIgnoreCase)))
                list.Add(name);
            Refresh();
        }

        private void Refresh()
        {
            OnPropertyChanged(nameof(ProtectedUsersWarning));
            OnPropertyChanged(nameof(HasProtectedUsersWarning));
        }

        /// <summary>
        /// Utilisateurs du silo (cités, ou membres d'un groupe cité) hors de Protected Users : ils peuvent encore s'authentifier
        /// par NTLM, que la stratégie du silo ne restreint pas.
        /// </summary>
        public string ProtectedUsersWarning
        {
            get
            {
                if (_context == null || Selected == null) return string.Empty;
                var protectedUsers = _context.Group("Protected Users");
                var exposed = _context.Objects
                    .Where(o => o.Type == ADObjectType.User && AuditContext.IsEnabled(o)
                                && Selected.Members.Any(m => string.Equals(m, o.Name, StringComparison.OrdinalIgnoreCase)
                                                             || _context.EffectiveGroups(o).Any(g => string.Equals(g.Name, m, StringComparison.OrdinalIgnoreCase)))
                                && !(protectedUsers != null && _context.EffectiveGroups(o).Contains(protectedUsers)))
                    .Select(o => o.Name).OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase).ToList();
                return exposed.Count == 0 ? string.Empty : string.Format(Loc["Dialog.Silos.NotProtected"], string.Join(", ", exposed));
            }
        }

        public bool HasProtectedUsersWarning => ProtectedUsersWarning.Length > 0;

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
