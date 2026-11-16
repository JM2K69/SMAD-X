using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace SMADX.Graph
{
    /// <summary>
    /// Les pictogrammes du graphe (🌐 👤 🔍 ⬇…) sont dessinés au trait, dans la couleur du texte : blancs sur les nœuds,
    /// pâlis avec un nœud estompé, teintés comme leur filtre dans la barre d'outils. Ils prennent donc Segoe UI Symbol,
    /// nommée sur le texte même. Sans elle, la police emoji du système, que la suite cherche en premier
    /// (<see cref="SMADX.Services.EmojiFont"/>), les dessinerait en couleur.
    /// </summary>
    public static class GraphSymbols
    {
        /// <summary>La police des pictogrammes au trait (Windows). Ailleurs, elle manque et l'emoji reste en couleur.</summary>
        public static readonly FontFamily Family = new(SMADX.Services.EmojiFont.LineArtFamily);

        /// <summary>
        /// Les pictogrammes d'un texte, en (début, longueur) : chaque symbole de U+2000 et plus (catégorie « autre
        /// symbole »), avec ses sélecteurs de variante, ses liants et ses teintes de peau. Le reste du texte, lettres,
        /// ponctuation et signes mathématiques (⊡, −), garde la police du texte.
        /// </summary>
        public static IReadOnlyList<(int Start, int Length)> Ranges(string? text)
        {
            var ranges = new List<(int Start, int Length)>();
            if (string.IsNullOrEmpty(text)) return ranges;
            int index = 0, start = -1;
            foreach (var rune in text.EnumerateRunes())
            {
                var symbol = Rune.GetUnicodeCategory(rune) == UnicodeCategory.OtherSymbol && rune.Value >= 0x2000;
                var joined = start >= 0 && rune.Value is 0xFE0E or 0xFE0F or 0x200D or (>= 0x1F3FB and <= 0x1F3FF);
                if (symbol || joined)
                {
                    if (start < 0) start = index;
                }
                else if (start >= 0)
                {
                    ranges.Add((start, index - start));
                    start = -1;
                }
                index += rune.Utf16SequenceLength;
            }
            if (start >= 0) ranges.Add((start, index - start));
            return ranges;
        }

        /// <summary>Un texte prêt à afficher : ses pictogrammes au trait, le reste dans la police du contrôle.</summary>
        public static TextBlock Label(string? text)
        {
            text ??= string.Empty;
            var inlines = new InlineCollection();
            var at = 0;
            foreach (var (start, length) in Ranges(text))
            {
                if (start > at) inlines.Add(new Run(text[at..start]));
                inlines.Add(new Run(text.Substring(start, length)) { FontFamily = Family });
                at = start + length;
            }
            if (at < text.Length) inlines.Add(new Run(text[at..]));
            return new TextBlock { Inlines = inlines };
        }

        /// <summary>Un texte du canevas : ses pictogrammes au trait.</summary>
        public static FormattedText Draw(FormattedText text, string source)
        {
            foreach (var (start, length) in Ranges(source))
                text.SetFontFamily(Family, start, length);
            return text;
        }
    }

    /// <summary>
    /// Le libellé d'un bouton, d'un filtre ou de la barre d'info du graphe, ses pictogrammes au trait
    /// (<see cref="GraphSymbols"/>). Le paramètre, s'il y en a un, est la classe de style du texte (<c>mono</c>).
    /// </summary>
    public sealed class GraphLabelConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            var label = GraphSymbols.Label(value as string);
            if (parameter is string style && style.Length > 0) label.Classes.Add(style);
            return label;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
