using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SMADX.Services;

namespace SMADX.Views
{
    /// <summary>
    /// Affiche une erreur inattendue signalée par <see cref="ErrorReporter"/> : message, détail technique
    /// et emplacement du journal. Les erreurs suivantes, tant que la fenêtre est ouverte, sont seulement comptées.
    /// </summary>
    public partial class ErrorWindow : Window
    {
        private readonly ReportedError _error;
        private int _additionalErrors;

        /// <summary>Constructeur du concepteur XAML et du chargeur Avalonia.</summary>
        public ErrorWindow() : this(new ReportedError("UI", new InvalidOperationException("Exemple"), ErrorReporter.CurrentLogFile))
        {
        }

        public ErrorWindow(ReportedError error)
        {
            _error = error;
            InitializeComponent();
            MessageText.Text = error.Message;
            DetailsText.Text = $"[{error.Source}] {error.Exception}";
            LogFileText.Text = string.Format(LocalizationService.Instance["Error.LogFile"], error.LogFile);
        }

        /// <summary>Une autre erreur est arrivée pendant que la fenêtre était ouverte.</summary>
        public void AddError()
        {
            _additionalErrors++;
            MoreErrorsText.Text = string.Format(LocalizationService.Instance["Error.MoreErrors"], _additionalErrors);
            MoreErrorsText.IsVisible = true;
        }

        private async void OnCopyClick(object? sender, RoutedEventArgs e)
        {
            if (Clipboard is { } clipboard)
                await clipboard.SetTextAsync(DetailsText.Text ?? string.Empty);
        }

        private async void OnOpenLogClick(object? sender, RoutedEventArgs e)
        {
            var folder = Path.GetDirectoryName(_error.LogFile);
            if (folder != null && Directory.Exists(folder))
                await Launcher.LaunchDirectoryInfoAsync(new DirectoryInfo(folder));
        }

        private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
    }
}
