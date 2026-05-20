using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace SMADX.Converters
{
    /// <summary>
    /// Convertit un SourceType (string) en emoji icône AD
    /// </summary>
    public class ObjectTypeToIconConverter : IValueConverter
    {
        public static readonly ObjectTypeToIconConverter Instance = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value?.ToString() switch
            {
                "Computer" => "🖥️",
                "User"     => "👤",
                "Group"    => "👥",
                "Domain"   => "🌐",
                "OU"       => "📁",
                "PSO"      => "🔑",
                _          => "📄"
            };
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
