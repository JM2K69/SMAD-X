using System;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using SMADX.Models;
using SMADX.Services;
using SMADX.Services.Tiering;

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
            StatusMessage = Loc["Status.RelationsUpdated"];
        }

        // ─── Graphe et analyses ───────────────────────────────────────────────────

        [RelayCommand]
        private void ShowGraphView()
        {
            if (RootObject == null) return;
            _dialogs.ShowGraph(new GraphViewModel(() => RootObject));
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

        /// <summary>
        /// Applique un modèle de cloisonnement (Microsoft, HardenAD) au domaine affiché : ses OU, groupes, comptes, GPO et
        /// délégations s'ajoutent, rien n'est déplacé. L'arbre est reconstruit, l'application s'annule d'un seul coup.
        /// </summary>
        [RelayCommand(CanExecute = nameof(IsFreeMode))]
        private async Task ApplyTierModel()
        {
            if (RootObject == null) return;
            var model = TierModelCatalog.Find(await _dialogs.ShowTierModelAsync(RootObject.TierModel));
            if (model == null) return;

            var report = TierModelService.Apply(RootObject, model);
            Restore(SerializeDocument());
            RecordHistory();
            StatusMessage = report.Total == 0
                ? string.Format(Loc["Status.TierModelUpToDate"], model.Name)
                : string.Format(Loc["Status.TierModelApplied"], model.Name, report.OrganizationalUnits, report.Groups, report.Accounts,
                    report.GroupPolicies, report.Delegations);
        }

        /// <summary>
        /// Analyse > Simuler un cloisonnement : migration d'une copie du domaine vers chaque modèle, bilan avant / après.
        /// Le domaine affiché ne change que si l'on choisit de le migrer.
        /// </summary>
        [RelayCommand(CanExecute = nameof(IsFreeMode))]
        private void SimulateTiering()
        {
            if (RootObject == null) return;
            RecordHistory();
            _dialogs.ShowTierImpact(new TierImpactViewModel(() => RootObject, MigrateToTierModel));
        }

        /// <summary>
        /// Migre le domaine affiché vers un modèle, après confirmation : application du modèle, rangement des objets et
        /// adhésions du plan. L'arbre est reconstruit, la migration s'annule d'un seul coup.
        /// </summary>
        public async Task<bool> MigrateToTierModel(TierModelDefinition model)
        {
            if (RootObject == null || !IsFreeMode) return false;
            if (!await _dialogs.ConfirmAsync(string.Format(Loc["Dialog.TierImpact.ConfirmTitle"], model.Name),
                    string.Format(Loc["Dialog.TierImpact.Confirm"], model.Name), Loc["Dialog.TierImpact.Migrate"], Loc["Dialog.TierImpact.Cancel"]))
                return false;

            var result = TierMigrationService.Migrate(RootObject, model);
            Restore(SerializeDocument());
            RecordHistory();
            StatusMessage = string.Format(Loc["Status.TierMigrated"], model.Name, result.Moves, result.Memberships, result.Issues.Count);
            return true;
        }

        /// <summary>
        /// Configuration > Silos d'authentification : les silos du domaine, leur mode et leurs membres. Valider les pose sur
        /// le domaine, en une étape annulable.
        /// </summary>
        [RelayCommand]
        private async Task ShowAuthenticationSilos()
        {
            if (RootObject == null) return;
            var editor = new AuthenticationSilosViewModel(RootObject.AuthenticationSilos);
            if (!await _dialogs.ShowAuthenticationSilosAsync(editor) || editor.Error != null) return;
            var silos = editor.Result();
            RootObject.AuthenticationSilos = silos.Count > 0 ? new(silos) : null;
            RecordHistory();
            StatusMessage = string.Format(Loc["Status.SilosSaved"], silos.Count, silos.Count(s => s.Enforced));
        }

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
