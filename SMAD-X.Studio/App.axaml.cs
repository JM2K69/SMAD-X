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
                window.DataContext = new StudioViewModel(new StudioDialogs(window));
                desktop.MainWindow = window;
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
