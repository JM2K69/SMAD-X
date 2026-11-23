using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SMADX.Models;
using SMADX.Services;
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

        /// <summary>
        /// Fenêtre non modale : elle se recale sur le domaine affiché à chaque fois qu'on y revient (fichier ouvert,
        /// annulation, objet supprimé dans la fenêtre principale), et signale chaque délégation ajoutée ou retirée.
        /// </summary>
        public DelegationsWindow(Func<ADObject?> getRoot, Action changed) : this()
        {
            if (DataContext is not DelegationsViewModel vm) return;
            vm.Changed += changed;
            void Follow()
            {
                if (getRoot() is { } root) vm.Refresh(root);
            }
            Follow();
            Activated += (_, _) => Follow();
        }

        private void OnOUTreeSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (DataContext is not DelegationsViewModel vm) return;
            if (OUTreeView.SelectedItem is ADObject node)
                vm.SelectedOUNode = node;
        }

        private async void OnExportCsvClick(object? sender, RoutedEventArgs e)
        {
            if (DataContext is not DelegationsViewModel vm) return;
            if (vm.FilteredDelegations.Count == 0) return;

            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title             = LocalizationService.Instance["Delegations.Export.Title"],
                SuggestedFileName = "delegations.csv",
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

