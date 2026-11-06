using System;
using System.IO;
using Avalonia.Controls;
using SMARX.Services.Labs;
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
        private static readonly FilePickerFileType LabFiles = new(SmarxText.Instance.T("Exercice SMAR-X", "SMAR-X lab")) { Patterns = ["*" + SmarxLab.FileExtension] };
        private SmarxLabWindow? _labWindow;

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
                SuggestedFileName = ViewModel.SuggestedFileName
            });
            if (file == null) return;
            await using var stream = await file.OpenWriteAsync();
            await using var writer = new StreamWriter(stream);
            await writer.WriteAsync(ViewModel.ToDocument().Serialize());
        }

        private void OnToggleTheme(object? sender, RoutedEventArgs e) => ThemeService.Instance.ToggleTheme();

        /// <summary>Le menu des exercices : ceux fournis, un fichier d'exercice, quitter l'exercice en cours.</summary>
        private void OnLabs(object? sender, RoutedEventArgs e)
        {
            if (sender is not Button button || ViewModel == null) return;
            var text = SmarxText.Instance;
            var flyout = new MenuFlyout();
            foreach (var lab in ViewModel.BuiltInLabs)
            {
                var item = new MenuItem { Header = lab.Title.Resolve(text.Culture) };
                item.Click += (_, _) => OpenLab(lab);
                flyout.Items.Add(item);
            }
            flyout.Items.Add(new Separator());
            var open = new MenuItem { Header = text["Lab.OpenFile"] };
            open.Click += OnOpenLabFile;
            flyout.Items.Add(open);
            if (ViewModel.HasLab)
            {
                var show = new MenuItem { Header = text["Lab.Show"] };
                show.Click += (_, _) => ShowLabWindow();
                flyout.Items.Add(show);
                var leave = new MenuItem { Header = text["Lab.Leave"] };
                leave.Click += (_, _) =>
                {
                    _labWindow?.Close();
                    ViewModel.LeaveLabCommand.Execute(null);
                };
                flyout.Items.Add(leave);
            }
            flyout.ShowAt(button);
        }

        private async void OnOpenLabFile(object? sender, RoutedEventArgs e)
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { FileTypeFilter = [LabFiles], AllowMultiple = false });
            if (files.Count == 0) return;
            var text = SmarxText.Instance;
            try
            {
                await using var stream = await files[0].OpenReadAsync();
                using var reader = new StreamReader(stream);
                var lab = SmarxLabService.Parse(await reader.ReadToEndAsync());
                // L'exemplaire du formateur d'un examen contient le corrigé : on distribue le sujet
                if (lab.IsTrainerCopy)
                {
                    await ChoiceDialog.ShowAsync(this, text["Lab.TrainerCopy.Title"], text["Lab.TrainerCopy"], "OK");
                    return;
                }
                OpenLab(lab);
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException)
            {
                await ChoiceDialog.ShowAsync(this, text["Lab.OpenFile"], ex.Message, "OK");
            }
        }

        private void OpenLab(SmarxLab lab)
        {
            if (ViewModel == null) return;
            ViewModel.OpenLab(lab);
            _labWindow?.Close();
            _labWindow = null;
            ShowLabWindow();
        }

        private void ShowLabWindow()
        {
            if (ViewModel?.Lab == null) return;
            if (_labWindow == null)
            {
                _labWindow = new SmarxLabWindow { DataContext = ViewModel.Lab };
                _labWindow.Closed += (_, _) => _labWindow = null;
                _labWindow.Show(this);
            }
            else
                _labWindow.Activate();
        }

        private void OnGlossary(object? sender, RoutedEventArgs e) => GlossaryWindow.Show(this, ViewModel?.IsWindows == true ? 0 : 1);
    }
}
