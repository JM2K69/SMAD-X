using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using SMADX.Models;
using SMADX.Services.AttackPaths;
using SMADX.Services.Audit;

namespace SMADX.Services.Tiering
{
    /// <summary>État de sécurité d'un domaine : audit et chemins d'attaque qui partent d'une source hors Tier 0.</summary>
    public sealed record TierSecurityState(AuditReport Audit, IReadOnlyList<AttackPath> PathsOutsideTier0, IReadOnlyList<BlockedSession> BlockedSessions)
    {
        public static TierSecurityState Of(ADObject domain, DateTime now)
        {
            var paths = new AttackPathService(domain, now);
            return new TierSecurityState(new ADSecurityAuditService().Run(domain, now), paths.PathsOutsideTier0(), paths.BlockedSessions);
        }
    }

    /// <summary>Effet de la migration vers un modèle, mesuré sur une copie du domaine.</summary>
    public sealed record TierImpact(TierModelDefinition Model, TierMigrationResult Migration, TierSecurityState Before, TierSecurityState After)
    {
        /// <summary>Chemins hors Tier 0 qui n'existent plus après la migration (la source n'atteint plus le domaine, ou elle est désormais de Tier 0).</summary>
        public IReadOnlyList<AttackPath> CutPaths =>
            Before.PathsOutsideTier0.Where(b => After.PathsOutsideTier0.All(a => a.Start.Id != b.Start.Id)).ToList();

        /// <summary>Chemins hors Tier 0 qui apparaissent avec la migration.</summary>
        public IReadOnlyList<AttackPath> NewPaths =>
            After.PathsOutsideTier0.Where(a => Before.PathsOutsideTier0.All(b => b.Start.Id != a.Start.Id)).ToList();

        public IReadOnlyList<AuditFinding> FixedFindings => Except(Before.Audit.Findings, After.Audit.Findings);
        public IReadOnlyList<AuditFinding> NewFindings => Except(After.Audit.Findings, Before.Audit.Findings);

        /// <summary>Un constat est identifié par sa règle et l'objet concerné : la copie garde les identifiants des objets.</summary>
        private static List<AuditFinding> Except(IEnumerable<AuditFinding> source, IEnumerable<AuditFinding> other)
        {
            var keys = other.Select(f => (f.Rule.Id, f.Object.Id)).ToHashSet();
            return source.Where(f => !keys.Contains((f.Rule.Id, f.Object.Id))).ToList();
        }
    }

    /// <summary>
    /// Simule la migration d'un domaine vers un modèle de cloisonnement sans le modifier : la migration est faite sur une
    /// copie, et le score, les constats et les chemins d'attaque sont comparés avant et après. Les tiers propres au modèle
    /// (Legacy de HardenAD) ne sont pas ajoutés à la configuration du document.
    /// </summary>
    public static class TierImpactSimulator
    {
        public static TierImpact Simulate(ADObject domain, TierModelDefinition model, DateTime now, TierSecurityState? before = null)
        {
            before ??= TierSecurityState.Of(domain, now);
            var copy = Copy(domain);
            var migration = TierMigrationService.Migrate(copy, model, now, registerTiers: false);
            return new TierImpact(model, migration, before, TierSecurityState.Of(copy, now));
        }

        /// <summary>Chaque modèle du catalogue, comparé au même état de départ.</summary>
        public static IReadOnlyList<TierImpact> Compare(ADObject domain, DateTime now)
        {
            var before = TierSecurityState.Of(domain, now);
            return TierModelCatalog.All.Select(m => Simulate(domain, m, now, before)).ToList();
        }

        /// <summary>Copie complète du domaine, avec les mêmes identifiants d'objets.</summary>
        public static ADObject Copy(ADObject domain)
        {
            var copy = JsonSerializer.Deserialize<ADObject>(JsonSerializer.Serialize(domain, ADDataService.SerializerOptions), ADDataService.SerializerOptions)!;
            ADDataService.RestoreParentReferences(copy, null);
            return copy;
        }
    }
}
