using System;
using System.IO;
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

        public Task<UnsavedChoice> AskUnsavedAsync(bool submission)
        {
            var loc = LocalizationService.Instance;
            return ChoiceDialog.AskUnsavedAsync(_owner, loc["Unsaved.Title"], loc[submission ? "Unsaved.Submission" : "Unsaved.Domain"],
                loc[submission ? "Unsaved.SaveSubmission" : "Unsaved.Save"], loc["Unsaved.Discard"], loc["Unsaved.Cancel"]);
        }

        public async Task<bool> ConfirmAsync(string title, string message, string yes, string no) =>
            await ChoiceDialog.ShowAsync(_owner, title, message, yes, no) == 0;

        public Task ShowMessageAsync(string title, string message) => ChoiceDialog.ShowAsync(_owner, title, message, "OK");

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
                ? new NewDomainRequest(dialog.DomainName, dialog.EnableTiering, dialog.ServerVersion)
                : null;
        }

        public Task ShowRelationsAsync(RelationsViewModel viewModel) =>
            new RelationsWindow { DataContext = viewModel }.ShowDialog(_owner);

        public void ShowGraph(GraphViewModel viewModel) =>
            new GraphWindow(viewModel).Show(_owner);

        public void ShowAudit(AuditViewModel viewModel) =>
            new AuditWindow { DataContext = viewModel }.Show(_owner);

        private LabWindow? _labWindow;

        /// <summary>Une seule fenêtre par exercice : « Afficher l'exercice » ramène celle qui est ouverte.</summary>
        public void ShowLab(LabViewModel viewModel)
        {
            if (_labWindow is { IsVisible: true } open && open.DataContext == viewModel)
            {
                open.Activate();
                return;
            }
            _labWindow?.Close();
            _labWindow = new LabWindow { DataContext = viewModel };
            _labWindow.Closed += (s, _) => { if (s == _labWindow) _labWindow = null; };
            _labWindow.Show(_owner);
        }

        public void ShowComparison(ComparisonViewModel viewModel) =>
            new ComparisonWindow { DataContext = viewModel }.Show(_owner);

        public void ShowAttackPaths(AttackPathsViewModel viewModel) =>
            new AttackPathsWindow { DataContext = viewModel }.Show(_owner);

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
            var loc = LocalizationService.Instance;
            var closeButton = new Button
            {
                Content = loc["Shortcut.CloseButton"],
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                Padding = new Avalonia.Thickness(20, 5),
                Margin = new Avalonia.Thickness(0, 20, 0, 0)
            };

            var dialog = new Window
            {
                Title = loc["Shortcut.WindowTitle"],
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
                                Text = loc["Shortcut.Header"],
                                FontSize = 16,
                                FontWeight = Avalonia.Media.FontWeight.Bold,
                                Margin = new Avalonia.Thickness(0, 0, 0, 10)
                            },
                            CreateShortcutItem("Ctrl+N", loc["Shortcut.Ctrl_N"]),
                            CreateShortcutItem("Ctrl+Shift+N", loc["Shortcut.Ctrl_Shift_N"]),
                            CreateShortcutItem("Ctrl+O", loc["Shortcut.Ctrl_O"]),
                            CreateShortcutItem("Ctrl+S", loc["Shortcut.Ctrl_S"]),
                            CreateShortcutItem("Ctrl+E", loc["Shortcut.Ctrl_E"]),
                            CreateShortcutItem("Ctrl+Shift+R", loc["Shortcut.Ctrl_Shift_R"]),
                            new Avalonia.Controls.Separator(),
                            CreateShortcutItem("Ctrl+C", loc["Shortcut.Ctrl_C"]),
                            CreateShortcutItem("Ctrl+V", loc["Shortcut.Ctrl_V"]),
                            CreateShortcutItem("F2", loc["Shortcut.F2"]),
                            CreateShortcutItem("Delete", loc["Shortcut.Delete"]),
                            new Avalonia.Controls.Separator(),
                            CreateShortcutItem("Ctrl+T", loc["Shortcut.Ctrl_T"]),
                            CreateShortcutItem("Ctrl+D", loc["Shortcut.Ctrl_D"]),
                            CreateShortcutItem("Ctrl+Shift+S", loc["Shortcut.Ctrl_Shift_S"]),
                            CreateShortcutItem("Ctrl+Shift+A", loc["Shortcut.Ctrl_Shift_A"]),
                            new Avalonia.Controls.Separator(),
                            new TextBlock
                            {
                                Text = loc["Shortcut.Tip"],
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
