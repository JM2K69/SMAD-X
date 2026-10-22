using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using SMADX.Services;

namespace SMADX.ViewModels
{
    // Fenêtres secondaires (relations, graphe, analyses, paramètres, aide), thème et langue
    public partial class MainWindowViewModel
    {
        // ─── Relations ────────────────────────────────────────────────────────────

        [RelayCommand]
        private Task ManageRelations() => OpenRelationsAsync(null);

        [RelayCommand]
        private Task AddToGroup() => OpenRelationsAsync(vm => vm.PreselectSource(SelectedNode!.Data));

        [RelayCommand]
        private Task AddMember() => OpenRelationsAsync(vm => vm.PreselectTarget(SelectedNode!.Data));

        /// <summary>Pré-sélectionne le groupe courant comme source dans l'onglet Group-in-Group.</summary>
        [RelayCommand]
        private Task AddGroupToGroup() => OpenRelationsAsync(vm => vm.PreselectNestingSource(SelectedNode!.Data));

        /// <summary>
        /// Ouvre la fenêtre des relations, éventuellement pré-remplie à partir de l'objet sélectionné
        /// (qui est alors requis), puis rafraîchit le panneau de détails.
        /// </summary>
        private async Task OpenRelationsAsync(Action<RelationsViewModel>? preselect)
        {
            if (RootObject == null || (preselect != null && SelectedNode == null)) return;

            var vm = new RelationsViewModel(RootObject, SitesTopology);
            preselect?.Invoke(vm);
            await _dialogs.ShowRelationsAsync(vm);

            OnPropertyChanged(nameof(SelectedMemberOf));
            OnPropertyChanged(nameof(SelectedLinkedGPOs));
            OnPropertyChanged(nameof(SelectedPSOAppliesTo));
            RecordHistory();
            StatusMessage = "Relations mises à jour.";
        }

        // ─── Graphe et analyses ───────────────────────────────────────────────────

        [RelayCommand]
        private void ShowGraphView()
        {
            if (RootObject == null) return;
            _dialogs.ShowGraph(new GraphViewModel(RootObject));
        }

        [RelayCommand]
        private Task ShowTimeline() => _dialogs.ShowTimelineAsync();

        [RelayCommand]
        private async Task ShowDelegations()
        {
            await _dialogs.ShowDelegationsAsync(RootObject);
            RecordHistory(); // délégations ajoutées ou retirées dans la fenêtre
        }

        [RelayCommand]
        private Task ShowSites() => _dialogs.ShowSitesAsync(SitesTopology);

        // ─── Paramètres et aide ───────────────────────────────────────────────────

        [RelayCommand]
        private Task ShowTierConfiguration() => _dialogs.ShowTierConfigurationAsync();

        [RelayCommand]
        private Task ShowAbout() => _dialogs.ShowAboutAsync();

        [RelayCommand]
        private Task ShowShortcuts() => _dialogs.ShowShortcutsAsync();

        // ─── Thème et langue ──────────────────────────────────────────────────────

        /// <summary>Paramètre XAML : "Light" ou "Dark".</summary>
        [RelayCommand]
        private void SetTheme(string theme) =>
            ThemeService.Instance.ApplyTheme(Enum.Parse<AppTheme>(theme));

        /// <summary>Paramètre XAML : nom de culture, ex. "fr-FR".</summary>
        [RelayCommand]
        private void SetLanguage(string cultureName) =>
            LocalizationService.Instance.SetLanguage(cultureName);
    }
}
