using SMADX.Services;
using SMADX.ViewModels;
using SMADX.Views;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using System.Linq;

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
            ThemeService.Instance.ApplyTheme(AppTheme.Dark);

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mainWindow = new MainWindow();
                mainWindow.DataContext = new MainWindowViewModel(new AvaloniaDialogService(mainWindow));
                desktop.MainWindow = mainWindow;
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}