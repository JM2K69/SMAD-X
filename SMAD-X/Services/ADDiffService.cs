using System;
using System.Collections.Generic;
using System.Linq;
using SMADX.Models;

namespace SMADX.Services
{
    /// <summary>
    /// Compares two DomainSnapshot trees and produces a flat list of ADChangeItem.
    /// </summary>
    public static class ADDiffService
    {
        /// <summary>
        /// Compare a baseline snapshot against a current snapshot.
        /// </summary>
        public static List<ADChangeItem> Compare(DomainSnapshot baseline, DomainSnapshot current)
        {
            var result = new List<ADChangeItem>();

            var baselineFlat = Flatten(baseline.Root);
            var currentFlat  = Flatten(current.Root);

            var baselineByDn = baselineFlat.ToDictionary(o => o.DistinguishedName, StringComparer.OrdinalIgnoreCase);
            var currentByDn  = currentFlat .ToDictionary(o => o.DistinguishedName, StringComparer.OrdinalIgnoreCase);

            // Added
            foreach (var dn in currentByDn.Keys.Except(baselineByDn.Keys, StringComparer.OrdinalIgnoreCase))
            {
                var obj = currentByDn[dn];
                result.Add(new ADChangeItem
                {
                    ChangeType        = ChangeType.Added,
                    ObjectName        = obj.Name,
                    DistinguishedName = obj.DistinguishedName,
                    ObjectType        = obj.Type
                });
            }

            // Removed
            foreach (var dn in baselineByDn.Keys.Except(currentByDn.Keys, StringComparer.OrdinalIgnoreCase))
            {
                var obj = baselineByDn[dn];
                result.Add(new ADChangeItem
                {
                    ChangeType        = ChangeType.Removed,
                    ObjectName        = obj.Name,
                    DistinguishedName = obj.DistinguishedName,
                    ObjectType        = obj.Type
                });
            }

            // Modified
            foreach (var dn in baselineByDn.Keys.Intersect(currentByDn.Keys, StringComparer.OrdinalIgnoreCase))
            {
                var before = baselineByDn[dn];
                var after  = currentByDn[dn];
                var fields = DetectChanges(before, after);
                if (fields.Count > 0)
                {
                    result.Add(new ADChangeItem
                    {
                        ChangeType        = ChangeType.Modified,
                        ObjectName        = after.Name,
                        DistinguishedName = after.DistinguishedName,
                        ObjectType        = after.Type,
                        ChangedFields     = fields
                    });
                }
            }

            return result;
        }

        // ── helpers ────────────────────────────────────────────────────────────

        private static IEnumerable<ADObject> Flatten(ADObject root)
        {
            yield return root;
            foreach (var child in root.Children)
                foreach (var desc in Flatten(child))
                    yield return desc;
        }

        private static List<(string Field, string OldValue, string NewValue)> DetectChanges(ADObject before, ADObject after)
        {
            var changes = new List<(string, string, string)>();

            Check(changes, nameof(ADObject.Name),        before.Name,        after.Name);
            Check(changes, nameof(ADObject.Description), before.Description, after.Description);
            Check(changes, nameof(ADObject.Tier),        before.Tier ?? "",  after.Tier ?? "");
            Check(changes, nameof(ADObject.Type),        before.Type.ToString(), after.Type.ToString());

            // MemberOf delta
            var addedMemberships   = after.MemberOf.Except(before.MemberOf).ToList();
            var removedMemberships = before.MemberOf.Except(after.MemberOf).ToList();
            if (addedMemberships.Count > 0)
                changes.Add(("MemberOf +", string.Empty, string.Join(", ", addedMemberships)));
            if (removedMemberships.Count > 0)
                changes.Add(("MemberOf -", string.Join(", ", removedMemberships), string.Empty));

            // LinkedGPOs delta
            var addedGpos   = after.LinkedGPOs.Except(before.LinkedGPOs).ToList();
            var removedGpos = before.LinkedGPOs.Except(after.LinkedGPOs).ToList();
            if (addedGpos.Count > 0)
                changes.Add(("LinkedGPOs +", string.Empty, string.Join(", ", addedGpos)));
            if (removedGpos.Count > 0)
                changes.Add(("LinkedGPOs -", string.Join(", ", removedGpos), string.Empty));

            return changes;
        }

        private static void Check(
            List<(string, string, string)> list,
            string field, string oldVal, string newVal)
        {
            if (!string.Equals(oldVal, newVal, StringComparison.Ordinal))
                list.Add((field, oldVal, newVal));
        }
    }
}
