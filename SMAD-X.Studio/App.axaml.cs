using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using SMADX.Services;
using SMADX.Studio.ViewModels;
using SMADX.Studio.Views;

namespace SMADX.Studio
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            // Textes de l'interface, liés par {Binding [clé], Source={StaticResource S}}
            Resources["S"] = StudioText.Instance;
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            ThemeService.Instance.ApplyTheme(AppTheme.Dark);
            ErrorReporter.LogDirectory = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(ErrorReporter.LogDirectory)!, "Studio");

            Dispatcher.UIThread.UnhandledException += (_, e) =>
            {
                ErrorReporter.Log("Studio", e.Exception);
                e.Handled = true;
            };

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var window = new MainWindow();
                var viewModel = new StudioViewModel(new StudioDialogs(window));
                window.DataContext = viewModel;
                desktop.MainWindow = window;

                // Fichier donné au lancement : un domaine envoyé par SMAD-X (« Créer un exercice avec SMAD-X Studio »),
                // ou un exercice à ouvrir
                if (desktop.Args is { Length: > 0 } args)
                {
                    void OpenStartupFile(object? sender, System.EventArgs e)
                    {
                        window.Opened -= OpenStartupFile;
                        _ = viewModel.OpenPathAsync(args[0]);
                    }
                    window.Opened += OpenStartupFile;
                }
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
