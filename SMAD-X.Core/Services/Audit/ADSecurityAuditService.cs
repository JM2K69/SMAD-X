using System;
using System.Collections.Generic;
using System.Linq;
using SMADX.Models;

namespace SMADX.Services.Audit
{
    /// <summary>
    /// Évalue un domaine avec le catalogue de règles (<see cref="AuditRules"/>) et produit un rapport
    /// trié par gravité. Purement en mémoire : peut être relancé après chaque modification.
    /// </summary>
    public sealed class ADSecurityAuditService
    {
        private readonly IReadOnlyList<AuditRule> _rules;

        public ADSecurityAuditService(IReadOnlyList<AuditRule>? rules = null) => _rules = rules ?? AuditRules.All;

        /// <param name="now">Date de référence des règles de durée (dormance, âge des mots de passe).</param>
        public AuditReport Run(ADObject domain, DateTime? now = null)
        {
            var context = new AuditContext(domain, now ?? DateTime.Now);
            var findings = _rules
                .SelectMany(rule => rule.Evaluate(context).Select(f => new AuditFinding
                {
                    Rule = rule,
                    Object = f.Object,
                    Detail = rule.FormatDetail(f.DetailArgs),
                    Args = f.DetailArgs
                }))
                .OrderBy(f => f.Severity)
                .ThenBy(f => f.Rule.Id)
                .ThenBy(f => f.Object.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new AuditReport { Findings = findings, Rules = _rules, ReferenceDate = context.Now };
        }
    }
}
