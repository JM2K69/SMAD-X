using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
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

        private async void OnAddSnapshotsClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not DomainTimelineViewModel vm) return;

            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title         = "Select SMAD-X snapshots",
                AllowMultiple = true,
                FileTypeFilter = new List<FilePickerFileType>
                {
                    new("SMAD-X snapshots") { Patterns = new[] { "*.smad-x.json" } },
                    FilePickerFileTypes.All
                }
            });

            if (files.Count == 0) return;

            var paths = files.Select(f => f.Path.LocalPath);
            await vm.AddSnapshotsCommand.ExecuteAsync(paths);
        }

        private async void OnExportCsvClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not DomainTimelineViewModel vm) return;
            if (vm.FilteredChanges.Count == 0) return;

            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title           = "Export changes to CSV",
                SuggestedFileName = $"{vm.SelectedBaseline?.DomainName ?? "domain"}-diff.csv",
                FileTypeChoices = new List<FilePickerFileType>
                {
                    new("CSV file") { Patterns = new[] { "*.csv" } }
                }
            });

            if (file is null) return;
            await vm.ExportToCsvCommand.ExecuteAsync(file.Path.LocalPath);
        }
    }
}
