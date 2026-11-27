using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SMAGX.Views;

/// <summary>L'éditeur que Git ouvre (message de commit, de fusion) : le texte proposé, la première ligne prête à écrire.</summary>
public partial class EditorWindow : Window
{
    public EditorWindow() => InitializeComponent();

    public EditorWindow(string text) : this()
    {
        TextBox.Text = text;
        Opened += (_, _) =>
        {
            TextBox.Focus();
            TextBox.CaretIndex = 0;
        };
    }

    private void OnSave(object? sender, RoutedEventArgs e) => Close((TextBox.Text ?? "").Replace("\r\n", "\n"));

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
}
