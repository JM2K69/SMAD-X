using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using SMARX.Labs;

namespace SMADX.Studio.ViewModels
{
    /// <summary>Une recette SMAR-X choisie, pour une entreprise, dans une langue.</summary>
    public sealed record SmarxRecipeRequest(SmarxRecipe Recipe, SmarxOrganization Organization, string Culture);

    /// <summary>Une recette SMAR-X de la liste : son titre, sa situation, son niveau et ses plateformes.</summary>
    public sealed class SmarxRecipeItem(SmarxRecipe recipe, string culture)
    {
        public SmarxRecipe Recipe { get; } = recipe;
        public string Id => Recipe.Id;
        public string Title { get; } = recipe.Title.Resolve(culture);
        public string Situation { get; } = recipe.Situation.Resolve(culture);
        public string Platforms { get; } = recipe.Platforms.Count == 3
            ? StudioText.Instance.T("Windows, Linux ou les deux", "Windows, Linux or both")
            : string.Join(" · ", recipe.Platforms.Select(p => SmarxRecipePickerViewModel.PlatformLabel(p, culture)));
        public string Level { get; } = recipe.Level switch
        {
            SMADX.Models.LabLevel.Beginner => StudioText.Instance.T("débutant", "beginner"),
            SMADX.Models.LabLevel.Intermediate => StudioText.Instance.T("intermédiaire", "intermediate"),
            _ => StudioText.Instance.T("avancé", "advanced")
        };
    }

    /// <summary>
    /// Fenêtre « Nouvel exercice SMAR-X à partir d'une recette » : le scénario, l'entreprise (qui donne le domaine), ses
    /// services, la plateforme et la langue de l'exercice. Les règles de gestion apparaissent dans l'énoncé.
    /// </summary>
    public sealed partial class SmarxRecipePickerViewModel : ObservableObject
    {
        public SmarxRecipePickerViewModel()
        {
            var culture = StudioText.Instance.Culture;
            foreach (var r in SmarxRecipes.All) Recipes.Add(new SmarxRecipeItem(r, culture));
            _selected = Recipes[0];
            _culture = Cultures.First(c => c.Value == culture);
            _platform = Platforms[0];
            Refresh();
        }

        public List<SmarxRecipeItem> Recipes { get; } = new();
        public IReadOnlyList<Choice<string>> Cultures { get; } = [new("fr-FR", "Français"), new("en-US", "English")];
        public IReadOnlyList<Choice<SmarxPlatform>> Platforms { get; } =
            [.. new[] { SmarxPlatform.Windows, SmarxPlatform.Linux, SmarxPlatform.Both }.Select(p => new Choice<SmarxPlatform>(p, PlatformLabel(p, StudioText.Instance.Culture)))];

        [ObservableProperty] private SmarxRecipeItem _selected;
        [ObservableProperty] private Choice<string> _culture;
        [ObservableProperty] private Choice<SmarxPlatform> _platform;
        [ObservableProperty] private string _company = "Lumen";
        [ObservableProperty] private string _services = "Compta, RH, Ventes, Direction";

        /// <summary>Ce que donnera le choix : le domaine et les groupes, ou ce qui empêche de créer.</summary>
        [ObservableProperty] private string _preview = "";
        [ObservableProperty] private string _problem = "";
        public bool HasProblem => Problem.Length > 0;
        public bool CanCreate => !HasProblem;

        partial void OnSelectedChanged(SmarxRecipeItem value) => Refresh();
        partial void OnPlatformChanged(Choice<SmarxPlatform> value) => Refresh();
        partial void OnCompanyChanged(string value) => Refresh();
        partial void OnServicesChanged(string value) => Refresh();
        partial void OnCultureChanged(Choice<string> value) => Refresh();
        partial void OnProblemChanged(string value)
        {
            OnPropertyChanged(nameof(HasProblem));
            OnPropertyChanged(nameof(CanCreate));
        }

        private IReadOnlyList<string> ServiceNames => Services.Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        private void Refresh()
        {
            var text = StudioText.Instance;
            var problem = SmarxOrganization.Problem(ServiceNames, text.Culture);
            if (problem == null && !Selected.Recipe.Supports(Platform.Value))
                problem = text.T($"Cette recette se fait sur : {Selected.Platforms}.", $"This recipe runs on: {Selected.Platforms}.");
            Problem = problem ?? "";
            if (problem != null)
            {
                Preview = "";
                return;
            }
            var org = new SmarxOrganization(Company, ServiceNames, Platform.Value);
            Preview = text.T(
                $"Domaine {org.DnsDomain} ({org.Domain}) · groupes {string.Join(", ", org.Services.Select(s => s.Global))} · deux comptes par service",
                $"Domain {org.DnsDomain} ({org.Domain}) · groups {string.Join(", ", org.Services.Select(s => s.Global))} · two accounts per department");
        }

        public SmarxRecipeRequest? Request() =>
            CanCreate ? new SmarxRecipeRequest(Selected.Recipe, new SmarxOrganization(Company, ServiceNames, Platform.Value), Culture.Value) : null;

        public static string PlatformLabel(SmarxPlatform platform, string culture) => platform switch
        {
            SmarxPlatform.Windows => "Windows",
            SmarxPlatform.Linux => "Linux",
            _ => culture.StartsWith("en") ? "Windows and Linux" : "Windows et Linux"
        };
    }
}
