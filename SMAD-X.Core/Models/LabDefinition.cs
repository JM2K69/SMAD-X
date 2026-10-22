using System;
using System.Collections.Generic;
using System.Linq;
using SMADX.Services.Audit;

namespace SMADX.Models
{
    /// <summary>
    /// Exercice (« lab ») au format .smadx-lab.json : un énoncé, un domaine de départ et des objectifs
    /// vérifiés automatiquement par le moteur d'audit et la comparaison avec le domaine de départ.
    /// </summary>
    public sealed class LabDefinition
    {
        /// <summary>Version du format des exercices écrite par cette version de SMAD-X.</summary>
        public const int CurrentFormatVersion = 1;

        public string Format { get; set; } = "smadx-lab";
        public int FormatVersion { get; set; } = CurrentFormatVersion;

        /// <summary>Identifiant stable, ex. « kerberoasting » (sert aussi au nom du rapport).</summary>
        public string Id { get; set; } = string.Empty;

        public LocalizedText Title { get; set; } = new();

        /// <summary>
        /// Langue de l'exercice (« fr-FR », « en-US ») quand son domaine est écrit dans une seule langue ;
        /// null pour un exercice bilingue. Le menu ne propose que les exercices de la langue de l'interface.
        /// </summary>
        public string? Language { get; set; }

        /// <summary>Série de l'exercice dans le menu (ex. « attack-paths ») ; null pour les exercices généraux.</summary>
        public string? Category { get; set; }

        public LabLevel Level { get; set; } = LabLevel.Beginner;
        public int DurationMinutes { get; set; } = 20;

        /// <summary>Énoncé en Markdown.</summary>
        public LocalizedText Statement { get; set; } = new();

        /// <summary>
        /// Date à laquelle les dates du domaine de départ ont été figées. À l'ouverture, toutes les dates
        /// sont décalées de (maintenant − ReferenceDate) : un compte « actif » reste actif quelle que soit
        /// la date à laquelle l'exercice est fait.
        /// </summary>
        public DateTime ReferenceDate { get; set; } = DateTime.Today;

        /// <summary>Domaine de départ (document SMAD-X complet, migré à la lecture comme un fichier .smad-x.json).</summary>
        public ADRootDocument Start { get; set; } = null!;

        public List<LabObjective> Objectives { get; set; } = new();

        public int MaxPoints => Objectives.Sum(o => o.Points);
    }

    public enum LabLevel
    {
        Beginner,
        Intermediate,
        Advanced
    }

    /// <summary>Texte par culture (« fr-FR », « en-US »), avec repli sur le français puis sur le premier texte.</summary>
    public sealed class LocalizedText : Dictionary<string, string>
    {
        public LocalizedText() : base(StringComparer.OrdinalIgnoreCase)
        {
        }

        public static LocalizedText Of(string fr, string en) => new() { ["fr-FR"] = fr, ["en-US"] = en };

        public string Resolve(string culture) =>
            TryGetValue(culture, out var text) ? text
            : TryGetValue("fr-FR", out var fr) ? fr
            : Values.FirstOrDefault() ?? string.Empty;
    }

    public sealed class LabObjective
    {
        public string Id { get; set; } = string.Empty;
        public LocalizedText Title { get; set; } = new();
        public LocalizedText Hint { get; set; } = new();
        public int Points { get; set; } = 1;
        public LabCheck Check { get; set; } = new();
    }

    /// <summary>Ce que vérifie un objectif.</summary>
    public enum LabCheckType
    {
        /// <summary>Plus aucun constat de la règle RuleId (sur Object si précisé).</summary>
        RuleClear,
        /// <summary>Plus aucun constat de gravité Severity ou plus grave.</summary>
        NoFindingAtOrAbove,
        /// <summary>Score d'audit au moins égal à Score.</summary>
        ScoreAtLeast,
        ObjectExists,
        ObjectAbsent,
        /// <summary>Object est membre de Group (effectivement, par imbrication, si Effective).</summary>
        MemberOf,
        NotMemberOf,
        HasFlag,
        NotHasFlag,
        /// <summary>Object est classé dans le tier Tier.</summary>
        TierIs,
        /// <summary>Trustee a une délégation directe de catégorie RightCategory sur Target.</summary>
        DelegationExists,
        /// <summary>Trustee a au moins une délégation directe, et toutes sont de catégories AllowedCategories.</summary>
        DelegationOnly,
        /// <summary>Object n'a subi aucun changement par rapport au domaine de départ.</summary>
        NotModified,
        /// <summary>
        /// Plus aucun chemin d'attaque vers le contrôle du domaine depuis Object (« any-domain-user » :
        /// n'importe quel utilisateur), ou, sans Object, depuis aucune source hors Tier 0.
        /// </summary>
        NoAttackPath
    }

    /// <summary>Vérification déclarative ; seuls les champs utiles au type sont renseignés.</summary>
    public sealed class LabCheck
    {
        public LabCheckType Type { get; set; }
        public string? RuleId { get; set; }
        public AuditSeverity? Severity { get; set; }
        public int? Score { get; set; }

        /// <summary>Nom de l'objet visé (unique dans le domaine de l'exercice).</summary>
        public string? Object { get; set; }
        public ADObjectType? ObjectType { get; set; }
        public string? Group { get; set; }
        public bool Effective { get; set; } = true;
        public ADAccountFlags? Flag { get; set; }
        public string? Tier { get; set; }
        public string? Trustee { get; set; }
        public string? Target { get; set; }
        public RightCategory? RightCategory { get; set; }
        public List<RightCategory>? AllowedCategories { get; set; }
    }
}
