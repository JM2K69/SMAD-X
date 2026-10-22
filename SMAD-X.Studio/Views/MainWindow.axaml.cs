using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SMADX.Studio.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow() => InitializeComponent();

        private void Quit(object? sender, RoutedEventArgs e) => Close();
    }
}
