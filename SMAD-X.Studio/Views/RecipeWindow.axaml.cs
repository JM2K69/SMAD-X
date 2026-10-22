using Avalonia.Controls;
using Avalonia.Interactivity;
using SMADX.Studio.ViewModels;

namespace SMADX.Studio.Views
{
    public partial class RecipeWindow : Window
    {
        public RecipeWindow() => InitializeComponent();

        private void Create(object? sender, RoutedEventArgs e)
        {
            if (DataContext is RecipePickerViewModel vm && vm.Request() is { } request)
                Close(request);
        }

        private void Cancel(object? sender, RoutedEventArgs e) => Close(null);

        private void ClearChecks(object? sender, RoutedEventArgs e) => (DataContext as RecipePickerViewModel)?.ClearChecks();
    }
}
