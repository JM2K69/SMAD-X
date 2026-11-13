using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SMADX.Services.Sources
{
    /// <summary>Un point de la checklist ANSSI, en français et en anglais, avec son texte et sa recommandation en Markdown.</summary>
    /// <param name="Levels">Les niveaux du point (1 : le plus critique) ; un point peut en avoir plusieurs, chacun avec son seuil.</param>
    public sealed record AnssiPoint(string Id, string TitleFr, string TitleEn, IReadOnlyList<int> Levels, string? Added, string? Updated,
        string BodyFr, string BodyEn, string RecoFr, string RecoEn);

    /// <summary>
    /// La checklist « Points de contrôle Active Directory » du CERT-FR (ANSSI) : une page dont le code (minifié) porte,
    /// pour chaque point, son titre en français et en anglais, son niveau, son historique, son texte et sa recommandation,
    /// écrits en appels JSX. Les composants sont reconnus à ce qu'ils font, pas à leur nom minifié, qui change d'une
    /// publication à l'autre : un lien a un href, un titre un level, un tableau de résultats un caption ou des columns, un
    /// composant en police à chasse fixe est du code.
    /// </summary>
    public sealed partial class AnssiChecklist(string page)
    {
        private readonly HashSet<string> _monospace = MonospaceComponents(page);

        /// <summary>Le point de ce nom, ou null s'il n'est plus dans la checklist.</summary>
        public AnssiPoint? Find(string id)
        {
            var obj = Entry(id);
            if (obj == null) return null;
            var props = Js.Object(obj);
            var title = props.TryGetValue("title", out var t) ? Js.Object(t) : [];
            var body = Languages(props.GetValueOrDefault("body"));
            var reco = Languages(Resolve(props.GetValueOrDefault("reco")));
            var dates = props.TryGetValue("changelog", out var changelog)
                ? DateRegex().Matches(changelog).Select(m => m.Groups[1].Value).Order().ToList() : [];
            var levels = props.TryGetValue("grades", out var grades) ? LevelRegex().Matches(grades).Select(g => int.Parse(g.Value)).ToList() : [];
            return new AnssiPoint(id,
                Clean(Render(title.GetValueOrDefault("fr") ?? "")), Clean(Render(title.GetValueOrDefault("en") ?? "")),
                levels, dates.FirstOrDefault(), dates.LastOrDefault(),
                Text(body.Fr, "fr"), Text(body.En, "en"), Text(reco.Fr, "fr"), Text(reco.En, "en"));
        }

        /// <summary>Le Markdown d'un texte ; un tableau des objets trouvés par l'analyse devient une mention dans sa langue.</summary>
        private string Text(string expr, string lang) => Clean(Render(expr).Replace(TableMark, lang == "fr"
            ? "*(Tableau des objets concernés, dans le rapport d'analyse.)*"
            : "*(Table of the affected objects, in the analysis report.)*"));

        private const string TableMark = "\u0001";

        /// <summary>L'objet du point : écrit sur place (« id:{title:… ») ou dans une variable (« id:X » et « X={title:… »).</summary>
        private string? Entry(string id)
        {
            foreach (Match m in Regex.Matches(page, $@"(?<![\w$]){Regex.Escape(id)}:(\{{title:|[\w$]+(?=[,}}]))"))
            {
                if (m.Groups[1].Value.StartsWith('{'))
                {
                    var start = m.Groups[1].Index;
                    return page[start..(Js.Close(page, start) + 1)];
                }
                var variable = Regex.Match(page, $@"(?<![\w$.]){Regex.Escape(m.Groups[1].Value)}=\{{title:");
                if (variable.Success)
                {
                    var start = variable.Index + variable.Length - "{title:".Length;
                    return page[start..(Js.Close(page, start) + 1)];
                }
            }
            return null;
        }

        /// <summary>« e=>X » : la valeur de la variable X ; sinon la valeur telle quelle.</summary>
        private string? Resolve(string? value)
        {
            if (value == null) return null;
            var arrow = ArrowVariableRegex().Match(value);
            if (!arrow.Success) return value;
            var definition = Regex.Match(page, $@"(?<![\w$.]){Regex.Escape(arrow.Groups[1].Value)}=");
            if (!definition.Success) return null;
            var start = definition.Index + definition.Length;
            while (start < page.Length && page[start] != '{' && page[start] != '(') start++;
            return page[start..(Js.Close(page, start) + 1)];
        }

        /// <summary>Les deux langues d'un texte : le premier objet « {fr:…,en:…} » de la valeur.</summary>
        private static (string Fr, string En) Languages(string? value)
        {
            if (value == null) return ("", "");
            var at = value.IndexOf("{fr:", StringComparison.Ordinal);
            if (at < 0) return ("", "");
            var obj = Js.Object(value[at..(Js.Close(value, at) + 1)]);
            return (obj.GetValueOrDefault("fr") ?? "", obj.GetValueOrDefault("en") ?? "");
        }

        /// <summary>Les composants qui écrivent leur contenu en police à chasse fixe : du code dans le Markdown.</summary>
        private static HashSet<string> MonospaceComponents(string page)
        {
            var names = new HashSet<string>();
            foreach (Match m in FunctionRegex().Matches(page))
            {
                var head = page.Substring(m.Index, Math.Min(260, page.Length - m.Index));
                if (head.Contains("monospace", StringComparison.Ordinal)) names.Add(m.Groups[1].Value);
            }
            return names;
        }

        /// <summary>Le Markdown d'une expression : chaîne, tableau, appel JSX ; une donnée calculée à l'affichage est omise.</summary>
        public string Render(string expr)
        {
            expr = expr.Trim();
            if (expr.Length == 0) return "";
            var c = expr[0];
            if (c is '`' or '"' or '\'') return Js.Literal(expr[..Js.SkipString(expr, 0)]);
            if (c == '[') return string.Concat(Js.Split(expr[1..Js.Close(expr, 0)]).Select(Render));
            var call = JsxRegex().Match(expr);
            if (call.Success)
            {
                var open = call.Index + call.Length - 1;
                var args = Js.Split(expr[(open + 1)..Js.Close(expr, open)]);
                return Element(args[0], args.Count > 1 ? Js.Object(args[1]) : []);
            }
            if (c == '(' && Js.Close(expr, 0) == expr.Length - 1) return Render(expr[1..^1]);
            var and = TopLevel(expr, "&&");
            if (and >= 0) return Render(expr[(and + 2)..]);
            // Un nombre, un pourcentage, un nom calculés quand l'analyse a des résultats
            return DataRegex().IsMatch(expr) ? "…" : "";
        }

        /// <summary>La dernière position de l'opérateur au premier niveau de l'expression, ou -1.</summary>
        private static int TopLevel(string expr, string op)
        {
            var depth = 0;
            var found = -1;
            for (var i = 0; i < expr.Length; i++)
            {
                var ch = expr[i];
                if (ch is '\'' or '"' or '`') { i = Js.SkipString(expr, i) - 1; continue; }
                if (ch is '(' or '[' or '{') depth++;
                else if (ch is ')' or ']' or '}') depth--;
                else if (depth == 0 && string.CompareOrdinal(expr, i, op, 0, op.Length) == 0) found = i;
            }
            return found;
        }

        private string Element(string type, Dictionary<string, string> props)
        {
            var children = props.TryGetValue("children", out var ch) ? Render(ch) : "";
            string? Text(string key) => props.TryGetValue(key, out var v) && v.Length > 0 && v[0] is '`' or '"' or '\'' ? Js.Literal(v) : null;
            if (type[0] is '`' or '"' or '\'')
            {
                return Js.Literal(type) switch
                {
                    "p" => "\n\n" + Inline(children) + "\n\n",
                    "ul" or "ol" => "\n\n" + children.Trim('\n') + "\n\n",
                    "li" => Item(children),
                    "code" or "pre" or "kbd" or "samp" => Code(children),
                    "strong" or "b" => $"**{children.Trim()}**",
                    "em" or "i" => $"*{children.Trim()}*",
                    "a" => Link(children, Text("href")),
                    "h1" or "h2" or "h3" or "h4" or "h5" or "h6" => "\n\n#### " + Inline(children) + "\n\n",
                    "br" => "\n",
                    "table" or "thead" or "tbody" or "tr" or "td" or "th" or "colgroup" => "",
                    _ => children
                };
            }
            if (props.ContainsKey("href")) return Link(children, Text("href"));
            // Un titre a un niveau et un texte ; un niveau seul est la pastille d'un niveau ANSSI : son numéro
            if (props.TryGetValue("level", out var level)) return children.Trim().Length > 0 ? "\n\n#### " + Inline(children) + "\n\n" : $"**{level}**";
            // Un tableau ou une liste des objets trouvés par l'analyse : rien à reprendre hors d'un rapport
            if (props.ContainsKey("caption") || props.ContainsKey("columns") || props.ContainsKey("rows")) return "\n\n" + TableMark + "\n\n";
            if (props.ContainsKey("items")) return "";
            if (Text("SID") is { } sid) return Code(sid);
            if (props.ContainsKey("dn")) return Text("dn") is { } dn ? Code(dn) : "";
            if (_monospace.Contains(type)) return Code(children);
            return children;
        }

        private static string Inline(string text) => WhitespaceRegex().Replace(text.Trim(), " ");

        /// <summary>Un élément de liste ; une liste imbriquée dedans garde ses lignes, en retrait.</summary>
        private static string Item(string children)
        {
            var lines = children.Replace("\r", "").Split('\n').Select(l => l.TrimEnd()).Where(l => l.Trim().Length > 0).ToList();
            var nested = lines.FindIndex(l => l.TrimStart().StartsWith("- "));
            if (nested < 0) return "\n- " + Inline(children);
            var head = Inline(string.Join(" ", lines.Take(nested)));
            return "\n- " + head + string.Concat(lines.Skip(nested).Select(l => "\n  " + l));
        }

        private static string Code(string text) => text.Contains('`') ? text : $"`{text}`";

        private static string Link(string text, string? href) =>
            href == null ? text : $"[{(string.IsNullOrWhiteSpace(text) ? href : text.Trim())}]({href})";

        /// <summary>Paragraphes séparés d'une ligne vide, listes serrées, pas d'espaces en trop.</summary>
        public static string Clean(string markdown)
        {
            var text = markdown.Replace("\r", "");
            text = Regex.Replace(text, @"[ \t]+\n", "\n");
            text = Regex.Replace(text, @"\n{3,}", "\n\n");
            var sb = new StringBuilder();
            foreach (var line in text.Split('\n')) sb.Append(line.TrimEnd()).Append('\n');
            return Regex.Replace(sb.ToString(), @"\n{3,}", "\n\n").Trim();
        }

        [GeneratedRegex(@"^\(0,[\w$]+\.jsxs?\)\(")]
        private static partial Regex JsxRegex();

        [GeneratedRegex(@"function ([\w$]+)\(")]
        private static partial Regex FunctionRegex();

        [GeneratedRegex(@"^\s*[\w$]+\s*=>\s*([\w$]+)\s*$")]
        private static partial Regex ArrowVariableRegex();

        [GeneratedRegex(@"date:`(\d{4}-\d{2}-\d{2})`")]
        private static partial Regex DateRegex();

        [GeneratedRegex(@"\d+")]
        private static partial Regex LevelRegex();

        [GeneratedRegex(@"\s*\n\s*")]
        private static partial Regex WhitespaceRegex();

        [GeneratedRegex(@"^[\w$.?()\[\]+\-*/%, ]+$")]
        private static partial Regex DataRegex();
    }
}
