using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using SMADX.Services;
using SMADX.Studio.ViewModels;

namespace SMADX.Studio.Views
{
    /// <summary>Sélecteurs de fichiers du système et fenêtre de choix de la recette.</summary>
    public sealed class StudioDialogs(Window owner) : IStudioDialogs
    {
        public Task<UnsavedChoice> AskUnsavedAsync()
        {
            var text = StudioText.Instance;
            return ChoiceDialog.AskUnsavedAsync(owner, text["Unsaved.Title"], text["Unsaved.Message"], text["Unsaved.Save"], text["Unsaved.Discard"], text["Unsaved.Cancel"]);
        }

        public async Task<string?> OpenFileAsync(string title, string filterName, string pattern)
        {
            var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = title,
                AllowMultiple = false,
                FileTypeFilter = [new FilePickerFileType(filterName) { Patterns = [pattern] }, FilePickerFileTypes.All]
            });
            return files.Count > 0 ? files[0].Path.LocalPath : null;
        }

        public async Task<IReadOnlyList<string>> OpenFilesAsync(string title, string filterName, string pattern)
        {
            var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = title,
                AllowMultiple = true,
                FileTypeFilter = [new FilePickerFileType(filterName) { Patterns = [pattern] }, FilePickerFileTypes.All]
            });
            return files.Select(f => f.Path.LocalPath).ToList();
        }

        public async Task<string?> PickFolderAsync(string title, string? startFolder = null)
        {
            var start = startFolder != null ? await owner.StorageProvider.TryGetFolderFromPathAsync(startFolder) : null;
            var folders = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = title, AllowMultiple = false, SuggestedStartLocation = start });
            return folders.Count > 0 ? folders[0].Path.LocalPath : null;
        }

        public async Task ShowFolderAsync(string folder)
        {
            if (await owner.StorageProvider.TryGetFolderFromPathAsync(folder) is { } item)
                await owner.Launcher.LaunchFileAsync(item);
        }

        public async Task<string?> SaveFileAsync(string title, string suggestedName, string filterName, string pattern)
        {
            var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = title,
                SuggestedFileName = suggestedName,
                DefaultExtension = System.IO.Path.GetExtension(pattern),
                FileTypeChoices = [new FilePickerFileType(filterName) { Patterns = [pattern] }]
            });
            return file?.Path.LocalPath;
        }

        public Task<RecipeRequest?> ChooseRecipeAsync() =>
            new RecipeWindow { DataContext = new RecipePickerViewModel() }.ShowDialog<RecipeRequest?>(owner);
    }
}
