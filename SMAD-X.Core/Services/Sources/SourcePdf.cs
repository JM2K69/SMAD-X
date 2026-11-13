using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using OfficeIMO.Drawing;
using OfficeIMO.Pdf;
using SMADX.Services.Audit;

namespace SMADX.Services.Sources
{
    /// <summary>
    /// Le PDF d'une fiche, mis en page aux couleurs de SMAD-X : sur chaque page, un bandeau bleu nuit (le logo, « SMAD-X ·
    /// Fiches des sources », le référentiel) et un pied de page (d'où vient le document, la page) ; puis le bloc de titre
    /// (pastilles du référentiel et du niveau ANSSI, titre officiel, identifiant), le tableau d'identité, les règles de
    /// SMAD-X qui citent la source (à la couleur de leur gravité), le texte officiel, la recommandation officielle dans un
    /// encadré, et la source et la licence dans un cartouche. Une fiche par PDF : l'enseignant donne ou imprime la seule
    /// recommandation qui l'intéresse.
    /// </summary>
    public static partial class SourcePdf
    {
        private const string Repository = "https://github.com/JM2K69/SMAD-X";
        private const double PageWidth = 595.28, PageHeight = 841.89, Side = 50, Band = 64;

        // La palette de SMAD-X : le bleu nuit du bandeau, le dégradé bleu → turquoise du logo, des gris d'interface
        private static readonly PdfColor Navy = Hex("#0B1F3A");
        private static readonly PdfColor NavyText = Hex("#123B6D");
        private static readonly PdfColor Blue = Hex("#1F6FEB");
        private static readonly PdfColor Teal = Hex("#2BB3A3");
        private static readonly PdfColor Ink = Hex("#1A202C");
        private static readonly PdfColor Muted = Hex("#64748B");
        private static readonly PdfColor Rule = Hex("#E2E8F0");
        private static readonly PdfColor Soft = Hex("#F4F6FA");
        private static readonly PdfColor CodeBack = Hex("#EEF2F7");
        private static readonly PdfColor CodeInk = Hex("#9D174D");
        private static readonly PdfColor RecoBack = Hex("#EFF9F6");
        private static readonly PdfColor White = Hex("#FFFFFF");
        private static readonly PdfColor BandText = Hex("#A9C4E6");

        private static string T(string lang, string fr, string en) => lang == "fr" ? fr : en;

        private static PdfColor Hex(string hex) => PdfColor.FromRgb(Convert.ToByte(hex[1..3], 16), Convert.ToByte(hex[3..5], 16), Convert.ToByte(hex[5..7], 16));

        /// <summary>La couleur d'un niveau ANSSI (1 : le plus critique) ou d'une gravité de SMAD-X.</summary>
        private static PdfColor LevelColor(int level) => level switch { 1 => Hex("#C0392B"), 2 => Hex("#D35400"), 3 => Hex("#9A7D0A"), _ => Hex("#2E86C1") };

        private static PdfColor SeverityColor(AuditSeverity severity) => severity switch
        {
            AuditSeverity.Critical => LevelColor(1),
            AuditSeverity.High => LevelColor(2),
            AuditSeverity.Medium => LevelColor(3),
            AuditSeverity.Low => LevelColor(4),
            _ => Muted
        };

        /// <summary>Le nom court du référentiel, en capitales, pour sa pastille.</summary>
        private static string Badge(string lang, string framework) => framework switch
        {
            "anssi" => "ANSSI",
            "nist" => "NIST",
            "cis" => "CIS CONTROLS",
            "mitre-attack" => "MITRE ATT&CK",
            "microsoft" => "MICROSOFT LEARN",
            _ => T(lang, "MODÈLE DE CLOISONNEMENT", "TIER MODEL")
        };

        public static void Save(string file, string version, SourceFramework f, SourceItem item, string lang,
            IReadOnlyDictionary<string, (string Fr, string En)> rules)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            var culture = CultureInfo.GetCultureInfo(T(lang, "fr-FR", "en-US"));
            var day = DateTime.UtcNow.ToString(T(lang, "d MMMM yyyy", "MMMM d, yyyy"), culture);
            var name = T(lang, f.NameFr, f.NameEn);
            var title = T(lang, item.TitleFr, item.TitleEn);
            var sheetUrl = $"{Repository}/blob/master/sources/{f.Id}/{lang}/{Uri.EscapeDataString(SourceSheets.FileName(item))}.md";

            var document = PdfDocument.Create(MarkdownPdf.NewPdfOptions());
            document.Meta(
                title: item.Framework is "microsoft" or "tier-models" ? title : $"{item.Id} — {title}",
                author: $"SMAD-X {version}",
                subject: T(lang, $"{name} — fiche extraite de la suite SMA-X : {Repository}", $"{name} — sheet taken from the SMA-X suite: {Repository}"),
                keywords: $"SMAD-X, SMA-X, {name}, {item.Id}, Active Directory");
            document.Compose(d => d.Page(page =>
            {
                page.Size(PageWidth, PageHeight);
                page.Margin(Side, Band + 34, Side, 64);
                page.DefaultTextStyle(new PdfTextStyle { Font = PdfStandardFont.Helvetica, FontSize = 9.5, Color = Ink });
                page.RegisterEmbeddedFontFallbacks(MarkdownPdf.FontFallbacks);
                page.UseTextFallbacks();

                // Le bandeau : bleu nuit en dégradé, un liseré bleu → turquoise dessous ; un filet au-dessus du pied de page
                page.BackgroundTopBand(Band, Navy, fillGradient: OfficeLinearGradient.Horizontal(OfficeColor.ParseHex("#0B1F3A"), OfficeColor.ParseHex("#17497F")));
                page.BackgroundRectangle(0, PageHeight - Band - 3, PageWidth, 3, Blue,
                    fillGradient: OfficeLinearGradient.Horizontal(OfficeColor.ParseHex("#4C9AFF"), OfficeColor.ParseHex("#2BB3A3")));
                page.BackgroundRectangle(Side, 46, PageWidth - 2 * Side, 0.6, Rule);

                page.Header(h => h.Content(c => c.Row(r =>
                {
                    r.FixedColumn(40, col => col.Image(Logo.Value, 30, 30));
                    r.RelativeColumn(col => col.Paragraph(p => p
                        .Bold(true).FontSize(13).Color(White).Text("SMAD-X")
                        .Bold(false).FontSize(8.5).Color(BandText).LineBreak().Text(T(lang, "Fiches des sources · suite SMA-X", "Source sheets · SMA-X suite"))));
                    r.RelativeColumn(col => col.Paragraph(p => p
                        .Bold(true).FontSize(9).Color(White).Text(name)
                        .Bold(false).FontSize(8).Color(BandText).LineBreak().Text(T(lang, $"Relevé du {f.Retrieved}", $"Retrieved on {f.Retrieved}")),
                        PdfAlign.Right), 1.4);
                }), 17));

                page.Footer(foot =>
                {
                    foot.FontSize(7.5).Color(Muted).Offset(30);
                    foot.Zones(T(lang, $"SMAD-X {version} · extrait de la suite SMA-X · github.com/JM2K69/SMAD-X", $"SMAD-X {version} · taken from the SMA-X suite · github.com/JM2K69/SMAD-X"),
                        null!, T(lang, "Page {page} / {pages}", "Page {page} / {pages}"));
                });

                page.Content(c =>
                {
                    // Le bloc de titre : pastilles, titre officiel, identifiant
                    c.Paragraph(p =>
                    {
                        Pill(p, Badge(lang, f.Id), Blue);
                        if (item.Levels is { Count: > 0 } levels)
                            foreach (var level in levels)
                            {
                                p.Text(Nbsp + Nbsp);
                                Pill(p, T(lang, $"NIVEAU {level}", $"LEVEL {level}"), LevelColor(level));
                            }
                        p.Text(Nbsp + Nbsp);
                        Pill(p, T(lang, "FICHE SOURCE", "SOURCE SHEET"), Muted);
                    }, style: new PdfParagraphStyle { SpacingAfter = 10 });
                    c.Paragraph(p => p.Bold(true).FontSize(19).Color(Navy).Text(title),
                        style: new PdfParagraphStyle { LineHeight = 1.18, SpacingAfter = 3 });
                    if (item.Framework is not ("microsoft" or "tier-models"))
                        c.Paragraph(p => p.Font(PdfStandardFont.Courier).FontSize(10.5).Color(Blue).Text(item.Id),
                            style: new PdfParagraphStyle { SpacingAfter = 12 });
                    else
                        c.Paragraph(p => p.FontSize(10).Color(Muted).Text(name), style: new PdfParagraphStyle { SpacingAfter = 12 });

                    // Le tableau d'identité
                    c.KeyValueTable(SourceSheets.Facts(lang, f, item).Select(Fact), style: new PdfTableStyle
                    {
                        BorderColor = Rule, BorderWidth = 0.6, RowSeparatorColor = Rule, RowSeparatorWidth = 0.6,
                        BodyColumnFills = [Soft, null], ColumnWidthPoints = [150, null],
                        CellPaddingX = 9, CellPaddingY = 5.5, FontSize = 9.5, SpacingAfter = 4, RightAlignNumeric = false
                    });

                    // Les règles de SMAD-X qui citent la source, à la couleur de leur gravité
                    if (item.UsedBy.Count > 0)
                    {
                        Section(c, T(lang, "Règles de SMAD-X qui la citent", "SMAD-X rules that cite it"));
                        foreach (var id in item.UsedBy)
                        {
                            var severity = AuditRules.All.FirstOrDefault(r => r.Id == id)?.Severity ?? AuditSeverity.Info;
                            var ruleTitle = rules.TryGetValue(id, out var t) ? T(lang, t.Fr, t.En) : "";
                            c.Paragraph(p =>
                            {
                                Pill(p, id, SeverityColor(severity));
                                p.Text(Nbsp + Nbsp + Nbsp).Color(Ink).Text(ruleTitle);
                            }, style: new PdfParagraphStyle { SpacingAfter = 5 });
                        }
                    }

                    // Le texte repris de la source, puis la recommandation officielle dans son encadré
                    if (SourceSheets.Text(lang, item) is { } text && !string.IsNullOrWhiteSpace(text))
                    {
                        Section(c, SourceSheets.TextLabel(lang, item));
                        if (SourceSheets.EnglishOnly(lang, item))
                            c.Paragraph(p => p.Italic(true).FontSize(8.5).Color(Muted).Text("En anglais : c'est la version qui fait foi."),
                                style: new PdfParagraphStyle { SpacingAfter = 6 });
                        Blocks(c, text);
                    }
                    if (SourceSheets.Reco(lang, item) is { } reco && !string.IsNullOrWhiteSpace(reco))
                    {
                        Section(c, T(lang, "Recommandation officielle", "Official recommendation"));
                        c.Panel(panel => Blocks(panel, reco), new PdfPanelStyle
                        {
                            Background = RecoBack, LeftBorder = new PdfPanelBorder { Color = Teal, Width = 3 },
                            PaddingX = 14, PaddingY = 10, SpacingBefore = 2, SpacingAfter = 6
                        });
                    }

                    // La source et la licence, dans un cartouche
                    Section(c, T(lang, "Source et licence", "Source and licence"));
                    c.Panel(panel =>
                    {
                        Line(panel, lang, "Source", [PdfTextRun.Link(f.Url, f.Url, Blue, false, fontSize: 8.5)]);
                        if (f.Published != null) Line(panel, lang, T(lang, "Publication relevée", "Retrieved publication"), [PdfTextRun.Normal(f.Published, Ink, 8.5)]);
                        Line(panel, lang, T(lang, "Relevé le", "Retrieved on"), [PdfTextRun.Normal(T(lang, $"{f.Retrieved}, par tools/SmadxSources", $"{f.Retrieved}, by tools/SmadxSources"), Ink, 8.5)]);
                        Line(panel, lang, T(lang, "Licence", "Licence"), Inline(T(lang, f.LicenseFr, f.LicenseEn), 8.5, Ink));
                        if (f.Notice != null) Line(panel, lang, T(lang, "Mention", "Notice"), Inline(f.Notice, 8.5, Ink));
                    }, new PdfPanelStyle { Background = Soft, BorderColor = Rule, BorderWidth = 0.6, CornerRadius = 4, PaddingX = 12, PaddingY = 9, SpacingAfter = 10, KeepTogether = true });

                    c.Paragraph(p => p.Italic(true).FontSize(7.5).Color(Muted)
                        .Text(T(lang, $"Fiche extraite de SMAD-X {version}, suite SMA-X (Simuler, Modéliser, Auditer – eXpert), le {day}. Fiche Markdown : ",
                                      $"Sheet taken from SMAD-X {version}, SMA-X suite (Simulate, Model, Audit – eXpert), on {day}. Markdown sheet: "))
                        .Link(sheetUrl, sheetUrl, Blue), style: new PdfParagraphStyle { LineHeight = 1.3 });
                });
            }));
            document.Save(file);
        }

        /// <summary>Une pastille : le texte en capitales, blanc et gras sur la couleur donnée.</summary>
        private static void Pill(PdfParagraphBuilder p, string text, PdfColor color) =>
            p.BackgroundColor(color).Color(White).Bold(true).FontSize(7.5).Text($"{Nbsp}{text}{Nbsp}").ResetBackgroundColor().ResetColor().Bold(false).ResetFontSize();

        /// <summary>Une espace insécable : les espaces ordinaires en bord de segment ne s'impriment pas.</summary>
        private const string Nbsp = " ";

        /// <summary>
        /// Un titre de section : petites capitales bleues soulignées d'un filet. Un panneau à bordure basse plutôt qu'un
        /// paragraphe suivi d'un filet : il reste avec ce qui le suit, au lieu de finir seul en bas de page.
        /// </summary>
        private static void Section(PdfContentBuilder c, string title) =>
            c.Panel(panel => panel.Paragraph(p => p.Runs([new PdfTextRun(title.ToUpperInvariant(), bold: true, color: Blue, fontSize: 9)]),
                    style: new PdfParagraphStyle { SpacingAfter = 0 }),
                new PdfPanelStyle
                {
                    BottomBorder = new PdfPanelBorder { Color = Rule, Width = 0.8 },
                    PaddingX = 0, PaddingY = 4, SpacingBefore = 14, SpacingAfter = 9, KeepTogether = true, KeepWithNext = true
                });

        /// <summary>Une ligne « libellé : valeur » du cartouche de la source.</summary>
        private static void Line(PdfContentBuilder c, string lang, string label, IEnumerable<PdfTextRun> value) =>
            c.Paragraph(p => p.Runs([PdfTextRun.Bolded(label + T(lang, " : ", ": "), Muted, 8.5), .. value]),
                style: new PdfParagraphStyle { SpacingAfter = 3, LineHeight = 1.3 });

        /// <summary>Une ligne du tableau d'identité : le libellé en gris, la valeur en code, en lien ou en texte.</summary>
        private static PdfKeyValueRow Fact(SheetFact fact) => PdfKeyValueRow.Rich(
            [PdfTextRun.Bolded(fact.Label, Muted, 8.5)],
            [fact.Code ? PdfTextRun.Normal(fact.Value, CodeInk, 9, CodeBack, PdfStandardFont.Courier)
                : fact.Link != null ? PdfTextRun.Link(fact.Value, fact.Link, Blue, false, fontSize: 9.5)
                : PdfTextRun.Normal(fact.Value, Ink, 9.5)]);

        // ─── Le Markdown des textes officiels : paragraphes, listes (imbriquées), intertitres, et en ligne le gras,
        //     l'italique, le code et les liens ───

        private static void Blocks(PdfContentBuilder c, string markdown)
        {
            var paragraph = new List<string>();
            var list = new List<(int Level, bool Numbered, string Text)>();

            void FlushParagraph()
            {
                if (paragraph.Count == 0) return;
                var text = string.Join(" ", paragraph);
                paragraph.Clear();
                // Une ligne entière en italique entre parenthèses (« (Tableau des objets concernés…) ») : une note, en gris
                var note = Regex.IsMatch(text, @"^\*\(.*\)\*$");
                c.Paragraph(p => p.Runs(Inline(text, 9.5, note ? Muted : Ink)), style: new PdfParagraphStyle { LineHeight = 1.38, SpacingAfter = 7 });
            }

            void FlushList()
            {
                var i = 0;
                while (i < list.Count)
                {
                    var (level, numbered, _) = list[i];
                    var group = list.Skip(i).TakeWhile(x => x.Level == level && x.Numbered == numbered).ToList();
                    var style = new PdfListStyle { LeftIndent = 6 + level * 16, ItemSpacing = 3, LineHeight = 1.35, SpacingAfter = 5, MarkerColor = Teal };
                    var items = group.Select(x => PdfListItem.Rich(Inline(x.Text, 9.5, Ink))).ToList();
                    if (numbered) c.RichNumbered(items, style: style);
                    else c.RichBullets(items, style: style);
                    i += group.Count;
                }
                list.Clear();
            }

            foreach (var raw in markdown.Replace("\r\n", "\n").Split('\n'))
            {
                var line = raw.TrimEnd();
                if (line.Trim().Length == 0)
                {
                    FlushParagraph();
                    FlushList();
                    continue;
                }
                if (ListLine().Match(line) is { Success: true } item)
                {
                    FlushParagraph();
                    list.Add((Math.Min(item.Groups[1].Value.Length / 2, 3), char.IsDigit(item.Groups[2].Value[0]), item.Groups[3].Value));
                    continue;
                }
                if (list.Count > 0 && raw.StartsWith(' '))
                {
                    list[^1] = list[^1] with { Text = list[^1].Text + " " + line.Trim() };
                    continue;
                }
                if (HeadingLine().Match(line) is { Success: true } heading)
                {
                    FlushParagraph();
                    FlushList();
                    c.Paragraph(p => p.Bold(true).FontSize(10.5).Color(NavyText).Text(heading.Groups[1].Value.Trim()),
                        style: new PdfParagraphStyle { SpacingBefore = 6, SpacingAfter = 4, KeepWithNext = true });
                    continue;
                }
                FlushList();
                paragraph.Add(line.Trim());
            }
            FlushParagraph();
            FlushList();
        }

        /// <summary>Le Markdown en ligne : `code`, **gras**, *italique*, [texte](lien) ; le reste en texte.</summary>
        private static IEnumerable<PdfTextRun> Inline(string text, double size, PdfColor color)
        {
            var position = 0;
            foreach (Match m in InlineToken().Matches(text))
            {
                if (m.Index > position) yield return PdfTextRun.Normal(Unescape(text[position..m.Index]), color, size);
                if (m.Groups["code"].Success) yield return PdfTextRun.Normal(m.Groups["code"].Value, CodeInk, size - 0.5, CodeBack, PdfStandardFont.Courier);
                else if (m.Groups["bold"].Success) yield return PdfTextRun.Bolded(Unescape(m.Groups["bold"].Value), color, size);
                else if (m.Groups["italic"].Success) yield return PdfTextRun.Italicized(Unescape(m.Groups["italic"].Value), color, size);
                else yield return PdfTextRun.Link(Unescape(m.Groups["label"].Value), m.Groups["url"].Value, Blue, false, fontSize: size);
                position = m.Index + m.Length;
            }
            if (position < text.Length) yield return PdfTextRun.Normal(Unescape(text[position..]), color, size);
        }

        private static string Unescape(string text) => text.Replace("\\|", "|").Replace("\\*", "*").Replace("\\_", "_");

        [GeneratedRegex(@"`(?<code>[^`]+)`|\*\*(?<bold>.+?)\*\*|\*(?<italic>[^*\s][^*]*?)\*|\[(?<label>[^\]]+)\]\((?<url>[^)\s]+)\)")]
        private static partial Regex InlineToken();

        [GeneratedRegex(@"^(\s*)([-*+]|\d+\.)\s+(.*)$")]
        private static partial Regex ListLine();

        [GeneratedRegex(@"^#{1,6}\s+(.*)$")]
        private static partial Regex HeadingLine();

        /// <summary>La version de SMAD-X, d'après Directory.Build.props.</summary>
        public static string Version(string repo)
        {
            var props = File.ReadAllText(Path.Combine(repo, "Directory.Build.props"));
            return Regex.Match(props, @"<Version>([^<]+)</Version>") is { Success: true } m ? m.Groups[1].Value : "?";
        }

        private static readonly Lazy<byte[]> Logo = new(() =>
        {
            using var stream = typeof(SourcePdf).Assembly.GetManifestResourceStream("Sources/smadx-logo.png")
                ?? throw new InvalidOperationException("Embedded resource Sources/smadx-logo.png is missing");
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            return buffer.ToArray();
        });
    }
}
