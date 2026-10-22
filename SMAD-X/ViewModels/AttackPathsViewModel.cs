using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SMADX.Models;
using SMADX.Services;
using SMADX.Services.Audit;
using SMADX.Services.AttackPaths;

namespace SMADX.ViewModels
{
    /// <summary>Une source qui peut atteindre le contrôle du domaine.</summary>
    public sealed class AttackPathItem
    {
        public AttackPathItem(AttackPath path) => Path = path;

        public AttackPath Path { get; }
        public string Label => Path.Start.Label;
        public string Icon => Path.Start.Object?.Icon ?? "🌍";
        public string Tier => Path.Start.Object?.Tier ?? string.Empty;
        public string StepCountLabel => string.Format(LocalizationService.Instance["AttackPath.StepCount"], Path.Length);
        public bool IsTier0 => AttackPathService.IsTier0(Path.Start);
    }

    /// <summary>Une étape du chemin sélectionné.</summary>
    public sealed record AttackStepItem(int Number, string KindLabel, string From, string To, string Detail, ADObject? Target);

    /// <summary>
    /// Fenêtre « Chemins d'attaque » : sources qui peuvent prendre le contrôle du domaine, étapes du
    /// chemin le plus court, affichage dans l'arbre ou surligné dans le graphe des relations.
    /// </summary>
    public sealed partial class AttackPathsViewModel : ViewModelBase
    {
        private readonly Func<ADObject?> _getDomain;
        private readonly Action<ADObject> _showInTree;
        private readonly IDialogService _dialogs;
        private AttackPathItem[] _all = Array.Empty<AttackPathItem>();

        private static LocalizationService Loc => LocalizationService.Instance;

        public AttackPathsViewModel(Func<ADObject?> getDomain, Action<ADObject> showInTree, IDialogService dialogs)
        {
            _getDomain = getDomain;
            _showInTree = showInTree;
            _dialogs = dialogs;
            Refresh();
        }

        public ObservableCollection<AttackPathItem> Paths { get; } = new();
        public ObservableCollection<AttackStepItem> Steps { get; } = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasSelection))]
        [NotifyCanExecuteChangedFor(nameof(ShowInTreeCommand), nameof(ShowInGraphCommand))]
        private AttackPathItem? _selectedPath;

        [ObservableProperty]
        private AttackStepItem? _selectedStep;

        /// <summary>Les comptes de Tier 0 contrôlent le domaine par définition : masqués par défaut.</summary>
        [ObservableProperty]
        private bool _showTier0;

        [ObservableProperty]
        private string _summary = string.Empty;

        public bool HasSelection => SelectedPath != null;
        public bool HasNoPath => Paths.Count == 0;

        partial void OnShowTier0Changed(bool value) => ApplyFilter();

        partial void OnSelectedPathChanged(AttackPathItem? value)
        {
            Steps.Clear();
            if (value == null) return;
            var number = 1;
            foreach (var edge in value.Path.Steps)
                Steps.Add(new AttackStepItem(number++, Loc[$"AttackPath.Kind.{edge.Kind}"], edge.From.Label, edge.To.Label, edge.Detail, edge.To.Object));
        }

        [RelayCommand]
        private void Refresh()
        {
            var domain = _getDomain();
            if (domain == null) return;

            _all = new AttackPathService(domain).PathsToDomainControl()
                .Where(p => p.Start.Object != domain)
                .Select(p => new AttackPathItem(p))
                .ToArray();
            Summary = string.Format(Loc["AttackPath.Summary"], _all.Length, _all.Count(p => !p.IsTier0));
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            var previous = SelectedPath?.Path.Start.Id;
            Paths.Clear();
            foreach (var item in _all.Where(p => ShowTier0 || !p.IsTier0))
                Paths.Add(item);
            SelectedPath = Paths.FirstOrDefault(p => p.Path.Start.Id == previous) ?? Paths.FirstOrDefault();
            OnPropertyChanged(nameof(HasNoPath));
        }

        [RelayCommand(CanExecute = nameof(HasSelection))]
        private void ShowInTree()
        {
            var target = SelectedStep?.Target ?? SelectedPath?.Path.Start.Object;
            if (target != null)
                _showInTree(target);
        }

        [RelayCommand(CanExecute = nameof(HasSelection))]
        private void ShowInGraph()
        {
            var domain = _getDomain();
            if (domain == null || SelectedPath == null) return;

            var ids = new[] { SelectedPath.Path.Start }
                .Concat(SelectedPath.Path.Steps.Select(s => s.To))
                .Where(n => n.Object != null)
                .Select(n => n.Id)
                .ToList();
            _dialogs.ShowGraph(new GraphViewModel(domain, ids));
        }
    }
}
