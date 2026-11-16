using System;
using Avalonia;
using Avalonia.Media;

namespace SMADX.Services
{
    /// <summary>
    /// Les emoji en couleur dans toutes les fenêtres de la suite. Un caractère que la police du texte n'a pas
    /// (👤 dans Consolas, par exemple) est cherché d'abord dans la police emoji du système. Sans cela, Avalonia reprend
    /// la police déjà chargée qui lui convient le mieux : une fois Segoe UI Symbol chargée (pour « ⊡ » de la barre du
    /// graphe), elle l'emportait sur Segoe UI Emoji et les emoji de toutes les fenêtres ouvertes ensuite sortaient au
    /// trait, en noir et blanc.
    /// </summary>
    public static class EmojiFont
    {
        /// <summary>La police emoji du système : Segoe UI Emoji (Windows), Apple Color Emoji (macOS), Noto Color Emoji (Linux).</summary>
        public static string Family =>
            OperatingSystem.IsWindows() ? "Segoe UI Emoji"
            : OperatingSystem.IsMacOS() ? "Apple Color Emoji"
            : "Noto Color Emoji";

        /// <summary>Les mêmes pictogrammes, au trait et dans la couleur du texte : Segoe UI Symbol (Windows seulement).</summary>
        public const string LineArtFamily = "Segoe UI Symbol";

        /// <summary>Place la police emoji du système en tête des polices de secours.</summary>
        public static AppBuilder WithEmojiFont(this AppBuilder builder)
            => builder.With(new FontManagerOptions
            {
                FontFallbacks = [new FontFallback { FontFamily = new FontFamily(Family) }]
            });
    }
}
