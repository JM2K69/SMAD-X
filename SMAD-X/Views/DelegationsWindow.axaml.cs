using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SMADX.Models;
using SMADX.ViewModels;

namespace SMADX.Views
{
    public partial class DelegationsWindow : Window
    {
        public DelegationsWindow()
        {
            InitializeComponent();
            DataContext = new DelegationsViewModel();
        }

        public DelegationsWindow(ADObject root) : this()
        {
            if (DataContext is DelegationsViewModel vm)
                vm.LoadFromTree(root);
        }

        private async void OnExportCsvClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not DelegationsViewModel vm) return;
            if (vm.FilteredDelegations.Count == 0) return;

            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title             = "Export delegations to CSV",
                SuggestedFileName = "delegations.csv",
                FileTypeChoices   = new List<FilePickerFileType>
                {
                    new("CSV file") { Patterns = new[] { "*.csv" } }
                }
            });

            if (file is null) return;
            await vm.ExportToCsvCommand.ExecuteAsync(file.Path.LocalPath);
        }
    }
}
