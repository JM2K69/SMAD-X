using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace SMADX.Services.Sources
{
    /// <summary>
    /// Juste assez de JavaScript pour lire le code minifié d'une page : sauter une chaîne (y compris un gabarit `…${…}…`),
    /// trouver la parenthèse, le crochet ou l'accolade qui ferme, lire les propriétés d'un objet littéral, et la valeur
    /// d'une chaîne.
    /// </summary>
    public static class Js
    {
        /// <summary>Position juste après la chaîne qui commence en <paramref name="i"/> (', " ou `).</summary>
        public static int SkipString(string s, int i)
        {
            var quote = s[i];
            i++;
            while (i < s.Length)
            {
                var c = s[i];
                if (c == '\\') { i += 2; continue; }
                if (c == quote) return i + 1;
                if (quote == '`' && c == '$' && i + 1 < s.Length && s[i + 1] == '{')
                {
                    i = Close(s, i + 1) + 1;
                    continue;
                }
                i++;
            }
            return s.Length;
        }

        /// <summary>Position du caractère qui ferme celui en <paramref name="i"/> ( ( [ { ), chaînes comprises.</summary>
        public static int Close(string s, int i)
        {
            var depth = 0;
            while (i < s.Length)
            {
                var c = s[i];
                if (c is '\'' or '"' or '`') { i = SkipString(s, i); continue; }
                if (c is '(' or '[' or '{') depth++;
                else if (c is ')' or ']' or '}')
                {
                    depth--;
                    if (depth == 0) return i;
                }
                i++;
            }
            return s.Length - 1;
        }

        /// <summary>Les parties d'une liste séparée par des virgules au premier niveau (le contenu d'un objet, d'un tableau, d'un appel).</summary>
        public static List<string> Split(string inner)
        {
            var parts = new List<string>();
            var depth = 0;
            var start = 0;
            for (var i = 0; i < inner.Length; i++)
            {
                var c = inner[i];
                if (c is '\'' or '"' or '`') { i = SkipString(inner, i) - 1; continue; }
                if (c is '(' or '[' or '{') depth++;
                else if (c is ')' or ']' or '}') depth--;
                else if (c == ',' && depth == 0)
                {
                    parts.Add(inner[start..i]);
                    start = i + 1;
                }
            }
            if (start < inner.Length) parts.Add(inner[start..]);
            return parts.Select(p => p.Trim()).Where(p => p.Length > 0).ToList();
        }

        /// <summary>Les propriétés d'un objet littéral « {a:…,b:…} » : nom, puis le texte de la valeur.</summary>
        public static Dictionary<string, string> Object(string obj)
        {
            var props = new Dictionary<string, string>();
            var text = obj.Trim();
            if (!text.StartsWith('{')) return props;
            foreach (var part in Split(text[1..Close(text, 0)]))
            {
                if (part.StartsWith("...")) continue;
                var colon = KeyEnd(part);
                if (colon < 0) continue;
                var key = part[..colon].Trim().Trim('"', '\'', '`');
                props[key] = part[(colon + 1)..].Trim();
            }
            return props;
        }

        private static int KeyEnd(string part)
        {
            if (part.Length > 0 && part[0] is '"' or '\'' or '`')
            {
                var end = SkipString(part, 0);
                return end < part.Length && part[end] == ':' ? end : -1;
            }
            for (var i = 0; i < part.Length; i++)
            {
                var c = part[i];
                if (c == ':') return i;
                if (!(char.IsLetterOrDigit(c) || c is '_' or '$')) return -1;
            }
            return -1;
        }

        /// <summary>La valeur d'une chaîne littérale ; dans un gabarit, une expression ${…} devient « … ».</summary>
        public static string Literal(string token)
        {
            var quote = token[0];
            var body = token[1..^1];
            var sb = new StringBuilder();
            for (var i = 0; i < body.Length; i++)
            {
                var c = body[i];
                if (quote == '`' && c == '$' && i + 1 < body.Length && body[i + 1] == '{')
                {
                    i = Close(body, i + 1);
                    sb.Append('…');
                    continue;
                }
                if (c != '\\' || i + 1 >= body.Length) { sb.Append(c); continue; }
                var next = body[++i];
                switch (next)
                {
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append(' '); break;
                    case 'r': break;
                    case 'x' when i + 2 < body.Length:
                        sb.Append((char)int.Parse(body.Substring(i + 1, 2), NumberStyles.HexNumber));
                        i += 2;
                        break;
                    case 'u' when i + 1 < body.Length && body[i + 1] == '{':
                        var close = body.IndexOf('}', i);
                        sb.Append(char.ConvertFromUtf32(int.Parse(body[(i + 2)..close], NumberStyles.HexNumber)));
                        i = close;
                        break;
                    case 'u' when i + 4 < body.Length:
                        sb.Append((char)int.Parse(body.Substring(i + 1, 4), NumberStyles.HexNumber));
                        i += 4;
                        break;
                    default: sb.Append(next); break;
                }
            }
            // Une espace insécable (\xA0) est une espace dans le Markdown
            return sb.ToString().Replace(' ', ' ');
        }
    }
}
