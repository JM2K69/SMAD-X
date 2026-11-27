using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SMAGX.Views;

/// <summary>La page « Clés SSH » d'un compte de la forge : les clés enregistrées, et l'ajout d'une clé publique collée.</summary>
public partial class KeysWindow : Window
{
    public KeysWindow() => InitializeComponent();

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
