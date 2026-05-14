using Avalonia.Data.Converters;
using System;
using System.Globalization;
using SMADX.Services;

namespace SMADX.Converters
{
    /// <summary>
    /// Convertisseur pour la localisation dans les bindings XAML
    /// </summary>
    public class LocalizeConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (parameter is string key)
            {
                return LocalizationService.Instance[key];
            }
            return parameter?.ToString() ?? string.Empty;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
