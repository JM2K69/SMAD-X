using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SMARX.ViewModels;

namespace SMARX.Views
{
    /// <summary>Aide-mémoire des abréviations et des commandes, ouvert à côté de la fenêtre principale (une seule fois à la fois).</summary>
    public partial class GlossaryWindow : Window
    {
        private static GlossaryWindow? _open;

        public GlossaryWindow()
        {
            InitializeComponent();
        }

        /// <summary>Ouvre l'aide-mémoire sur un onglet (0 : Windows, 1 : Linux), ou l'amène devant s'il est déjà ouvert.</summary>
        public static void Show(Window? owner, int tab)
        {
            if (_open is { } window)
            {
                ((GlossaryViewModel)window.DataContext!).Tab = tab;
                window.Activate();
                return;
            }
            var vm = new GlossaryViewModel(tab);
            _open = new GlossaryWindow { DataContext = vm };
            _open.Closed += (_, _) =>
            {
                vm.Detach();
                _open = null;
            };
            if (owner != null) _open.Show(owner); else _open.Show();
        }

        /// <summary>« Exporter en PDF… » : les deux onglets, dans la langue de l'interface, là où on le choisit.</summary>
        private async void OnExportPdf(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not GlossaryViewModel vm) return;
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                FileTypeChoices = [new FilePickerFileType("PDF") { Patterns = ["*.pdf"] }],
                SuggestedFileName = vm.PdfFileName,
                DefaultExtension = "pdf"
            });
            if (file?.TryGetLocalPath() is { } path) vm.ExportPdf(path);
        }
    }
}
