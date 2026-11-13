using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using SMADX.Services.Audit;

namespace SMADX.Services.Sources
{
    /// <summary>
    /// Le relevé : chaque référentiel cité par SMAD-X est relu sur le web, ses éléments cités sont extraits ; les textes
    /// officiels ne sont gardés que si la licence le permet (ANSSI : licence ouverte Etalab 2.0 ; NIST : domaine public ;
    /// MITRE ATT&amp;CK : ses conditions, avec leur mention). CIS : titres seulement ; Microsoft : titre et description de la
    /// page.
    /// </summary>
    public static class SourceUpdate
    {
        public const string AnssiChecklistUrl = "https://cert.cyber.gouv.fr/uploads/ad_checklist.html";
        private const string CisCas = "https://cas.docs.cisecurity.org/en/latest/source/";

        /// <summary>
        /// Relit les référentiels sur le web (tous, ou ceux de <paramref name="only"/>) et rend leur relevé, ou les
        /// erreurs si un élément cité par SMAD-X a disparu de sa source. <paramref name="progress"/> reçoit l'identifiant
        /// de chaque référentiel au moment de le relire ; une source injoignable lève une <see cref="HttpRequestException"/>.
        /// </summary>
        public static async Task<(SourcesSnapshot? Snapshot, List<string> Errors)> RunAsync(string today, IReadOnlyCollection<string>? only = null,
            Action<string>? progress = null, string tool = "tools/SmadxSources")
        {
            bool Wants(string id) => only == null || only.Contains(id);
            var cited = SourceCitations.References();
            var errors = new List<string>();
            var items = new List<SourceItem>();
            var frameworks = new List<SourceFramework>();
            IReadOnlyList<string> Users(CitedReference c) => c.Rules.Select(r => r.Rule).ToList();

            if (Wants("anssi"))
            {
                // ─── ANSSI : la checklist (points de contrôle) et le guide d'administration ───
                progress?.Invoke("anssi");
                var anssiPage = await Web.GetAsync(AnssiChecklistUrl);
                var checklist = new AnssiChecklist(anssiPage.Content);
                foreach (var c in cited.Where(c => c.Framework == AuditFramework.Anssi && c.Url == AnssiChecklistUrl))
                {
                    if (checklist.Find(c.Id) is not { } p) { errors.Add($"ANSSI : le point {c.Id} n'est plus dans la checklist."); continue; }
                    items.Add(new SourceItem
                    {
                        Framework = "anssi", Id = c.Id, TitleFr = p.TitleFr, TitleEn = p.TitleEn, Url = AnssiChecklistUrl, Levels = p.Levels.Count > 0 ? p.Levels : null,
                        Version = p.Added, Dated = p.Updated, TextFr = p.BodyFr, TextEn = p.BodyEn, RecoFr = Empty(p.RecoFr), RecoEn = Empty(p.RecoEn),
                        UsedBy = Users(c)
                    });
                }
                foreach (var c in cited.Where(c => c.Framework == AuditFramework.Anssi && c.Url != AnssiChecklistUrl))
                {
                    var page = Pages.Read(await Web.GetAsync(c.Url));
                    items.Add(new SourceItem
                    {
                        Framework = "anssi", Id = c.Id, TitleFr = page.Title, TitleEn = page.Title + " (document in French)", Url = c.Url,
                        Dated = page.Date, TextFr = page.Description, TextEn = page.Description, UsedBy = Users(c)
                    });
                }
                frameworks.Add(new SourceFramework
                {
                    Id = "anssi", NameFr = "ANSSI — Points de contrôle Active Directory", NameEn = "ANSSI — Active Directory control points",
                    Url = AnssiChecklistUrl, Published = anssiPage.LastModified, Retrieved = today,
                    LicenseFr = "licence ouverte Etalab 2.0 : réutilisation libre en mentionnant la source (CERT-FR / ANSSI) et la date de sa mise à jour ; textes officiels repris en français et en anglais",
                    LicenseEn = "Etalab Open Licence 2.0: free reuse citing the source (CERT-FR / ANSSI) and the date of its update; official texts reproduced in French and English"
                });
            }

            if (Wants("nist"))
            {
                // ─── NIST : SP 800-53 Rev. 5 (catalogue OSCAL) et SP 800-63B ───
                progress?.Invoke("nist");
                var catalog = new NistCatalog((await Web.GetAsync(NistCatalog.Url)).Content);
                foreach (var c in cited.Where(c => c.Framework == AuditFramework.Nist))
                {
                    if (c.Id.StartsWith("SP 800-53 Rev. 5 "))
                    {
                        if (catalog.Find(c.ItemId) is not { } control) { errors.Add($"NIST : le contrôle {c.ItemId} n'est plus dans le catalogue."); continue; }
                        items.Add(new SourceItem
                        {
                            Framework = "nist", Id = c.Id, TitleFr = control.Title, TitleEn = control.Title, Url = c.Url, Version = catalog.Version,
                            TextFr = control.Statement, TextEn = control.Statement, UsedBy = Users(c)
                        });
                    }
                    else
                    {
                        var fetched = await Web.GetAsync(c.Url);
                        var page = Pages.Read(fetched);
                        items.Add(new SourceItem
                        {
                            Framework = "nist", Id = c.Id, TitleFr = page.Title, TitleEn = page.Title, Url = c.Url, Dated = fetched.LastModified,
                            UsedBy = Users(c)
                        });
                    }
                }
                frameworks.Add(new SourceFramework
                {
                    Id = "nist", NameFr = "NIST — SP 800-53 Rev. 5 et SP 800-63B", NameEn = "NIST — SP 800-53 Rev. 5 and SP 800-63B",
                    Url = "https://csrc.nist.gov/pubs/sp/800/53/r5/upd1/final", Retrieved = today,
                    Published = catalog.Version != null ? $"SP 800-53 Rev. 5, OSCAL {catalog.Version} ({catalog.Published})" : catalog.Published,
                    LicenseFr = "domaine public : publication du gouvernement des États-Unis (17 U.S.C. § 105) ; énoncés officiels repris, en anglais",
                    LicenseEn = "public domain: a United States Government publication (17 U.S.C. § 105); official statements reproduced"
                });
            }

            if (Wants("cis"))
            {
                // ─── CIS Controls v8 : titres des mesures ───
                progress?.Invoke("cis");
                var cisPublished = new List<string>();
                var safeguards = new Dictionary<string, string>();
                foreach (var control in cited.Where(c => c.Framework == AuditFramework.Cis).Select(c => c.ItemId.Split('.')[0]).Distinct())
                {
                    var page = await Web.GetAsync($"{CisCas}Controls{control}/");
                    if (page.LastModified != null) cisPublished.Add(page.LastModified);
                    foreach (var (id, title) in CisControls.Safeguards(page.Content)) safeguards[id] = title;
                }
                foreach (var c in cited.Where(c => c.Framework == AuditFramework.Cis))
                {
                    if (!safeguards.TryGetValue(c.ItemId, out var title)) { errors.Add($"CIS : la mesure {c.ItemId} n'est plus sur sa page."); continue; }
                    items.Add(new SourceItem { Framework = "cis", Id = c.Id, TitleFr = title, TitleEn = title, Url = c.Url, UsedBy = Users(c) });
                }
                frameworks.Add(new SourceFramework
                {
                    Id = "cis", NameFr = "CIS Controls v8", NameEn = "CIS Controls v8", Url = "https://www.cisecurity.org/controls/v8",
                    Published = cisPublished.Count > 0 ? cisPublished.Max() : null, Retrieved = today,
                    LicenseFr = "© Center for Internet Security, licence CC BY-NC-ND 4.0 : seuls l'identifiant et le titre officiel de chaque mesure sont repris, avec le lien",
                    LicenseEn = "© Center for Internet Security, CC BY-NC-ND 4.0 licence: only the identifier and official title of each safeguard are reproduced, with the link"
                });
            }

            if (Wants("mitre-attack"))
            {
                // ─── MITRE ATT&CK : données STIX ───
                progress?.Invoke("mitre-attack");
                var attack = new MitreAttack((await Web.GetAsync(MitreAttack.Url)).Content);
                foreach (var c in cited.Where(c => c.Framework == AuditFramework.MitreAttack))
                {
                    if (attack.Find(c.Id) is not { } t) { errors.Add($"MITRE ATT&CK : la technique {c.Id} n'est plus dans les données."); continue; }
                    items.Add(new SourceItem
                    {
                        Framework = "mitre-attack", Id = c.Id, TitleFr = t.Name, TitleEn = t.Name, Url = c.Url, Version = t.Version, Dated = t.Modified,
                        TextFr = t.Description, TextEn = t.Description, UsedBy = Users(c)
                    });
                }
                var year = today[..4];
                frameworks.Add(new SourceFramework
                {
                    Id = "mitre-attack", NameFr = "MITRE ATT&CK® (Enterprise)", NameEn = "MITRE ATT&CK® (Enterprise)", Url = "https://attack.mitre.org/",
                    Published = attack.Version != null ? "ATT&CK v" + attack.Version : null, Retrieved = today,
                    Notice = $"© {year} The MITRE Corporation. This work is reproduced and distributed with the permission of The MITRE Corporation. [Terms of use](https://attack.mitre.org/resources/legal-and-branding/terms-of-use/)",
                    LicenseFr = "conditions d'utilisation de MITRE ATT&CK : reproduction autorisée avec la mention ci-dessous ; premier paragraphe de chaque description repris, en anglais",
                    LicenseEn = "MITRE ATT&CK terms of use: reproduction allowed with the notice below; first paragraph of each description reproduced"
                });
            }

            if (Wants("microsoft"))
            {
                // ─── Microsoft Learn : titre et description de chaque page, en anglais et en français ───
                progress?.Invoke("microsoft");
                foreach (var c in cited.Where(c => c.FrameworkId == "microsoft"))
                {
                    var en = Pages.Read(await Web.GetAsync(c.Url));
                    var frUrl = c.Url.Replace("/en-us/", "/fr-fr/");
                    var fr = frUrl != c.Url ? Pages.Read(await Web.GetAsync(frUrl)) : en;
                    items.Add(new SourceItem
                    {
                        Framework = "microsoft", Id = c.Url, TitleFr = fr.Title, TitleEn = en.Title, Url = c.Url, UrlFr = frUrl != c.Url ? frUrl : null,
                        Dated = en.Date, TextFr = fr.Description, TextEn = en.Description, UsedBy = Users(c)
                    });
                }
                frameworks.Add(new SourceFramework
                {
                    Id = "microsoft", NameFr = "Microsoft Learn", NameEn = "Microsoft Learn", Url = "https://learn.microsoft.com/", Retrieved = today,
                    LicenseFr = "© Microsoft : seuls le titre et la description de chaque page sont repris, avec le lien ; le contenu reste sur Microsoft Learn",
                    LicenseEn = "© Microsoft: only the title and description of each page are reproduced, with the link; the content stays on Microsoft Learn"
                });
            }

            if (Wants("tier-models"))
            {
                // ─── Modèles de cloisonnement reproduits (SMAD-X.Core/TierModels) ───
                var tierRefs = cited.Where(c => c.FrameworkId == "tier-models").ToList();
                progress?.Invoke("tier-models");
                foreach (var model in SourceCitations.TierModels())
                {
                    var users = tierRefs.Where(c => string.Equals(c.Url.TrimEnd('/'), model.Repository.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
                        .SelectMany(c => c.Rules.Select(r => r.Rule)).Distinct().ToList();
                    items.Add(new SourceItem
                    {
                        Framework = "tier-models", Id = model.Id, TitleFr = model.Name, TitleEn = model.Name, Url = model.Repository,
                        Version = model.Version, Dated = model.Commit[..12], UsedBy = users,
                        TextFr = $"Reproduit depuis [{model.Repository}]({model.Repository}), version {model.Version} (commit `{model.Commit}`). {model.Copyright.TrimEnd('.')}. Licence {model.License}.",
                        TextEn = $"Reproduced from [{model.Repository}]({model.Repository}), version {model.Version} (commit `{model.Commit}`). {model.Copyright.TrimEnd('.')}. {model.License} licence."
                    });
                }
                foreach (var c in tierRefs.Where(c => !items.Any(i => i.Framework == "tier-models" && string.Equals(i.Url.TrimEnd('/'), c.Url.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))))
                    errors.Add($"Modèles : {c.Url} est cité mais aucun modèle de SMAD-X.Core/TierModels ne vient de là.");
                frameworks.Add(new SourceFramework
                {
                    Id = "tier-models", NameFr = "Modèles de cloisonnement reproduits", NameEn = "Reproduced tier models",
                    Url = "https://github.com/JM2K69/SMAD-X/tree/master/SMAD-X.Core/TierModels", Retrieved = today,
                    LicenseFr = "MIT, avec le copyright de chaque projet ; les modèles sont reproduits dans SMAD-X.Core/TierModels",
                    LicenseEn = "MIT, with each project's copyright; the models are reproduced in SMAD-X.Core/TierModels"
                });
            }

            if (errors.Count > 0) return (null, errors);
            var order = SourceSheets.Order.ToList();
            return (new SourcesSnapshot
            {
                Retrieved = today,
                Tool = tool,
                Frameworks = frameworks.OrderBy(f => order.IndexOf(f.Id)).ToList(),
                Items = items.OrderBy(i => order.IndexOf(i.Framework)).ThenBy(i => i.Id, StringComparer.Ordinal).ToList()
            }, errors);
        }

        private static string? Empty(string text) => string.IsNullOrWhiteSpace(text) ? null : text;

        /// <summary>Ce qui a changé depuis le relevé précédent : éléments ajoutés, retirés, et champs modifiés.</summary>
        /// <param name="lang">« fr » ou « en » : la langue des messages.</param>
        public static List<string> Diff(SourcesSnapshot? before, SourcesSnapshot after, string lang = "fr")
        {
            string T(string fr, string en) => lang == "fr" ? fr : en;
            var changes = new List<string>();
            if (before == null) return [T("(premier relevé)", "(first retrieval)")];
            var old = before.Items.ToDictionary(i => i.Key);
            foreach (var item in after.Items)
            {
                if (!old.TryGetValue(item.Key, out var o)) { changes.Add($"+ {item.Framework} {item.Id}"); continue; }
                void Field(string name, object? a, object? b)
                {
                    if (Equals(a, b)) return;
                    var (sa, sb) = (a?.ToString() ?? "—", b?.ToString() ?? "—");
                    changes.Add($"~ {item.Framework} {item.Id}{T(" : ", ": ")}{name}" + (sa.Length < 60 && sb.Length < 60 ? T($" « {sa} » → « {sb} »", $" “{sa}” → “{sb}”") : T(" (texte modifié)", " (text changed)")));
                }
                Field(T("titre (fr)", "title (fr)"), o.TitleFr, item.TitleFr);
                Field(T("titre (en)", "title (en)"), o.TitleEn, item.TitleEn);
                Field(T("niveaux", "levels"), o.Levels is null ? null : string.Join(", ", o.Levels), item.Levels is null ? null : string.Join(", ", item.Levels));
                Field("version", o.Version, item.Version);
                Field("date", o.Dated, item.Dated);
                Field(T("texte (fr)", "text (fr)"), o.TextFr, item.TextFr);
                Field(T("texte (en)", "text (en)"), o.TextEn, item.TextEn);
                Field(T("recommandation (fr)", "recommendation (fr)"), o.RecoFr, item.RecoFr);
                Field(T("recommandation (en)", "recommendation (en)"), o.RecoEn, item.RecoEn);
                if (!o.UsedBy.SequenceEqual(item.UsedBy)) changes.Add($"~ {item.Framework} {item.Id}{T(" : cité par ", ": cited by ")}{string.Join(", ", item.UsedBy)}");
            }
            foreach (var removed in before.Items.Where(i => after.Items.All(a => a.Key != i.Key)))
                changes.Add($"- {removed.Framework} {removed.Id} {T("(plus cité par SMAD-X)", "(no longer cited by SMAD-X)")}");
            foreach (var f in after.Frameworks)
                if (before.Frameworks.FirstOrDefault(b => b.Id == f.Id) is { } b && b.Published != f.Published)
                    changes.Add($"~ {f.Id}{T(" : publication « ", ": publication “")}{b.Published}{T(" » → « ", "” → “")}{f.Published}{T(" »", "”")}");
            return changes;
        }
    }
}
