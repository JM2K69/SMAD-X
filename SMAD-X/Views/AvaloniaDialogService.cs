using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using SMADX.Models;
using SMADX.Services;
using SMADX.ViewModels;

namespace SMADX.Views
{
    /// <summary>
    /// Implémentation Avalonia de <see cref="IDialogService"/> : toutes les fenêtres
    /// s'ouvrent en modale sur la fenêtre propriétaire (la fenêtre principale).
    /// </summary>
    public sealed class AvaloniaDialogService : IDialogService
    {
        private readonly Window _owner;

        public AvaloniaDialogService(Window owner) => _owner = owner;

        private static FilePickerFileType[] ToPickerTypes(FileFilter[] filters) =>
            filters.Select(f => new FilePickerFileType(f.Name) { Patterns = f.Patterns }).ToArray();

        public async Task<string?> PickSaveFileAsync(string title, string suggestedFileName, string defaultExtension, params FileFilter[] filters)
        {
            var file = await _owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title             = title,
                SuggestedFileName = suggestedFileName,
                DefaultExtension  = defaultExtension,
                FileTypeChoices   = ToPickerTypes(filters)
            });
            return file?.Path.LocalPath;
        }

        public async Task<string?> PickOpenFileAsync(string title, params FileFilter[] filters)
        {
            var files = await _owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title          = title,
                AllowMultiple  = false,
                FileTypeFilter = ToPickerTypes(filters)
            });
            return files.Count > 0 ? files[0].Path.LocalPath : null;
        }

        public async Task<NewDomainRequest?> ShowNewDomainAsync()
        {
            var dialog = new NewDomainDialog();
            var accepted = await dialog.ShowDialog<bool>(_owner);
            return accepted && !string.IsNullOrEmpty(dialog.DomainName)
                ? new NewDomainRequest(dialog.DomainName, dialog.EnableTiering)
                : null;
        }

        public Task ShowRelationsAsync(RelationsViewModel viewModel) =>
            new RelationsWindow { DataContext = viewModel }.ShowDialog(_owner);

        public void ShowGraph(GraphViewModel viewModel) =>
            new GraphWindow(viewModel).Show(_owner);

        public Task ShowTierConfigurationAsync() =>
            new TierConfigurationWindow().ShowDialog(_owner);

        public Task ShowTimelineAsync() =>
            new DomainTimelineWindow().ShowDialog(_owner);

        public Task ShowDelegationsAsync(ADObject? root) =>
            (root is not null ? new DelegationsWindow(root) : new DelegationsWindow()).ShowDialog(_owner);

        public Task ShowSitesAsync(ADSitesTopology? topology) =>
            (topology is not null ? new SitesWindow(topology) : new SitesWindow()).ShowDialog(_owner);

        public Task<string?> ShowExportReportAsync(ADRootDocument document) =>
            new ExportReportDialog(document).ShowDialog<string?>(_owner);

        public Task ShowAboutAsync() =>
            new AboutDialog().ShowDialog(_owner);

        public async Task ShowShortcutsAsync()
        {
            var closeButton = new Button
            {
                Content = "Fermer",
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                Padding = new Avalonia.Thickness(20, 5),
                Margin = new Avalonia.Thickness(0, 20, 0, 0)
            };

            var dialog = new Window
            {
                Title = "Raccourcis clavier",
                Width = 500,
                Height = 400,
                CanResize = false,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new ScrollViewer
                {
                    Content = new StackPanel
                    {
                        Margin = new Avalonia.Thickness(20),
                        Spacing = 10,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = "Raccourcis clavier disponibles",
                                FontSize = 16,
                                FontWeight = Avalonia.Media.FontWeight.Bold,
                                Margin = new Avalonia.Thickness(0, 0, 0, 10)
                            },
                            CreateShortcutItem("Ctrl+N", "Nouveau domaine"),
                            CreateShortcutItem("Ctrl+Shift+N", "Structure d'exemple"),
                            CreateShortcutItem("Ctrl+O", "Ouvrir un fichier"),
                            CreateShortcutItem("Ctrl+S", "Enregistrer"),
                            CreateShortcutItem("Ctrl+E / Ctrl+P", "Exporter en JSON / PowerShell"),
                            CreateShortcutItem("Ctrl+Shift+R", "Exporter la documentation"),
                            new Avalonia.Controls.Separator(),
                            CreateShortcutItem("Ctrl+C", "Copier l'objet sélectionné"),
                            CreateShortcutItem("Ctrl+V", "Coller l'objet copié"),
                            CreateShortcutItem("F2", "Renommer l'objet sélectionné"),
                            CreateShortcutItem("Delete", "Supprimer l'objet sélectionné"),
                            new Avalonia.Controls.Separator(),
                            CreateShortcutItem("Ctrl+T", "Évolution du domaine"),
                            CreateShortcutItem("Ctrl+D", "Délégations"),
                            CreateShortcutItem("Ctrl+Shift+S", "Sites AD"),
                            new Avalonia.Controls.Separator(),
                            new TextBlock
                            {
                                Text = "Astuce : Vous pouvez aussi utiliser les menus et les boutons de la barre d'outils.",
                                FontStyle = Avalonia.Media.FontStyle.Italic,
                                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                                Margin = new Avalonia.Thickness(0, 10, 0, 0)
                            },
                            closeButton
                        }
                    }
                }
            };

            closeButton.Click += (s, e) => dialog.Close();
            await dialog.ShowDialog(_owner);
        }

        private static Grid CreateShortcutItem(string shortcut, string description)
        {
            return new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("150,*"),
                Margin = new Avalonia.Thickness(0, 5, 0, 5),
                Children =
                {
                    new TextBlock
                    {
                        [Grid.ColumnProperty] = 0,
                        Text = shortcut,
                        FontWeight = Avalonia.Media.FontWeight.SemiBold,
                        FontFamily = new Avalonia.Media.FontFamily("Consolas, Courier New, monospace")
                    },
                    new TextBlock
                    {
                        [Grid.ColumnProperty] = 1,
                        Text = description
                    }
                }
            };
        }
    }
}
