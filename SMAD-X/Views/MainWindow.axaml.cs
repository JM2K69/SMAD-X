using System;
using System.IO;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using SMADX.Services;
using SMADX.ViewModels;

namespace SMADX.Views
{
    /// <summary>
    /// Fenêtre principale. Le code-behind ne garde que ce qui relève de la vue :
    /// raccourcis clavier, fermeture et capture PNG de l'arbre. Le reste passe par
    /// les commandes de <see cref="MainWindowViewModel"/>.
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            this.KeyDown += OnKeyDown;
        }

        private void OnTierComboBoxSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox cb && DataContext is MainWindowViewModel vm)
                vm.SelectedObjectTierIndex = cb.SelectedIndex;
        }

        private void OnExitClick(object? sender, RoutedEventArgs e)
        {
            Close();
        }

        /// <summary>
        /// Raccourcis affichés dans les menus (InputGesture n'est qu'un libellé dans Avalonia).
        /// Géré sur KeyDown, et non par KeyBindings, pour qu'une zone de texte qui traite déjà
        /// la touche (Suppr, Ctrl+C…) ne déclenche pas aussi l'action sur l'objet AD.
        /// </summary>
        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (DataContext is not MainWindowViewModel vm)
                return;

            bool ctrl  = e.KeyModifiers.HasFlag(KeyModifiers.Control);
            bool shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

            ICommand? command = (e.Key, ctrl, shift) switch
            {
                (Key.N, true, false)      => vm.CreateNewDomainCommand,
                (Key.N, true, true)       => vm.InitializeSampleDataCommand,
                (Key.O, true, false)      => vm.LoadFromFileCommand,
                (Key.S, true, false)      => vm.SaveToFileCommand,
                (Key.E, true, false)      => vm.ExportToJsonCommand,
                (Key.P, true, false)      => vm.ExportToPowerShellCommand,
                (Key.I, true, false)      => vm.GenerateImportScriptCommand,
                (Key.R, true, true)       => vm.ExportReportCommand,
                (Key.M, true, true)       => vm.ImportDescriptionCommand,
                (Key.C, true, false)      => vm.CopyObjectCommand,
                (Key.V, true, false)      => vm.PasteObjectCommand,
                (Key.T, true, false)      => vm.ShowTimelineCommand,
                (Key.D, true, false)      => vm.ShowDelegationsCommand,
                (Key.S, true, true)       => vm.ShowSitesCommand,
                (Key.A, true, true)       => vm.ShowAuditCommand,
                (Key.K, true, true)       => vm.ShowAttackPathsCommand,
                (Key.B, true, true)       => vm.ShowComparisonCommand,
                (Key.Z, true, false)      => vm.UndoCommand,
                (Key.Z, true, true)       => vm.RedoCommand,
                (Key.Y, true, false)      => vm.RedoCommand,
                (Key.F2, false, false)    => vm.RenameObjectCommand,
                (Key.Delete, false, false) => vm.DeleteObjectCommand,
                _ => null
            };

            if (command != null)
            {
                command.Execute(null);
                e.Handled = true;
            }
        }

        private async void OnExportTreePngClick(object? sender, RoutedEventArgs e)
        {
            // Expand all nodes
            if (DataContext is MainWindowViewModel vm)
                vm.ExpandAllCommand.Execute(null);

            // Laisser Avalonia re-mesurer et rendre tous les nœuds
            await System.Threading.Tasks.Task.Delay(400);

            var loc = LocalizationService.Instance;
            var path = await new AvaloniaDialogService(this).PickSaveFileAsync(
                loc["Dialog.ExportPng.Title"], "arborescence_ad.png", "png",
                new FileFilter(loc["FileType.ImagePNG"], "*.png"));

            if (path is null) return;

            var tree = this.FindControl<TreeView>("MainTreeView")!;

            int width  = (int)Math.Max(tree.Bounds.Width,  200);
            int height = (int)Math.Max(tree.Bounds.Height, 200);

            using var bitmap = new RenderTargetBitmap(
                new Avalonia.PixelSize(width, height),
                new Avalonia.Vector(96, 96));

            bitmap.Render(tree);

            using var fs = File.Create(path);
            bitmap.Save(fs);
        }
    }
}
