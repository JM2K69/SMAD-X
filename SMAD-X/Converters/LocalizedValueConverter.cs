using Avalonia;
using Avalonia.Data;
using Avalonia.Data.Converters;
using System;
using System.Collections.Generic;
using System.Globalization;
using SMADX.Services;

namespace SMADX.Converters
{
    /// <summary>
    /// Traduit une valeur (énumération, booléen ou texte) par la clé « {ConverterParameter}.{valeur} » :
    /// RightCategory.FullControl avec le paramètre « Delegation.Category » affiche Loc["Delegation.Category.FullControl"].
    /// Une valeur vide (le choix « tous » d'un filtre) affiche la clé « {ConverterParameter}.All ».
    /// En MultiBinding, seule la première valeur est traduite : les suivantes (Loc.CurrentCulture)
    /// font seulement recalculer le texte au changement de langue.
    /// </summary>
    public class LocalizedValueConverter : IValueConverter, IMultiValueConverter
    {
        public static string Translate(object? value, object? prefix)
        {
            if (value is UnsetValueType) return string.Empty;
            var name = value?.ToString();
            return LocalizationService.Instance[$"{prefix}.{(string.IsNullOrEmpty(name) ? "All" : name)}"];
        }

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            Translate(value, parameter);

        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture) =>
            Translate(values.Count > 0 ? values[0] : null, parameter);

        /// <summary>Un texte traduit ne repart jamais vers le modèle (une liaison en écriture y remettrait la valeur par défaut).</summary>
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => BindingOperations.DoNothing;
    }
}
