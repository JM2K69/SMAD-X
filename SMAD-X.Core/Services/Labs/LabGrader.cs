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
        /// <summary>Une garde ne rapporte pas de point.</summary>
        public int EarnedPoints => Passed && !Objective.Guard ? Objective.Points : 0;
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
    /// Une garde violée (le compte vulnérable supprimé au lieu d'être corrigé…) annule les objectifs qui
    /// concernaient l'objet protégé au départ : son constat, les chemins d'attaque qui le traversaient et
    /// les vérifications qui le nomment. La solution brutale ne rapporte donc rien.
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

            var lazyAudit = new Lazy<AuditReport>(() => audit);
            var results = lab.Objectives
                .Select(o =>
                {
                    var (passed, feedback) = Evaluate(o.Check, lazyAudit, context, start, changes, attackPaths);
                    return new LabObjectiveResult(o, passed, feedback);
                })
                .ToList();

            var broken = results.Where(r => r.Objective.Guard && !r.Passed).ToList();
            if (broken.Count > 0)
            {
                var scope = new StartScope(start, now);
                var culture = Loc.CurrentCulture.Name;
                results = results.Select(r =>
                {
                    if (r.Objective.Guard || !r.Passed) return r;
                    var involved = scope.Involved(r.Objective.Check);
                    var guard = broken.FirstOrDefault(g => Protected(g.Objective.Check).Overlaps(involved));
                    return guard == null ? r : r with
                    {
                        Passed = false,
                        Feedback = string.Format(Loc["Lab.Feedback.GuardBroken"], guard.Objective.Title.Resolve(culture), guard.Feedback)
                    };
                }).ToList();
            }
            return new LabResult(lab, results, audit, now);
        }

        /// <summary>Vrai si la vérification est atteinte sur ce domaine (comparé à lui-même pour « non modifié ») : sert à reconnaître les gardes.</summary>
        public static bool IsMet(LabCheck check, ADObject domain, DateTime now)
        {
            var audit = new Lazy<AuditReport>(() => new ADSecurityAuditService().Run(domain, now));
            var paths = new Lazy<AttackPathService>(() => new AttackPathService(domain, now));
            return Evaluate(check, audit, new AuditContext(domain, now), domain, new Lazy<List<ADChangeItem>>(() => new()), paths).Passed;
        }

        /// <summary>Objets que protège une garde : celui qu'elle nomme, ou le mandataire d'une délégation.</summary>
        private static HashSet<string> Protected(LabCheck check) =>
            new[] { check.Object, check.Trustee }.OfType<string>()
                .Where(n => !string.Equals(n, AttackPathService.AnyUserId, StringComparison.OrdinalIgnoreCase))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

        /// <summary>Objets qu'une vérification concernait au départ : ceux qu'elle nomme, ses constats et ses chemins d'attaque.</summary>
        private sealed class StartScope(ADObject start, DateTime now)
        {
            private readonly Lazy<AuditReport> _audit = new(() => new ADSecurityAuditService().Run(start, now));
            private readonly Lazy<AttackPathService> _paths = new(() => new AttackPathService(start, now));
            private readonly Lazy<AuditContext> _context = new(() => new AuditContext(start, now));

            public HashSet<string> Involved(LabCheck check)
            {
                var names = new[] { check.Object, check.Group, check.Trustee, check.Target }.OfType<string>()
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                switch (check.Type)
                {
                    case LabCheckType.RuleClear:
                        names.UnionWith(_audit.Value.Findings
                            .Where(f => f.Rule.Id == check.RuleId && (check.Object is null || string.Equals(f.Object.Name, check.Object, StringComparison.OrdinalIgnoreCase)))
                            .Select(f => f.Object.Name));
                        break;
                    case LabCheckType.NoFindingAtOrAbove:
                        names.UnionWith(_audit.Value.Findings.Where(f => f.Severity <= (check.Severity ?? AuditSeverity.Critical)).Select(f => f.Object.Name));
                        break;
                    case LabCheckType.ScoreAtLeast:
                        names.UnionWith(_audit.Value.Findings.Select(f => f.Object.Name));
                        break;
                    case LabCheckType.NoAttackPath:
                        IEnumerable<AttackPath> paths = check.Object is null ? _paths.Value.PathsOutsideTier0()
                            : string.Equals(check.Object, AttackPathService.AnyUserId, StringComparison.OrdinalIgnoreCase)
                                ? _paths.Value.PathsToDomainControl().Where(p => p.Start.IsAnyUser)
                            : Find(_context.Value, check) is { } source && _paths.Value.PathFrom(source) is { } path ? [path] : [];
                        foreach (var p in paths)
                            names.UnionWith(new[] { p.Start }.Concat(p.Steps.Select(s => s.To)).Select(n => n.Object?.Name).OfType<string>());
                        break;
                }
                return names;
            }
        }

        private static (bool Passed, string Feedback) Evaluate(LabCheck check, Lazy<AuditReport> lazyAudit,
            AuditContext context, ADObject start, Lazy<List<ADChangeItem>> changes, Lazy<AttackPathService> attackPaths)
        {
            return check.Type switch
            {
                LabCheckType.RuleClear => RuleClear(check, lazyAudit.Value),
                LabCheckType.NoFindingAtOrAbove => NoFindingAtOrAbove(check, lazyAudit.Value),
                LabCheckType.ScoreAtLeast => lazyAudit.Value.Score >= (check.Score ?? 100)
                    ? Ok()
                    : Fail("Lab.Feedback.Score", lazyAudit.Value.Score, check.Score ?? 100),
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

            var touched = changes.Where(c => string.Equals(c.DistinguishedName, original.DistinguishedName, StringComparison.OrdinalIgnoreCase)
                                             || string.Equals(c.PreviousDistinguishedName, original.DistinguishedName, StringComparison.OrdinalIgnoreCase)).ToList();
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
