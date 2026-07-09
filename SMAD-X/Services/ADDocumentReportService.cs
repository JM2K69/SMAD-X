using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OfficeIMO.Markdown;
using OfficeIMO.Markdown.Pdf;
using OfficeIMO.Pdf;
using OfficeIMO.Word;
using OfficeIMO.Word.Markdown;
using SMADX.Models;

namespace SMADX.Services
{
    /// <summary>
    /// Generates documentation reports from an ADRootDocument.
    /// Supports Markdown (native), DOCX (OfficeIMO.Word.Markdown), and PDF (OfficeIMO.Markdown.Pdf).
    /// </summary>
    public class ADDocumentReportService
    {
        // Log file next to the executable — always visible regardless of build config
        private static readonly string LogPath = Path.Combine(
            AppContext.BaseDirectory, "SMAD-X-export.log");

        private static void Log(string message)
        {
            try
            {
                File.AppendAllText(LogPath,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}");
            }
            catch { /* never crash the app because of logging */ }
            Debug.WriteLine($"[SMAD-X] {message}");
        }
        // ── Public entry points ──────────────────────────────────────────────

        public async Task<bool> ExportMarkdownAsync(ADRootDocument document, string filePath)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
                var sb = new StringBuilder();
                BuildMarkdown(document, sb);
                await File.WriteAllTextAsync(filePath, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                return true;
            }
            catch (Exception ex)
            {
                Log($"Markdown export error: {ex}");
                return false;
            }
        }

        // ── Single-object / single-site / single-sitelink export ─────────────

        /// <summary>Export the Description markdown of a single ADObject to a .md file.</summary>
        public async Task<bool> ExportSingleObjectMarkdownAsync(ADObject obj, string filePath)
        {
            try
            {
                var content = obj.Description ?? string.Empty;
                await File.WriteAllTextAsync(filePath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Single object MD export error: {ex.Message}");
                return false;
            }
        }

        /// <summary>Export the Description markdown of a single ADSite to a .md file.</summary>
        public async Task<bool> ExportSingleSiteMarkdownAsync(ADSite site, string filePath)
        {
            try
            {
                var content = site.Description ?? string.Empty;
                await File.WriteAllTextAsync(filePath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Single site MD export error: {ex.Message}");
                return false;
            }
        }

        /// <summary>Export the Description markdown of a single ADSiteLink to a .md file.</summary>
        public async Task<bool> ExportSingleSiteLinkMarkdownAsync(ADSiteLink link, string filePath)
        {
            try
            {
                var content = link.Description ?? string.Empty;
                await File.WriteAllTextAsync(filePath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Single site-link MD export error: {ex.Message}");
                return false;
            }
        }

        /// <summary>Read a markdown file and return its content (to be applied as Description).</summary>
        public async Task<string?> ImportMarkdownAsync(string filePath)
        {
            try
            {
                return await File.ReadAllTextAsync(filePath, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Markdown import error: {ex.Message}");
                return null;
            }
        }

        // ── Single-element DOCX export ───────────────────────────────────────

        public Task<bool> ExportSingleElementDocxAsync(string name, string typeLabel, string? description, string filePath, string theme = "WordLike")
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
                var md = BuildSingleElementMarkdown(name, typeLabel, description);
                var opts = new MarkdownToWordOptions { Theme = ResolveVisualTheme(theme) };
                using var word = MarkdownReader.Parse(md).ToWordDocument(opts);
                word.SaveAs(filePath);
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                Log($"Single element DOCX export error: {ex}");
                return Task.FromResult(false);
            }
        }

        // ── Single-element PDF export ────────────────────────────────────────

        public Task<bool> ExportSingleElementPdfAsync(string name, string typeLabel, string? description, string filePath, string theme = "WordLike")
        {
            try
            {
                var dir = Path.GetDirectoryName(filePath)!;
                Directory.CreateDirectory(dir);
                var md = BuildSingleElementMarkdown(name, typeLabel, description);
                RenderPdf(md, filePath, theme);
                Log($"PDF created: {filePath}");
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                Log($"Single element PDF export error: {ex}");
                return Task.FromResult(false);
            }
        }

        /// <summary>Build markdown text for a single documented element.</summary>
        private static string BuildSingleElementMarkdown(string name, string typeLabel, string? description)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# {name}");
            sb.AppendLine();
            sb.AppendLine($"**Type :** {typeLabel}");
            sb.AppendLine();
            if (!string.IsNullOrWhiteSpace(description))
            {
                sb.AppendLine("## Documentation");
                sb.AppendLine();
                sb.AppendLine(description);
            }
            return sb.ToString();
        }

        // ── Single-object markdown builders ─────────────────────────────────

        private static void BuildSingleObjectMarkdown(ADObject obj, StringBuilder sb)
        {
            var icon = TypeIcon(obj.Type);
            sb.AppendLine($"# {icon} {obj.Name}");
            sb.AppendLine();
            sb.AppendLine($"- **Type :** {obj.Type}");
            sb.AppendLine($"- **DN :** `{obj.DistinguishedName}`");
            if (!string.IsNullOrWhiteSpace(obj.Tier))
                sb.AppendLine($"- **Tier :** {obj.Tier}");
            if (obj.LinkedGPOs.Count > 0)
                sb.AppendLine($"- **GPO liées :** {string.Join(", ", obj.LinkedGPOs)}");
            sb.AppendLine();
            if (!string.IsNullOrWhiteSpace(obj.Description))
            {
                sb.AppendLine("## Documentation");
                sb.AppendLine();
                sb.AppendLine(obj.Description);
                sb.AppendLine();
            }
        }

        private static void BuildSingleSiteMarkdown(ADSite site, StringBuilder sb)
        {
            sb.AppendLine($"# 🏢 {site.Name}");
            sb.AppendLine();
            sb.AppendLine($"- **Localisation :** {site.Location}");
            if (!string.IsNullOrWhiteSpace(site.Tier))
                sb.AppendLine($"- **Tier :** {site.Tier}");
            if (site.Subnets.Count > 0)
                sb.AppendLine($"- **Sous-réseaux :** {string.Join(", ", site.Subnets.Select(s => s.Cidr))}");
            if (site.DomainControllers.Count > 0)
                sb.AppendLine($"- **Contrôleurs de domaine :** {string.Join(", ", site.DomainControllers)}");
            if (site.LinkedGPOs.Count > 0)
                sb.AppendLine($"- **GPO liées :** {string.Join(", ", site.LinkedGPOs)}");
            sb.AppendLine();
            if (!string.IsNullOrWhiteSpace(site.Description))
            {
                sb.AppendLine("## Documentation");
                sb.AppendLine();
                sb.AppendLine(site.Description);
                sb.AppendLine();
            }
        }

        private static void BuildSingleSiteLinkMarkdown(ADSiteLink link, StringBuilder sb)
        {
            sb.AppendLine($"# 🔗 {link.Name}");
            sb.AppendLine();
            sb.AppendLine($"- **Transport :** {link.Transport}");
            sb.AppendLine($"- **Coût :** {link.Cost}");
            sb.AppendLine($"- **Intervalle :** {link.ReplicationIntervalMinutes} min");
            sb.AppendLine($"- **Planification :** {link.ReplicationSchedule}");
            sb.AppendLine($"- **Sites :** {string.Join(" ↔ ", link.SiteNames)}");
            sb.AppendLine();
            if (!string.IsNullOrWhiteSpace(link.Description))
            {
                sb.AppendLine("## Documentation");
                sb.AppendLine();
                sb.AppendLine(link.Description);
                sb.AppendLine();
            }
        }

        // ── DOCX export ──────────────────────────────────────────────────────

        public async Task<bool> ExportDocxAsync(ADRootDocument document, string filePath, string theme = "WordLike")
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
                var sb = new StringBuilder();
                BuildMarkdown(document, sb);
                var markdownDoc = MarkdownReader.Parse(sb.ToString());
                var opts = new MarkdownToWordOptions { Theme = ResolveVisualTheme(theme) };
                using var word = markdownDoc.ToWordDocument(opts);
                word.SaveAs(filePath);
                return true;
            }
            catch (Exception ex)
            {
                Log($"DOCX export error: {ex}");
                return false;
            }
        }

        // ── PDF export ───────────────────────────────────────────────────────

        public async Task<bool> ExportPdfAsync(ADRootDocument document, string filePath, string theme = "WordLike")
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
                var sb = new StringBuilder();
                BuildMarkdown(document, sb);
                RenderPdf(sb.ToString(), filePath, theme);
                Log($"PDF created: {filePath}");
                return true;
            }
            catch (Exception ex)
            {
                Log($"PDF export error: {ex}");
                return false;
            }
        }

        // ── Markdown builder ─────────────────────────────────────────────────

        private static void BuildMarkdown(ADRootDocument document, StringBuilder sb)
        {
            var now    = DateTime.Now;
            var domain = document.Domain;
            var sites  = document.SitesTopology;

            sb.AppendLine("# Rapport de documentation Active Directory");
            sb.AppendLine();
            sb.AppendLine($"> Généré le {now:dd/MM/yyyy} à {now:HH:mm}  |  Format v{document.Version}");
            sb.AppendLine();

            // ── Domain overview ──────────────────────────────────────────────
            sb.AppendLine("## Domaine");
            sb.AppendLine();
            if (domain is not null)
            {
                sb.AppendLine($"- **Nom :** {domain.Name}");
                sb.AppendLine($"- **DN :** `{domain.DistinguishedName}`");
                if (!string.IsNullOrWhiteSpace(domain.Description))
                    sb.AppendLine($"- **Description :** {domain.Description}");
                sb.AppendLine($"- **Créé le :** {domain.CreatedDate:dd/MM/yyyy}");
                sb.AppendLine($"- **Modifié le :** {domain.ModifiedDate:dd/MM/yyyy}");
                sb.AppendLine();

                // Object counts
                var all   = FlattenTree(domain).ToList();
                var types = all.GroupBy(o => o.Type).OrderByDescending(g => g.Count());
                sb.AppendLine("### Statistiques des objets");
                sb.AppendLine();
                sb.AppendLine("| Type | Nombre |");
                sb.AppendLine("|------|--------|");
                foreach (var g in types)
                    sb.AppendLine($"| {g.Key} | {g.Count()} |");
                sb.AppendLine();

                // Tree
                sb.AppendLine("### Arborescence");
                sb.AppendLine();
                sb.AppendLine("```");
                AppendTree(domain, sb, 0);
                sb.AppendLine("```");
                sb.AppendLine();

                // PSO policies
                var psos = all.Where(o => o.Type == ADObjectType.PasswordSettingsObject).ToList();
                if (psos.Count > 0)
                {
                    sb.AppendLine("### Stratégies de mot de passe (PSO)");
                    sb.AppendLine();
                    sb.AppendLine("| Nom | Longueur min | Historique | Âge max (j) | Verrouillage |");
                    sb.AppendLine("|-----|-------------|-----------|-------------|--------------|");
                    foreach (var p in psos)
                        sb.AppendLine($"| {p.Name} | {p.PSOMinPasswordLength} | {p.PSOPasswordHistoryCount} | {p.PSOMaxPasswordAgeDays} | {p.PSOLockoutThreshold} tentatives |");
                    sb.AppendLine();
                }

                // Delegations
                var delegations = all.SelectMany(o => o.Delegations ?? Enumerable.Empty<ADDelegation>()).ToList();
                if (delegations.Count > 0)
                {
                    sb.AppendLine("### Délégations");
                    sb.AppendLine();
                    sb.AppendLine("| Mandataire | Cible | Catégorie | Hérité |");
                    sb.AppendLine("|-----------|-------|-----------|--------|");
                    foreach (var d in delegations)
                        sb.AppendLine($"| {d.TrusteeName} | {d.TargetDN} | {d.RightCategory} | {(d.IsInherited ? "✓" : "")} |");
                    sb.AppendLine();
                }
            }

            // ── Sites topology ───────────────────────────────────────────────
            if (sites is not null && sites.Sites.Count > 0)
            {
                sb.AppendLine("## Topologie des sites AD");
                sb.AppendLine();
                sb.AppendLine($"**{sites.Sites.Count}** site(s) — **{sites.SiteLinks.Count}** lien(s) de réplication");
                sb.AppendLine();

                sb.AppendLine("### Sites");
                sb.AppendLine();
                sb.AppendLine("| Nom | Localisation | Sous-réseaux | DCs | GPOs |");
                sb.AppendLine("|-----|-------------|-------------|-----|------|");
                foreach (var s in sites.Sites)
                    sb.AppendLine($"| {s.Name} | {s.Location} | {s.Subnets.Count} | {s.DomainControllers.Count} | {s.LinkedGPOs.Count} |");
                sb.AppendLine();

                // Per-site detailed documentation
                foreach (var site in sites.Sites)
                {
                    sb.AppendLine($"#### 🏢 {site.Name}");
                    sb.AppendLine();
                    if (site.Subnets.Count > 0)
                        sb.AppendLine($"- **Sous-réseaux :** {string.Join(", ", site.Subnets.Select(s => s.Cidr))}");
                    if (site.DomainControllers.Count > 0)
                        sb.AppendLine($"- **DCs :** {string.Join(", ", site.DomainControllers)}");
                    if (site.LinkedGPOs.Count > 0)
                        sb.AppendLine($"- **GPOs liées :** {string.Join(", ", site.LinkedGPOs)}");
                    if (!string.IsNullOrWhiteSpace(site.Description))
                    {
                        sb.AppendLine();
                        sb.AppendLine(site.Description);
                    }
                    sb.AppendLine();
                }

                if (sites.SiteLinks.Count > 0)
                {
                    sb.AppendLine("### Liens de réplication");
                    sb.AppendLine();
                    sb.AppendLine("| Lien | Transport | Coût | Intervalle (min) | Sites liés |");
                    sb.AppendLine("|------|-----------|------|-----------------|-----------|");
                    foreach (var l in sites.SiteLinks)
                        sb.AppendLine($"| {l.Name} | {l.Transport} | {l.Cost} | {l.ReplicationIntervalMinutes} | {string.Join(", ", l.SiteNames)} |");
                    sb.AppendLine();

                    // Per-site-link documentation
                    foreach (var link in sites.SiteLinks.Where(l => !string.IsNullOrWhiteSpace(l.Description)))
                    {
                        sb.AppendLine($"#### 🔗 {link.Name}");
                        sb.AppendLine();
                        sb.AppendLine(link.Description);
                        sb.AppendLine();
                    }
                }
            }

            // ── Per-object documentation ─────────────────────────────────────
            if (domain is not null)
            {
                var documented = FlattenTree(domain)
                    .Where(o => !string.IsNullOrWhiteSpace(o.Description))
                    .ToList();
                if (documented.Count > 0)
                {
                    sb.AppendLine("## Documentation des objets");
                    sb.AppendLine();
                    foreach (var obj in documented)
                    {
                        sb.AppendLine($"### {TypeIcon(obj.Type)} {obj.Name}");
                        sb.AppendLine();
                        sb.AppendLine($"- **Type :** {obj.Type}");
                        sb.AppendLine($"- **DN :** `{obj.DistinguishedName}`");
                        if (!string.IsNullOrWhiteSpace(obj.Tier))
                            sb.AppendLine($"- **Tier :** {obj.Tier}");
                        if (obj.LinkedGPOs.Count > 0)
                            sb.AppendLine($"- **GPOs liées :** {string.Join(", ", obj.LinkedGPOs)}");
                        sb.AppendLine();
                        sb.AppendLine(obj.Description);
                        sb.AppendLine();
                    }
                }
            }

            sb.AppendLine("---");
            sb.AppendLine($"*Rapport généré par SMAD-X*");
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static System.Collections.Generic.IEnumerable<ADObject> FlattenTree(ADObject node)
        {
            yield return node;
            foreach (var child in node.Children)
                foreach (var n in FlattenTree(child))
                    yield return n;
        }

        private static void AppendTree(ADObject node, StringBuilder sb, int depth)
        {
            var indent  = new string(' ', depth * 2);
            var icon    = TypeIcon(node.Type);
            var tierTag = string.IsNullOrWhiteSpace(node.Tier) ? string.Empty : $" [{node.Tier}]";
            sb.AppendLine($"{indent}{icon} {node.Name}{tierTag}");
            foreach (var child in node.Children)
                AppendTree(child, sb, depth + 1);
        }

        private static string TypeIcon(ADObjectType type) => type switch
        {
            ADObjectType.Domain                 => "🌐",
            ADObjectType.OrganizationalUnit     => "📁",
            ADObjectType.Container              => "📦",
            ADObjectType.User                   => "👤",
            ADObjectType.Group                  => "👥",
            ADObjectType.Computer               => "🖥",
            ADObjectType.GMSA                   => "🔧",
            ADObjectType.Policy                 => "📋",
            ADObjectType.PasswordSettingsObject => "🔑",
            _                                   => "•"
        };

        // ── PDF render helper ─────────────────────────────────────────────────

        /// <summary>
        /// Renders markdown to PDF. First attempt uses system font embedding so emoji
        /// and Unicode render natively. If the OfficeIMO preflight still raises an
        /// encoding error (e.g. Segoe UI Emoji not installed), a second attempt
        /// substitutes known app emoji with short text labels — DOCX/.md are never affected.
        /// </summary>
        private static void RenderPdf(string markdown, string filePath, string theme)
        {
            var opts = new MarkdownPdfSaveOptions
            {
                Theme                    = ResolveVisualTheme(theme),
                AllowSystemFontEmbedding = true,
                TextFallbacks            = PdfTextFallbackFeatures.Default
            };

            try
            {
                MarkdownReader.Parse(markdown).SaveAsPdf(filePath, opts);
            }
            catch (Exception ex) when (ex.GetType().Name.Contains("Encoding")
                                        || ex.GetType().Name.Contains("Preflight"))
            {
                // System fonts not sufficient — substitute app emoji then retry
                MarkdownReader.Parse(EmojiSafeMarkdown(markdown)).SaveAsPdf(filePath, opts);
            }
        }

        /// <summary>
        /// Strips / replaces all characters that Arial cannot encode so the PDF
        /// preflight never fails. Called only on the PDF copy; DOCX and .md are untouched.
        ///
        /// Strategy:
        ///   1. Named substitutions for code points that have a readable ASCII equivalent.
        ///   2. Strip all remaining surrogate pairs (U+10000+) — emoji in free-text
        ///      descriptions that we don't recognise specifically.
        ///   3. Strip the few BMP code points outside Arial's coverage that appear in
        ///      the app's sample descriptions (✅ ❌ ✕ ✏ ⊡ 🕸 …).
        ///   4. Variation selectors (U+FE0x) removed as they are meaningless without emoji.
        /// </summary>
        private static string EmojiSafeMarkdown(string input)
        {
            // ── 1. Named substitutions (preserves readability) ────────────────
            var s = input
                // TypeIcon() outputs
                .Replace("\U0001F310", "[Domain]")
                .Replace("\U0001F4C1", "[OU]")
                .Replace("\U0001F4E6", "[Container]")
                .Replace("\U0001F464", "[User]")
                .Replace("\U0001F465", "[Group]")
                .Replace("\U0001F5A5\uFE0F", "[Computer]")
                .Replace("\U0001F5A5", "[Computer]")
                .Replace("\U0001F527", "[GMSA]")
                .Replace("\U0001F4CB", "[Policy]")
                .Replace("\U0001F511", "[PSO]")
                // Site icons
                .Replace("\U0001F3E2", "[Site]")
                .Replace("\U0001F517", "[SiteLink]")
                .Replace("\U0001F3D9", "[Site]")       // 🏙 cityscape (site descriptions)
                .Replace("\U0001F3DB", "[Building]")   // 🏛 classical building
                // Warning / status
                .Replace("\u26A0\uFE0F", "[!]")
                .Replace("\u26A0", "[!]")              // ⚠
                .Replace("\u2705", "[OK]")             // ✅
                .Replace("\u274C", "[X]")              // ❌
                .Replace("\u2715", "[x]")              // ✕
                .Replace("\u270F\uFE0F", "[edit]")
                .Replace("\u270F", "[edit]")           // ✏
                // Misc app icons found in localization
                .Replace("\U0001F4D6", "[doc]")        // 📖
                .Replace("\U0001F4C4", "[file]")       // 📄
                .Replace("\U0001F4C2", "[folder]")     // 📂
                .Replace("\U0001F504", "[refresh]")    // 🔄
                .Replace("\U0001F50D", "[search]")     // 🔍
                .Replace("\U0001F510", "[lock]")       // 🔐
                .Replace("\U0001F512", "[lock]")       // 🔒
                .Replace("\U0001F534", "[red]")        // 🔴
                .Replace("\U0001F7E0", "[orange]")     // 🟠
                .Replace("\U0001F7E1", "[yellow]")     // 🟡
                .Replace("\U0001F7E2", "[green]")      // 🟢
                .Replace("\U0001F4BE", "[save]")       // 💾
                .Replace("\U0001F4BB", "[PC]")         // 💻
                .Replace("\U0001F3A8", "[theme]")      // 🎨
                .Replace("\U0001F3AB", "[ticket]")     // 🎫
                .Replace("\U0001F4CD", "[pin]")        // 📍
                .Replace("\U0001F578", "[web]")        // 🕸
                .Replace("\U0001F5A7", "[DC]")         // 🖧 network server
                .Replace("\U0001F5D1", "[delete]")     // 🗑
                .Replace("\U0001F6E1", "[shield]")     // 🛡
                .Replace("\U0001F4B2", "[cost]")       // 💲
                .Replace("\u2699\uFE0F", "[config]")
                .Replace("\u2699", "[config]")         // ⚙
                .Replace("\u22A1", "[box]")            // ⊡
                .Replace("\u2B07\uFE0F", "[down]")
                .Replace("\u2B07", "[down]")           // ⬇
                .Replace("\u2795", "[+]")              // ➕
                .Replace("\u21BA", "[reset]")          // ↺
                .Replace("\u2139\uFE0F", "[i]")
                .Replace("\u2139", "[i]");             // ℹ

            // ── 2. Strip remaining surrogate pairs and variation selectors ────
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                // Variation selectors U+FE00–U+FE0F: drop silently
                if (c >= '\uFE00' && c <= '\uFE0F') continue;
                // Surrogate pairs (anything > U+FFFF not caught above)
                if (char.IsHighSurrogate(c))
                {
                    if (i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]))
                        i++; // consume the low surrogate
                    continue; // drop the pair
                }
                if (char.IsLowSurrogate(c)) continue;
                sb.Append(c);
            }
            return sb.ToString();
        }

        // ── Theme resolvers ──────────────────────────────────────────────────

        /// <summary>
        /// Map a theme key string to the shared MarkdownVisualTheme used by both
        /// DOCX (MarkdownToWordOptions.Theme) and PDF (MarkdownPdfVisualTheme.FromVisualTheme).
        /// </summary>
        private static MarkdownVisualTheme ResolveVisualTheme(string theme) => theme switch
        {
            "Plain"             => MarkdownVisualTheme.Plain(),
            "TechnicalDocument" => MarkdownVisualTheme.TechnicalDocument(),
            "GitHubLike"        => MarkdownVisualTheme.GitHubLike(),
            "Compact"           => MarkdownVisualTheme.Compact(),
            "Report"            => MarkdownVisualTheme.Report(),
            _                   => MarkdownVisualTheme.WordLike()  // default
        };
    }
}
