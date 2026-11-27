using Avalonia.Controls;
using Avalonia.Interactivity;
using SMADX.Services;
using SMAGX.ViewModels;

namespace SMAGX.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        // Au premier lancement, l'élève choisit son identité avant de commencer
        Opened += async (_, _) =>
        {
            if (DataContext is MainWindowViewModel { HasIdentity: false } vm) await vm.AskIdentityAsync();
        };
        // Le serveur de la forge s'arrête avec la fenêtre
        Closed += (_, _) => (DataContext as MainWindowViewModel)?.Dispose();
    }

    private void OnToggleTheme(object? sender, RoutedEventArgs e) => ThemeService.Instance.ToggleTheme();
}
