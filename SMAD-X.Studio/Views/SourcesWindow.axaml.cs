using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SMADX.Studio.ViewModels;

namespace SMADX.Studio.Views
{
    /// <summary>
    /// Les fiches des sources : ouverte depuis le menu ou l'accueil (une seule à la fois, à côté de Studio), ou depuis une
    /// recette, sur la règle de la recette (au-dessus de la fenêtre des recettes).
    /// </summary>
    public partial class SourcesWindow : Window, ISourceDialogs
    {
        private static SourcesWindow? _open;

        public SourcesWindow() => InitializeComponent();

        /// <summary>À côté de Studio : la fenêtre déjà ouverte revient au premier plan, sur la règle demandée.</summary>
        public static void Open(Window owner, string? ruleId = null)
        {
            if (_open != null)
            {
                if (ruleId != null && _open.DataContext is SourcesViewModel vm) vm.Select(ruleId);
                _open.Activate();
                return;
            }
            _open = Create(ruleId);
            _open.Closed += (_, _) => _open = null;
            _open.Show(owner);
        }

        /// <summary>Au-dessus d'une fenêtre modale (les recettes), sur la règle de la recette.</summary>
        public static Task ShowOverAsync(Window owner, string? ruleId) => Create(ruleId).ShowDialog(owner);

        private static SourcesWindow Create(string? ruleId)
        {
            var window = new SourcesWindow();
            window.DataContext = new SourcesViewModel(window, ruleId);
            return window;
        }

        private void CloseWindow(object? sender, RoutedEventArgs e) => Close();

        private void ClearChecks(object? sender, RoutedEventArgs e) => (DataContext as SourcesViewModel)?.ClearChecks();

        public async Task<string?> SaveFileAsync(string title, string suggestedName, string filterName, string pattern)
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = title,
                SuggestedFileName = suggestedName,
                DefaultExtension = System.IO.Path.GetExtension(pattern),
                FileTypeChoices = [new FilePickerFileType(filterName) { Patterns = [pattern] }]
            });
            return file == null ? null : StudioDialogs.LocalPath(file);
        }

        public async Task<string?> PickFolderAsync(string title)
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = title, AllowMultiple = false });
            return folders.Count > 0 ? StudioDialogs.LocalPath(folders[0]) : null;
        }

        public async Task OpenUrlAsync(string url)
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps)
                await Launcher.LaunchUriAsync(uri);
        }

        public async Task ShowFolderAsync(string folder)
        {
            if (await StorageProvider.TryGetFolderFromPathAsync(folder) is { } item)
                await Launcher.LaunchFileAsync(item);
        }
    }
}
