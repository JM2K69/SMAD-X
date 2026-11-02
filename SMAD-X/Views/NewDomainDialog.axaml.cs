using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Linq;
using SMADX.Models;
using SMADX.Services;
using SMADX.Services.Tiering;

namespace SMADX.Views
{
    public partial class NewDomainDialog : Window
    {
        public string? DomainName { get; private set; }
        public bool EnableTiering { get; private set; }
        public WindowsServerVersion ServerVersion { get; private set; } = WindowsServerProfile.DefaultVersion;

        /// <summary>Modèle de cloisonnement à appliquer au domaine neuf (identifiant), ou null.</summary>
        public string? TierModel { get; private set; }

        /// <summary>Choix de la liste : aucun cloisonnement, étiquettes seules, puis les modèles publiés.</summary>
        private const int NoTiering = 0, LabelsOnly = 1;

        public NewDomainDialog()
        {
            InitializeComponent();
            var loc = LocalizationService.Instance;

            // Nom proposé : le domaine d'exemple de Microsoft, identique dans toutes les langues
            DomainNameTextBox.Text = "contoso.com";

            ServerVersionComboBox.ItemsSource = WindowsServerProfile.All.Select(p => p.Name).ToList();
            ServerVersionComboBox.SelectedIndex = WindowsServerProfile.All.ToList().FindIndex(p => p.Version == WindowsServerProfile.DefaultVersion);

            TieringComboBox.ItemsSource = new[] { loc["Dialog.NewDomain.Tiering.None"], loc["Dialog.NewDomain.Tiering.Labels"] }
                .Concat(TierModelCatalog.All.Select(TierModelCatalog.DisplayName)).ToList();
            TieringComboBox.SelectedIndex = LabelsOnly;

            // Focus sur le champ de texte au démarrage
            DomainNameTextBox.AttachedToVisualTree += (s, e) =>
            {
                DomainNameTextBox.Focus();
                DomainNameTextBox.SelectAll();
            };
        }

        private void OnCreateClick(object? sender, RoutedEventArgs e)
        {
            var domainName = DomainNameTextBox.Text?.Trim();
            var loc = LocalizationService.Instance;

            // Validation
            if (string.IsNullOrWhiteSpace(domainName))
            {
                ValidationMessage.Text = loc["Dialog.NewDomain.ValidationEmpty"];
                ValidationMessage.IsVisible = true;
                return;
            }

            if (!domainName.Contains('.'))
            {
                ValidationMessage.Text = loc["Dialog.NewDomain.ValidationNoDot"];
                ValidationMessage.IsVisible = true;
                return;
            }

            // Validation des caractères
            if (domainName.Any(c => !char.IsLetterOrDigit(c) && c != '.' && c != '-'))
            {
                ValidationMessage.Text = loc["Dialog.NewDomain.ValidationInvalidChars"];
                ValidationMessage.IsVisible = true;
                return;
            }

            DomainName = domainName;
            EnableTiering = TieringComboBox.SelectedIndex != NoTiering;
            TierModel = SelectedModel()?.Id;
            if (ServerVersionComboBox.SelectedIndex >= 0)
                ServerVersion = WindowsServerProfile.All[ServerVersionComboBox.SelectedIndex].Version;
            Close(true);
        }

        private TierModelDefinition? SelectedModel() =>
            TieringComboBox.SelectedIndex > LabelsOnly ? TierModelCatalog.All[TieringComboBox.SelectedIndex - LabelsOnly - 1] : null;

        /// <summary>Un modèle publié s'affiche avec sa source et sa licence.</summary>
        private void OnTieringChanged(object? sender, SelectionChangedEventArgs e)
        {
            var model = SelectedModel();
            TierModelSourceText.IsVisible = model != null;
            if (model != null) TierModelSourceText.Text = TierModelCatalog.SourceText(model);
        }

        private void OnCancelClick(object? sender, RoutedEventArgs e)
        {
            Close(false);
        }
    }
}
