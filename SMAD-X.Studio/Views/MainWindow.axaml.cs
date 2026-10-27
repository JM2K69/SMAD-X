using Avalonia.Controls;
using Avalonia.Interactivity;
using SMADX.Studio.ViewModels;

namespace SMADX.Studio.Views
{
    public partial class MainWindow : Window
    {
        private bool _closeConfirmed;

        public MainWindow()
        {
            InitializeComponent();
            Closing += OnClosing;
        }

        private void Quit(object? sender, RoutedEventArgs e) => Close();

        /// <summary>Un exercice modifié et non enregistré fait demander « enregistrer ? » avant de fermer.</summary>
        private async void OnClosing(object? sender, WindowClosingEventArgs e)
        {
            if (_closeConfirmed || DataContext is not StudioViewModel vm) return;
            e.Cancel = true;
            if (!await vm.ConfirmCloseAsync()) return;
            _closeConfirmed = true;
            Close();
        }
    }
}
