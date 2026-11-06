using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SMARX.Views
{
    /// <summary>« Sélectionner des utilisateurs ou des groupes » : OK rend vrai, Annuler ou la croix rendent faux.</summary>
    public partial class DirectoryPickerWindow : Window
    {
        public DirectoryPickerWindow()
        {
            InitializeComponent();
        }

        private void OnOk(object? sender, RoutedEventArgs e) => Close(true);

        private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
    }
}
