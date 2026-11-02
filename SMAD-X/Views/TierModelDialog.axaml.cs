using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SMADX.Services.Tiering;

namespace SMADX.Views
{
    /// <summary>Choix d'un modèle de cloisonnement à appliquer au domaine affiché ; renvoie son identifiant.</summary>
    public partial class TierModelDialog : Window
    {
        public TierModelDialog() : this(null)
        {
        }

        public TierModelDialog(string? current)
        {
            InitializeComponent();
            ModelComboBox.ItemsSource = TierModelCatalog.All.Select(TierModelCatalog.DisplayName).ToList();
            var index = TierModelCatalog.All.ToList().FindIndex(m => m.Id == current);
            ModelComboBox.SelectedIndex = index >= 0 ? index : 0;
        }

        private void OnModelChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (ModelComboBox.SelectedIndex < 0) return;
            var model = TierModelCatalog.All[ModelComboBox.SelectedIndex];
            ContentTextBlock.Text = TierModelCatalog.ContentText(model);
            SourceTextBlock.Text = TierModelCatalog.SourceText(model);
            NotesList.ItemsSource = TierModelCatalog.Notes(model);
        }

        private void OnApplyClick(object? sender, RoutedEventArgs e) =>
            Close(ModelComboBox.SelectedIndex >= 0 ? TierModelCatalog.All[ModelComboBox.SelectedIndex].Id : null);

        private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(null);
    }
}
