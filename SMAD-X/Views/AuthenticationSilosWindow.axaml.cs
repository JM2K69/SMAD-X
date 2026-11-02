using Avalonia.Controls;
using Avalonia.Interactivity;
using SMADX.ViewModels;

namespace SMADX.Views
{
    /// <summary>Gestion des silos d'authentification du domaine (modale) ; renvoie true si l'on valide.</summary>
    public partial class AuthenticationSilosWindow : Window
    {
        public AuthenticationSilosWindow()
        {
            InitializeComponent();
        }

        private void OnOkClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is AuthenticationSilosViewModel { Error: { } error })
            {
                ErrorTextBlock.Text = error;
                return;
            }
            Close(true);
        }

        private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);
    }
}
