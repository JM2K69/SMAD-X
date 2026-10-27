using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SMADX.Services;
using SMADX.ViewModels;

namespace SMADX.Views
{
    public partial class DomainTimelineWindow : Window
    {
        public DomainTimelineWindow()
        {
            InitializeComponent();
            DataContext = new DomainTimelineViewModel();
        }

        private async void OnOpenFileAClick(object? sender, RoutedEventArgs e)
            => await PickAndLoad(isFileA: true);

        private async void OnOpenFileBClick(object? sender, RoutedEventArgs e)
            => await PickAndLoad(isFileA: false);

        private async System.Threading.Tasks.Task PickAndLoad(bool isFileA)
        {
            if (DataContext is not DomainTimelineViewModel vm) return;

            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title          = LocalizationService.Instance[isFileA ? "Timeline.OpenA.Title" : "Timeline.OpenB.Title"],
                AllowMultiple  = false,
                FileTypeFilter = new List<FilePickerFileType>
                {
                    new(LocalizationService.Instance["FileType.SMADXJSON"]) { Patterns = new[] { "*.smad-x.json" } },
                    FilePickerFileTypes.All
                }
            });

            if (files.Count == 0) return;
            await vm.LoadFileAsync(files[0].Path.LocalPath, isFileA);
        }

        private async void OnExportCsvClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not DomainTimelineViewModel vm) return;
            if (vm.FilteredChanges.Count == 0) return;

            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title             = LocalizationService.Instance["Timeline.Export.Title"],
                SuggestedFileName = $"{vm.FileA?.DomainName ?? "domain"}-diff.csv",
                FileTypeChoices   = new List<FilePickerFileType>
                {
                    new(LocalizationService.Instance["Csv.FileType"]) { Patterns = new[] { "*.csv" } }
                }
            });

            if (file is null) return;
            await vm.ExportToCsvCommand.ExecuteAsync(file.Path.LocalPath);
        }
    }
}

