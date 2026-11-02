using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SMADX.Models;
using SMADX.Services;
using SMADX.Services.AttackPaths;
using SMADX.Services.Audit;
using SMADX.Services.Tiering;

namespace SMADX.ViewModels
{
    /// <summary>Indicateur du bilan : sa valeur avant, et après la migration vers chacun des deux modèles.</summary>
    public sealed record TierImpactRow(string Label, string Before, string First, string Second);

    /// <summary>Étape du plan de migration, prête à afficher.</summary>
    public sealed record TierStepItem(string Icon, string ObjectName, string From, string To, string Reason);

    /// <summary>Chemin d'attaque : sa source et ses étapes.</summary>
    public sealed record TierPathItem(string Source, string Steps);

    /// <summary>Un modèle simulé, au choix du détail.</summary>
    public sealed record TierImpactItem(TierImpact Impact)
    {
        public string Name => TierModelCatalog.DisplayName(Impact.Model);
        public override string ToString() => Name;
    }

    /// <summary>
    /// Fenêtre « Simuler un cloisonnement » : migre une copie du domaine vers chaque modèle (Microsoft, HardenAD) et compare
    /// la sécurité avant et après — score, niveau ANSSI, chemins hors Tier 0, sessions refusées — avec le plan de migration
    /// et ce qu'il reste à revoir. Le domaine affiché n'est modifié que par « Migrer le domaine vers ce modèle ».
    /// </summary>
    public sealed partial class TierImpactViewModel : ViewModelBase
    {
        private readonly Func<ADObject?> _getDomain;
        private readonly Func<TierModelDefinition, Task<bool>> _migrate;

        private static LocalizationService Loc => LocalizationService.Instance;

        /// <param name="getDomain">Domaine affiché, relu à chaque simulation.</param>
        /// <param name="migrate">Migre le domaine affiché vers un modèle (après confirmation) ; false si rien n'a été fait.</param>
        public TierImpactViewModel(Func<ADObject?> getDomain, Func<TierModelDefinition, Task<bool>> migrate)
        {
            _getDomain = getDomain;
            _migrate = migrate;
            Loading = Refresh();
        }

        /// <summary>Simulation lancée à l'ouverture.</summary>
        public Task Loading { get; }

        public string FirstModel => TierModelCatalog.DisplayName(TierModelCatalog.All[0]);
        public string SecondModel => TierModelCatalog.DisplayName(TierModelCatalog.All[1]);

        public ObservableCollection<TierImpactRow> Rows { get; } = new();
        public ObservableCollection<TierImpactItem> Impacts { get; } = new();
        public ObservableCollection<TierStepItem> Steps { get; } = new();
        public ObservableCollection<string> Issues { get; } = new();
        public ObservableCollection<TierPathItem> CutPaths { get; } = new();
        public ObservableCollection<TierPathItem> RemainingPaths { get; } = new();
        public ObservableCollection<ComparedFinding> FixedFindings { get; } = new();
        public ObservableCollection<ComparedFinding> NewFindings { get; } = new();

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(MigrateCommand), nameof(RefreshCommand))]
        private bool _isBusy;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(MigrateCommand))]
        private TierImpactItem? _selectedImpact;

        [ObservableProperty] private string _detailSummary = string.Empty;

        private bool CanRefresh => !IsBusy;
        private bool CanMigrate => !IsBusy && SelectedImpact != null;

        [RelayCommand(CanExecute = nameof(CanRefresh))]
        private async Task Refresh()
        {
            if (_getDomain() is not { } domain) return;
            IsBusy = true;
            try
            {
                var previous = SelectedImpact?.Impact.Model.Id;
                // La copie se fait ici, sur le fil de l'interface ; la simulation tourne à côté
                var copy = TierImpactSimulator.Copy(domain);
                var now = DateTime.Now;
                var impacts = await Task.Run(() => TierImpactSimulator.Compare(copy, now));

                Impacts.Clear();
                foreach (var impact in impacts) Impacts.Add(new TierImpactItem(impact));
                FillRows(impacts);
                SelectedImpact = Impacts.FirstOrDefault(i => i.Impact.Model.Id == previous) ?? Impacts.FirstOrDefault();
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand(CanExecute = nameof(CanMigrate))]
        private async Task Migrate()
        {
            if (SelectedImpact == null || !await _migrate(SelectedImpact.Impact.Model)) return;
            await Refresh();
        }

        partial void OnSelectedImpactChanged(TierImpactItem? value)
        {
            Steps.Clear();
            Issues.Clear();
            CutPaths.Clear();
            RemainingPaths.Clear();
            FixedFindings.Clear();
            NewFindings.Clear();
            if (value == null) return;
            var impact = value.Impact;
            foreach (var s in impact.Migration.Steps)
                Steps.Add(new TierStepItem(s.Action == TierMigrationAction.Move ? "➜" : "➕", s.ObjectName, s.From, s.To, s.Reason));
            foreach (var i in impact.Migration.Issues)
                Issues.Add($"{i.ObjectName} : {i.Reason}");
            foreach (var p in impact.CutPaths) CutPaths.Add(Path(p));
            foreach (var p in impact.After.PathsOutsideTier0) RemainingPaths.Add(Path(p));
            Fill(FixedFindings, impact.FixedFindings);
            Fill(NewFindings, impact.NewFindings);
            DetailSummary = string.Format(Loc["TierImpact.DetailSummary"], impact.Migration.Moves, impact.Migration.Memberships,
                impact.Migration.Issues.Count, impact.CutPaths.Count, impact.After.PathsOutsideTier0.Count);
        }

        private void FillRows(IReadOnlyList<TierImpact> impacts)
        {
            Rows.Clear();
            if (impacts.Count < 2) return;
            var before = impacts[0].Before;
            static int Outside(TierSecurityState s) => s.Audit.Findings.Count(f => f.Rule.Id == "TIER-06");
            void Row(string key, Func<TierSecurityState, string> state) =>
                Rows.Add(new TierImpactRow(Loc[key], state(before), state(impacts[0].After), state(impacts[1].After)));
            void Plan(string key, Func<TierImpact, int> value) =>
                Rows.Add(new TierImpactRow(Loc[key], "—", value(impacts[0]).ToString(), value(impacts[1]).ToString()));

            Row("TierImpact.Score", s => $"{s.Audit.Score} / 100");
            Row("TierImpact.Level", s => $"{s.Audit.EstimatedAnssiLevel} / 5");
            Row("TierImpact.Paths", s => s.PathsOutsideTier0.Count.ToString());
            Row("TierImpact.Blocked", s => s.BlockedSessions.Count.ToString());
            Rows.Add(new TierImpactRow(Loc["TierImpact.Findings"], "—",
                $"{impacts[0].FixedFindings.Count} / {impacts[0].NewFindings.Count}", $"{impacts[1].FixedFindings.Count} / {impacts[1].NewFindings.Count}"));
            Plan("TierImpact.Moves", i => i.Migration.Moves);
            Plan("TierImpact.Memberships", i => i.Migration.Memberships);
            Plan("TierImpact.Issues", i => i.Migration.Issues.Count);
            // Sans modèle, TIER-06 ne dit rien : la colonne « avant » n'a pas de valeur
            Plan("TierImpact.Outside", i => Outside(i.After));
        }

        private static TierPathItem Path(AttackPath path) =>
            new(path.Start.Label, string.Join(" → ", path.Steps.Select(s => $"{Loc[$"AttackPath.Kind.{s.Kind}"]} {s.To.Label}")));

        private static void Fill(ObservableCollection<ComparedFinding> target, IEnumerable<AuditFinding> findings)
        {
            foreach (var f in findings)
                target.Add(new ComparedFinding(f.Rule.Id, f.Rule.Title, AuditViewModel.SeverityLabel(f.Severity),
                    AuditViewModel.SeverityColor(f.Severity), f.Object.Name, f.Detail));
        }
    }
}
