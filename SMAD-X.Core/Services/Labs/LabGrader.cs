using System;
using System.Collections.Generic;
using System.Linq;
using SMADX.Models;
using SMADX.Services.Audit;
using SMADX.Services.AttackPaths;

namespace SMADX.Services.Labs
{
    /// <summary>Résultat d'un objectif : réussi ou non, et pourquoi (pour guider l'apprenant).</summary>
    public sealed record LabObjectiveResult(LabObjective Objective, bool Passed, string Feedback)
    {
        public int EarnedPoints => Passed ? Objective.Points : 0;
    }

    public sealed record LabResult(LabDefinition Lab, IReadOnlyList<LabObjectiveResult> Objectives, AuditReport Audit, DateTime GradedAt)
    {
        public int Points => Objectives.Sum(o => o.EarnedPoints);
        public int MaxPoints => Lab.MaxPoints;
        public bool Completed => Objectives.All(o => o.Passed);
    }

    /// <summary>
    /// Corrige un exercice : chaque objectif est une vérification déclarative évaluée sur le domaine
    /// de l'apprenant, avec l'audit de sécurité et la comparaison avec le domaine de départ.
    /// </summary>
    public sealed class LabGrader
    {
        private static LocalizationService Loc => LocalizationService.Instance;

        /// <param name="start">Domaine de départ préparé (le même que celui donné à l'apprenant).</param>
        /// <param name="current">Domaine dans son état actuel.</param>
        public LabResult Grade(LabDefinition lab, ADObject start, ADObject current, DateTime now)
        {
            var audit = new ADSecurityAuditService().Run(current, now);
            var attackPaths = new Lazy<AttackPathService>(() => new AttackPathService(current, now));
            var context = new AuditContext(current, now);
            var changes = new Lazy<List<ADChangeItem>>(() => ADDiffService.Compare(
                new DomainSnapshot { Root = start }, new DomainSnapshot { Root = current }));

            var results = lab.Objectives
                .Select(o => Evaluate(o, o.Check, audit, context, start, changes, attackPaths))
                .ToList();
            return new LabResult(lab, results, audit, now);
        }

        private static LabObjectiveResult Evaluate(LabObjective objective, LabCheck check, AuditReport audit,
            AuditContext context, ADObject start, Lazy<List<ADChangeItem>> changes, Lazy<AttackPathService> attackPaths)
        {
            (bool passed, string feedback) = check.Type switch
            {
                LabCheckType.RuleClear => RuleClear(check, audit),
                LabCheckType.NoFindingAtOrAbove => NoFindingAtOrAbove(check, audit),
                LabCheckType.ScoreAtLeast => audit.Score >= (check.Score ?? 100)
                    ? Ok()
                    : Fail("Lab.Feedback.Score", audit.Score, check.Score ?? 100),
                LabCheckType.ObjectExists => Find(context, check) != null ? Ok() : Fail("Lab.Feedback.Missing", check.Object!),
                LabCheckType.ObjectAbsent => Find(context, check) == null ? Ok() : Fail("Lab.Feedback.StillPresent", check.Object!),
                LabCheckType.MemberOf => Membership(check, context, expected: true),
                LabCheckType.NotMemberOf => Membership(check, context, expected: false),
                LabCheckType.HasFlag => Flag(check, context, expected: true),
                LabCheckType.NotHasFlag => Flag(check, context, expected: false),
                LabCheckType.TierIs => Find(context, check) is not { } o ? Fail("Lab.Feedback.Missing", check.Object!)
                    : string.Equals(o.Tier, check.Tier, StringComparison.OrdinalIgnoreCase) ? Ok()
                    : Fail("Lab.Feedback.Tier", o.Name, o.Tier ?? "—", check.Tier!),
                LabCheckType.DelegationExists => DelegationExists(check, context),
                LabCheckType.DelegationOnly => DelegationOnly(check, context),
                LabCheckType.NotModified => NotModified(check, start, changes.Value),
                LabCheckType.NoAttackPath => NoAttackPath(check, context, attackPaths.Value),
                _ => Fail("Lab.Feedback.UnknownCheck", check.Type)
            };
            return new LabObjectiveResult(objective, passed, feedback);
        }

        private static (bool, string) Ok() => (true, Loc["Lab.Feedback.Ok"]);

        private static (bool, string) Fail(string key, params object[] args) => (false, string.Format(Loc[key], args));

        private static ADObject? Find(AuditContext context, LabCheck check, string? name = null) =>
            context.Objects.FirstOrDefault(o =>
                string.Equals(o.Name, name ?? check.Object, StringComparison.OrdinalIgnoreCase) &&
                (check.ObjectType is null || name != null || o.Type == check.ObjectType));

        private static (bool, string) RuleClear(LabCheck check, AuditReport audit)
        {
            var remaining = audit.Findings
                .Where(f => f.Rule.Id == check.RuleId && (check.Object is null || string.Equals(f.Object.Name, check.Object, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            return remaining.Count == 0
                ? Ok()
                : Fail("Lab.Feedback.RuleRemaining", check.RuleId!, string.Join(", ", remaining.Select(f => f.Object.Name).Distinct()));
        }

        private static (bool, string) NoFindingAtOrAbove(LabCheck check, AuditReport audit)
        {
            var threshold = check.Severity ?? AuditSeverity.Critical;
            var remaining = audit.Findings.Where(f => f.Severity <= threshold).ToList();
            return remaining.Count == 0
                ? Ok()
                : Fail("Lab.Feedback.FindingsRemaining", remaining.Count, string.Join(", ", remaining.Select(f => f.Rule.Id).Distinct()));
        }

        private static (bool, string) Membership(LabCheck check, AuditContext context, bool expected)
        {
            if (Find(context, check) is not { } member) return Fail("Lab.Feedback.Missing", check.Object!);
            var group = context.Group(check.Group!);
            var isMember = group != null && (check.Effective
                ? context.EffectiveGroups(member).Contains(group)
                : member.MemberOf.Contains(group.Name, StringComparer.OrdinalIgnoreCase));
            if (isMember == expected) return Ok();
            return expected
                ? Fail("Lab.Feedback.NotMember", member.Name, check.Group!)
                : Fail("Lab.Feedback.StillMember", member.Name, check.Group!);
        }

        private static (bool, string) Flag(LabCheck check, AuditContext context, bool expected)
        {
            if (Find(context, check) is not { } o) return Fail("Lab.Feedback.Missing", check.Object!);
            var flag = check.Flag ?? ADAccountFlags.None;
            return o.AccountFlags.HasFlag(flag) == expected
                ? Ok()
                : Fail(expected ? "Lab.Feedback.FlagMissing" : "Lab.Feedback.FlagPresent", o.Name, flag);
        }

        private static IEnumerable<(ADObject Target, ADDelegation Delegation)> DirectDelegations(AuditContext context, string trustee) =>
            context.Objects.SelectMany(o => o.Delegations
                .Where(d => !d.IsInherited && string.Equals(d.TrusteeName, trustee, StringComparison.OrdinalIgnoreCase))
                .Select(d => (o, d)));

        private static (bool, string) DelegationExists(LabCheck check, AuditContext context)
        {
            var found = DirectDelegations(context, check.Trustee!)
                .Any(x => string.Equals(x.Target.Name, check.Target, StringComparison.OrdinalIgnoreCase) && x.Delegation.RightCategory == check.RightCategory);
            return found ? Ok() : Fail("Lab.Feedback.DelegationMissing", check.Trustee!, check.RightCategory!, check.Target!);
        }

        private static (bool, string) DelegationOnly(LabCheck check, AuditContext context)
        {
            var delegations = DirectDelegations(context, check.Trustee!).ToList();
            if (delegations.Count == 0) return Fail("Lab.Feedback.NoDelegation", check.Trustee!);

            var allowed = check.AllowedCategories ?? new List<RightCategory>();
            var extra = delegations.Where(x => !allowed.Contains(x.Delegation.RightCategory)).ToList();
            return extra.Count == 0
                ? Ok()
                : Fail("Lab.Feedback.DelegationExtra", check.Trustee!,
                    string.Join(", ", extra.Select(x => $"{x.Delegation.RightCategory} ({x.Delegation.Right}) → {x.Target.Name}")));
        }

        private static (bool, string) NotModified(LabCheck check, ADObject start, List<ADChangeItem> changes)
        {
            var original = Flatten(start).FirstOrDefault(o => string.Equals(o.Name, check.Object, StringComparison.OrdinalIgnoreCase)
                                                              && (check.ObjectType is null || o.Type == check.ObjectType));
            if (original == null) return Fail("Lab.Feedback.Missing", check.Object!);

            var touched = changes.Where(c => string.Equals(c.DistinguishedName, original.DistinguishedName, StringComparison.OrdinalIgnoreCase)).ToList();
            return touched.Count == 0
                ? Ok()
                : Fail("Lab.Feedback.Modified", original.Name,
                    string.Join(", ", touched.Select(c => c.ChangeType == ChangeType.Removed ? Loc["Lab.Feedback.Removed"] : c.ChangedFieldsSummary)));
        }

        private static (bool, string) NoAttackPath(LabCheck check, AuditContext context, AttackPathService service)
        {
            AttackPath? remaining;
            if (check.Object is null)
                remaining = service.PathsOutsideTier0().FirstOrDefault();
            else if (string.Equals(check.Object, AttackPathService.AnyUserId, StringComparison.OrdinalIgnoreCase))
                remaining = service.PathsToDomainControl().FirstOrDefault(p => p.Start.IsAnyUser);
            else if (Find(context, check) is { } source)
                remaining = service.PathFrom(source);
            else
                return Fail("Lab.Feedback.Missing", check.Object);

            return remaining == null
                ? Ok()
                : Fail("Lab.Feedback.AttackPathRemaining", remaining.Start.Label,
                    string.Join(" → ", new[] { remaining.Start.Label }.Concat(remaining.Steps.Select(s => s.To.Label))));
        }

        private static IEnumerable<ADObject> Flatten(ADObject root) =>
            new[] { root }.Concat(root.Children.SelectMany(Flatten));
    }
}
