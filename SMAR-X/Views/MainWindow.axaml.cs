using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SMADX.Services;
using SMARX.Models;
using SMARX.ViewModels;

namespace SMARX.Views
{
    public partial class MainWindow : Window
    {
        private static readonly FilePickerFileType SmarxFiles = new("SMAR-X") { Patterns = ["*" + SmarxDocument.FileExtension] };

        public MainWindow()
        {
            InitializeComponent();
        }

        private MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;

        private async void OnOpen(object? sender, RoutedEventArgs e)
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { FileTypeFilter = [SmarxFiles], AllowMultiple = false });
            if (files.Count == 0 || ViewModel == null) return;
            try
            {
                await using var stream = await files[0].OpenReadAsync();
                using var reader = new StreamReader(stream);
                ViewModel.Load(SmarxDocument.Parse(await reader.ReadToEndAsync()));
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException or FormatException)
            {
                ErrorReporter.Log("Ouverture", ex);
            }
        }

        private async void OnSave(object? sender, RoutedEventArgs e)
        {
            if (ViewModel == null) return;
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                FileTypeChoices = [SmarxFiles],
                SuggestedFileName = ViewModel.Host.Name + SmarxDocument.FileExtension
            });
            if (file == null) return;
            await using var stream = await file.OpenWriteAsync();
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync(ViewModel.ToDocument().Serialize());
        }

        private void OnToggleTheme(object? sender, RoutedEventArgs e) => ThemeService.Instance.ToggleTheme();
    }
}
