using Avalonia.Controls;
using Avalonia.Interactivity;
using SMADX.Studio.ViewModels;

namespace SMADX.Studio.Views
{
    /// <summary>« Nouvel exercice SMAR-X à partir d'une recette » : renvoie la recette et l'entreprise choisies.</summary>
    public partial class SmarxRecipeWindow : Window
    {
        public SmarxRecipeWindow() => InitializeComponent();

        private void Create(object? sender, RoutedEventArgs e)
        {
            if (DataContext is SmarxRecipePickerViewModel vm && vm.Request() is { } request)
                Close(request);
        }

        private void Cancel(object? sender, RoutedEventArgs e) => Close(null);
    }
}
