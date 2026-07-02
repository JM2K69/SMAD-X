using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OfficeIMO.Word;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SMADX.Models;

namespace SMADX.Services
{
    /// <summary>
    /// Generates documentation reports from an ADRootDocument.
    /// Supports Markdown (native), DOCX (OfficeIMO.Word), and PDF (QuestPDF).
    /// </summary>
    public class ADDocumentReportService
    {
        // ── Public entry points ──────────────────────────────────────────────

        public async Task<bool> ExportMarkdownAsync(ADRootDocument document, string filePath)
        {
            try
            {
                var sb = new StringBuilder();
                BuildMarkdown(document, sb);
                await File.WriteAllTextAsync(filePath, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Markdown export error: {ex.Message}");
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

        public Task<bool> ExportDocxAsync(ADRootDocument document, string filePath)
        {
            try
            {
                using var word = WordDocument.Create(filePath);
                var domain = document.Domain;
                var sites  = document.SitesTopology;
                var now    = DateTime.Now;

                // Title
                var title = word.AddParagraph($"Rapport AD — {domain?.Name ?? "Domaine"}");
                title.Style = WordParagraphStyles.Heading1;
                word.AddParagraph($"Généré le {now:dd/MM/yyyy} à {now:HH:mm}  |  Format v{document.Version}");

                // Domain section
                var h2 = word.AddParagraph("Domaine");
                h2.Style = WordParagraphStyles.Heading2;

                if (domain is not null)
                {
                    word.AddParagraph($"Nom : {domain.Name}");
                    word.AddParagraph($"DN  : {domain.DistinguishedName}");
                    if (!string.IsNullOrWhiteSpace(domain.Description))
                        word.AddParagraph($"Description : {domain.Description}");

                    // Object counts table
                    var h3 = word.AddParagraph("Statistiques des objets");
                    h3.Style = WordParagraphStyles.Heading3;

                    var all   = FlattenTree(domain).ToList();
                    var types = all.GroupBy(o => o.Type).OrderByDescending(g => g.Count()).ToList();

                    var tbl = word.AddTable(types.Count + 1, 2, WordTableStyle.TableGrid);
                    tbl.Rows[0].Cells[0].Paragraphs[0].Text = "Type";
                    tbl.Rows[0].Cells[1].Paragraphs[0].Text = "Nombre";
                    for (int i = 0; i < types.Count; i++)
                    {
                        tbl.Rows[i + 1].Cells[0].Paragraphs[0].Text = types[i].Key.ToString();
                        tbl.Rows[i + 1].Cells[1].Paragraphs[0].Text = types[i].Count().ToString();
                    }

                    // PSOs
                    var psos = all.Where(o => o.Type == ADObjectType.PasswordSettingsObject).ToList();
                    if (psos.Count > 0)
                    {
                        var psoH = word.AddParagraph("Stratégies de mot de passe (PSO)");
                        psoH.Style = WordParagraphStyles.Heading3;
                        var pt = word.AddTable(psos.Count + 1, 5, WordTableStyle.TableGrid);
                        pt.Rows[0].Cells[0].Paragraphs[0].Text = "Nom";
                        pt.Rows[0].Cells[1].Paragraphs[0].Text = "Long. min";
                        pt.Rows[0].Cells[2].Paragraphs[0].Text = "Historique";
                        pt.Rows[0].Cells[3].Paragraphs[0].Text = "Âge max (j)";
                        pt.Rows[0].Cells[4].Paragraphs[0].Text = "Verrouillage";
                        for (int i = 0; i < psos.Count; i++)
                        {
                            var p = psos[i];
                            pt.Rows[i + 1].Cells[0].Paragraphs[0].Text = p.Name;
                            pt.Rows[i + 1].Cells[1].Paragraphs[0].Text = (p.PSOMinPasswordLength?.ToString()) ?? "—";
                            pt.Rows[i + 1].Cells[2].Paragraphs[0].Text = (p.PSOPasswordHistoryCount?.ToString()) ?? "—";
                            pt.Rows[i + 1].Cells[3].Paragraphs[0].Text = (p.PSOMaxPasswordAgeDays?.ToString()) ?? "—";
                            pt.Rows[i + 1].Cells[4].Paragraphs[0].Text = (p.PSOLockoutThreshold?.ToString()) ?? "—";
                        }
                    }

                    // Per-object documentation (objects with non-empty Description)
                    var documented = all.Where(o => !string.IsNullOrWhiteSpace(o.Description)).ToList();
                    if (documented.Count > 0)
                    {
                        var docH = word.AddParagraph("Documentation des objets");
                        docH.Style = WordParagraphStyles.Heading2;
                        foreach (var obj in documented)
                        {
                            var objH = word.AddParagraph($"{TypeIcon(obj.Type)} {obj.Name}");
                            objH.Style = WordParagraphStyles.Heading3;
                            // Strip markdown fences and emit as plain paragraphs
                            foreach (var line in obj.Description.Split('\n'))
                            {
                                var stripped = StripMarkdownLine(line);
                                if (stripped is not null)
                                    word.AddParagraph(stripped);
                            }
                        }
                    }
                }

                // Sites section
                if (sites is not null && sites.Sites.Count > 0)
                {
                    var sH2 = word.AddParagraph("Topologie des sites AD");
                    sH2.Style = WordParagraphStyles.Heading2;
                    word.AddParagraph($"{sites.Sites.Count} site(s) — {sites.SiteLinks.Count} lien(s) de réplication");

                    var sH3 = word.AddParagraph("Sites");
                    sH3.Style = WordParagraphStyles.Heading3;

                    var st = word.AddTable(sites.Sites.Count + 1, 4, WordTableStyle.TableGrid);
                    st.Rows[0].Cells[0].Paragraphs[0].Text = "Nom";
                    st.Rows[0].Cells[1].Paragraphs[0].Text = "Localisation";
                    st.Rows[0].Cells[2].Paragraphs[0].Text = "Sous-réseaux";
                    st.Rows[0].Cells[3].Paragraphs[0].Text = "DCs";
                    for (int i = 0; i < sites.Sites.Count; i++)
                    {
                        var s = sites.Sites[i];
                        st.Rows[i + 1].Cells[0].Paragraphs[0].Text = s.Name;
                        st.Rows[i + 1].Cells[1].Paragraphs[0].Text = s.Location;
                        st.Rows[i + 1].Cells[2].Paragraphs[0].Text = s.Subnets.Count.ToString();
                        st.Rows[i + 1].Cells[3].Paragraphs[0].Text = s.DomainControllers.Count.ToString();
                    }

                    if (sites.SiteLinks.Count > 0)
                    {
                        var lH3 = word.AddParagraph("Liens de réplication");
                        lH3.Style = WordParagraphStyles.Heading3;
                        var lt = word.AddTable(sites.SiteLinks.Count + 1, 5, WordTableStyle.TableGrid);
                        lt.Rows[0].Cells[0].Paragraphs[0].Text = "Lien";
                        lt.Rows[0].Cells[1].Paragraphs[0].Text = "Transport";
                        lt.Rows[0].Cells[2].Paragraphs[0].Text = "Coût";
                        lt.Rows[0].Cells[3].Paragraphs[0].Text = "Intervalle (min)";
                        lt.Rows[0].Cells[4].Paragraphs[0].Text = "Sites";
                        for (int i = 0; i < sites.SiteLinks.Count; i++)
                        {
                            var l = sites.SiteLinks[i];
                            lt.Rows[i + 1].Cells[0].Paragraphs[0].Text = l.Name;
                            lt.Rows[i + 1].Cells[1].Paragraphs[0].Text = l.Transport;
                            lt.Rows[i + 1].Cells[2].Paragraphs[0].Text = l.Cost.ToString();
                            lt.Rows[i + 1].Cells[3].Paragraphs[0].Text = l.ReplicationIntervalMinutes.ToString();
                            lt.Rows[i + 1].Cells[4].Paragraphs[0].Text = string.Join(", ", l.SiteNames);
                        }
                    }

                    // Per-site documentation
                    var docSites = sites.Sites.Where(s => !string.IsNullOrWhiteSpace(s.Description)).ToList();
                    if (docSites.Count > 0)
                    {
                        var sdH = word.AddParagraph("Documentation des sites");
                        sdH.Style = WordParagraphStyles.Heading3;
                        foreach (var site in docSites)
                        {
                            var sH = word.AddParagraph($"🏢 {site.Name}");
                            sH.Style = WordParagraphStyles.Heading4;
                            foreach (var line in site.Description.Split('\n'))
                            {
                                var stripped = StripMarkdownLine(line);
                                if (stripped is not null)
                                    word.AddParagraph(stripped);
                            }
                        }
                    }

                    // Per-site-link documentation
                    var docLinks = sites.SiteLinks.Where(l => !string.IsNullOrWhiteSpace(l.Description)).ToList();
                    if (docLinks.Count > 0)
                    {
                        var slH = word.AddParagraph("Documentation des liens");
                        slH.Style = WordParagraphStyles.Heading3;
                        foreach (var link in docLinks)
                        {
                            var lH = word.AddParagraph($"🔗 {link.Name}");
                            lH.Style = WordParagraphStyles.Heading4;
                            foreach (var line in link.Description.Split('\n'))
                            {
                                var stripped = StripMarkdownLine(line);
                                if (stripped is not null)
                                    word.AddParagraph(stripped);
                            }
                        }
                    }
                }

                word.Save();
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DOCX export error: {ex.Message}");
                return Task.FromResult(false);
            }
        }

        public Task<bool> ExportPdfAsync(ADRootDocument document, string filePath)
        {
            try
            {
                QuestPDF.Settings.License = LicenseType.Community;

                var domain = document.Domain;
                var sites  = document.SitesTopology;
                var now    = DateTime.Now;

                Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(2, Unit.Centimetre);
                        page.DefaultTextStyle(x => x.FontSize(10));

                        page.Header().Text($"Rapport AD — {domain?.Name ?? "Domaine"}")
                            .SemiBold().FontSize(18).FontColor(Colors.Blue.Darken2);

                        page.Content().Column(col =>
                        {
                            col.Item().Text($"Généré le {now:dd/MM/yyyy} à {now:HH:mm}")
                               .FontColor(Colors.Grey.Medium);
                            col.Item().PaddingTop(10);

                            // Domain
                            if (domain is not null)
                            {
                                col.Item().Text("Domaine").SemiBold().FontSize(14);
                                col.Item().Text($"Nom : {domain.Name}");
                                col.Item().Text($"DN  : {domain.DistinguishedName}").FontFamily(Fonts.Courier);
                                if (!string.IsNullOrWhiteSpace(domain.Description))
                                    col.Item().Text($"Description : {domain.Description}");
                                col.Item().PaddingTop(6);

                                // Object counts
                                var all   = FlattenTree(domain).ToList();
                                var types = all.GroupBy(o => o.Type).OrderByDescending(g => g.Count()).ToList();
                                col.Item().Text("Statistiques des objets").SemiBold().FontSize(12);
                                col.Item().Table(tbl =>
                                {
                                    tbl.ColumnsDefinition(c => { c.RelativeColumn(3); c.RelativeColumn(1); });
                                    tbl.Header(h =>
                                    {
                                        h.Cell().Background(Colors.Blue.Lighten4).Padding(4).Text("Type").SemiBold();
                                        h.Cell().Background(Colors.Blue.Lighten4).Padding(4).Text("Nombre").SemiBold();
                                    });
                                    foreach (var g in types)
                                    {
                                        tbl.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(3).Text(g.Key.ToString());
                                        tbl.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(3).Text(g.Count().ToString());
                                    }
                                });
                                col.Item().PaddingTop(8);

                                // PSO
                                var psos = all.Where(o => o.Type == ADObjectType.PasswordSettingsObject).ToList();
                                if (psos.Count > 0)
                                {
                                    col.Item().Text("Stratégies de mot de passe (PSO)").SemiBold().FontSize(12);
                                    col.Item().Table(tbl =>
                                    {
                                        tbl.ColumnsDefinition(c =>
                                        {
                                            c.RelativeColumn(3); c.RelativeColumn(1); c.RelativeColumn(1);
                                            c.RelativeColumn(1); c.RelativeColumn(2);
                                        });
                                        tbl.Header(h =>
                                        {
                                            foreach (var t in new[] { "Nom", "Long.", "Hist.", "Âge max", "Verrou." })
                                                h.Cell().Background(Colors.Blue.Lighten4).Padding(4).Text(t).SemiBold();
                                        });
                                        foreach (var p in psos)
                                        {
                                            tbl.Cell().Padding(3).Text(p.Name);
                                            tbl.Cell().Padding(3).Text(p.PSOMinPasswordLength.ToString());
                                            tbl.Cell().Padding(3).Text(p.PSOPasswordHistoryCount.ToString());
                                            tbl.Cell().Padding(3).Text(p.PSOMaxPasswordAgeDays.ToString());
                                            tbl.Cell().Padding(3).Text(p.PSOLockoutThreshold.ToString());
                                        }
                                    });
                                }
                            }

                            // Sites
                            if (sites is not null && sites.Sites.Count > 0)
                            {
                                col.Item().PaddingTop(10).Text("Topologie des sites AD").SemiBold().FontSize(14);
                                col.Item().Text($"{sites.Sites.Count} site(s) — {sites.SiteLinks.Count} lien(s)");
                                col.Item().PaddingTop(4);

                                col.Item().Table(tbl =>
                                {
                                    tbl.ColumnsDefinition(c =>
                                    {
                                        c.RelativeColumn(3); c.RelativeColumn(3);
                                        c.RelativeColumn(1); c.RelativeColumn(1);
                                    });
                                    tbl.Header(h =>
                                    {
                                        foreach (var t in new[] { "Nom", "Localisation", "Subnets", "DCs" })
                                            h.Cell().Background(Colors.Blue.Lighten4).Padding(4).Text(t).SemiBold();
                                    });
                                    foreach (var s in sites.Sites)
                                    {
                                        tbl.Cell().Padding(3).Text(s.Name);
                                        tbl.Cell().Padding(3).Text(s.Location);
                                        tbl.Cell().Padding(3).Text(s.Subnets.Count.ToString());
                                        tbl.Cell().Padding(3).Text(s.DomainControllers.Count.ToString());
                                    }
                                });

                                if (sites.SiteLinks.Count > 0)
                                {
                                    col.Item().PaddingTop(6).Text("Liens de réplication").SemiBold().FontSize(12);
                                    col.Item().Table(tbl =>
                                    {
                                        tbl.ColumnsDefinition(c =>
                                        {
                                            c.RelativeColumn(3); c.RelativeColumn(1); c.RelativeColumn(1);
                                            c.RelativeColumn(2); c.RelativeColumn(3);
                                        });
                                        tbl.Header(h =>
                                        {
                                            foreach (var t in new[] { "Lien", "Transport", "Coût", "Intervalle", "Sites" })
                                                h.Cell().Background(Colors.Blue.Lighten4).Padding(4).Text(t).SemiBold();
                                        });
                                        foreach (var l in sites.SiteLinks)
                                        {
                                            tbl.Cell().Padding(3).Text(l.Name);
                                            tbl.Cell().Padding(3).Text(l.Transport);
                                            tbl.Cell().Padding(3).Text(l.Cost.ToString());
                                            tbl.Cell().Padding(3).Text(l.ReplicationIntervalMinutes.ToString());
                                            tbl.Cell().Padding(3).Text(string.Join(", ", l.SiteNames));
                                        }
                                    });
                                }

                                // Per-site documentation
                                foreach (var site in sites.Sites.Where(s => !string.IsNullOrWhiteSpace(s.Description)))
                                {
                                    col.Item().PaddingTop(6).Text($"🏢 {site.Name}").SemiBold().FontSize(11);
                                    col.Item().Text(StripMarkdownBlock(site.Description))
                                       .FontSize(9).FontColor(Colors.Grey.Darken1);
                                }

                                // Per-site-link documentation
                                foreach (var link in sites.SiteLinks.Where(l => !string.IsNullOrWhiteSpace(l.Description)))
                                {
                                    col.Item().PaddingTop(6).Text($"🔗 {link.Name}").SemiBold().FontSize(11);
                                    col.Item().Text(StripMarkdownBlock(link.Description))
                                       .FontSize(9).FontColor(Colors.Grey.Darken1);
                                }
                            }

                            // Per-object documentation
                            if (domain is not null)
                            {
                                var documented = FlattenTree(domain)
                                    .Where(o => !string.IsNullOrWhiteSpace(o.Description))
                                    .ToList();
                                if (documented.Count > 0)
                                {
                                    col.Item().PaddingTop(10).Text("Documentation des objets").SemiBold().FontSize(14);
                                    foreach (var obj in documented)
                                    {
                                        col.Item().PaddingTop(6).Text($"{TypeIcon(obj.Type)} {obj.Name}").SemiBold().FontSize(11);
                                        col.Item().Text(StripMarkdownBlock(obj.Description))
                                           .FontSize(9).FontColor(Colors.Grey.Darken1);
                                    }
                                }
                            }
                        });

                        page.Footer().AlignRight().Text(t =>
                        {
                            t.Span("Page "); t.CurrentPageNumber(); t.Span(" / "); t.TotalPages();
                        });
                    });
                }).GeneratePdf(filePath);

                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"PDF export error: {ex.Message}");
                return Task.FromResult(false);
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

        /// <summary>
        /// Strip markdown syntax from a single line for plain-text formats (DOCX/PDF).
        /// Returns null for lines that should be omitted (e.g. code fences, table separators).
        /// </summary>
        private static string? StripMarkdownLine(string raw)
        {
            var line = raw.TrimEnd();
            if (line == "---" || line == "```" || line.StartsWith("```")) return null;
            if (line.StartsWith('|') && line.Replace("|", "").Replace("-", "").Replace(":", "").Trim().Length == 0) return null; // separator row
            // Headings → plain text
            if (line.StartsWith("# "))  return line[2..].Trim();
            if (line.StartsWith("## ")) return line[3..].Trim();
            if (line.StartsWith("### ")) return line[4..].Trim();
            if (line.StartsWith("#### ")) return line[5..].Trim();
            // Strip bold/italic/code inline
            line = System.Text.RegularExpressions.Regex.Replace(line, @"\*{1,2}([^*]+)\*{1,2}", "$1");
            line = System.Text.RegularExpressions.Regex.Replace(line, @"`([^`]+)`", "$1");
            // Strip > blockquote
            if (line.StartsWith('>')) line = line.TrimStart('>', ' ');
            return line.Length == 0 ? null : line;
        }

        /// <summary>Strip markdown for a whole block of text (for PDF inline text).</summary>
        private static string StripMarkdownBlock(string markdown)
        {
            var lines = markdown.Split('\n')
                .Select(StripMarkdownLine)
                .Where(l => l is not null)
                .Select(l => l!);
            return string.Join(" ", lines).Trim();
        }
    }
}
