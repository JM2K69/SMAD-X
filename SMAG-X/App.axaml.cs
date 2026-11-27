using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using SMAGX.ViewModels;
using SMAGX.Views;

namespace SMAGX;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        SMADX.Controls.CopyButton.French = () => SmagxText.Instance.French;
        // Textes de l'interface : une ressource par clé, réécrite à chaque changement de langue (comme SMAR-X : une
        // liaison par indexeur hérité du Localizer ne se rafraîchirait pas sous Avalonia 12)
        ApplyTexts();
        SmagxText.Instance.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SmagxText.CurrentCulture)) ApplyTexts();
        };
    }

    private void ApplyTexts()
    {
        var text = SmagxText.Instance;
        foreach (var key in text.KeysOf("fr-FR"))
            Resources[key] = text[key];
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow();
            window.DataContext = new MainWindowViewModel(SmagxSettings.Load(), new WindowDialogs(window));
            desktop.MainWindow = window;
        }
        base.OnFrameworkInitializationCompleted();
    }
}
