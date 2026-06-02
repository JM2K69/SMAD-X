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

            var flatA = Flatten(fileA.Root).ToDictionary(o => o.DistinguishedName, StringComparer.OrdinalIgnoreCase);
            var flatB = Flatten(fileB.Root).ToDictionary(o => o.DistinguishedName, StringComparer.OrdinalIgnoreCase);

            // ── Structural: Added objects ──────────────────────────────────
            foreach (var dn in flatB.Keys.Except(flatA.Keys, StringComparer.OrdinalIgnoreCase))
            {
                var obj = flatB[dn];
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
            foreach (var dn in flatA.Keys.Except(flatB.Keys, StringComparer.OrdinalIgnoreCase))
            {
                var obj = flatA[dn];
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
            foreach (var dn in flatA.Keys.Intersect(flatB.Keys, StringComparer.OrdinalIgnoreCase))
            {
                var before = flatA[dn];
                var after  = flatB[dn];

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
            }

            // ── Delegations on added objects ───────────────────────────────
            foreach (var dn in flatB.Keys.Except(flatA.Keys, StringComparer.OrdinalIgnoreCase))
            {
                var obj = flatB[dn];
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

        private static List<(string, string, string)> DetectDelegationChanges(ADObject before, ADObject after)
        {
            var c = new List<(string, string, string)>();

            // Use "Trustee|Right|Target" as a stable key
            static string Key(ADDelegation d) =>
                $"{d.TrusteeName}|{d.Right}|{d.TargetDN}".ToLowerInvariant();

            var oldKeys = before.Delegations.ToDictionary(Key);
            var newKeys = after.Delegations.ToDictionary(Key);

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
