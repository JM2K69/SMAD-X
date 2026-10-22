using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using SMADX.Models;
using SMADX.Services;
using SMADX.Services.Labs;

namespace SMADX.ViewModels
{
    /// <summary>Exercice proposé dans le menu « Exercices ».</summary>
    public sealed record LabMenuItem(LabDefinition Lab, string Title);

    // Exercices : ouverture (fournis ou fichier), recommencer, création à partir du domaine actuel
    public partial class MainWindowViewModel
    {
        private readonly LabService _labService = new();
        private LabDefinition[]? _allBuiltInLabs;

        private static FileFilter LabFilter => new(Loc["FileType.Lab"], "*" + LabService.FileExtension);

        /// <summary>Exercices généraux fournis avec l'application.</summary>
        public ObservableCollection<LabMenuItem> BuiltInLabs => LabMenu(category: null);

        /// <summary>Exercices de la série « Chemins d'attaque ».</summary>
        public ObservableCollection<LabMenuItem> AttackPathLabs => LabMenu(AttackPathCategory);

        public const string AttackPathCategory = "attack-paths";

        /// <summary>
        /// Exercices d'une série, dans la langue de l'interface : un exercice écrit dans une seule langue
        /// (son domaine compris) n'est proposé que dans celle-ci.
        /// </summary>
        private ObservableCollection<LabMenuItem> LabMenu(string? category)
        {
            var culture = Loc.CurrentCulture.Name;
            var items = new ObservableCollection<LabMenuItem>();
            foreach (var lab in LoadBuiltInLabs().Where(l => l.Category == category && (l.Language is null || string.Equals(l.Language, culture, StringComparison.OrdinalIgnoreCase))))
                items.Add(new LabMenuItem(lab, $"{Loc[$"Lab.Level.{lab.Level}"]} — {lab.Title.Resolve(culture)}"));
            return items;
        }

        /// <summary>Lus une fois : les exercices sont embarqués dans l'exe.</summary>
        private LabDefinition[] LoadBuiltInLabs()
        {
            if (_allBuiltInLabs != null) return _allBuiltInLabs;
            try
            {
                _allBuiltInLabs = _labService.BuiltInLabs().ToArray();
            }
            catch (LabFormatException ex)
            {
                ErrorReporter.Log("Exercices", ex);
                _allBuiltInLabs = Array.Empty<LabDefinition>();
            }
            return _allBuiltInLabs;
        }

        /// <summary>Titres et liste des exercices dans la nouvelle langue.</summary>
        private void RefreshLabMenus()
        {
            OnPropertyChanged(nameof(BuiltInLabs));
            OnPropertyChanged(nameof(AttackPathLabs));
        }

        /// <summary>
        /// Paramètre object : le menu évalue CanExecute avec le DataContext de l'entrée, qui n'est pas
        /// toujours un LabMenuItem (un RelayCommand&lt;LabMenuItem&gt; lèverait alors une exception).
        /// </summary>
        [RelayCommand]
        private void OpenBuiltInLab(object? parameter)
        {
            if (parameter is LabMenuItem item)
                StartLab(item.Lab);
        }

        [RelayCommand]
        private Task OpenLabFile() => RunFileCommandAsync(async () =>
        {
            var path = await _dialogs.PickOpenFileAsync(Loc["Lab.Open.Title"], LabFilter, AllFilesFilter);
            if (path == null) return;

            try
            {
                StartLab(_labService.LoadFile(path));
            }
            catch (LabFormatException ex)
            {
                StatusMessage = ex.Message;
            }
        });

        /// <summary>Squelette d'exercice : le domaine actuel devient le départ, les constats d'audit deviennent les objectifs.</summary>
        [RelayCommand]
        private Task CreateLabFromDomain() => RunFileCommandAsync(async () =>
        {
            if (RootObject == null) return;
            var path = await _dialogs.PickSaveFileAsync(Loc["Lab.Create.Title"], "nouvel-exercice" + LabService.FileExtension,
                "smadx-lab.json", LabFilter);
            if (path == null) return;

            var lab = _labService.CreateFromDocument(new ADRootDocument
            {
                Version = ADRootDocument.CurrentVersion,
                Domain = RootObject,
                SitesTopology = SitesTopology,
                // Les tiers personnalisés de l'enseignant font partie de l'exercice
                TierConfigurations = TierConfigurationService.Instance.Tiers
                    .Select(t => new TierConfiguration(t.Name, t.Color, t.Description, t.Level)).ToList()
            }, DateTime.Now);
            _labService.SaveFile(lab, path);
            StatusMessage = string.Format(Loc["Lab.Created"], path, lab.Objectives.Count);
        });

        /// <summary>Charge le domaine de départ dans la fenêtre principale et ouvre la fenêtre de l'exercice.</summary>
        private void StartLab(LabDefinition lab)
        {
            var reference = _labService.PrepareStart(lab, DateTime.Now);
            LoadLabStart(lab);
            _dialogs.ShowLab(new LabViewModel(lab, reference.Domain, () => RootObject, () => LoadLabStart(lab), _dialogs));
        }

        private void LoadLabStart(LabDefinition lab)
        {
            var working = _labService.PrepareStart(lab, DateTime.Now);
            _dataService.ApplyTierConfiguration(working);
            ShowDomain(working, expandDefaultNodes: true);
            StatusMessage = string.Format(Loc["Lab.Started"], lab.Title.Resolve(Loc.CurrentCulture.Name));
        }
    }
}
