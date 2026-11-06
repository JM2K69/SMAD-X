using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using OfficeIMO.Markdown;
using OfficeIMO.Pdf;
using SMADX.Services;

namespace SMARX.Services
{
    /// <summary>
    /// L'aide-mémoire en Markdown puis en PDF, en français ou en anglais : le bouclier de SMAR-X et d'où vient le document
    /// (SMAR-X, sa version, la suite SMA-X et son dépôt GitHub), puis les onglets Windows et Linux, abréviations et
    /// commandes d'exemple.
    /// </summary>
    public static partial class Glossary
    {
        public const string RepositoryUrl = "https://github.com/JM2K69/SMAD-X";
        public const string DocumentationUrl = "https://jm2k69.github.io/SMAD-X/smar-x/";

        /// <summary>Le nom proposé pour le PDF.</summary>
        public static string PdfFileName(string culture) => Text(culture, "aide-memoire-smar-x.pdf", "smar-x-cheat-sheet.pdf");

        /// <summary>Le bouclier de l'en-tête de SMAR-X, en PNG (rendu par tools/SmarxScreens --logo).</summary>
        public static byte[] Logo()
        {
            using var stream = typeof(Glossary).Assembly.GetManifestResourceStream("Assets/smarx-icon.png")
                ?? throw new InvalidOperationException("Embedded resource Assets/smarx-icon.png is missing");
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            return buffer.ToArray();
        }

        /// <summary>L'aide-mémoire en Markdown ; <paramref name="version"/> est celle de SMAR-X, <paramref name="date"/> celle de l'export.</summary>
        public static string Markdown(string culture, string version, DateTime date)
        {
            string T(string fr, string en) => Text(culture, fr, en);
            var lf = Lf.ToString();
            var s = new StringBuilder();
            s.Append("![SMAR-X](data:image/png;base64,").Append(Convert.ToBase64String(Logo())).Append(')').Append(lf).Append(lf);
            s.Append(T("# Aide-mémoire de SMAR-X", "# SMAR-X cheat sheet")).Append(lf).Append(lf);
            var day = date.ToString(T("d MMMM yyyy", "MMMM d, yyyy"), CultureInfo.GetCultureInfo(T("fr-FR", "en-US")));
            s.Append(T($"Extrait de **SMAR-X {version}**, de la suite **SMA-X** (*Simuler, Modéliser, Auditer – eXpert*), le {day}.",
                       $"Taken from **SMAR-X {version}**, part of the **SMA-X** suite (*Simulate, Model, Audit – eXpert*), on {day}."))
                .Append(lf).Append(lf);
            s.Append("- ").Append(T("**Code source et versions** : ", "**Source code and releases**: ")).Append($"[{RepositoryUrl}]({RepositoryUrl})").Append(lf);
            s.Append("- ").Append(T("**Documentation** : ", "**Documentation**: ")).Append($"[{DocumentationUrl}]({DocumentationUrl})").Append(lf).Append(lf);

            foreach (var (title, sections) in new[] { ("Windows", Windows(culture)), ("Linux", Linux(culture)) })
            {
                s.Append("## ").Append(title).Append(lf).Append(lf);
                foreach (var section in sections) Section(s, section, culture);
            }

            s.Append("---").Append(lf).Append(lf);
            s.Append(T($"*Aide-mémoire extrait de la suite SMA-X (SMAR-X {version}) : {RepositoryUrl}*",
                       $"*Cheat sheet taken from the SMA-X suite (SMAR-X {version}): {RepositoryUrl}*")).Append(lf);
            return s.ToString();
        }

        private static void Section(StringBuilder s, GlossarySection section, string culture)
        {
            string T(string fr, string en) => Text(culture, fr, en);
            var lf = Lf.ToString();
            s.Append("### ").Append(section.Title).Append(lf).Append(lf);
            if (section.Intro is { Length: > 0 } intro) s.Append(intro).Append(lf).Append(lf);
            if (section.HasRows)
            {
                var values = section.Rows.Any(r => r.Value != null);
                // Le nom, en gras, ouvre la colonne de l'explication : une colonne à lui seul serait trop étroite
                s.Append(T("| Notation | Ce que cela veut dire |", "| Notation | What it means |")).Append(values ? T(" Valeur |", " Value |") : "").Append(lf);
                s.Append("|---|---|").Append(values ? "---|" : "").Append(lf);
                foreach (var row in section.Rows)
                {
                    var meaning = row.Name.Length > 0 ? $"**{Cell(row.Name)}** — {Cell(row.Meaning)}" : Cell(row.Meaning);
                    s.Append("| `").Append(Cell(row.Code)).Append("` | ").Append(meaning).Append(" |");
                    if (values) s.Append(row.Value is { } v ? " `" + v + "` |" : "  |");
                    s.Append(lf);
                }
                s.Append(lf);
            }
            // Ce que fait la commande, puis la commande ; l'équivalent icacls et la commande du module dans un même bloc
            foreach (var command in section.CommandList)
            {
                s.Append(command.Meaning).Append(lf).Append(lf);
                Code(s, command.Icacls is { } icacls ? Lines("# icacls", icacls, "# NTFSSecurity", command.Command) : command.Command);
            }
        }

        private static void Code(StringBuilder s, string code)
        {
            var lf = Lf.ToString();
            s.Append("```").Append(lf).Append(code).Append(lf).Append("```").Append(lf).Append(lf);
        }

        private static string Cell(string text) => text.Replace("|", "\\|");

        /// <summary>Taille du bouclier en tête du PDF, en points.</summary>
        private const double LogoSize = 44;

        /// <summary>
        /// Écrit l'aide-mémoire en PDF : le bouclier en tête, à sa taille ; sur chaque page, en haut le titre, en bas
        /// d'où il vient (la suite SMA-X et son dépôt GitHub) et la page sur le nombre de pages ; les métadonnées du PDF
        /// (titre, auteur : SMAR-X et sa version, sujet : la suite SMA-X) ; les polices embarquées de la suite.
        /// </summary>
        public static void SavePdf(string filePath, string culture, string version, DateTime date)
        {
            string T(string fr, string en) => Text(culture, fr, en);
            var directory = Path.GetDirectoryName(Path.GetFullPath(filePath));
            if (directory != null) Directory.CreateDirectory(directory);

            // Le bouclier, posé à sa taille ; le reste du Markdown suit (sans sa première ligne, l'image)
            var markdown = Markdown(culture, version, date);
            var body = markdown[(markdown.IndexOf(Lf + Lf.ToString(), StringComparison.Ordinal) + 2)..];
            var document = MarkdownDoc.Create();
            document.Image("data:image/png;base64," + Convert.ToBase64String(Logo()), "SMAR-X", "SMAR-X", LogoSize, LogoSize);
            document.AddRange(MarkdownPdf.Parse(body).Blocks);

            MarkdownPdf.Save(document, filePath, configure: options =>
            {
                options.Title = T("Aide-mémoire de SMAR-X", "SMAR-X cheat sheet");
                options.Author = $"SMAR-X {version}";
                options.Subject = T($"Extrait de la suite SMA-X : {RepositoryUrl}", $"Taken from the SMA-X suite: {RepositoryUrl}");
                options.Keywords = "SMAR-X, SMA-X, icacls, NTFSSecurity, PowerShell, chmod, setfacl, getfacl, Samba";
                options.IncludeImages = true;
                options.MaximumDataUriImageBytes = 1_000_000;
                var pdf = options.PdfOptions!;
                var grey = PdfColor.FromRgb(110, 118, 129);
                pdf.ShowHeader = true;
                pdf.HeaderFormat = T($"Aide-mémoire de SMAR-X {version}", $"SMAR-X {version} cheat sheet");
                pdf.HeaderAlign = PdfAlign.Right;
                pdf.HeaderFontSize = 8;
                pdf.HeaderTextColor = grey;
                pdf.ShowPageNumbers = true;
                pdf.FooterFormat = T("Extrait de la suite SMA-X · github.com/JM2K69/SMAD-X · page {page} / {pages}",
                                     "Taken from the SMA-X suite · github.com/JM2K69/SMAD-X · page {page} / {pages}");
                pdf.FooterAlign = PdfAlign.Center;
                pdf.FooterFontSize = 8;
                pdf.FooterTextColor = grey;
            });
        }
    }
}
