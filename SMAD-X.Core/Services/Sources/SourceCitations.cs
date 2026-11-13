using System;
using System.Collections.Generic;
using System.Linq;
using SMADX.Services.Audit;
using SMADX.Services.Tiering;

namespace SMADX.Services.Sources
{
    /// <summary>Une référence citée par SMAD-X, avec les règles qui la citent (identifiant et titre, en français et en anglais).</summary>
    public sealed record CitedReference(AuditFramework Framework, string Id, string Title, string Url, IReadOnlyList<(string Rule, string TitleFr, string TitleEn)> Rules)
    {
        public string FrameworkId => SourceCitations.FrameworkId(Framework, Url);

        /// <summary>L'identifiant dans le référentiel : « AC-6(5) », « 5.4 », « T1558.003 », l'adresse d'une page Microsoft.</summary>
        public string ItemId => Framework switch
        {
            AuditFramework.Nist when Id.StartsWith("SP 800-53 Rev. 5 ", StringComparison.Ordinal) => Id["SP 800-53 Rev. 5 ".Length..],
            AuditFramework.Cis => Id.Replace("CIS Controls v8 ", ""),
            AuditFramework.Microsoft when Id == "Microsoft Learn" => Url,
            _ => Id
        };
    }

    /// <summary>Une version de modèle de cloisonnement reproduite par SMAD-X (SMAD-X.Core/TierModels).</summary>
    public sealed record CitedTierModel(string Id, string Name, string Repository, string Version, string Commit, string License, string Copyright);

    /// <summary>
    /// Ce que SMAD-X cite : les références de chaque règle d'audit, les modèles de cloisonnement qu'il reproduit, et la
    /// fiche de chaque référence dans un relevé.
    /// </summary>
    public static class SourceCitations
    {
        /// <summary>
        /// Le référentiel d'une référence : anssi, nist, cis, mitre-attack, microsoft ; un modèle de cloisonnement publié
        /// sur GitHub (cité comme référence Microsoft) va dans tier-models.
        /// </summary>
        public static string FrameworkId(AuditFramework framework, string url) => framework switch
        {
            AuditFramework.Anssi => "anssi",
            AuditFramework.Nist => "nist",
            AuditFramework.Cis => "cis",
            AuditFramework.MitreAttack => "mitre-attack",
            AuditFramework.Microsoft when url.Contains("github.com", StringComparison.OrdinalIgnoreCase) => "tier-models",
            _ => "microsoft"
        };

        /// <summary>Le titre d'une règle dans une langue (« fr-FR », « en-US »), sans changer la langue de l'interface.</summary>
        public static string RuleTitle(string ruleId, string culture) =>
            LocalizationService.Instance.Translate($"Audit.{ruleId}", culture).Split('\n')[0].TrimStart('#', ' ').Trim();

        /// <summary>Le titre de chaque règle d'audit, en français et en anglais.</summary>
        public static IReadOnlyDictionary<string, (string Fr, string En)> RuleTitles() =>
            AuditRules.All.ToDictionary(r => r.Id, r => (RuleTitle(r.Id, "fr-FR"), RuleTitle(r.Id, "en-US")));

        /// <summary>Les références des règles d'audit, chacune avec les règles qui la citent ; une page Microsoft par adresse.</summary>
        public static IReadOnlyList<CitedReference> References()
        {
            var titles = RuleTitles();
            return AuditRules.All.SelectMany(r => r.References.Select(x => (Rule: r, Ref: x)))
                .GroupBy(p => (p.Ref.Framework, p.Ref.Id, p.Ref.Framework == AuditFramework.Microsoft ? p.Ref.Url : ""))
                .Select(g =>
                {
                    var first = g.First().Ref;
                    var users = g.Select(p => p.Rule.Id).Distinct().Select(id => (id, titles[id].Fr, titles[id].En)).ToList();
                    return new CitedReference(first.Framework, first.Id, first.Title, first.Url, users);
                })
                .OrderBy(c => c.Framework).ThenBy(c => c.Id, StringComparer.Ordinal).ToList();
        }

        /// <summary>Les modèles de cloisonnement reproduits, tels qu'embarqués dans SMAD-X.Core.</summary>
        public static IReadOnlyList<CitedTierModel> TierModels() =>
            TierModelCatalog.All.Select(m => new CitedTierModel(m.Id, m.Name, m.Source.Repository, m.Source.Version, m.Source.Commit,
                m.Source.License, m.Source.Copyright)).OrderBy(m => m.Id, StringComparer.Ordinal).ToList();

        /// <summary>La fiche d'une référence dans un relevé : le point, le contrôle, la technique, la mesure, la page ou le modèle cité.</summary>
        public static SourceItem? Find(SourcesSnapshot snapshot, AuditReference reference)
        {
            var framework = FrameworkId(reference.Framework, reference.Url);
            return snapshot.Items.FirstOrDefault(i => i.Framework == framework && framework switch
            {
                "microsoft" => i.Id == reference.Url,
                "tier-models" => string.Equals(i.Url.TrimEnd('/'), reference.Url.TrimEnd('/'), StringComparison.OrdinalIgnoreCase),
                _ => i.Id == reference.Id
            });
        }
    }
}
