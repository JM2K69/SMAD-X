using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using SMADX.Services;
using SMADX.Services.Audit;
using SMADX.Services.Labs;

namespace SMADX.Studio.ViewModels
{
    /// <summary>Une recette de la liste : la règle, son titre dans la langue de l'interface, le point ANSSI ; cochée pour la combiner.</summary>
    public sealed partial class RecipeItem(LabRecipe recipe, string title, string situation) : ObservableObject
    {
        public LabRecipe Recipe { get; } = recipe;
        public string Title { get; } = title;
        public string Situation { get; } = situation;
        public string RuleId => Recipe.RuleId;
        public AuditSeverity Severity => Recipe.Rule.Severity;
        public string Anssi => Recipe.Anssi is { } a ? a.Id : "—";
        public string Level => Recipe.Rule.AnssiLevel is { } l ? $"N{l}" : "";
        public string SeverityColor => Severity switch
        {
            AuditSeverity.Critical => "#E74C3C",
            AuditSeverity.High => "#E67E22",
            AuditSeverity.Medium => "#F1C40F",
            _ => "#3498DB"
        };

        [ObservableProperty] private bool _isChecked;

        public bool Matches(string filter) =>
            string.IsNullOrWhiteSpace(filter)
            || new[] { RuleId, Title, Anssi, Situation }.Any(t => t.Contains(filter.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Fenêtre « Nouvel exercice à partir d'une recommandation » : une ou plusieurs recettes (cochées), la langue
    /// et le nom du domaine. Sans case cochée, la recette surlignée est utilisée.
    /// </summary>
    public sealed partial class RecipePickerViewModel : ObservableObject
    {
        private readonly List<RecipeItem> _all;
        private readonly DateTime _reference;

        public RecipePickerViewModel(DateTime? reference = null)
        {
            _reference = (reference ?? DateTime.Today).Date;
            var culture = StudioText.Instance.Culture;
            _all = LabRecipes.All
                .Select(r => new RecipeItem(r, Title(r.Rule, culture), r.Situation.Resolve(culture)))
                .OrderBy(r => r.Severity == AuditSeverity.Critical ? 0 : r.Severity == AuditSeverity.High ? 1 : r.Severity == AuditSeverity.Medium ? 2 : 3)
                .ThenBy(r => r.RuleId)
                .ToList();
            foreach (var r in _all)
            {
                Recipes.Add(r);
                r.PropertyChanged += OnItemChanged;
            }
            _selected = Recipes.FirstOrDefault();
            _culture = Cultures.First(c => c.Value == culture);
            Refresh();
        }

        public ObservableCollection<RecipeItem> Recipes { get; } = new();

        public IReadOnlyList<Choice<string>> Cultures { get; } = [new("fr-FR", "Français"), new("en-US", "English")];

        [ObservableProperty] private Choice<string> _culture;
        [ObservableProperty] private string _domainName = "corp.example.lab";
        [ObservableProperty] private string _filter = "";

        [ObservableProperty] private RecipeItem? _selected;

        /// <summary>Recettes retenues, dans l'ordre de la liste : les cochées, sinon la surlignée.</summary>
        public IReadOnlyList<LabRecipe> Chosen
        {
            get
            {
                var checkedOnes = _all.Where(r => r.IsChecked).Select(r => r.Recipe).ToList();
                return checkedOnes.Count > 0 ? checkedOnes : Selected is { } s ? [s.Recipe] : [];
            }
        }

        /// <summary>Résumé de la sélection : « 3 recommandations combinées : KRB-01, DLG-02, ACL-01 ».</summary>
        [ObservableProperty] private string _selectionSummary = "";

        /// <summary>Pourquoi ces recettes ne peuvent pas aller ensemble, ou vide.</summary>
        [ObservableProperty] private string _incompatibility = "";

        public bool HasIncompatibility => Incompatibility.Length > 0;

        public bool CanCreate => Chosen.Count > 0 && !HasIncompatibility && LooksLikeDomain(DomainName);

        partial void OnDomainNameChanged(string value) => OnPropertyChanged(nameof(CanCreate));
        partial void OnSelectedChanged(RecipeItem? value) => Refresh();
        partial void OnCultureChanged(Choice<string> value) => Refresh();
        partial void OnIncompatibilityChanged(string value) => OnPropertyChanged(nameof(HasIncompatibility));

        private void OnItemChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(RecipeItem.IsChecked)) Refresh();
        }

        partial void OnFilterChanged(string value)
        {
            var keep = Selected;
            Recipes.Clear();
            foreach (var r in _all.Where(r => r.Matches(value))) Recipes.Add(r);
            Selected = keep != null && Recipes.Contains(keep) ? keep : Recipes.FirstOrDefault();
        }

        /// <summary>Décoche tout.</summary>
        public void ClearChecks()
        {
            foreach (var r in _all) r.IsChecked = false;
        }

        private void Refresh()
        {
            var chosen = Chosen;
            var text = StudioText.Instance;
            SelectionSummary = chosen.Count > 1
                ? string.Format(text["Recipe.Combined"], chosen.Count, string.Join(", ", chosen.Select(r => r.RuleId)))
                : "";
            Incompatibility = chosen.Count > 1 ? LabRecipes.Incompatibility(chosen, Culture?.Value ?? text.Culture, _reference) ?? "" : "";
            OnPropertyChanged(nameof(Chosen));
            OnPropertyChanged(nameof(CanCreate));
        }

        public RecipeRequest? Request() =>
            CanCreate ? new RecipeRequest(Chosen, Culture.Value, DomainName.Trim().ToLowerInvariant()) : null;

        /// <summary>Au moins deux étiquettes séparées par des points, lettres, chiffres et tirets.</summary>
        public static bool LooksLikeDomain(string? name) =>
            !string.IsNullOrWhiteSpace(name)
            && name.Trim().Split('.') is { Length: >= 2 } labels
            && labels.All(l => l.Length > 0 && l.All(c => char.IsAsciiLetterOrDigit(c) || c == '-') && l[0] != '-' && l[^1] != '-');

        private static string Title(AuditRule rule, string culture) =>
            LocalizationService.Instance.Translate($"Audit.{rule.Id}", culture).Split('\n')[0].TrimStart('#', ' ').Trim();
    }
}
