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
        private readonly Avalonia.Threading.DispatcherTimer _autoSave = new() { Interval = TimeSpan.FromMinutes(1) };
        private bool _closeConfirmed;

        public MainWindow()
        {
            InitializeComponent();
            this.KeyDown += OnKeyDown;
            Closing += OnClosing;
            _autoSave.Tick += (_, _) => (DataContext as MainWindowViewModel)?.AutoSave();
            Opened += (_, _) => _autoSave.Start();
            Closed += (_, _) => _autoSave.Stop();
        }

        /// <summary>
        /// Fermeture : une copie de secours d'abord (mode exercice), puis, s'il reste des modifications non
        /// enregistrées, la question « enregistrer ? ». La fenêtre se ferme ensuite pour de bon.
        /// </summary>
        private async void OnClosing(object? sender, WindowClosingEventArgs e)
        {
            if (_closeConfirmed || DataContext is not MainWindowViewModel vm) return;
            e.Cancel = true;
            vm.AutoSave();
            if (!await vm.ConfirmCloseAsync()) return;
            _closeConfirmed = true;
            Close();
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
                (Key.S, true, false)      => vm.IsLabMode ? vm.SaveSubmissionCommand : vm.SaveToFileCommand,
                (Key.E, true, false)      => vm.ExportToJsonCommand,
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

            // CanExecute : en mode exercice, Ctrl+N ou Ctrl+O ne remplacent pas le domaine de l'apprenant
            if (command != null && command.CanExecute(null))
            {
                command.Execute(null);
                e.Handled = true;
            }
        }

        /// <summary>Entrée valide le nouveau nom, Échap reprend l'ancien ; le focus revient à l'arbre.</summary>
        private void OnObjectNameKeyDown(object? sender, KeyEventArgs e)
        {
            if (sender is not TextBox box || DataContext is not MainWindowViewModel vm || e.Key is not (Key.Enter or Key.Escape))
                return;

            if (e.Key == Key.Enter)
                CommitObjectName(box, vm);
            else
                box.SetCurrentValue(TextBox.TextProperty, vm.SelectedObjectName);
            if (vm.SelectedNode != null)
                vm.SelectedNode.IsEditing = false;
            // Retour à l'élément sélectionné de l'arbre, pour enchaîner au clavier
            var tree = this.FindControl<TreeView>("MainTreeView");
            if (tree?.TreeContainerFromItem(vm.SelectedNode!) is Control item)
                item.Focus();
            e.Handled = true;
        }

        private void OnObjectNameLostFocus(object? sender, RoutedEventArgs e)
        {
            if (sender is TextBox box && DataContext is MainWindowViewModel vm)
                CommitObjectName(box, vm);
        }

        /// <summary>
        /// Transmet le nom saisi ; s'il est refusé, le champ reprend celui de l'objet. Si la sélection a déjà
        /// changé, le champ affiche le nom du nouvel objet et rien n'est renommé.
        /// SetCurrentValue garde la liaison du champ, qu'une affectation de Text remplacerait.
        /// </summary>
        private static void CommitObjectName(TextBox box, MainWindowViewModel vm)
        {
            vm.SelectedObjectName = box.Text ?? string.Empty;
            box.SetCurrentValue(TextBox.TextProperty, vm.SelectedObjectName);
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
                loc["Dialog.ExportPng.Title"], loc["Dialog.ExportPng.DefaultName"], "png",
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
