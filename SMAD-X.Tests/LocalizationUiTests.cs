using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using SMADX.Helpers;
using SMADX.Models;
using SMADX.Services;

namespace SMADX.Tests;

/// <summary>
/// Tout texte visible de SMAD-X existe en français et en anglais : les deux langues ont les mêmes clés,
/// et ni les vues ni les messages des ViewModels n'écrivent de texte en dur.
/// </summary>
public partial class LocalizationUiTests
{
    private static readonly string[] Cultures = ["fr-FR", "en-US"];

    // ── Clés ────────────────────────────────────────────────────────────────

    [Fact]
    public void EveryKey_ExistsInBothCultures()
    {
        var loc = LocalizationService.Instance;
        var fr = loc.KeysOf("fr-FR").ToHashSet();
        var en = loc.KeysOf("en-US").ToHashSet();

        Assert.NotEmpty(fr);
        AssertNone(fr.Except(en).Order().Select(k => "fr-FR sans en-US : " + k));
        AssertNone(en.Except(fr).Order().Select(k => "en-US sans fr-FR : " + k));
    }

    [Fact]
    public void EveryObjectType_HasALabelInBothCultures()
    {
        var loc = LocalizationService.Instance;
        foreach (var culture in Cultures)
            foreach (var type in Enum.GetValues<ADObjectType>())
                Assert.Contains($"ObjectType.{type}", loc.KeysOf(culture));
    }

    /// <summary>Chaque propriété de <see cref="LocalizationProxy"/> lit une clé qui existe dans les deux langues.</summary>
    [Fact]
    public void LocalizationProxy_ReadsExistingKeys()
    {
        var loc = LocalizationService.Instance;
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "SMAD-X", "Helpers", "LocalizationProxy.cs"));
        var keys = ProxyKeyRegex().Matches(source).Select(m => m.Groups[1].Value).ToList();

        Assert.NotEmpty(keys);
        foreach (var culture in Cultures)
            AssertNone(keys.Where(k => !loc.KeysOf(culture).Contains(k)).Select(k => $"{culture} : {k}"));
    }

    /// <summary>« {Binding Source={StaticResource Loc}, Path=X} » : X est bien une propriété du proxy (sinon le texte reste vide).</summary>
    [Fact]
    public void Views_BindOnlyExistingProxyProperties()
    {
        var properties = typeof(LocalizationProxy).GetProperties().Select(p => p.Name).ToHashSet();
        var missing = new List<string>();
        foreach (var file in ViewFiles())
            foreach (Match m in LocPathRegex().Matches(File.ReadAllText(file)))
                if (!properties.Contains(m.Groups[1].Value))
                    missing.Add($"{Path.GetFileName(file)} : {m.Groups[1].Value}");

        AssertNone(missing);
    }

    // ── Vues (XAML) ─────────────────────────────────────────────────────────

    /// <summary>Attributs XAML qui portent un texte affiché.</summary>
    private static readonly HashSet<string> TextAttributes =
        ["Text", "Header", "Content", "Title", "ToolTip.Tip", "PlaceholderText", "Watermark"];

    /// <summary>
    /// Valeurs littérales admises bien qu'elles contiennent une lettre : ce ne sont pas des mots à traduire.
    /// Garder cette liste minuscule, chaque entrée justifiée.
    /// </summary>
    private static readonly HashSet<string> AllowedLiterals =
    [
        "SMAD-X", // nom du produit, identique dans toutes les langues
    ];

    [Fact]
    public void Views_HaveNoHardCodedText()
    {
        var problems = new List<string>();
        foreach (var file in ViewFiles())
        {
            var name = Path.GetFileName(file);
            var document = XDocument.Load(file, LoadOptions.SetLineInfo);

            foreach (var element in document.Descendants())
            {
                foreach (var attribute in element.Attributes())
                {
                    if (!TextAttributes.Contains(attribute.Name.LocalName)) continue;
                    var value = attribute.Value;
                    // « {} » échappe une accolade : la suite est un littéral
                    var literal = value.StartsWith("{}", StringComparison.Ordinal) ? value[2..]
                        : value.StartsWith('{') ? null
                        : value;
                    if (literal != null && IsText(literal))
                        problems.Add($"{name}:{Line(attribute)} {attribute.Name.LocalName}=\"{value}\"");
                }
            }

            // Texte écrit entre deux balises (<ComboBoxItem>Texte</ComboBoxItem>, <ToolTip.Tip>Texte</ToolTip.Tip>…)
            foreach (var text in document.DescendantNodes().OfType<XText>())
                if (IsText(text.Value.Trim()))
                    problems.Add($"{name}:{Line(text)} <{text.Parent?.Name.LocalName}>{text.Value.Trim()}<");
        }

        AssertNone(problems);
    }

    /// <summary>Les valeurs écrites dans une liaison (FallbackValue, TargetNullValue, StringFormat) ne passent pas par la traduction.</summary>
    [Fact]
    public void Views_HaveNoHardCodedTextInBindings()
    {
        var problems = new List<string>();
        foreach (var file in ViewFiles())
        {
            var document = XDocument.Load(file, LoadOptions.SetLineInfo);
            foreach (var attribute in document.Descendants().Attributes())
            {
                if (!attribute.Value.StartsWith('{')) continue;
                foreach (Match m in BindingLiteralRegex().Matches(attribute.Value))
                {
                    var value = m.Groups["quoted"].Success ? m.Groups["quoted"].Value : m.Groups["bare"].Value;
                    if (bool.TryParse(value.Trim(), out _)) continue; // FallbackValue=False : une valeur, pas un texte
                    if (m.Groups["name"].Value == "StringFormat")
                        value = FormatPlaceholderRegex().Replace(value, string.Empty); // {0}, {0:yyyy-MM-dd}, {} ne sont pas du texte
                    if (IsText(value))
                        problems.Add($"{Path.GetFileName(file)}:{Line(attribute)} {m.Value}");
                }
            }
        }

        AssertNone(problems);
    }

    /// <summary>
    /// Une colonne de DataGrid se lie en écriture par défaut : traduite, sa valeur repartirait vers le modèle et
    /// y remettrait la valeur par défaut (toutes les délégations devenaient « réinitialisation du mot de passe »).
    /// </summary>
    [Fact]
    public void TranslatedGridColumns_AreBoundOneWay()
    {
        var problems = new List<string>();
        foreach (var file in ViewFiles())
            foreach (var column in XDocument.Load(file, LoadOptions.SetLineInfo).Descendants().Where(e => e.Name.LocalName == "DataGridTextColumn"))
                if (column.Attribute("Binding")?.Value is { } binding && binding.Contains("Converter=") && !binding.Contains("Mode=OneWay"))
                    problems.Add($"{Path.GetFileName(file)}:{Line(column.Attribute("Binding")!)} {binding}");

        AssertNone(problems);
    }

    // ── ViewModels et graphe (C#) ───────────────────────────────────────────

    /// <summary>
    /// Propriétés de texte affiché (StatusMessage, StatsText, SelectedNodeInfo, Label…) : la valeur affectée ne contient
    /// aucune chaîne littérale avec une lettre, hormis les clés de traduction (Loc["Clé"]).
    /// </summary>
    [Fact]
    public void ViewModels_HaveNoHardCodedMessages()
    {
        var problems = new List<string>();
        foreach (var file in SourceFiles("SMAD-X", "ViewModels").Concat(SourceFiles("SMAD-X", "Graph")))
        {
            var source = File.ReadAllText(file);
            var scan = CSharpScan.Run(source);
            foreach (Match m in DisplayedAssignmentRegex().Matches(scan.Masked))
            {
                var start = m.Index + m.Length;
                var end = scan.StatementEnd(start);
                foreach (var literal in scan.Literals.Where(l => l.Start >= start && l.Start < end && !l.IsKey && IsText(l.Text)))
                    problems.Add($"{Path.GetFileName(file)}:{LineOf(source, literal.Start)} {m.Groups[1].Value} = \"{literal.Text}\"");
            }
        }

        AssertNone(problems);
    }

    /// <summary>Le graphe dessine ses textes lui-même : aucune chaîne avec une lettre hors des clés de traduction.</summary>
    [Fact]
    public void Graph_DrawsOnlyTranslatedText()
    {
        var problems = new List<string>();
        foreach (var file in SourceFiles("SMAD-X", "Graph"))
        {
            var source = File.ReadAllText(file);
            foreach (var literal in CSharpScan.Run(source).Literals.Where(l => !l.IsKey && IsText(l.Text)))
                problems.Add($"{Path.GetFileName(file)}:{LineOf(source, literal.Start)} \"{literal.Text}\"");
        }

        AssertNone(problems);
    }

    // ── Outils ──────────────────────────────────────────────────────────────

    /// <summary>Échoue en listant tous les problèmes (Assert.Empty tronque la liste).</summary>
    private static void AssertNone(IEnumerable<string> problems)
    {
        var list = problems.ToList();
        Assert.True(list.Count == 0, $"{list.Count} problème(s) :{Environment.NewLine}{string.Join(Environment.NewLine, list)}");
    }

    /// <summary>Texte à traduire : au moins une lettre, et pas un littéral admis.</summary>
    private static bool IsText(string value) =>
        value.Any(char.IsLetter) && !AllowedLiterals.Contains(value.Trim());

    private static int Line(IXmlLineInfo node) => node.LineNumber;

    private static int LineOf(string source, int index) => source.AsSpan(0, index).Count('\n') + 1;

    private static IEnumerable<string> ViewFiles() =>
        Directory.GetFiles(Path.Combine(RepoRoot(), "SMAD-X", "Views"), "*.axaml").Order();

    private static IEnumerable<string> SourceFiles(params string[] folder) =>
        Directory.GetFiles(Path.Combine([RepoRoot(), .. folder]), "*.cs").Order();

    /// <summary>
    /// Racine du dépôt (dossier de SMAD-X.slnx), en remontant depuis les binaires des tests ; si les binaires sont
    /// construits hors du dépôt (--artifacts-path), depuis l'emplacement de ce fichier source.
    /// </summary>
    private static string RepoRoot()
    {
        return Find(AppContext.BaseDirectory) ?? Find(Path.GetDirectoryName(ThisFile())!)
            ?? throw new DirectoryNotFoundException("SMAD-X.slnx introuvable au-dessus de " + AppContext.BaseDirectory);

        static string? Find(string start)
        {
            for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
                if (File.Exists(Path.Combine(dir.FullName, "SMAD-X.slnx")))
                    return dir.FullName;
            return null;
        }
    }

    private static string ThisFile([CallerFilePath] string path = "") => path;

    [GeneratedRegex(@"_localizationService\[""([^""]+)""\]")]
    private static partial Regex ProxyKeyRegex();

    [GeneratedRegex(@"Source=\{StaticResource Loc\},\s*Path=(\w+)")]
    private static partial Regex LocPathRegex();

    [GeneratedRegex(@"(?<name>FallbackValue|TargetNullValue|StringFormat)=(?:'(?<quoted>[^']*)'|(?<bare>[^,}']*))")]
    private static partial Regex BindingLiteralRegex();

    [GeneratedRegex(@"\{[^{}]*\}")]
    private static partial Regex FormatPlaceholderRegex();

    /// <summary>Affectation d'une propriété de texte affiché : « StatusMessage = », « Label => », « Text = » dans un initialiseur…</summary>
    [GeneratedRegex(@"\b(\w*(?:Message|Text|Info|Status|Summary|Label|Title))\s*=>?(?!=)")]
    private static partial Regex DisplayedAssignmentRegex();

    /// <summary>
    /// Lecture sommaire d'un fichier C# : chaînes littérales (y compris interpolées, avec le texte hors des trous)
    /// et source « masquée » où les commentaires et le texte des chaînes sont remplacés par des espaces
    /// (le code des trous d'interpolation reste lisible, pour reconnaître Loc["Clé"] jusque dans un $"…").
    /// </summary>
    private sealed class CSharpScan
    {
        public sealed record Literal(int Start, string Text, bool IsKey);

        public List<Literal> Literals { get; } = new();
        public string Masked { get; private set; } = string.Empty;

        private string _source = string.Empty;
        private StringBuilder _masked = new();
        private readonly List<(int Start, string Text)> _found = new();

        public static CSharpScan Run(string source)
        {
            var scan = new CSharpScan { _source = source, _masked = new StringBuilder(source) };
            scan.ScanCode(0, topLevel: true);
            scan.Masked = scan._masked.ToString();
            scan.Literals.AddRange(scan._found.Select(f => new Literal(f.Start, f.Text, scan.IsKeyAt(f.Start))));
            return scan;
        }

        /// <summary>Fin de l'instruction commencée en <paramref name="start"/> : « ; », ou « , » / fermeture au niveau 0.</summary>
        public int StatementEnd(int start)
        {
            var depth = 0;
            for (var i = start; i < Masked.Length; i++)
            {
                switch (Masked[i])
                {
                    case '(' or '[' or '{': depth++; break;
                    case ')' or ']' or '}':
                        if (--depth < 0) return i;
                        break;
                    case ';': return i;
                    case ',' when depth == 0: return i;
                }
            }
            return Masked.Length;
        }

        /// <summary>Parcourt du code ; dans un trou d'interpolation, s'arrête à l'accolade fermante.</summary>
        private int ScanCode(int i, bool topLevel)
        {
            var depth = 0;
            while (i < _source.Length)
            {
                var c = _source[i];
                var next = i + 1 < _source.Length ? _source[i + 1] : '\0';
                if (c == '/' && next == '/')
                {
                    var end = _source.IndexOf('\n', i);
                    end = end < 0 ? _source.Length : end;
                    Blank(i, end);
                    i = end;
                }
                else if (c == '/' && next == '*')
                {
                    var end = _source.IndexOf("*/", i + 2, StringComparison.Ordinal);
                    end = end < 0 ? _source.Length : end + 2;
                    Blank(i, end);
                    i = end;
                }
                else if (c == '\'')
                {
                    var end = i + 1;
                    while (end < _source.Length && _source[end] != '\'')
                        end += _source[end] == '\\' ? 2 : 1;
                    Blank(i, end + 1);
                    i = end + 1;
                }
                else if (c == '"' || (c is '$' or '@' && (next == '"' || (next is '$' or '@' && i + 2 < _source.Length && _source[i + 2] == '"'))))
                {
                    i = ScanString(i);
                }
                else if (!topLevel && c == '{') { depth++; i++; }
                else if (!topLevel && c == '}')
                {
                    if (depth == 0) return i;
                    depth--;
                    i++;
                }
                else i++;
            }
            return i;
        }

        /// <summary>Lit une chaîne commençant en <paramref name="start"/> et renvoie l'indice qui la suit.</summary>
        private int ScanString(int start)
        {
            var i = start;
            bool interpolated = false, verbatim = false;
            while (_source[i] != '"')
            {
                interpolated |= _source[i] == '$';
                verbatim |= _source[i] == '@';
                i++;
            }
            i++; // guillemet ouvrant
            Blank(start, i);

            // Le texte de la chaîne est masqué ; le code des trous d'interpolation reste lisible (accolades comprises)
            var text = new StringBuilder();
            while (i < _source.Length)
            {
                var c = _source[i];
                var next = i + 1 < _source.Length ? _source[i + 1] : '\0';
                var from = i;
                if (verbatim && c == '"' && next == '"') { text.Append('"'); i += 2; }
                else if (c == '"') { Blank(i, i + 1); i++; break; }
                else if (!verbatim && c == '\\')
                {
                    // Séquence d'échappement (\n, \", —…) : pas une lettre du texte
                    text.Append(' ');
                    i += next switch { 'u' => 6, 'U' => 10, _ => 2 };
                }
                else if (interpolated && c == '{' && next == '{') { text.Append('{'); i += 2; }
                else if (interpolated && c == '}' && next == '}') { text.Append('}'); i += 2; }
                else if (interpolated && c == '{')
                {
                    // Trou d'interpolation : du code, dont les chaînes sont relevées pour elles-mêmes
                    i = ScanCode(i + 1, topLevel: false) + 1;
                    text.Append(' ');
                    continue;
                }
                else { text.Append(c); i++; }
                Blank(from, i);
            }

            _found.Add((start, text.ToString()));
            return i;
        }

        /// <summary>
        /// Chaîne qui n'est pas un texte affiché : clé passée à un indexeur (Loc["Clé"], Loc[exam ? "A" : "B"],
        /// loc[$"ObjectType.{t}"]), à Translate, ou format passé à ToString("HH:mm").
        /// </summary>
        private bool IsKeyAt(int start)
        {
            var depth = 0;
            for (var j = start - 1; j >= 0; j--)
            {
                var c = Masked[j];
                if (c is ')' or ']' or '}') depth++;
                else if (c is '(' or '[' or '{' && depth > 0) depth--;
                else if (c == '[') return true;
                else if (c == '(')
                {
                    var before = Masked.AsSpan(0, j).TrimEnd();
                    return before.EndsWith("Translate", StringComparison.Ordinal) || before.EndsWith("ToString", StringComparison.Ordinal);
                }
                else if (c is '{' or ';') return false;
            }
            return false;
        }

        private void Blank(int start, int end)
        {
            for (var k = start; k < Math.Min(end, _masked.Length); k++)
                if (_masked[k] != '\n') _masked[k] = ' ';
        }
    }
}
