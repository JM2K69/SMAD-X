using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SMARX.Models;
using SMARX.Services.Labs;
using SMARX.ViewModels;

namespace SMARX.Views
{
    /// <summary>La fenêtre d'un exercice, à côté de la fenêtre principale où l'apprenant travaille.</summary>
    public partial class SmarxLabWindow : Window
    {
        private static readonly FilePickerFileType SubmissionFiles =
            new(SmarxText.Instance.T("Copie SMAR-X", "SMAR-X copy")) { Patterns = ["*" + SmarxSubmission.FileExtension] };

        public SmarxLabWindow()
        {
            InitializeComponent();
        }

        private SmarxLabViewModel? ViewModel => DataContext as SmarxLabViewModel;

        /// <summary>Enregistre la copie scellée, à rendre au formateur.</summary>
        private async void OnSave(object? sender, RoutedEventArgs e)
        {
            if (ViewModel is not { HasStudentName: true } vm) return;
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                FileTypeChoices = [SubmissionFiles],
                SuggestedFileName = vm.SuggestedFileName
            });
            if (file == null) return;
            await using (var stream = await file.OpenWriteAsync())
            await using (var writer = new StreamWriter(stream))
                await writer.WriteAsync(SmarxLabService.SerializeSubmission(vm.Submission()));
            vm.Saved(file.Name);
        }
    }
}
