using Avalonia;
using Avalonia.Styling;
using System;

namespace SMADX.Services
{
    public enum AppTheme
    {
        Light,
        Dark
    }

    public class ThemeService
    {
        private static ThemeService? _instance;
        public static ThemeService Instance => _instance ??= new ThemeService();

        public AppTheme CurrentTheme =>
            Application.Current?.RequestedThemeVariant == ThemeVariant.Dark
                ? AppTheme.Dark
                : AppTheme.Light;

        public event EventHandler<AppTheme>? ThemeChanged;

        private ThemeService() { }

        public void ApplyTheme(AppTheme theme)
        {
            var app = Application.Current;
            if (app == null) return;

            app.RequestedThemeVariant = theme == AppTheme.Dark
                ? ThemeVariant.Dark
                : ThemeVariant.Light;

            ThemeChanged?.Invoke(this, theme);
        }

        public void ToggleTheme()
        {
            ApplyTheme(CurrentTheme == AppTheme.Light ? AppTheme.Dark : AppTheme.Light);
        }
    }
}
