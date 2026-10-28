using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
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
            // De retour d'un explorateur où l'on a déposé des copies : Studio les corrige
            Activated += (_, _) => (DataContext as StudioViewModel)?.PickUpNewSubmissions();
            DragDrop.SetAllowDrop(this, true);
            AddHandler(DragDrop.DragOverEvent, OnDragOver);
            AddHandler(DragDrop.DropEvent, OnDrop);
        }

        private static void OnDragOver(object? sender, DragEventArgs e) =>
            e.DragEffects = e.DataTransfer.Contains(DataFormat.File) ? DragDropEffects.Copy : DragDropEffects.None;

        /// <summary>Le dossier d'un exercice l'ouvre ; des copies ou un dossier de copies sont rangés et corrigés.</summary>
        private async void OnDrop(object? sender, DragEventArgs e)
        {
            if (DataContext is not StudioViewModel vm || e.DataTransfer.TryGetFiles() is not { } items) return;
            var paths = items.Select(i => i.TryGetLocalPath()).OfType<string>().ToList();
            if (paths.Count > 0) await vm.DropAsync(paths);
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
