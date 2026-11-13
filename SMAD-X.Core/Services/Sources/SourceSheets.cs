using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace SMADX.Services.Sources
{
    /// <summary>Une ligne du tableau d'identité d'une fiche : libellé, valeur, lien de la valeur, valeur en code.</summary>
    public sealed record SheetFact(string Label, string Value, string? Link = null, bool Code = false);

    /// <summary>
    /// Les fiches Markdown des sources : un dossier par référentiel (sources/anssi, sources/nist, sources/cis,
    /// sources/mitre-attack, sources/microsoft, sources/tier-models), un sous-dossier par langue (fr, en), et dedans une
    /// fiche par élément cité, nommée d'après l'identifiant que SMAD-X affiche (vuln_spn_priv.md, T1558.003.md,
    /// AC-6(5).md…), avec un sommaire (README.md). Chaque fiche se suffit : l'élément, les règles de SMAD-X qui le citent,
    /// le texte officiel quand la licence le permet, puis la source et la licence. L'index de provenance est
    /// sources/README.md (anglais) et sources/ReadmeFR.md (français). Tout ce qui n'est pas un texte officiel est rédigé
    /// dans la langue de la fiche ; un texte officiel qui n'existe qu'en anglais reste en anglais, et la fiche française le dit.
    /// </summary>
    public static class SourceSheets
    {
        public static readonly string[] Order = ["anssi", "nist", "cis", "mitre-attack", "microsoft", "tier-models"];
        public static readonly string[] Languages = ["fr", "en"];

        private static string T(string lang, string fr, string en) => lang == "fr" ? fr : en;

        /// <summary>
        /// Le nom de la fiche d'un élément, sans extension : l'identifiant que SMAD-X affiche (vuln_spn_priv, T1558.003),
        /// sans le préfixe du référentiel (AC-6(5) pour « SP 800-53 Rev. 5 AC-6(5) », 5.4 pour « CIS Controls v8 5.4 ») ;
        /// pour une page Microsoft Learn, le nom de la page dans son adresse.
        /// </summary>
        public static string FileName(SourceItem item)
        {
            var name = item.Framework switch
            {
                "nist" => item.Id.Replace("SP 800-53 Rev. 5 ", ""),
                "cis" => item.Id.Replace("CIS Controls v8 ", ""),
                "microsoft" => new Uri(item.Url).Segments.Last().Trim('/'),
                _ => item.Id
            };
            return string.Concat(name.Select(c => char.IsWhiteSpace(c) || "<>:\"/\\|?*".Contains(c) ? '-' : c));
        }

        /// <summary>Le dossier des fiches d'un référentiel dans une langue : sources/{référentiel}/{langue}.</summary>
        public static string Folder(string repo, string framework, string lang) => Path.Combine(repo, "sources", framework, lang);

        public static void WriteAll(string repo, SourcesSnapshot snapshot, IReadOnlyDictionary<string, (string Fr, string En)> rules)
        {
            foreach (var framework in snapshot.Frameworks)
            {
                var items = snapshot.Items.Where(i => i.Framework == framework.Id).ToList();
                var twins = items.GroupBy(FileName, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
                if (twins.Count > 0) throw new InvalidOperationException($"{framework.Id} : deux éléments donnent la même fiche ({string.Join(", ", twins)})");
                foreach (var lang in Languages)
                {
                    var dir = Folder(repo, framework.Id, lang);
                    Directory.CreateDirectory(dir);
                    var written = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "README.md" };
                    foreach (var item in items)
                    {
                        var name = FileName(item) + ".md";
                        Write(Path.Combine(dir, name), Sheet(lang, framework, item, rules, links: true));
                        written.Add(name);
                    }
                    Write(Path.Combine(dir, "README.md"), Contents(lang, framework, items));
                    // La fiche d'un élément que SMAD-X ne cite plus disparaît, avec son PDF
                    foreach (var stale in Directory.GetFiles(dir, "*.md").Where(p => !written.Contains(Path.GetFileName(p))))
                    {
                        File.Delete(stale);
                        File.Delete(Path.ChangeExtension(stale, ".pdf"));
                    }
                }
            }
            Write(Path.Combine(repo, "sources", "README.md"), Index("en", snapshot));
            Write(Path.Combine(repo, "sources", "ReadmeFR.md"), Index("fr", snapshot));
        }

        private static void Write(string path, string text) => File.WriteAllText(path, text.Replace("\r\n", "\n").TrimEnd() + "\n", new UTF8Encoding(false));

        /// <summary>Un lien Markdown vers un fichier voisin, dont le nom peut contenir des parenthèses (AC-6(5).md).</summary>
        private static string Link(string text, string path) => $"[{text}]({string.Join("/", path.Split('/').Select(Uri.EscapeDataString))})";

        /// <summary>L'index : d'où vient chaque référentiel, quand il a été relevé, sous quelle licence, et comment mettre à jour.</summary>
        public static string Index(string lang, SourcesSnapshot snapshot)
        {
            var s = new StringBuilder();
            s.Append(T(lang, "# Les sources de SMAD-X\n\n", "# SMAD-X sources\n\n"));
            s.Append(T(lang, "> English: [README.md](README.md)\n\n", "> Français : [ReadmeFR.md](ReadmeFR.md)\n\n"));
            s.Append(T(lang,
                "Les règles d'audit de SMAD-X, ses niveaux et ses recettes d'exercice s'appuient sur ces référentiels. Chaque fiche dit d'où vient l'élément, quand il a été relevé, sous quelle licence, et quelles règles de SMAD-X le citent. Les textes officiels ne sont repris que lorsque la licence le permet.\n\n",
                "SMAD-X's audit rules, levels and lab recipes rely on these frameworks. Each sheet says where the item comes from, when it was retrieved, under which licence, and which SMAD-X rules cite it. Official texts are only reproduced where the licence allows it.\n\n"));
            s.Append(T(lang, $"Relevé du {snapshot.Retrieved}, par `{snapshot.Tool}`.\n\n", $"Retrieved on {snapshot.Retrieved}, by `{snapshot.Tool}`.\n\n"));
            s.Append(T(lang, "| Référentiel | Source | Publication relevée | Fiches | Licence |\n|---|---|---|---|---|\n",
                             "| Framework | Source | Retrieved publication | Sheets | Licence |\n|---|---|---|---|---|\n"));
            foreach (var f in snapshot.Frameworks)
            {
                var count = snapshot.Items.Count(i => i.Framework == f.Id);
                s.Append($"| {Link(T(lang, f.NameFr, f.NameEn), $"{f.Id}/{lang}/README.md")} | [{Host(f.Url)}]({f.Url}) | {f.Published ?? "—"} | {count} | {Cell(T(lang, f.LicenseFr, f.LicenseEn))} |\n");
            }
            s.Append(T(lang, """

                ## Rangement

                Un dossier par référentiel (`anssi`, `nist`, `cis`, `mitre-attack`, `microsoft`, `tier-models`), et dedans un dossier par langue (`fr`, `en`). Chaque élément cité a sa fiche, nommée d'après l'identifiant que SMAD-X affiche : `anssi/fr/vuln_spn_priv.md`, `mitre-attack/fr/T1558.003.md`, `nist/fr/AC-6(5).md`, `cis/fr/5.4.md` ; pour une page Microsoft Learn, le nom de la page (`microsoft/fr/protected-users-security-group.md`). Le `README.md` de chaque dossier en est le sommaire.

                Une fois générés, les PDF sont à côté des fiches, sous le même nom : `anssi/fr/vuln_spn_priv.pdf`. On peut ainsi donner ou imprimer la seule fiche d'une recommandation.

                ## Mettre à jour

                ```bash
                dotnet run --project tools/SmadxSources -- update
                ```

                L'outil relit chaque source sur le web, réécrit ces fiches et `sources/sources.json`, et affiche ce qui a changé depuis le relevé précédent (titre, niveau, texte). Si un élément cité par SMAD-X a disparu de sa source, il s'arrête sans rien écrire. Relisez le diff avant de commiter.

                ```bash
                dotnet run --project tools/SmadxSources -- write
                ```

                Réécrit les fiches depuis `sources/sources.json`, sans rien relire sur le web : après le changement du titre d'une règle, par exemple. Une nouvelle référence dans une règle demande un `update`.

                ## Les PDF

                ```bash
                dotnet run --project tools/SmadxSources -- pdf
                dotnet run --project tools/SmadxSources -- pdf --only vuln_spn_priv
                ```

                Un PDF par fiche et par langue, aux couleurs de SMAD-X, à côté de la fiche. `--only` ne génère que les fiches d'un identifiant (`vuln_spn_priv`) ou d'un référentiel (`anssi`). Les PDF ne sont pas versionnés : on les régénère depuis les fiches.

                ## Dans SMAD-X Studio

                Studio embarque ce relevé : **Fichier > Fiches des sources...** montre les fiches règle par règle, les exporte en PDF et relit une source sur le web pour voir ce qui a changé depuis la version installée, sans le dépôt ni le SDK .NET. Le code est le même que celui de cet outil (`SMAD-X.Core/Services/Sources`).
                """, """

                ## Layout

                One folder per framework (`anssi`, `nist`, `cis`, `mitre-attack`, `microsoft`, `tier-models`), with one folder per language inside (`fr`, `en`). Each cited item has its sheet, named after the identifier SMAD-X shows: `anssi/en/vuln_spn_priv.md`, `mitre-attack/en/T1558.003.md`, `nist/en/AC-6(5).md`, `cis/en/5.4.md`; for a Microsoft Learn page, the page name (`microsoft/en/protected-users-security-group.md`). Each folder's `README.md` lists its sheets.

                Once generated, the PDFs sit next to the sheets, under the same name: `anssi/en/vuln_spn_priv.pdf`. You can hand out or print the sheet of a single recommendation.

                ## Updating

                ```bash
                dotnet run --project tools/SmadxSources -- update
                ```

                The tool reads each source again on the web, rewrites these sheets and `sources/sources.json`, and shows what changed since the previous retrieval (title, level, text). If an item cited by SMAD-X has disappeared from its source, it stops without writing anything. Review the diff before committing.

                ```bash
                dotnet run --project tools/SmadxSources -- write
                ```

                Rewrites the sheets from `sources/sources.json`, without reading anything on the web: after a rule's title changes, for instance. A new reference in a rule needs an `update`.

                ## The PDFs

                ```bash
                dotnet run --project tools/SmadxSources -- pdf
                dotnet run --project tools/SmadxSources -- pdf --only vuln_spn_priv
                ```

                One PDF per sheet and language, in SMAD-X colours, next to the sheet. `--only` generates only the sheets of one identifier (`vuln_spn_priv`) or one framework (`anssi`). The PDFs are not versioned: they are generated again from the sheets.

                ## In SMAD-X Studio

                Studio ships this retrieval: **File > Source sheets...** shows the sheets rule by rule, exports them as PDF and reads a source again on the web to see what changed since the installed version, without the repository or the .NET SDK. The code is the same as this tool's (`SMAD-X.Core/Services/Sources`).
                """));
            return s.ToString();
        }

        /// <summary>Le sommaire d'un dossier : la provenance du référentiel, puis une ligne par fiche.</summary>
        public static string Contents(string lang, SourceFramework f, IReadOnlyList<SourceItem> items)
        {
            var s = new StringBuilder();
            s.Append("# ").Append(T(lang, f.NameFr, f.NameEn)).Append("\n\n");
            s.Append(T(lang, "> [Toutes les sources](../../ReadmeFR.md) · English: [../en/README.md](../en/README.md)\n\n",
                             "> [All sources](../../README.md) · Français : [../fr/README.md](../fr/README.md)\n\n"));
            Origin(s, lang, f);
            s.Append(T(lang, "## Fiches\n\n", "## Sheets\n\n"));
            var levels = items.Any(i => i.Levels is { Count: > 0 });
            s.Append(T(lang, "| Fiche | Titre |", "| Sheet | Title |")).Append(levels ? T(lang, " Niveau ANSSI |", " ANSSI level |") : "")
             .Append(T(lang, " Cité par SMAD-X |\n", " Cited by SMAD-X |\n")).Append(levels ? "|---|---|---|---|\n" : "|---|---|---|\n");
            foreach (var item in items)
            {
                var name = FileName(item);
                s.Append($"| {Link(name, name + ".md")} | {Cell(T(lang, item.TitleFr, item.TitleEn))} |");
                if (levels) s.Append(' ').Append(item.Levels is { Count: > 0 } l ? string.Join(", ", l) : "—").Append(" |");
                s.Append(' ').Append(item.UsedBy.Count == 0 ? T(lang, "son modèle de cloisonnement", "its tier model") : string.Join(" · ", item.UsedBy)).Append(" |\n");
            }
            s.Append(T(lang, "\nLe PDF de chaque fiche est à côté d'elle, sous le même nom, une fois généré par `dotnet run --project tools/SmadxSources -- pdf`.\n",
                             "\nEach sheet's PDF sits next to it, under the same name, once generated by `dotnet run --project tools/SmadxSources -- pdf`.\n"));
            return s.ToString();
        }

        /// <summary>D'où vient le référentiel : sa page, la publication relevée, la date du relevé, sa licence et sa mention.</summary>
        private static void Origin(StringBuilder s, string lang, SourceFramework f)
        {
            s.Append(T(lang, "- **Source** : ", "- **Source**: ")).Append($"[{f.Url}]({f.Url})\n");
            if (f.Published != null) s.Append(T(lang, "- **Publication relevée** : ", "- **Retrieved publication**: ")).Append(f.Published).Append('\n');
            s.Append(T(lang, "- **Relevé le** : ", "- **Retrieved on**: ")).Append(f.Retrieved).Append(T(lang, ", par `tools/SmadxSources`\n", ", by `tools/SmadxSources`\n"));
            s.Append(T(lang, "- **Licence** : ", "- **Licence**: ")).Append(T(lang, f.LicenseFr, f.LicenseEn)).Append('\n');
            if (f.Notice != null) s.Append(T(lang, "- **Mention** : ", "- **Notice**: ")).Append(f.Notice).Append('\n');
            s.Append('\n');
        }

        /// <summary>
        /// La fiche d'un élément : son titre, son tableau (référentiel, identifiant, niveaux, dates, lien, règles de SMAD-X
        /// qui le citent), le texte officiel et la recommandation quand la licence le permet, puis la source et la licence.
        /// Dans le dépôt (<paramref name="links"/>), une ligne mène au sommaire et à la fiche dans l'autre langue ; le PDF
        /// s'en passe.
        /// </summary>
        public static string Sheet(string lang, SourceFramework f, SourceItem item, IReadOnlyDictionary<string, (string Fr, string En)> rules, bool links)
        {
            var s = new StringBuilder();
            var title = T(lang, item.TitleFr, item.TitleEn);
            var heading = item.Framework is "microsoft" or "tier-models" ? title : $"{item.Id} — {title}";
            s.Append("# ").Append(heading).Append("\n\n");
            if (links)
            {
                var other = T(lang, "en", "fr");
                s.Append(T(lang, "> [Sommaire : ", "> [Contents: ")).Append(T(lang, f.NameFr, f.NameEn)).Append("](README.md) · ")
                 .Append(T(lang, "English: ", "Français : ")).Append(Link(FileName(item) + ".md", $"../{other}/{FileName(item)}.md")).Append("\n\n");
            }
            // Alignement à gauche explicite : sans lui, le PDF aligne à droite les dates et les versions, qu'il prend pour des nombres
            s.Append(T(lang, "| Fiche | |\n|:---|:---|\n", "| Sheet | |\n|:---|:---|\n"));
            foreach (var fact in Facts(lang, f, item))
                s.Append("| ").Append(fact.Label).Append(" | ")
                 .Append(fact.Code ? $"`{fact.Value}`" : fact.Link != null ? $"[{fact.Value}]({fact.Link})" : Cell(fact.Value)).Append(" |\n");
            s.Append('\n');
            if (item.UsedBy.Count > 0)
            {
                s.Append(T(lang, "## Règles de SMAD-X qui le citent\n\n", "## SMAD-X rules that cite it\n\n"));
                foreach (var r in item.UsedBy)
                    s.Append("- **").Append(r).Append("**").Append(rules.TryGetValue(r, out var t) ? " — " + T(lang, t.Fr, t.En) : "").Append('\n');
                s.Append('\n');
            }

            var text = Text(lang, item);
            if (!string.IsNullOrWhiteSpace(text))
            {
                var label = TextLabel(lang, item) + (EnglishOnly(lang, item) ? " (en anglais : c'est la version qui fait foi)" : "");
                s.Append("## ").Append(label).Append("\n\n").Append(text.Trim()).Append("\n\n");
            }
            var reco = Reco(lang, item);
            if (!string.IsNullOrWhiteSpace(reco))
                s.Append("## ").Append(T(lang, "Recommandation officielle", "Official recommendation")).Append("\n\n").Append(reco.Trim()).Append("\n\n");
            s.Append(T(lang, "## Source et licence\n\n", "## Source and licence\n\n"));
            Origin(s, lang, f);
            return s.ToString();
        }

        /// <summary>
        /// Le tableau d'identité d'une fiche, ligne par ligne : référentiel, identifiant, niveaux ANSSI, dates, lien, règles
        /// qui la citent. Le Markdown et le PDF l'affichent l'un comme l'autre.
        /// </summary>
        public static IReadOnlyList<SheetFact> Facts(string lang, SourceFramework f, SourceItem item)
        {
            var facts = new List<SheetFact> { new(T(lang, "Référentiel", "Framework"), T(lang, f.NameFr, f.NameEn)) };
            if (item.Framework is not ("microsoft" or "tier-models")) facts.Add(new(T(lang, "Identifiant", "Identifier"), item.Id, Code: true));
            if (item.Levels is { Count: > 0 } levels)
                facts.Add(new(levels.Count == 1 ? T(lang, "Niveau ANSSI", "ANSSI level") : T(lang, "Niveaux ANSSI", "ANSSI levels"),
                    string.Join(", ", levels) + (levels.Count == 1 ? T(lang, " (1 : le plus critique)", " (1: most critical)")
                                                                   : T(lang, " (1 : le plus critique ; un seuil par niveau, voir le texte)", " (1: most critical; one threshold per level, see the text)"))));
            if (item.Version != null) facts.Add(new(item.Framework switch
            {
                "anssi" => T(lang, "Ajouté à la checklist", "Added to the checklist"),
                "tier-models" => T(lang, "Version reproduite", "Reproduced version"),
                _ => "Version"
            }, item.Version));
            if (item.Dated != null && item.Dated != item.Version) facts.Add(new(item.Framework switch
            {
                "anssi" => T(lang, "Dernière modification", "Last change"),
                "tier-models" => "Commit",
                _ => T(lang, "Mis à jour", "Updated")
            }, item.Dated));
            var url = SourceUrl(lang, item);
            facts.Add(new(T(lang, "Lien", "Link"), Host(url), url));
            facts.Add(new(T(lang, "Cité par SMAD-X", "Cited by SMAD-X"), item.UsedBy.Count == 0
                ? T(lang, "le modèle de cloisonnement de SMAD-X qui porte ce nom", "SMAD-X's tier model of that name")
                : string.Join(" · ", item.UsedBy)));
            return facts;
        }

        /// <summary>La page de la source, en français quand elle existe.</summary>
        public static string SourceUrl(string lang, SourceItem item) => lang == "fr" && item.UrlFr != null ? item.UrlFr : item.Url;

        /// <summary>Le texte repris de la source (officiel, ou la description d'une page), dans la langue de la fiche s'il existe.</summary>
        public static string? Text(string lang, SourceItem item) => lang == "fr" ? item.TextFr ?? item.TextEn : item.TextEn ?? item.TextFr;

        /// <summary>La recommandation officielle (ANSSI), dans la langue de la fiche.</summary>
        public static string? Reco(string lang, SourceItem item) => lang == "fr" ? item.RecoFr ?? item.RecoEn : item.RecoEn ?? item.RecoFr;

        /// <summary>Le titre de la section du texte repris : texte officiel, description de la page (Microsoft), provenance (modèles).</summary>
        public static string TextLabel(string lang, SourceItem item) => item.Framework switch
        {
            "microsoft" => T(lang, "Description de la page", "Page description"),
            "tier-models" => "Provenance",
            _ => T(lang, "Texte officiel", "Official text")
        };

        /// <summary>Le texte officiel n'existe qu'en anglais (NIST, MITRE, CIS) et la fiche est en français : elle le dit.</summary>
        public static bool EnglishOnly(string lang, SourceItem item) => lang == "fr" && item.Framework is "nist" or "mitre-attack" or "cis";

        private static string Host(string url) => Uri.TryCreate(url, UriKind.Absolute, out var u) ? u.Host + (u.AbsolutePath.Length > 1 ? "/…" : "") : url;

        private static string Cell(string text) => text.Replace("|", "\\|").Replace("\n", " ");
    }
}
