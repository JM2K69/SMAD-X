using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SMADX.Services.Sources
{
    /// <summary>Une page ou un fichier relevé : son contenu, et la date de publication que le serveur annonce.</summary>
    public sealed record Fetched(string Url, string Content, string? LastModified);

    /// <summary>Les relevés sur le web, avec un nom d'outil honnête et un délai raisonnable.</summary>
    public static class Web
    {
        private static readonly HttpClient Client = new(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All })
        {
            Timeout = TimeSpan.FromMinutes(5),
            DefaultRequestHeaders = { { "User-Agent", "SMAD-X sources tool (+https://github.com/JM2K69/SMAD-X)" } }
        };

        public static async Task<Fetched> GetAsync(string url)
        {
            using var response = await Client.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var bytes = await response.Content.ReadAsByteArrayAsync();
            var modified = response.Content.Headers.LastModified?.UtcDateTime.ToString("yyyy-MM-dd");
            return new Fetched(url, Encoding.UTF8.GetString(bytes), modified);
        }
    }

    /// <summary>Un contrôle du NIST SP 800-53 Rev. 5, d'après son catalogue OSCAL (domaine public).</summary>
    public sealed record NistControl(string Id, string Title, string Statement);

    /// <summary>Le catalogue OSCAL du NIST SP 800-53 Rev. 5 : le titre et l'énoncé de chaque contrôle, paramètres compris.</summary>
    public sealed partial class NistCatalog
    {
        public const string Url = "https://raw.githubusercontent.com/usnistgov/oscal-content/main/nist.gov/SP800-53/rev5/json/NIST_SP-800-53_rev5_catalog.json";
        private readonly Dictionary<string, JsonElement> _controls = new(StringComparer.OrdinalIgnoreCase);
        private readonly JsonDocument _document;

        public NistCatalog(string json)
        {
            _document = JsonDocument.Parse(json);
            var catalog = _document.RootElement.GetProperty("catalog");
            Version = catalog.GetProperty("metadata").TryGetProperty("version", out var v) ? v.GetString() : null;
            Published = catalog.GetProperty("metadata").TryGetProperty("last-modified", out var m) ? m.GetString()?[..10] : null;
            foreach (var group in catalog.GetProperty("groups").EnumerateArray())
                Add(group);
        }

        public string? Version { get; }
        public string? Published { get; }

        private void Add(JsonElement parent)
        {
            if (!parent.TryGetProperty("controls", out var controls)) return;
            foreach (var control in controls.EnumerateArray())
            {
                _controls[control.GetProperty("id").GetString()!] = control;
                Add(control);
            }
        }

        /// <summary>« AC-6(5) » : le contrôle ac-6.5.</summary>
        public NistControl? Find(string id)
        {
            var key = Regex.Replace(id.ToLowerInvariant(), @"\((\d+)\)", ".$1");
            if (!_controls.TryGetValue(key, out var control)) return null;
            var parameters = new Dictionary<string, JsonElement>();
            if (control.TryGetProperty("params", out var ps))
                foreach (var p in ps.EnumerateArray()) parameters[p.GetProperty("id").GetString()!] = p;
            var sb = new StringBuilder();
            if (control.TryGetProperty("parts", out var parts))
                foreach (var part in parts.EnumerateArray().Where(p => p.GetProperty("name").GetString() == "statement"))
                    Statement(part, parameters, sb, 0);
            return new NistControl(id, control.GetProperty("title").GetString()!, sb.ToString().Trim());
        }

        private static void Statement(JsonElement part, Dictionary<string, JsonElement> parameters, StringBuilder sb, int depth)
        {
            var label = part.TryGetProperty("props", out var props)
                ? props.EnumerateArray().Where(p => p.GetProperty("name").GetString() == "label").Select(p => p.GetProperty("value").GetString()).FirstOrDefault()
                : null;
            if (part.TryGetProperty("prose", out var prose))
            {
                var text = InsertRegex().Replace(prose.GetString()!, m => Parameter(m.Groups[1].Value, parameters));
                sb.Append(depth == 0 ? "" : new string(' ', (depth - 1) * 2) + "- ").Append(label != null ? label + " " : "").Append(text).Append('\n');
            }
            if (part.TryGetProperty("parts", out var children))
                foreach (var child in children.EnumerateArray()) Statement(child, parameters, sb, depth + 1);
        }

        /// <summary>Un paramètre de l'énoncé, comme le NIST l'écrit : [Assignment: …] ou [Selection: …].</summary>
        private static string Parameter(string id, Dictionary<string, JsonElement> parameters)
        {
            if (!parameters.TryGetValue(id, out var p)) return "[Assignment: organization-defined value]";
            if (p.TryGetProperty("select", out var select) && select.TryGetProperty("choice", out var choices))
            {
                var many = select.TryGetProperty("how-many", out var how) ? $" ({how.GetString()?.Replace('-', ' ')})" : "";
                return $"[Selection{many}: {string.Join("; ", choices.EnumerateArray().Select(c => InsertRegex().Replace(c.GetString()!, m => Parameter(m.Groups[1].Value, parameters))))}]";
            }
            return p.TryGetProperty("label", out var label) ? $"[Assignment: {label.GetString()}]" : "[Assignment: organization-defined value]";
        }

        [GeneratedRegex(@"\{\{\s*insert:\s*param,\s*([^}\s]+)\s*\}\}")]
        private static partial Regex InsertRegex();
    }

    /// <summary>Une technique MITRE ATT&amp;CK : son nom, le premier paragraphe de sa description, sa version.</summary>
    public sealed record MitreTechnique(string Id, string Name, string Description, string? Version, string? Modified);

    /// <summary>Les données STIX d'ATT&amp;CK Enterprise (dépôt mitre-attack/attack-stix-data).</summary>
    public sealed partial class MitreAttack
    {
        public const string Url = "https://raw.githubusercontent.com/mitre-attack/attack-stix-data/master/enterprise-attack/enterprise-attack.json";
        private readonly Dictionary<string, MitreTechnique> _techniques = new(StringComparer.OrdinalIgnoreCase);

        public MitreAttack(string json)
        {
            using var doc = JsonDocument.Parse(json);
            foreach (var o in doc.RootElement.GetProperty("objects").EnumerateArray())
            {
                var type = o.GetProperty("type").GetString();
                if (type == "x-mitre-collection") Version = o.TryGetProperty("x_mitre_version", out var v) ? v.GetString() : null;
                if (type != "attack-pattern" || Flag(o, "revoked") || Flag(o, "x_mitre_deprecated")) continue;
                var reference = o.GetProperty("external_references").EnumerateArray()
                    .FirstOrDefault(r => r.TryGetProperty("source_name", out var s) && s.GetString() == "mitre-attack");
                if (reference.ValueKind != JsonValueKind.Object || !reference.TryGetProperty("external_id", out var id)) continue;
                var description = o.TryGetProperty("description", out var d) ? d.GetString()! : "";
                var first = description.Replace("\r", "").Split("\n\n")[0];
                first = CitationRegex().Replace(first, "");
                first = CodeRegex().Replace(first, "`$1`");
                _techniques[id.GetString()!] = new MitreTechnique(id.GetString()!, o.GetProperty("name").GetString()!, first.Trim(),
                    o.TryGetProperty("x_mitre_version", out var tv) ? tv.GetString() : null,
                    o.TryGetProperty("modified", out var mod) ? mod.GetString()?[..10] : null);
            }
        }

        /// <summary>La version d'ATT&amp;CK des données relevées (« 18.1 »).</summary>
        public string? Version { get; private set; }

        public MitreTechnique? Find(string id) => _techniques.GetValueOrDefault(id);

        private static bool Flag(JsonElement o, string name) => o.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.True;

        [GeneratedRegex(@"\s*\(Citation:[^)]*\)")]
        private static partial Regex CitationRegex();

        [GeneratedRegex(@"<code>(.*?)</code>")]
        private static partial Regex CodeRegex();
    }

    /// <summary>Les titres des mesures des CIS Controls v8, d'après les pages de la spécification d'évaluation (CAS).</summary>
    public static partial class CisControls
    {
        public static Dictionary<string, string> Safeguards(string html) =>
            HeadingRegex().Matches(html)
                .Select(m => WebUtility.HtmlDecode(TagRegex().Replace(m.Groups[1].Value, "")).Trim())
                .Select(t => SafeguardRegex().Match(t))
                .Where(m => m.Success)
                .GroupBy(m => m.Groups[1].Value)
                .ToDictionary(g => g.Key, g => g.First().Groups[2].Value.Trim());

        [GeneratedRegex(@"<h[1-4][^>]*>(.*?)</h[1-4]>", RegexOptions.Singleline)]
        private static partial Regex HeadingRegex();

        [GeneratedRegex(@"<[^>]+>")]
        private static partial Regex TagRegex();

        [GeneratedRegex(@"^(\d+\.\d+):\s*(.+)$")]
        private static partial Regex SafeguardRegex();
    }

    /// <summary>Une page web : son titre, sa description et sa date, d'après ses métadonnées.</summary>
    public sealed record PageInfo(string Title, string? Description, string? Date);

    public static partial class Pages
    {
        /// <summary>Le titre (sans « | Microsoft Learn »), la description et la date de mise à jour d'une page.</summary>
        public static PageInfo Read(Fetched page)
        {
            var html = page.Content;
            var title = TitleRegex().Match(html) is { Success: true } t ? WebUtility.HtmlDecode(t.Groups[1].Value).Trim() : page.Url;
            // Le nom du site après « | » : « … | Microsoft Learn », « … | MesServicesCyber »
            title = Regex.Replace(title, @"\s*\|\s*[^|]+$", "");
            string? Meta(string name) => Regex.Match(html, $@"<meta\s+name=""{Regex.Escape(name)}""\s+content=""([^""]*)""", RegexOptions.IgnoreCase) is { Success: true } m
                ? WebUtility.HtmlDecode(m.Groups[1].Value).Trim() : null;
            var date = Meta("ms.date")?[..10] ?? page.LastModified;
            return new PageInfo(title, Meta("description"), date);
        }

        [GeneratedRegex(@"<title>(.*?)</title>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
        private static partial Regex TitleRegex();
    }
}
