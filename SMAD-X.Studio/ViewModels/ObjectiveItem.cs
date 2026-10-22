using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using SMADX.Models;
using SMADX.Services.Audit;
using SMADX.Services.Labs;

namespace SMADX.Studio.ViewModels
{
    /// <summary>Un objectif en cours d'édition : ses textes dans la langue éditée et les champs de sa vérification.</summary>
    public sealed partial class ObjectiveItem : ObservableObject
    {
        private readonly Func<string> _culture;

        public ObjectiveItem(LabObjective model, Func<string> culture)
        {
            Model = model;
            _culture = culture;
        }

        public LabObjective Model { get; }
        private LabCheck Check => Model.Check;

        public string Id => Model.Id;

        public string Title
        {
            get => Model.Title.GetValueOrDefault(_culture()) ?? "";
            set { Model.Title[_culture()] = value; OnPropertyChanged(); OnPropertyChanged(nameof(Summary)); }
        }

        public string Hint
        {
            get => Model.Hint.GetValueOrDefault(_culture()) ?? "";
            set { Model.Hint[_culture()] = value; OnPropertyChanged(); }
        }

        public int Points
        {
            get => Model.Points;
            set { Model.Points = Math.Max(0, value); OnPropertyChanged(); OnPropertyChanged(nameof(Summary)); }
        }

        public LabCheckType CheckType
        {
            get => Check.Type;
            set
            {
                Check.Type = value;
                OnPropertyChanged();
                Changed();
                foreach (var name in new[] { nameof(ShowObject), nameof(ShowObjectType), nameof(ShowGroup), nameof(ShowRule), nameof(ShowSeverity),
                                             nameof(ShowScore), nameof(ShowFlag), nameof(ShowTier), nameof(ShowTrustee), nameof(ShowTarget),
                                             nameof(ShowRight), nameof(ShowAnyUserHint), nameof(IsGuard) })
                    OnPropertyChanged(name);
            }
        }

        // Champs de la vérification ; une chaîne vide efface le champ
        public string Object { get => Check.Object ?? ""; set { Check.Object = Empty(value); Changed(); } }
        public ADObjectType? ObjectType { get => Check.ObjectType; set { Check.ObjectType = value; Changed(); } }
        public string Group { get => Check.Group ?? ""; set { Check.Group = Empty(value); Changed(); } }
        public bool Effective { get => Check.Effective; set { Check.Effective = value; Changed(); } }
        public string RuleId { get => Check.RuleId ?? ""; set { Check.RuleId = Empty(value); Changed(); } }
        public AuditSeverity? Severity { get => Check.Severity; set { Check.Severity = value; Changed(); } }
        public int Score { get => Check.Score ?? 100; set { Check.Score = value; Changed(); } }
        public ADAccountFlags? Flag { get => Check.Flag; set { Check.Flag = value; Changed(); } }
        public string Tier { get => Check.Tier ?? ""; set { Check.Tier = Empty(value); Changed(); } }
        public string Trustee { get => Check.Trustee ?? ""; set { Check.Trustee = Empty(value); Changed(); } }
        public string Target { get => Check.Target ?? ""; set { Check.Target = Empty(value); Changed(); } }
        public RightCategory? RightCategory { get => Check.RightCategory; set { Check.RightCategory = value; Changed(); } }

        public bool ShowObject => CheckType is LabCheckType.RuleClear or LabCheckType.NoAttackPath or LabCheckType.ObjectExists or LabCheckType.ObjectAbsent
            or LabCheckType.MemberOf or LabCheckType.NotMemberOf or LabCheckType.HasFlag or LabCheckType.NotHasFlag or LabCheckType.TierIs or LabCheckType.NotModified;
        public bool ShowObjectType => CheckType is LabCheckType.ObjectExists or LabCheckType.ObjectAbsent or LabCheckType.NotModified;
        public bool ShowGroup => CheckType is LabCheckType.MemberOf or LabCheckType.NotMemberOf;
        public bool ShowRule => CheckType == LabCheckType.RuleClear;
        public bool ShowSeverity => CheckType == LabCheckType.NoFindingAtOrAbove;
        public bool ShowScore => CheckType == LabCheckType.ScoreAtLeast;
        public bool ShowFlag => CheckType is LabCheckType.HasFlag or LabCheckType.NotHasFlag;
        public bool ShowTier => CheckType == LabCheckType.TierIs;
        public bool ShowTrustee => CheckType is LabCheckType.DelegationExists or LabCheckType.DelegationOnly;
        public bool ShowTarget => CheckType == LabCheckType.DelegationExists;
        public bool ShowRight => CheckType == LabCheckType.DelegationExists;
        public bool ShowAnyUserHint => CheckType == LabCheckType.NoAttackPath;
        public bool IsGuard => LabAuthoring.IsGuard(Check);

        /// <summary>Ligne de la liste : points, titre et vérification lisible.</summary>
        public string Summary => $"{Model.Id} · {Points} pt · {Title}";
        public string Description => LabAuthoring.Describe(Check, _culture());

        /// <summary>Après un changement de langue d'édition.</summary>
        public void Refresh()
        {
            OnPropertyChanged(nameof(Title));
            OnPropertyChanged(nameof(Hint));
            OnPropertyChanged(nameof(Summary));
            OnPropertyChanged(nameof(Description));
            OnPropertyChanged(nameof(Id));
        }

        private void Changed()
        {
            OnPropertyChanged(nameof(Description));
            OnPropertyChanged(nameof(IsGuard));
        }

        private static string? Empty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>Objectif proposé, à cocher pour l'ajouter.</summary>
    public sealed partial class SuggestionItem : ObservableObject
    {
        public SuggestionItem(LabObjectiveSuggestion suggestion, string culture)
        {
            Suggestion = suggestion;
            _isSelected = suggestion.Recommended;
            Title = suggestion.Objective.Title.Resolve(culture);
            Reason = suggestion.Reason.Resolve(culture);
            Detail = LabAuthoring.Describe(suggestion.Objective.Check, culture);
            IsGuard = LabAuthoring.IsGuard(suggestion.Objective.Check);
        }

        public LabObjectiveSuggestion Suggestion { get; }
        public string Title { get; }
        public string Reason { get; }
        public string Detail { get; }
        public bool IsGuard { get; }

        [ObservableProperty]
        private bool _isSelected;
    }

    /// <summary>Résultat de la validation, avec son pictogramme.</summary>
    public sealed record IssueItem(LabIssue Issue)
    {
        public string Icon => Issue.Level switch { LabIssueLevel.Error => "✗", LabIssueLevel.Warning => "⚠", _ => "✓" };
        public string Color => Issue.Level switch { LabIssueLevel.Error => "#E74C3C", LabIssueLevel.Warning => "#F39C12", _ => "#27AE60" };
        public string Text => Issue.ObjectiveId is { } id ? $"{id} — {Issue.Message}" : Issue.Message;
    }

    /// <summary>Choix d'une liste déroulante : valeur et libellé.</summary>
    public sealed record Choice<T>(T Value, string Label)
    {
        public override string ToString() => Label;
    }
}
