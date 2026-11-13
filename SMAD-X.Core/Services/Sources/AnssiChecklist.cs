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
    /// composant en police à chasse fixe est du code, un groupe désigné par son SID prend son nom dans la table de la page.
    /// Un encadré partagé par plusieurs points (« Contexte : groupes privilégiés », « Seuils de tolérance »…) est un
    /// composant appelé avec sa seule langue : il est ouvert, son texte est choisi pour la langue de la fiche, et il devient
    /// une citation (« > ») dans le Markdown.
    /// </summary>
    public sealed partial class AnssiChecklist(string page)
    {
        private readonly HashSet<string> _monospace = MonospaceComponents(page);
        private readonly Dictionary<string, (string Fr, string En)?> _groups = new();
        private readonly Dictionary<string, string?> _definitions = new();

        /// <summary>
        /// Ce que valent les variables du code rendu : la langue de la fiche, et dans un composant ouvert ses paramètres
        /// (sa langue, ses options : une option non donnée est fausse). La profondeur borne les composants ouverts.
        /// </summary>
        private sealed record Scope(string Lang, IReadOnlyDictionary<string, object?> Vars, int Depth)
        {
            public static Scope For(string lang) => new(lang, new Dictionary<string, object?>(), 0);
        }

        /// <summary>Le point de ce nom, ou null s'il n'est plus dans la checklist.</summary>
        public AnssiPoint? Find(string id)
        {
            var obj = Entry(id);
            if (obj == null) return null;
            var props = Js.Object(obj);
            var title = props.TryGetValue("title", out var t) ? Js.Object(t) : [];
            var body = Bodies(props.GetValueOrDefault("body"));
            var reco = Bodies(Resolve(props.GetValueOrDefault("reco")));
            var dates = props.TryGetValue("changelog", out var changelog)
                ? DateRegex().Matches(changelog).Select(m => m.Groups[1].Value).Order().ToList() : [];
            var levels = props.TryGetValue("grades", out var grades) ? LevelRegex().Matches(grades).Select(g => int.Parse(g.Value)).ToList() : [];
            return new AnssiPoint(id,
                Clean(Render(title.GetValueOrDefault("fr") ?? "", Scope.For("fr"))), Clean(Render(title.GetValueOrDefault("en") ?? "", Scope.For("en"))),
                levels, dates.FirstOrDefault(), dates.LastOrDefault(),
                Text(body.Fr, "fr", body.Vars), Text(body.En, "en", body.Vars), Text(reco.Fr, "fr", reco.Vars), Text(reco.En, "en", reco.Vars));
        }

        /// <summary>Le Markdown d'un texte ; un tableau des objets trouvés par l'analyse devient une mention dans sa langue.</summary>
        private string Text(string expr, string lang, IReadOnlyDictionary<string, object?> vars) => Clean(Render(expr, new Scope(lang, vars, 0)).Replace(TableMark, lang == "fr"
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

        /// <summary>
        /// Les deux langues d'un texte, et ce que valent ses variables : « e=>({fr:…,en:…}) », ou l'appel d'une fonction
        /// modèle « rA(`légende`,`caption`,texteFr,texteEn) » dont le corps rend « {fr:…,en:…} » à partir de ses
        /// paramètres ; chaque paramètre vaut alors l'argument donné (un texte JSX), rendu là où la fonction le place.
        /// </summary>
        private (string Fr, string En, IReadOnlyDictionary<string, object?> Vars) Bodies(string? value)
        {
            var none = new Dictionary<string, object?>();
            if (value == null) return ("", "", none);
            var text = value.Trim();
            var call = CallRegex().Match(text);
            if (call.Success && Js.Close(text, call.Length - 1) == text.Length - 1)
            {
                var definition = Regex.Match(page, $@"function {Regex.Escape(call.Groups[1].Value)}\(");
                if (definition.Success)
                {
                    var args = Js.Split(text[call.Length..^1]);
                    var open = definition.Index + definition.Length - 1;
                    var close = Js.Close(page, open);
                    var vars = new Dictionary<string, object?>();
                    var parameters = Js.Split(page[(open + 1)..close]);
                    for (var i = 0; i < parameters.Count; i++)
                    {
                        var name = parameters[i].Split('=')[0].Trim();
                        if (IdentifierRegex().IsMatch(name)) vars[name] = i < args.Count ? new Bound(args[i]) : null;
                    }
                    var bodyStart = page.IndexOf('{', close + 1);
                    var (fr, en) = Languages(page[bodyStart..(Js.Close(page, bodyStart) + 1)]);
                    return (fr, en, vars);
                }
            }
            var (f, e) = Languages(value);
            return (f, e, none);
        }

        /// <summary>Une expression du code laissée telle quelle (l'argument d'une fonction modèle), rendue quand on la rencontre.</summary>
        private sealed record Bound(string Text);

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

        /// <summary>Le Markdown d'une expression, en français.</summary>
        public string Render(string expr) => Render(expr, Scope.For("fr"));

        /// <summary>
        /// Le Markdown d'une expression : chaîne, tableau, appel JSX, condition sur la langue ; une donnée calculée à
        /// l'affichage devient « … », une condition qui dépend des résultats de l'analyse est omise.
        /// </summary>
        private string Render(string expr, Scope scope)
        {
            expr = expr.Trim();
            if (expr.Length == 0) return "";
            if (scope.Vars.TryGetValue(expr, out var bound) && bound is Bound argument) return Render(argument.Text, scope);
            if (Ternary(expr) is { } choice)
                return Truthy(choice.Cond, scope) switch
                {
                    true => Render(choice.Then, scope),
                    false => Render(choice.Else, scope),
                    null => ""
                };
            var c = expr[0];
            if (c is '`' or '"' or '\'') return Js.Literal(expr[..Js.SkipString(expr, 0)]);
            if (c == '[') return string.Concat(Js.Split(expr[1..Js.Close(expr, 0)]).Select(e => Render(e, scope)));
            var call = JsxRegex().Match(expr);
            if (call.Success)
            {
                var open = call.Index + call.Length - 1;
                var args = Js.Split(expr[(open + 1)..Js.Close(expr, open)]);
                return Element(args[0], args.Count > 1 ? Js.Object(args[1]) : [], scope);
            }
            if (c == '(' && Js.Close(expr, 0) == expr.Length - 1) return Render(expr[1..^1], scope);
            // Une fonction qui enrobe un appel JSX (« l((0,I.jsx)(…)) ») : ce qu'elle enrobe
            var wrapper = CallRegex().Match(expr);
            if (wrapper.Success && Js.Close(expr, wrapper.Length - 1) == expr.Length - 1
                && Js.Split(expr[wrapper.Length..^1]) is [var wrapped] && wrapped.StartsWith("(0,", StringComparison.Ordinal))
                return Render(wrapped, scope);
            var and = TopLevel(expr, "&&");
            // « condition && texte » : le texte si la condition est vraie. Une condition qui dépend du rapport d'analyse (ses
            // données, l'onglet du graphe) est fausse sur la checklist, qui ne montre pas ce texte : la fiche non plus
            if (and >= 0) return Truthy(expr[..and], scope) == true ? Render(expr[(and + 2)..], scope) : "";
            if (c == 'n' && expr == "null") return "";
            // Un texte partagé par plusieurs points, rangé dans une variable : « Uk.fr », « gA »
            if (Shared(expr, scope) is { } shared) return shared;
            // Un nombre, un pourcentage, un nom calculés quand l'analyse a des résultats
            return DataRegex().IsMatch(expr) ? "…" : "";
        }

        /// <summary>
        /// Un texte rangé dans une variable de la page : « X.fr » ou « X[e] » (un objet {fr:…,en:…}, dans la langue de la
        /// fiche), ou « X » (un appel JSX, une chaîne, un tableau, ou un objet {fr:…,en:…}). Null si ce n'est pas un texte :
        /// une donnée calculée, un paramètre du composant ouvert.
        /// </summary>
        private string? Shared(string expr, Scope scope)
        {
            if (scope.Depth >= 6) return null;
            var member = MemberRegex().Match(expr);
            string name;
            string? key = null;
            if (member.Success)
            {
                name = member.Groups[1].Value;
                key = member.Groups[2].Success ? member.Groups[2].Value
                    : scope.Vars.TryGetValue(member.Groups[3].Value, out var lang) ? lang as string : null;
                if (key is not ("fr" or "en")) return null;
            }
            else if (IdentifierRegex().IsMatch(expr) && expr.Length >= 2 && !scope.Vars.ContainsKey(expr)) name = expr;
            else return null;
            var value = Definition(name);
            if (value == null) return null;
            var inner = scope with { Depth = scope.Depth + 1 };
            if (value.StartsWith('{'))
            {
                var obj = Js.Object(value);
                return obj.TryGetValue(key ?? scope.Lang, out var text) ? Render(text, inner) : null;
            }
            return key == null ? Render(value, inner) : null;
        }

        /// <summary>
        /// La valeur d'une variable de la page (« X={fr:…} », « X=(0,I.jsx)(…) », « X=`…` », « X=[…] ») : la première
        /// définition qui est un texte ; null sinon.
        /// </summary>
        private string? Definition(string name)
        {
            if (_definitions.TryGetValue(name, out var cached)) return cached;
            string? found = null;
            foreach (Match m in Regex.Matches(page, $@"(?<![\w$.]){Regex.Escape(name)}=(?![=>])"))
            {
                var start = m.Index + m.Length;
                if (start >= page.Length) break;
                var head = page[start];
                if (page.AsSpan(start).StartsWith("(0,"))
                {
                    var end = Js.Close(page, start);
                    if (end + 1 < page.Length && page[end + 1] == '(') end = Js.Close(page, end + 1);
                    found = page[start..(end + 1)];
                }
                else if (page.AsSpan(start).StartsWith("{fr:")) found = page[start..(Js.Close(page, start) + 1)];
                else if (head == '`') found = page[start..Js.SkipString(page, start)];
                else if (head == '[') found = page[start..(Js.Close(page, start) + 1)];
                if (found != null) break;
            }
            _definitions[name] = found;
            return found;
        }

        /// <summary>
        /// Une expression « condition ? oui : non » au premier niveau : ses trois parties. « ?. » et « ?? » n'en sont pas ;
        /// une condition imbriquée dans « oui » garde son propre « : ».
        /// </summary>
        private static (string Cond, string Then, string Else)? Ternary(string expr)
        {
            int depth = 0, question = -1, nested = 0;
            for (var i = 0; i < expr.Length; i++)
            {
                var ch = expr[i];
                if (ch is '\'' or '"' or '`') { i = Js.SkipString(expr, i) - 1; continue; }
                if (ch is '(' or '[' or '{') depth++;
                else if (ch is ')' or ']' or '}') depth--;
                else if (depth == 0 && ch == '?')
                {
                    var next = i + 1 < expr.Length ? expr[i + 1] : '\0';
                    if (next == '?' || (next == '.' && !(i + 2 < expr.Length && char.IsDigit(expr[i + 2])))) { i++; continue; }
                    if (question < 0) question = i;
                    else nested++;
                }
                else if (depth == 0 && ch == ':' && question >= 0)
                {
                    if (nested > 0) { nested--; continue; }
                    return (expr[..question].Trim(), expr[(question + 1)..i].Trim(), expr[(i + 1)..].Trim());
                }
            }
            return null;
        }

        /// <summary>
        /// Ce que vaut une condition, si elle ne dépend que de la langue ou des paramètres d'un composant ouvert
        /// (« e===`fr` », « t », « !t », « n » calculé plus haut) ; null si elle dépend des résultats de l'analyse.
        /// </summary>
        private static bool? Truthy(string cond, Scope scope) => Evaluate(cond, scope) is (true, var value) ? IsTrue(value) : null;

        private static bool IsTrue(object? value) => value switch
        {
            null => false,
            bool b => b,
            double d => d != 0 && !double.IsNaN(d),
            string s => s.Length > 0,
            _ => true
        };

        /// <summary>
        /// La valeur d'une expression simple : chaîne, nombre, « !0 » et « !1 » (vrai, faux), null, variable connue,
        /// comparaisons (=== !== == !=), « && », « || » et « ! ». Known est faux pour tout le reste (une donnée calculée).
        /// </summary>
        private static (bool Known, object? Value) Evaluate(string expr, Scope scope)
        {
            expr = expr.Trim();
            while (expr.StartsWith('(') && Js.Close(expr, 0) == expr.Length - 1) expr = expr[1..^1].Trim();
            if (expr.Length == 0) return (false, null);
            var or = TopLevel(expr, "||");
            if (or > 0)
            {
                var left = Evaluate(expr[..or], scope);
                if (!left.Known) return (false, null);
                return IsTrue(left.Value) ? left : Evaluate(expr[(or + 2)..], scope);
            }
            var and = TopLevel(expr, "&&");
            if (and > 0)
            {
                var left = Evaluate(expr[..and], scope);
                if (!left.Known) return (false, null);
                return IsTrue(left.Value) ? Evaluate(expr[(and + 2)..], scope) : left;
            }
            foreach (var op in new[] { "!==", "===", "!=", "==" })
            {
                var at = TopLevel(expr, op);
                // « == » ne doit pas être le bout d'un « === » ou d'un « !== »
                if (at <= 0 || (op.Length == 2 && (expr[at - 1] is '=' or '!' || (at + 2 < expr.Length && expr[at + 2] == '=')))) continue;
                var a = Evaluate(expr[..at], scope);
                var b = Evaluate(expr[(at + op.Length)..], scope);
                if (!a.Known || !b.Known) return (false, null);
                var equal = op.Length == 3 ? StrictEquals(a.Value, b.Value) : LooseEquals(a.Value, b.Value);
                return (true, op[0] == '!' ? !equal : equal);
            }
            if (expr == "!0") return (true, true);
            if (expr == "!1") return (true, false);
            if (expr[0] == '!')
            {
                var inner = Evaluate(expr[1..], scope);
                return inner.Known ? (true, !IsTrue(inner.Value)) : (false, null);
            }
            if (expr[0] is '`' or '"' or '\'')
                return Js.SkipString(expr, 0) == expr.Length && !(expr[0] == '`' && expr.Contains("${")) ? (true, Js.Literal(expr)) : (false, null);
            if (double.TryParse(expr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var number)) return (true, number);
            if (expr is "null" or "void 0" or "undefined") return (true, null);
            if (expr is "true" or "false") return (true, expr == "true");
            if (IdentifierRegex().IsMatch(expr) && scope.Vars.TryGetValue(expr, out var value)) return (true, value);
            return (false, null);
        }

        private static bool StrictEquals(object? a, object? b) => (a, b) switch
        {
            (null, null) => true,
            (string x, string y) => x == y,
            (double x, double y) => x == y,
            (bool x, bool y) => x == y,
            _ => false
        };

        private static bool LooseEquals(object? a, object? b) => (a, b) switch
        {
            (null, null) => true,
            (null, _) or (_, null) => false,
            (string x, double y) => double.TryParse(x, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var n) && n == y,
            (double x, string y) => LooseEquals(b, a),
            (bool x, _) => LooseEquals(x ? 1d : 0d, b),
            (_, bool y) => LooseEquals(a, y ? 1d : 0d),
            _ => StrictEquals(a, b)
        };

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

        private string Element(string type, Dictionary<string, string> props, Scope scope)
        {
            var children = props.TryGetValue("children", out var ch) ? Render(ch, scope) : "";
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
            // Un groupe désigné par son SID : son nom dans la langue de la fiche, en italique comme sur la page
            if (Text("SID") is { } sid) return Group(sid) is { } names ? $"*{(( Text("lang") ?? scope.Lang) == "en" ? names.En : names.Fr)}*" : Code(sid);
            if (props.ContainsKey("dn")) return Text("dn") is { } dn ? Code(dn) : "";
            // Une pastille qui affiche sa valeur (le nom d'un droit dangereux, « WRITE_SPN ») : du code
            if (!props.ContainsKey("children") && Text("value") is { } shown) return Code(shown);
            // Du code passé en propriété : en ligne, ou en bloc s'il tient sur plusieurs lignes
            if (!props.ContainsKey("children") && props.TryGetValue("code", out var source) && StringOf(source) is { } code)
                return code.Contains('\n') ? "\n\n```\n" + code.Trim('\n') + "\n```\n\n" : Code(code);
            if (_monospace.Contains(type)) return Code(children);
            // Un composant appelé sans aucune propriété (« FO » : un exemple de code) : on l'ouvre
            if (props.Count == 0 && Parameterless(type) is { } content && scope.Depth < 6) return Render(content, scope with { Depth = scope.Depth + 1 });
            // Un masque d'accès : la liste des droits qu'il accorde, avec les libellés de la page
            if (props.TryGetValue("accessMask", out var mask) && long.TryParse(mask, out var bits))
                return AccessRights(type, bits, Value(props.GetValueOrDefault("lang")) as string ?? scope.Lang);
            // Un encadré partagé, appelé avec sa seule langue : on l'ouvre
            if (props.ContainsKey("lang") && !props.ContainsKey("children")) return Box(type, props, scope);
            return children;
        }

        /// <summary>
        /// Un composant appelé avec sa langue et sans contenu (un encadré partagé par plusieurs points) : sa définition
        /// « function X({lang:e,noContext:t}){…return …} » est rendue, ses paramètres valant ce que l'appel leur donne, et
        /// le résultat devient une citation. Un encadré sans texte (une légende calculée) est omis.
        /// </summary>
        private string Box(string type, Dictionary<string, string> props, Scope scope)
        {
            if (scope.Depth >= 4) return "";
            var definition = Regex.Match(page, $@"function {Regex.Escape(type)}\(\{{");
            if (!definition.Success) return "";
            var parametersStart = definition.Index + definition.Length - 1;
            var parametersEnd = Js.Close(page, parametersStart);
            var vars = new Dictionary<string, object?>();
            foreach (var part in Js.Split(page[(parametersStart + 1)..parametersEnd]))
            {
                var parameter = ParameterRegex().Match(part);
                if (!parameter.Success) continue;
                var given = props.TryGetValue(parameter.Groups[1].Value, out var value) ? value
                    : parameter.Groups[3].Success ? parameter.Groups[3].Value : null;
                vars[parameter.Groups[2].Value] = Value(given);
            }
            var bodyStart = page.IndexOf('{', parametersEnd + 1);
            if (bodyStart < 0) return "";
            var inner = new Scope(Value(props.GetValueOrDefault("lang")) as string ?? scope.Lang, vars, scope.Depth + 1);
            foreach (var statement in Statements(page[(bodyStart + 1)..Js.Close(page, bodyStart)]))
            {
                // « let n=e!==`0`&&… » : une valeur que la suite teste
                var declaration = LetRegex().Match(statement);
                if (declaration.Success)
                {
                    foreach (var part in Js.Split(statement[declaration.Length..]))
                    {
                        var assign = AssignRegex().Match(part);
                        if (assign.Success && Evaluate(part[assign.Length..], inner) is (true, var computed)) vars[assign.Groups[1].Value] = computed;
                    }
                    continue;
                }
                var guard = GuardRegex().Match(statement);
                if (guard.Success)
                {
                    var open = guard.Index + guard.Length - 1;
                    var close = Js.Close(statement, open);
                    if (statement[(close + 1)..].Trim() == "return null" && Truthy(statement[(open + 1)..close], inner) == true) return "";
                    continue;
                }
                if (!statement.StartsWith("return", StringComparison.Ordinal)) continue;
                var text = Clean(Render(statement["return".Length..], inner));
                if (!text.Any(char.IsLetter)) return "";
                // Un mot ou une courte phrase (« Désactivé ») reste dans la phrase ; un encadré devient une citation
                if (!text.Contains('\n') && !text.StartsWith('#')) return text;
                return "\n\n" + string.Join("\n", text.Split('\n').Select(l => l.Length == 0 ? ">" : "> " + l)) + "\n\n";
            }
            return "";
        }

        /// <summary>
        /// Les droits d'un masque d'accès, tels que le composant qui l'affiche les nomme : il parcourt un tableau de lignes
        /// « {mask:16,fr:`Propriété de lecture`,en:`Read property`} » et coche celles dont le bit est dans le masque.
        /// </summary>
        private string AccessRights(string type, long bits, string lang)
        {
            var definition = Regex.Match(page, $@"function {Regex.Escape(type)}\(");
            if (!definition.Success) return "";
            var bodyStart = page.IndexOf('{', Js.Close(page, definition.Index + definition.Length - 1) + 1);
            var body = page[bodyStart..(Js.Close(page, bodyStart) + 1)];
            foreach (Match table in MapRegex().Matches(body))
            {
                if (Definition(table.Groups[1].Value) is not { } rows || !rows.StartsWith('[')) continue;
                var rights = MaskRegex().Matches(rows)
                    .Select(m => Js.Object(rows[m.Index..(Js.Close(rows, m.Index) + 1)]))
                    .Where(o => o.TryGetValue("mask", out var m) && long.TryParse(m, out var bit) && (bits & bit) != 0 && o.ContainsKey(lang))
                    .Select(o => "- " + Js.Literal(o[lang]))
                    .ToList();
                if (rights.Count > 0) return "\n\n" + string.Join("\n", rights) + "\n\n";
            }
            return "";
        }

        /// <summary>Une chaîne : écrite telle quelle, ou rangée dans une variable de la page.</summary>
        private string? StringOf(string arg)
        {
            arg = arg.Trim();
            if (arg.Length > 0 && arg[0] is '`' or '"' or '\'') return Js.Literal(arg);
            return IdentifierRegex().IsMatch(arg) && Definition(arg) is { } value && value.StartsWith('`') ? Js.Literal(value) : null;
        }

        /// <summary>Ce que rend un composant sans paramètre : « X=()=>(0,I.jsx)(…) » ou « function X(){return …} ».</summary>
        private string? Parameterless(string name)
        {
            var arrow = Regex.Match(page, $@"(?<![\w$.]){Regex.Escape(name)}=\(\)=>");
            if (arrow.Success)
            {
                var start = arrow.Index + arrow.Length;
                if (page.AsSpan(start).StartsWith("(0,"))
                {
                    var end = Js.Close(page, start);
                    if (end + 1 < page.Length && page[end + 1] == '(') end = Js.Close(page, end + 1);
                    return page[start..(end + 1)];
                }
                if (page[start] == '(') return page[start..(Js.Close(page, start) + 1)];
                return null;
            }
            var function = Regex.Match(page, $@"function {Regex.Escape(name)}\(\)\{{return");
            if (!function.Success) return null;
            var bodyStart = function.Index + function.Length - "{return".Length;
            var body = page[(bodyStart + 1)..Js.Close(page, bodyStart)];
            return body.StartsWith("return", StringComparison.Ordinal) ? body["return".Length..] : null;
        }

        /// <summary>La valeur d'un argument (une chaîne, un nombre, « !0 », « !1 ») ; null pour une option absente ou une donnée calculée.</summary>
        private static object? Value(string? arg) => arg != null && Evaluate(arg, Scope.For("fr")) is (true, var value) ? value : null;

        /// <summary>Les instructions du corps d'une fonction, séparées par « ; » au premier niveau.</summary>
        private static List<string> Statements(string body)
        {
            var parts = new List<string>();
            var depth = 0;
            var start = 0;
            for (var i = 0; i < body.Length; i++)
            {
                var c = body[i];
                if (c is '\'' or '"' or '`') { i = Js.SkipString(body, i) - 1; continue; }
                if (c is '(' or '[' or '{') depth++;
                else if (c is ')' or ']' or '}') depth--;
                else if (c == ';' && depth == 0)
                {
                    parts.Add(body[start..i].Trim());
                    start = i + 1;
                }
            }
            if (start < body.Length) parts.Add(body[start..].Trim());
            return parts.Where(p => p.Length > 0).ToList();
        }

        /// <summary>Le nom d'un groupe d'après son SID, dans la table « "S-1-5-32-544":{fr:`Administrateurs`,en:`Administrators`} » de la page.</summary>
        private (string Fr, string En)? Group(string sid)
        {
            if (_groups.TryGetValue(sid, out var cached)) return cached;
            (string Fr, string En)? names = null;
            var entry = Regex.Match(page, $@"""{Regex.Escape(sid)}"":\{{fr:");
            if (entry.Success)
            {
                var start = entry.Index + entry.Length - "{fr:".Length;
                var obj = Js.Object(page[start..(Js.Close(page, start) + 1)]);
                if (obj.TryGetValue("fr", out var fr) && obj.TryGetValue("en", out var en) && fr.Length > 0 && en.Length > 0)
                    names = (Js.Literal(fr), Js.Literal(en));
            }
            _groups[sid] = names;
            return names;
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

        /// <summary>Un lien ; un lien interne au rapport d'analyse (« #/ad/… ») ne mène nulle part ailleurs : son texte seul.</summary>
        private static string Link(string text, string? href) =>
            href == null || href.StartsWith('#') ? text : $"[{(string.IsNullOrWhiteSpace(text) ? href : text.Trim())}]({href})";

        /// <summary>Paragraphes séparés d'une ligne vide, listes serrées, pas d'espaces en trop.</summary>
        public static string Clean(string markdown)
        {
            var text = markdown.Replace("\r", "");
            text = Regex.Replace(text, @"[ \t]+\n", "\n");
            text = Regex.Replace(text, @"\n{3,}", "\n\n");
            var sb = new StringBuilder();
            // Deux espaces au milieu d'une ligne (un saut de ligne devenu espace à côté d'une autre) : une seule
            foreach (var line in text.Split('\n')) sb.Append(InnerSpacesRegex().Replace(line.TrimEnd(), " ")).Append('\n');
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

        [GeneratedRegex(@"^[\w$]+$")]
        private static partial Regex IdentifierRegex();

        [GeneratedRegex(@"^([\w$]+)\s*:\s*([\w$]+)(?:\s*=\s*(.+))?$")]
        private static partial Regex ParameterRegex();

        [GeneratedRegex(@"^if\s*\(")]
        private static partial Regex GuardRegex();

        [GeneratedRegex(@"^([\w$]{2,})(?:\.(fr|en)|\[([\w$]+)\])$")]
        private static partial Regex MemberRegex();

        [GeneratedRegex(@"(?<=\S) {2,}(?=\S)")]
        private static partial Regex InnerSpacesRegex();

        [GeneratedRegex(@"^(let|const|var)\s+")]
        private static partial Regex LetRegex();

        [GeneratedRegex(@"^([\w$]+)\s*=(?!=)")]
        private static partial Regex AssignRegex();

        [GeneratedRegex(@"([\w$]+)\.map\(")]
        private static partial Regex MapRegex();

        [GeneratedRegex(@"\{mask:")]
        private static partial Regex MaskRegex();

        [GeneratedRegex(@"^([\w$]+)\(")]
        private static partial Regex CallRegex();
    }
}
