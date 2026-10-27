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
    /// <summary>Un constat tel qu'affiché dans la fenêtre d'audit.</summary>
    public sealed class AuditFindingItem
    {
        private readonly bool _hideFixGuidance;

        public AuditFindingItem(AuditFinding finding, bool hideFixGuidance = false)
        {
            Finding = finding;
            _hideFixGuidance = hideFixGuidance;
        }

        public AuditFinding Finding { get; }
        public AuditSeverity Severity => Finding.Severity;
        public string SeverityLabel => AuditViewModel.SeverityLabel(Severity);
        public string SeverityColor => AuditViewModel.SeverityColor(Severity);
        public string RuleId => Finding.Rule.Id;
        public string RuleTitle => Finding.Rule.Title;
        public string ObjectName => Finding.Object.Name;
        public string ObjectDistinguishedName => Finding.Object.DistinguishedName;
        public string Detail => Finding.Detail;
        public string Documentation => _hideFixGuidance ? WithoutFixGuidance(Finding.Rule.Documentation) : Finding.Rule.Documentation;

        /// <summary>La fiche sans sa section « Comment corriger » (examen) : le reste explique le constat sans donner la réponse.</summary>
        public static string WithoutFixGuidance(string markdown)
        {
            var lines = markdown.ReplaceLineEndings("\n").Split('\n');
            var kept = new List<string>();
            var skipping = false;
            foreach (var line in lines)
            {
                if (line.StartsWith("## ", StringComparison.Ordinal))
                    skipping = line.StartsWith("## Comment corriger", StringComparison.Ordinal) || line.StartsWith("## How to fix", StringComparison.Ordinal);
                if (!skipping) kept.Add(line);
            }
            return string.Join("\n", kept);
        }
        public IReadOnlyList<AuditReference> References => Finding.Rule.References;
        public string AnssiLevelLabel => Finding.Rule.AnssiLevel is { } level
            ? string.Format(LocalizationService.Instance["Audit.AnssiLevel.Item"], level)
            : string.Empty;
    }

    /// <summary>Choix du filtre de gravité (null = toutes).</summary>
    public sealed record SeverityFilterOption(AuditSeverity? Severity, string Label)
    {
        public override string ToString() => Label;
    }

    /// <summary>
    /// Fenêtre « Audit de sécurité » : lance l'audit sur le domaine courant, affiche le score, le niveau
    /// ANSSI estimé et les constats filtrables, et permet d'afficher l'objet concerné dans l'arbre.
    /// </summary>
    public sealed partial class AuditViewModel : ViewModelBase
    {
        private readonly Func<ADObject?> _getDomain;
        private readonly Action<ADObject> _showInTree;
        private readonly Action<AuditReport> _onReport;
        private readonly ADSecurityAuditService _audit = new();
        private readonly bool _hideFixGuidance;

        [ObservableProperty]
        private AuditReport? _report;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasSelection))]
        private AuditFindingItem? _selectedFinding;

        [ObservableProperty]
        private SeverityFilterOption _selectedFilter;

        public ObservableCollection<AuditFindingItem> Findings { get; } = new();

        public IReadOnlyList<SeverityFilterOption> FilterOptions { get; }

        /// <param name="getDomain">Domaine courant, relu à chaque audit (il change si l'utilisateur ouvre un autre fichier).</param>
        /// <param name="showInTree">Sélectionne un objet dans l'arbre de la fenêtre principale.</param>
        /// <param name="onReport">Appelé après chaque audit (badges de l'arbre).</param>
        /// <param name="hideFixGuidance">Examen : les fiches n'ont pas leur section « Comment corriger ».</param>
        public AuditViewModel(Func<ADObject?> getDomain, Action<ADObject> showInTree, Action<AuditReport> onReport, bool hideFixGuidance = false)
        {
            _hideFixGuidance = hideFixGuidance;
            _getDomain = getDomain;
            _showInTree = showInTree;
            _onReport = onReport;
            FilterOptions = new[] { new SeverityFilterOption(null, LocalizationService.Instance["Audit.Filter.All"]) }
                .Concat(Enum.GetValues<AuditSeverity>().Select(s => new SeverityFilterOption(s, SeverityLabel(s))))
                .ToList();
            _selectedFilter = FilterOptions[0];
            Run();
        }

        public bool HasSelection => SelectedFinding != null;
        public bool HasFindings => Report is { Findings.Count: > 0 };
        public int Score => Report?.Score ?? 100;
        public int EstimatedAnssiLevel => Report?.EstimatedAnssiLevel ?? 5;

        /// <summary>Couleur du score : vert, orange ou rouge.</summary>
        public string ScoreColor => Score switch { >= 80 => "#27AE60", >= 50 => "#E67E22", _ => "#C0392B" };

        public string Summary => Report is null
            ? string.Empty
            : Report.Findings.Count == 0
                ? LocalizationService.Instance["Audit.NoFinding"]
                : string.Format(LocalizationService.Instance["Audit.Summary"],
                    Report.Findings.Count, Report.FailedRules.Count(), Report.Rules.Count, Report.ReferenceDate);

        public int CriticalCount => Report?.Count(AuditSeverity.Critical) ?? 0;
        public int HighCount => Report?.Count(AuditSeverity.High) ?? 0;
        public int MediumCount => Report?.Count(AuditSeverity.Medium) ?? 0;
        public int LowCount => Report?.Count(AuditSeverity.Low) ?? 0;

        [RelayCommand]
        private void Run()
        {
            var domain = _getDomain();
            if (domain == null) return;

            Report = _audit.Run(domain);
            _onReport(Report);
            OnPropertyChanged(string.Empty); // score, compteurs, résumé
            ApplyFilter();
        }

        partial void OnSelectedFilterChanged(SeverityFilterOption value) => ApplyFilter();

        private void ApplyFilter()
        {
            var previous = SelectedFinding?.Finding;
            Findings.Clear();
            foreach (var finding in Report?.Findings ?? Array.Empty<AuditFinding>())
                if (SelectedFilter.Severity is null || finding.Severity == SelectedFilter.Severity)
                    Findings.Add(new AuditFindingItem(finding, _hideFixGuidance));

            SelectedFinding = Findings.FirstOrDefault(f => previous != null && f.Finding.Rule == previous.Rule && f.Finding.Object == previous.Object)
                              ?? Findings.FirstOrDefault();
        }

        [RelayCommand]
        private void ShowInTree()
        {
            if (SelectedFinding != null)
                _showInTree(SelectedFinding.Finding.Object);
        }

        public static string SeverityLabel(AuditSeverity severity) => LocalizationService.Instance[$"Audit.Severity.{severity}"];

        public static string SeverityColor(AuditSeverity severity) => severity switch
        {
            AuditSeverity.Critical => "#C0392B",
            AuditSeverity.High     => "#E67E22",
            AuditSeverity.Medium   => "#D4AC0D",
            AuditSeverity.Low      => "#2E86C1",
            _                      => "#7F8C8D"
        };
    }
}
