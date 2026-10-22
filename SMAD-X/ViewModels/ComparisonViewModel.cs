using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SMADX.Models;
using SMADX.Services;
using SMADX.Services.Audit;

namespace SMADX.ViewModels
{
    /// <summary>Constat d'audit corrigé ou apparu entre « avant » et « après ».</summary>
    public sealed record ComparedFinding(string RuleId, string RuleTitle, string SeverityLabel, string SeverityColor, string ObjectName, string Detail);

    /// <summary>Changement de configuration entre « avant » et « après ».</summary>
    public sealed record ComparedChange(string Icon, string ObjectName, string Category, string Summary);

    /// <summary>
    /// Fenêtre « Avant / après » : compare le point de référence (état à l'ouverture, ou posé à la main)
    /// à l'état actuel — changements de configuration, score d'audit, constats corrigés et apparus.
    /// </summary>
    public sealed partial class ComparisonViewModel : ViewModelBase
    {
        private readonly Func<string?> _getBaseline;
        private readonly Func<ADObject?> _getCurrent;
        private readonly ADSecurityAuditService _audit = new();

        private static LocalizationService Loc => LocalizationService.Instance;

        /// <param name="getBaseline">JSON du document au point de référence (relu à chaque actualisation).</param>
        /// <param name="getCurrent">Domaine dans son état actuel.</param>
        public ComparisonViewModel(Func<string?> getBaseline, Func<ADObject?> getCurrent)
        {
            _getBaseline = getBaseline;
            _getCurrent = getCurrent;
            Refresh();
        }

        public ObservableCollection<ComparedFinding> FixedFindings { get; } = new();
        public ObservableCollection<ComparedFinding> NewFindings { get; } = new();
        public ObservableCollection<ComparedChange> Changes { get; } = new();

        [ObservableProperty] private int _scoreBefore;
        [ObservableProperty] private int _scoreAfter;
        [ObservableProperty] private int _levelBefore;
        [ObservableProperty] private int _levelAfter;
        [ObservableProperty] private string _summary = string.Empty;

        public string ScoreTrendColor => ScoreAfter > ScoreBefore ? "#27AE60" : ScoreAfter < ScoreBefore ? "#C0392B" : "#7F8C8D";

        [RelayCommand]
        private void Refresh()
        {
            var current = _getCurrent();
            if (_getBaseline() is not { } json || current == null) return;

            var baseline = JsonSerializer.Deserialize<ADRootDocument>(json, ADDataService.SerializerOptions)!.Domain;
            ADDataService.RestoreParentReferences(baseline, null);

            var now = DateTime.Now;
            var before = _audit.Run(baseline, now);
            var after = _audit.Run(current, now);
            ScoreBefore = before.Score;
            ScoreAfter = after.Score;
            LevelBefore = before.EstimatedAnssiLevel;
            LevelAfter = after.EstimatedAnssiLevel;
            OnPropertyChanged(nameof(ScoreTrendColor));

            // Un constat est identifié par sa règle et l'objet concerné (DN : le nom peut avoir changé de place)
            static string Key(AuditFinding f) => $"{f.Rule.Id}|{f.Object.DistinguishedName}".ToLowerInvariant();
            var afterKeys = after.Findings.Select(Key).ToHashSet();
            var beforeKeys = before.Findings.Select(Key).ToHashSet();
            Fill(FixedFindings, before.Findings.Where(f => !afterKeys.Contains(Key(f))));
            Fill(NewFindings, after.Findings.Where(f => !beforeKeys.Contains(Key(f))));

            Changes.Clear();
            foreach (var change in ADDiffService.Compare(new DomainSnapshot { Root = baseline }, new DomainSnapshot { Root = current }))
                Changes.Add(new ComparedChange(
                    change.ChangeType switch { ChangeType.Added => "➕", ChangeType.Removed => "➖", _ => "✏" },
                    change.ObjectName,
                    change.ChangeCategory.ToString(),
                    change.ChangeType == ChangeType.Modified ? change.ChangedFieldsSummary : change.DistinguishedName));

            Summary = string.Format(Loc["Comparison.Summary"], Changes.Count, FixedFindings.Count, NewFindings.Count);
        }

        private static void Fill(ObservableCollection<ComparedFinding> target, System.Collections.Generic.IEnumerable<AuditFinding> findings)
        {
            target.Clear();
            foreach (var f in findings)
                target.Add(new ComparedFinding(f.Rule.Id, f.Rule.Title, AuditViewModel.SeverityLabel(f.Severity),
                    AuditViewModel.SeverityColor(f.Severity), f.Object.Name, f.Detail));
        }
    }
}
