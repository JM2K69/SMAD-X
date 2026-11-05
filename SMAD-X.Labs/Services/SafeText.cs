using System;
using System.Linq;
using System.Text;

namespace SMADX.Services
{
    /// <summary>
    /// Texte venu d'un fichier reçu (nom d'apprenant, domaine importé…) écrit dans un CSV, une console ou
    /// du Markdown : il s'affiche tel quel, sans être interprété comme une formule, une séquence de
    /// terminal ou un lien.
    /// </summary>
    public static class SafeText
    {
        /// <summary>Longueur maximale d'un nom affiché (apprenant, fichier).</summary>
        public const int MaxNameLength = 100;

        /// <summary>
        /// Nom sur une ligne : caractères de contrôle (sauts de ligne, échappements ANSI…) remplacés par
        /// une espace, blancs d'extrémité retirés, longueur bornée.
        /// </summary>
        public static string Name(string? text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            var clean = new string(text.Select(c => char.IsControl(c) ? ' ' : c).ToArray()).Trim();
            return clean.Length <= MaxNameLength ? clean : clean[..MaxNameLength].TrimEnd() + "…";
        }

        /// <summary>
        /// Cellule CSV. Une valeur qui commence par =, +, -, @, une tabulation ou un retour chariot est
        /// préfixée d'une apostrophe, pour qu'Excel ne l'évalue pas comme une formule (recommandation OWASP).
        /// </summary>
        public static string CsvCell(string? value, string separator, bool alwaysQuote = false)
        {
            value ??= string.Empty;
            if (value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
                value = "'" + value;
            var quote = alwaysQuote || value.Contains(separator) || value.IndexOfAny(['"', '\r', '\n']) >= 0;
            return quote ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
        }

        /// <summary>
        /// Texte inséré dans un tableau Markdown : sur une ligne, et sans lien, image, balise ni mise en forme.
        /// </summary>
        public static string Markdown(string? text)
        {
            var sb = new StringBuilder();
            foreach (var c in (text ?? string.Empty).ReplaceLineEndings(" "))
            {
                if (char.IsControl(c)) { sb.Append(' '); continue; }
                if ("\\`*_[]()!<>#|~".Contains(c)) sb.Append('\\');
                sb.Append(c);
            }
            return sb.ToString().Trim();
        }
    }
}
