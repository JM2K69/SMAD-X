using SMADX.Services;
using SMADX.ViewModels;
using SMADX.Views;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace SMADX
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            InstallGlobalExceptionHandlers();
            ThemeService.Instance.ApplyTheme(AppTheme.Dark);

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mainWindow = new MainWindow();
                ErrorReporter.ErrorRaised += error => Dispatcher.UIThread.Post(() => ShowError(mainWindow, error));
                var viewModel = new MainWindowViewModel(new AvaloniaDialogService(mainWindow));
                mainWindow.DataContext = viewModel;
                desktop.MainWindow = mainWindow;

                // Exercice, copie ou domaine donné au lancement (double-clic, « SMAD-X.exe fichier ») :
                // ouvert une fois la fenêtre affichée, pour que la fenêtre de l'exercice ait son propriétaire
                if (desktop.Args is { Length: > 0 } args)
                {
                    void OpenStartupFile(object? sender, EventArgs e)
                    {
                        mainWindow.Opened -= OpenStartupFile;
                        _ = viewModel.OpenStartupFileAsync(args[0]);
                    }
                    mainWindow.Opened += OpenStartupFile;
                }
            }

            base.OnFrameworkInitializationCompleted();
        }

        private static ErrorWindow? _openErrorWindow;

        /// <summary>Une seule fenêtre d'erreur à la fois : les erreurs suivantes y sont comptées.</summary>
        private static void ShowError(Window owner, ReportedError error)
        {
            if (_openErrorWindow != null)
            {
                _openErrorWindow.AddError();
                return;
            }

            _openErrorWindow = new ErrorWindow(error);
            _openErrorWindow.Closed += (_, _) => _openErrorWindow = null;
            if (owner.IsVisible)
                _openErrorWindow.Show(owner);
            else
                _openErrorWindow.Show();
        }

        /// <summary>
        /// Une exception levée par une commande ou un gestionnaire d'événement ne doit plus disparaître
        /// sans laisser de trace : elle est journalisée puis affichée, et l'application continue.
        /// </summary>
        private static void InstallGlobalExceptionHandlers()
        {
            // Erreurs qu'Avalonia gère lui-même et ne fait que journaliser (auparavant dans Trace, invisible)
            Avalonia.Logging.Logger.Sink = new Helpers.AvaloniaErrorLogSink();

            // Une commande lancée par un clic ou une touche s'exécute dans Dispatcher.Send, depuis la boucle
            // de messages Win32. Sans gestionnaire, Avalonia garde l'application en vie et l'exception
            // disparaît sans message : c'est ainsi qu'un échec d'ouverture de fenêtre passait inaperçu.
            Dispatcher.UIThread.UnhandledException += (_, e) =>
            {
                ErrorReporter.Report("UI", e.Exception);
                e.Handled = true;
            };
            TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                ErrorReporter.Report("Tâche", e.Exception);
                e.SetObserved();
            };
            // Dernier recours : l'application va s'arrêter, on garde au moins la trace
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                if (e.ExceptionObject is Exception ex)
                    ErrorReporter.Log("Fatal", ex);
            };
        }
    }
}