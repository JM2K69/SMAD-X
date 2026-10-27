using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using SMADX.Studio.ViewModels;

namespace SMADX.Studio.Views
{
    /// <summary>Sélecteurs de fichiers du système et fenêtre de choix de la recette.</summary>
    public sealed class StudioDialogs(Window owner) : IStudioDialogs
    {
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

        public async Task<string?> PickFolderAsync(string title)
        {
            var folders = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = title, AllowMultiple = false });
            return folders.Count > 0 ? folders[0].Path.LocalPath : null;
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
