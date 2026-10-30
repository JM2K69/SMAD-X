using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Linq;
using SMADX.Models;
using SMADX.Services;

namespace SMADX.Views
{
    public partial class NewDomainDialog : Window
    {
        public string? DomainName { get; private set; }
        public bool EnableTiering { get; private set; }
        public WindowsServerVersion ServerVersion { get; private set; } = WindowsServerProfile.DefaultVersion;

        public NewDomainDialog()
        {
            InitializeComponent();

            // Nom proposé : le domaine d'exemple de Microsoft, identique dans toutes les langues
            DomainNameTextBox.Text = "contoso.com";

            ServerVersionComboBox.ItemsSource = WindowsServerProfile.All.Select(p => p.Name).ToList();
            ServerVersionComboBox.SelectedIndex = WindowsServerProfile.All.ToList().FindIndex(p => p.Version == WindowsServerProfile.DefaultVersion);

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
            EnableTiering = EnableTieringCheckBox.IsChecked ?? true;
            if (ServerVersionComboBox.SelectedIndex >= 0)
                ServerVersion = WindowsServerProfile.All[ServerVersionComboBox.SelectedIndex].Version;
            Close(true);
        }

        private void OnCancelClick(object? sender, RoutedEventArgs e)
        {
            Close(false);
        }
    }
}
