using System;
using System.Collections.Generic;
using System.Linq;
using SMADX.Models;

namespace SMADX.Services
{
    /// <summary>
    /// Compares two DomainSnapshot trees and produces a flat list of ADChangeItem.
    /// Each item is tagged with a ChangeCategory for UI filtering.
    /// </summary>
    public static class ADDiffService
    {
        public static List<ADChangeItem> Compare(DomainSnapshot fileA, DomainSnapshot fileB)
        {
            var result = new List<ADChangeItem>();
            var (pairs, removed, added) = Pair(Flatten(fileA.Root).ToList(), Flatten(fileB.Root).ToList());

            // ── Structural: Added objects ──────────────────────────────────
            foreach (var obj in added)
            {
                result.Add(new ADChangeItem
                {
                    ChangeType        = ChangeType.Added,
                    ChangeCategory    = ChangeCategory.Structure,
                    ObjectName        = obj.Name,
                    DistinguishedName = obj.DistinguishedName,
                    ObjectType        = obj.Type
                });
            }

            // ── Structural: Removed objects ────────────────────────────────
            foreach (var obj in removed)
            {
                result.Add(new ADChangeItem
                {
                    ChangeType        = ChangeType.Removed,
                    ChangeCategory    = ChangeCategory.Structure,
                    ObjectName        = obj.Name,
                    DistinguishedName = obj.DistinguishedName,
                    ObjectType        = obj.Type
                });
            }

            // ── Per-object deltas for objects present in both files ────────
            foreach (var (before, after) in pairs)
            {
                var first = result.Count;

                // Structural field changes
                var structFields = DetectStructureChanges(before, after);
                if (structFields.Count > 0)
                    result.Add(Item(ChangeType.Modified, ChangeCategory.Structure, after, structFields));

                // MemberOf delta
                var moItems = DeltaStrings(before.MemberOf, after.MemberOf, "Member");
                if (moItems.Count > 0)
                    result.Add(Item(ChangeType.Modified, ChangeCategory.MemberOf, after, moItems));

                // LinkedGPOs delta
                var gpoItems = DeltaStrings(before.LinkedGPOs, after.LinkedGPOs, "GPO");
                if (gpoItems.Count > 0)
                    result.Add(Item(ChangeType.Modified, ChangeCategory.GPO, after, gpoItems));

                // PSO delta (applies-to list + settings)
                var psoItems = DetectPsoChanges(before, after);
                if (psoItems.Count > 0)
                    result.Add(Item(ChangeType.Modified, ChangeCategory.PSO, after, psoItems));

                // Delegation delta
                var delItems = DetectDelegationChanges(before, after);
                if (delItems.Count > 0)
                    result.Add(Item(ChangeType.Modified, ChangeCategory.Delegation, after, delItems));

                // Security attributes delta
                var secItems = DetectSecurityChanges(before, after);
                if (secItems.Count > 0)
                    result.Add(Item(ChangeType.Modified, ChangeCategory.Security, after, secItems));

                // Renamed or moved: keep the former DN so that the change can be found from it
                if (!string.Equals(before.DistinguishedName, after.DistinguishedName, StringComparison.OrdinalIgnoreCase))
                    for (var i = first; i < result.Count; i++)
                        result[i].PreviousDistinguishedName = before.DistinguishedName;
            }

            // ── Delegations on added objects ───────────────────────────────
            foreach (var obj in added)
            {
                foreach (var del in obj.Delegations)
                    result.Add(new ADChangeItem
                    {
                        ChangeType        = ChangeType.Added,
                        ChangeCategory    = ChangeCategory.Delegation,
                        ObjectName        = del.TrusteeName,
                        DistinguishedName = del.TargetDN,
                        ObjectType        = obj.Type,
                        ChangedFields     = new List<(string, string, string)>
                        {
                            ("Right", string.Empty, del.Right),
                            ("Target", string.Empty, del.TargetDN)
                        }
                    });
            }

            return result;
        }

        // ── Helpers ────────────────────────────────────────────────────────

        /// <summary>
        /// Pairs the objects of both trees by DN, then by Id: an object renamed or moved keeps its Id.
        /// Two siblings may share a DN in a file edited by hand: they pair in order instead of failing.
        /// </summary>
        private static (List<(ADObject Before, ADObject After)> Pairs, List<ADObject> Removed, List<ADObject> Added) Pair(
            List<ADObject> a, List<ADObject> b)
        {
            var byDn = b.GroupBy(o => o.DistinguishedName, StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(g => g.Key, g => new Queue<ADObject>(g), StringComparer.OrdinalIgnoreCase);
            var pairs = new List<(ADObject, ADObject)>();
            var removed = new List<ADObject>();
            var matched = new HashSet<ADObject>();

            foreach (var before in a)
            {
                if (byDn.TryGetValue(before.DistinguishedName, out var queue) && queue.Count > 0)
                {
                    var after = queue.Dequeue();
                    pairs.Add((before, after));
                    matched.Add(after);
                }
                else
                    removed.Add(before);
            }

            var unmatched = b.Where(o => !matched.Contains(o)).ToList();
            var byId = unmatched.Where(o => o.Id != Guid.Empty).GroupBy(o => o.Id).ToDictionary(g => g.Key, g => g.First());
            foreach (var before in removed.ToList())
            {
                if (before.Id == Guid.Empty || !byId.Remove(before.Id, out var after)) continue;
                pairs.Add((before, after));
                removed.Remove(before);
                unmatched.Remove(after);
            }
            return (pairs, removed, unmatched);
        }

        private static IEnumerable<ADObject> Flatten(ADObject root)
        {
            yield return root;
            foreach (var child in root.Children)
                foreach (var desc in Flatten(child))
                    yield return desc;
        }

        private static ADChangeItem Item(
            ChangeType type, ChangeCategory cat, ADObject obj,
            List<(string, string, string)> fields) =>
            new()
            {
                ChangeType        = type,
                ChangeCategory    = cat,
                ObjectName        = obj.Name,
                DistinguishedName = obj.DistinguishedName,
                ObjectType        = obj.Type,
                ChangedFields     = fields
            };

        private static List<(string, string, string)> DetectStructureChanges(ADObject before, ADObject after)
        {
            var c = new List<(string, string, string)>();
            Check(c, "Name",        before.Name,               after.Name);
            if (!string.Equals(before.DistinguishedName, after.DistinguishedName, StringComparison.OrdinalIgnoreCase))
                Check(c, "DN",      before.DistinguishedName,  after.DistinguishedName);
            Check(c, "Description", before.Description ?? "",  after.Description ?? "");
            Check(c, "Tier",        before.Tier ?? "",         after.Tier ?? "");
            Check(c, "Type",        before.Type.ToString(),    after.Type.ToString());
            return c;
        }

        private static List<(string, string, string)> DeltaStrings(
            IEnumerable<string> oldSet, IEnumerable<string> newSet, string label)
        {
            var c   = new List<(string, string, string)>();
            var old = oldSet.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var nw  = newSet.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var added   = nw.Except(old).ToList();
            var removed = old.Except(nw).ToList();
            if (added.Count > 0)   c.Add(($"{label} +", string.Empty, string.Join(", ", added)));
            if (removed.Count > 0) c.Add(($"{label} -", string.Join(", ", removed), string.Empty));
            return c;
        }

        private static List<(string, string, string)> DetectPsoChanges(ADObject before, ADObject after)
        {
            var c = new List<(string, string, string)>();
            CheckNullable(c, "PSO.Precedence",           before.PSOPrecedence,                  after.PSOPrecedence);
            CheckNullable(c, "PSO.MinLength",            before.PSOMinPasswordLength,            after.PSOMinPasswordLength);
            CheckNullable(c, "PSO.HistoryCount",         before.PSOPasswordHistoryCount,         after.PSOPasswordHistoryCount);
            CheckNullable(c, "PSO.Complexity",           before.PSOComplexityEnabled,            after.PSOComplexityEnabled);
            CheckNullable(c, "PSO.MaxAgeDays",           before.PSOMaxPasswordAgeDays,           after.PSOMaxPasswordAgeDays);
            CheckNullable(c, "PSO.LockoutThreshold",     before.PSOLockoutThreshold,             after.PSOLockoutThreshold);
            CheckNullable(c, "PSO.LockoutDurationMin",   before.PSOLockoutDurationMinutes,       after.PSOLockoutDurationMinutes);
            var psoAdded   = after.PSOAppliesTo.Except(before.PSOAppliesTo,  StringComparer.OrdinalIgnoreCase).ToList();
            var psoRemoved = before.PSOAppliesTo.Except(after.PSOAppliesTo, StringComparer.OrdinalIgnoreCase).ToList();
            if (psoAdded.Count > 0)   c.Add(("PSO.AppliesTo +", string.Empty, string.Join(", ", psoAdded)));
            if (psoRemoved.Count > 0) c.Add(("PSO.AppliesTo -", string.Join(", ", psoRemoved), string.Empty));
            return c;
        }

        private static List<(string, string, string)> DetectSecurityChanges(ADObject before, ADObject after)
        {
            var c = new List<(string, string, string)>();

            // Un drapeau à la fois : « DontRequirePreauth + » se lit mieux qu'un ensemble complet
            foreach (var flag in Enum.GetValues<ADAccountFlags>().Where(f => f != ADAccountFlags.None))
            {
                bool was = before.AccountFlags.HasFlag(flag), now = after.AccountFlags.HasFlag(flag);
                if (!was && now) c.Add(($"{flag} +", string.Empty, "true"));
                if (was && !now) c.Add(($"{flag} -", "true", string.Empty));
            }

            c.AddRange(DeltaStrings(before.ServicePrincipalNames, after.ServicePrincipalNames, "SPN"));
            c.AddRange(DeltaStrings(before.AllowedToDelegateTo, after.AllowedToDelegateTo, "AllowedToDelegateTo"));
            Check(c, "AdminCount",    before.AdminCount.ToString(),         after.AdminCount.ToString());
            Check(c, "GroupScope",    before.GroupScope?.ToString() ?? "",    after.GroupScope?.ToString() ?? "");
            Check(c, "GroupCategory", before.GroupCategory?.ToString() ?? "", after.GroupCategory?.ToString() ?? "");
            Check(c, "Owner",         before.Owner ?? "",                   after.Owner ?? "");
            CheckNullable(c, "MachineAccountQuota", before.MachineAccountQuota, after.MachineAccountQuota);
            // Ordinateurs : LAPS en place ou retiré, administrateurs locaux, sessions
            Check(c, "LAPS", (before.LapsPasswordExpiration != null).ToString(), (after.LapsPasswordExpiration != null).ToString());
            c.AddRange(DeltaStrings(before.LocalAdmins ?? [], after.LocalAdmins ?? [], "LocalAdmins"));
            c.AddRange(DeltaStrings(before.Sessions ?? [], after.Sessions ?? [], "Sessions"));
            // pwdLastSet oui (renouveler krbtgt est un événement à voir), lastLogonTimestamp non :
            // il change à chaque connexion et noierait les vrais changements de configuration
            Check(c, "PwdLastSet",    before.PwdLastSet?.ToString("yyyy-MM-dd") ?? "", after.PwdLastSet?.ToString("yyyy-MM-dd") ?? "");
            return c;
        }

        private static List<(string, string, string)> DetectDelegationChanges(ADObject before, ADObject after)
        {
            var c = new List<(string, string, string)>();

            // Use "Trustee|Right|Target|Inherited" as a stable key; an ACL can still hold
            // duplicate ACEs (e.g. several object types), so keep the first of each key
            static string Key(ADDelegation d) =>
                $"{d.TrusteeName}|{d.Right}|{d.TargetDN}|{d.IsInherited}".ToLowerInvariant();

            var oldKeys = before.Delegations.GroupBy(Key).ToDictionary(g => g.Key, g => g.First());
            var newKeys = after.Delegations.GroupBy(Key).ToDictionary(g => g.Key, g => g.First());

            foreach (var k in newKeys.Keys.Except(oldKeys.Keys))
            {
                var d = newKeys[k];
                c.Add(($"Delegation +", string.Empty, $"{d.TrusteeName} → {d.Right} on {d.TargetDN}"));
            }
            foreach (var k in oldKeys.Keys.Except(newKeys.Keys))
            {
                var d = oldKeys[k];
                c.Add(($"Delegation -", $"{d.TrusteeName} → {d.Right} on {d.TargetDN}", string.Empty));
            }
            return c;
        }

        private static void Check(List<(string, string, string)> list, string f, string a, string b)
        {
            if (!string.Equals(a, b, StringComparison.Ordinal)) list.Add((f, a, b));
        }

        private static void CheckNullable<T>(List<(string, string, string)> list, string f, T? a, T? b)
            where T : struct
        {
            var sa = a?.ToString() ?? string.Empty;
            var sb = b?.ToString() ?? string.Empty;
            if (sa != sb) list.Add((f, sa, sb));
        }
    }
}
