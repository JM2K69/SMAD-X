using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace SMADX.Services
{
    /// <summary>Réponse à « des modifications ne sont pas enregistrées ».</summary>
    public enum UnsavedChoice
    {
        Save,
        Discard,
        Cancel
    }

    /// <summary>
    /// Boîte de choix modale, partagée par SMAD-X et SMAD-X Studio : un message et des boutons. Le premier
    /// bouton répond à Entrée, le dernier à Échap.
    /// </summary>
    public static class ChoiceDialog
    {
        /// <returns>Index du bouton choisi, ou -1 si la fenêtre est fermée sans choix.</returns>
        public static async Task<int> ShowAsync(Window owner, string title, string message, params string[] buttons)
        {
            var result = -1;
            var dialog = new Window
            {
                Title = title,
                SizeToContent = SizeToContent.WidthAndHeight,
                CanResize = false,
                ShowInTaskbar = false,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
            for (var i = 0; i < buttons.Length; i++)
            {
                var index = i;
                var button = new Button { Content = buttons[i], IsDefault = i == 0, IsCancel = i == buttons.Length - 1, Padding = new Thickness(14, 5) };
                button.Click += (_, _) =>
                {
                    result = index;
                    dialog.Close();
                };
                row.Children.Add(button);
            }
            dialog.Content = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 18,
                Children =
                {
                    new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, MaxWidth = 460 },
                    row
                }
            };
            await dialog.ShowDialog(owner);
            return result;
        }

        /// <summary>Enregistrer, ne pas enregistrer ou annuler ; fermer la boîte annule.</summary>
        public static async Task<UnsavedChoice> AskUnsavedAsync(Window owner, string title, string message, string save, string discard, string cancel) =>
            await ShowAsync(owner, title, message, save, discard, cancel) switch
            {
                0 => UnsavedChoice.Save,
                1 => UnsavedChoice.Discard,
                _ => UnsavedChoice.Cancel
            };
    }
}
