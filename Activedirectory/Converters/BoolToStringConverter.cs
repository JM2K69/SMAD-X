using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace SMADX.Converters
{
    /// <summary>
    /// Convertisseur pour afficher différentes valeurs selon une condition booléenne
    /// </summary>
    public class BoolToStringConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool boolValue && parameter is string paramString)
            {
                var parts = paramString.Split('|');
                if (parts.Length == 2)
                {
                    return boolValue ? parts[0] : parts[1];
                }
            }
            return value;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
