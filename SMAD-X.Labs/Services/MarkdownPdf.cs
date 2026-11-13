using System;
using System.IO;
using System.Linq;
using OfficeIMO.Markdown;
using OfficeIMO.Markdown.Pdf;
using OfficeIMO.Pdf;

namespace SMADX.Services
{
    /// <summary>
    /// PDF d'un texte Markdown, commun aux outils de la suite (rapports et comptes rendus de SMAD-X et de Studio,
    /// aide-mémoire de SMAR-X). Les polices de secours (emoji, symboles, flèches) sont embarquées : le rendu est le même
    /// sous Windows, Linux et macOS, sans dépendre des polices installées. Le texte courant reste en Helvetica.
    /// </summary>
    public static class MarkdownPdf
    {
        private static readonly string[] FontFiles = ["NotoEmoji-wght.ttf", "NotoSansSymbols2-Regular.ttf", "NotoSansMono-Regular.ttf"];

        private static readonly Lazy<PdfEmbeddedFontFallbackSet> Fallbacks = new(() =>
            new PdfEmbeddedFontFallbackSet(new[]
            {
                Font("Noto Emoji", "NotoEmoji-wght.ttf"),
                Font("Noto Sans Symbols 2", "NotoSansSymbols2-Regular.ttf"),
                Font("Noto Sans Mono", "NotoSansMono-Regular.ttf")
            }));

        /// <summary>Les polices de secours embarquées (emoji, symboles, flèches), pour un PDF composé sans Markdown.</summary>
        public static PdfEmbeddedFontFallbackSet FontFallbacks => Fallbacks.Value;

        /// <summary>Le nombre de polices de secours embarquées (vérifié par le --self-test des exécutables publiés).</summary>
        public static int EmbeddedFontCount =>
            typeof(MarkdownPdf).Assembly.GetManifestResourceNames().Count(r => FontFiles.Any(f => r == "Fonts/" + f));

        /// <summary>Les options du PDF, avec les polices de secours embarquées.</summary>
        public static PdfOptions NewPdfOptions()
        {
            var options = new PdfOptions();
            options.RegisterEmbeddedFontFallbacks(Fallbacks.Value);
            return options;
        }

        /// <summary>
        /// Les options de rendu communes : le thème donné (Word par défaut), les polices de secours, et des ressources
        /// portables (aucune police système, aucune image distante ; une image en data URI est permise).
        /// </summary>
        public static MarkdownToPdfOptions NewOptions(MarkdownVisualTheme? theme = null) => new()
        {
            Theme = theme ?? MarkdownVisualTheme.WordLike(),
            TextFallbacks = PdfTextFallbackFeatures.Default,
            PdfOptions = NewPdfOptions(),
            ResourcePolicy = PdfResourcePolicy.CreatePortableDeterministic()
        };

        /// <summary>
        /// Lit le Markdown sans listes de définitions : « Terme : définition » reste une phrase. En français, le deux-points
        /// suit un espace, et chaque phrase « X : Y » deviendrait sinon un tableau à deux colonnes.
        /// </summary>
        public static MarkdownDoc Parse(string markdown) => MarkdownReader.Parse(markdown, new MarkdownReaderOptions { DefinitionLists = false });

        /// <summary>Écrit le PDF de ce Markdown ; <paramref name="configure"/> ajuste les options (titre, pied de page…).</summary>
        public static void Save(string markdown, string filePath, MarkdownVisualTheme? theme = null, Action<MarkdownToPdfOptions>? configure = null) =>
            Save(Parse(markdown), filePath, theme, configure);

        /// <summary>Écrit le PDF d'un document déjà construit (une image de taille donnée en tête, par exemple).</summary>
        public static void Save(MarkdownDoc document, string filePath, MarkdownVisualTheme? theme = null, Action<MarkdownToPdfOptions>? configure = null)
        {
            var options = NewOptions(theme);
            configure?.Invoke(options);
            document.SaveAsPdf(filePath, options);
        }

        private static PdfEmbeddedFontFallbackCandidate Font(string fontName, string fileName)
        {
            using var stream = typeof(MarkdownPdf).Assembly.GetManifestResourceStream("Fonts/" + fileName)
                ?? throw new InvalidOperationException($"Embedded font Fonts/{fileName} is missing");
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            return new PdfEmbeddedFontFallbackCandidate(fontName, buffer.ToArray());
        }
    }
}
