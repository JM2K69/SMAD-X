using System;
using System.Collections.Generic;
using System.Linq;
using SMADX.Models;

namespace SMADX.Services.Audit
{
    /// <summary>Gravité d'un constat. Dérivée du niveau ANSSI quand la règle en a un (1 → Critique … 4 → Faible).</summary>
    public enum AuditSeverity
    {
        Critical,
        High,
        Medium,
        Low,
        Info
    }

    public enum AuditFramework
    {
        Anssi,
        Nist,
        Cis,
        MitreAttack,
        Microsoft
    }

    /// <summary>Référence à un référentiel reconnu, citée par une règle.</summary>
    /// <param name="Id">Identifiant dans le référentiel, ex. vuln_spn_priv, AC-6(5), T1558.003.</param>
    public sealed record AuditReference(AuditFramework Framework, string Id, string Title, string Url)
    {
        public string FrameworkLabel => Framework switch
        {
            AuditFramework.Anssi       => "ANSSI",
            AuditFramework.Nist        => "NIST",
            AuditFramework.Cis         => "CIS",
            AuditFramework.MitreAttack => "MITRE ATT&CK",
            AuditFramework.Microsoft   => "Microsoft",
            _                          => Framework.ToString()
        };
    }

    /// <summary>
    /// Règle d'audit. Le titre et la fiche pédagogique (pourquoi c'est dangereux, comment corriger)
    /// viennent de Content/{culture}/Audit.{Id}.md ; le détail d'un constat, de la clé Audit.{Id}.Detail.
    /// </summary>
    public sealed class AuditRule
    {
        public required string Id { get; init; }
        public required AuditSeverity Severity { get; init; }

        /// <summary>Niveau du point de contrôle ANSSI correspondant (1 = le plus critique), null s'il n'y en a pas.</summary>
        public int? AnssiLevel { get; init; }

        public required IReadOnlyList<AuditReference> References { get; init; }

        /// <summary>Objets concernés et arguments du message de détail.</summary>
        public required Func<AuditContext, IEnumerable<(ADObject Object, object[] DetailArgs)>> Evaluate { get; init; }

        private static LocalizationService Loc => LocalizationService.Instance;

        /// <summary>Fiche Markdown complète de la règle, dans la langue courante.</summary>
        public string Documentation => Loc[$"Audit.{Id}"];

        /// <summary>Titre = première ligne de la fiche, sans le « # ».</summary>
        public string Title => Documentation.Split('\n')[0].TrimStart('#', ' ').Trim();

        public string FormatDetail(object[] args) => string.Format(Loc[$"Audit.{Id}.Detail"], args);
    }

    public sealed class AuditFinding
    {
        public required AuditRule Rule { get; init; }
        public required ADObject Object { get; init; }
        public required string Detail { get; init; }

        /// <summary>Arguments du détail (le mandataire, le droit, la machine…), pour les corrections proposées par Studio.</summary>
        public IReadOnlyList<object> Args { get; init; } = [];

        public AuditSeverity Severity => Rule.Severity;
    }

    public sealed class AuditReport
    {
        /// <summary>Pénalité d'une règle en échec, comptée une fois quel que soit le nombre d'objets concernés.</summary>
        public static int Penalty(AuditSeverity severity) => severity switch
        {
            AuditSeverity.Critical => 12,
            AuditSeverity.High     => 6,
            AuditSeverity.Medium   => 3,
            AuditSeverity.Low      => 1,
            _                      => 0
        };

        public required IReadOnlyList<AuditFinding> Findings { get; init; }
        public required IReadOnlyList<AuditRule> Rules { get; init; }
        public required DateTime ReferenceDate { get; init; }

        /// <summary>
        /// Score de 0 à 100 : 100 moins la pénalité de chaque règle en échec. Compter la règle une seule fois
        /// évite qu'un problème répandu (dix comptes dormants) écrase un problème rare mais critique.
        /// </summary>
        public int Score => Math.Max(0, 100 - FailedRules.Sum(r => Penalty(r.Severity)));

        public IEnumerable<AuditRule> FailedRules => Findings.Select(f => f.Rule).Distinct();

        /// <summary>
        /// Estimation à la manière de l'ANSSI : niveau du point de contrôle le plus critique en échec,
        /// 5 si aucun point ANSSI n'est en échec. Ce n'est pas le calcul officiel de l'outil ADS.
        /// </summary>
        public int EstimatedAnssiLevel => Findings.Select(f => f.Rule.AnssiLevel).Where(l => l.HasValue).Select(l => l!.Value)
            .DefaultIfEmpty(5).Min();

        public int Count(AuditSeverity severity) => Findings.Count(f => f.Severity == severity);

        public IEnumerable<AuditFinding> For(ADObject obj) => Findings.Where(f => f.Object == obj);
    }
}
