using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using SMARX.ViewModels;
using SMARX.Views;

namespace SMARX
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
            // Textes de l'interface : une ressource par clé ({DynamicResource Tester.Who}), réécrite à chaque changement
            // de langue. Avalonia 12 ne rafraîchit plus les liaisons par indexeur sur une notification « Item ».
            ApplyTexts();
            SmarxText.Instance.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(SmarxText.CurrentCulture)) ApplyTexts();
            };
        }

        private void ApplyTexts()
        {
            var text = SmarxText.Instance;
            foreach (var key in text.KeysOf("fr-FR"))
                Resources[key] = text[key];
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                desktop.MainWindow = new MainWindow { DataContext = new MainWindowViewModel() };
            base.OnFrameworkInitializationCompleted();
        }
    }
}
