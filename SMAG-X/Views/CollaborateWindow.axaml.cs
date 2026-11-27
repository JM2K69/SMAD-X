using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SMAGX.Views;

/// <summary>
/// Le travail à deux : servir la forge de ce SMAG-X sur le réseau de la salle (adresse et code à donner au camarade), ou
/// rejoindre celle d'un camarade. Le serveur continue de tourner une fois la fenêtre fermée.
/// </summary>
public partial class CollaborateWindow : Window
{
    public CollaborateWindow() => InitializeComponent();

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
