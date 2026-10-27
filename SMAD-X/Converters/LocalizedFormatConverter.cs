using Avalonia.Data;
using Avalonia.Data.Converters;
using System;
using System.Globalization;
using SMADX.Services;

namespace SMADX.Converters
{
    /// <summary>
    /// Place la valeur liée dans le texte traduit de la clé ConverterParameter :
    /// avec « TierConfig.Level » (« Niveau : {0} »), le niveau 2 affiche « Niveau : 2 ».
    /// </summary>
    public class LocalizedFormatConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            string.Format(LocalizationService.Instance[parameter as string ?? string.Empty], value);

        /// <summary>Un texte traduit ne repart jamais vers le modèle (une liaison en écriture y remettrait la valeur par défaut).</summary>
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => BindingOperations.DoNothing;
    }
}
