using System.Collections.Generic;

namespace SMADX.Models
{
    public enum ChangeType
    {
        Added,
        Removed,
        Modified
    }

    /// <summary>
    /// Broad category for the kind of change — used as a filter axis in the Timeline UI.
    /// </summary>
    public enum ChangeCategory
    {
        Structure,    // object added / removed / renamed / moved
        MemberOf,     // group membership changes
        GPO,          // GPO link added / removed
        PSO,          // PSO assignment or settings changed
        Delegation    // delegation (ACE) added / removed / changed
    }

    /// <summary>
    /// Represents a single change detected between two AD snapshots.
    /// </summary>
    public class ADChangeItem
    {
        public ChangeType     ChangeType        { get; set; }
        public ChangeCategory ChangeCategory    { get; set; } = ChangeCategory.Structure;
        public string         ObjectName        { get; set; } = string.Empty;
        public string         DistinguishedName { get; set; } = string.Empty;
        public ADObjectType   ObjectType        { get; set; }

        /// <summary>
        /// For Modified items: list of (Field, OldValue, NewValue) tuples.
        /// </summary>
        public List<(string Field, string OldValue, string NewValue)> ChangedFields { get; set; } = new();

        public string ChangedFieldsSummary =>
            ChangedFields.Count == 0
                ? string.Empty
                : string.Join("; ", ChangedFields.ConvertAll(f =>
                    string.IsNullOrEmpty(f.OldValue) ? $"+{f.Field}: {f.NewValue}"
                    : string.IsNullOrEmpty(f.NewValue) ? $"-{f.Field}: {f.OldValue}"
                    : $"{f.Field}: {f.OldValue} → {f.NewValue}"));
    }
}
