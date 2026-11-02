using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SMADX.Views
{
    /// <summary>Choix d'objets dans l'annuaire (modale) ; renvoie true si l'on ajoute les objets cochés.</summary>
    public partial class ObjectPickerWindow : Window
    {
        public ObjectPickerWindow()
        {
            InitializeComponent();
        }

        private void OnAddClick(object? sender, RoutedEventArgs e) => Close(true);

        private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);
    }
}
