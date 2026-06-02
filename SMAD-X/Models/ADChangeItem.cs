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
    /// Represents a single change detected between two AD snapshots.
    /// </summary>
    public class ADChangeItem
    {
        public ChangeType ChangeType { get; set; }
        public string ObjectName { get; set; } = string.Empty;
        public string DistinguishedName { get; set; } = string.Empty;
        public ADObjectType ObjectType { get; set; }

        /// <summary>
        /// For Modified items: list of (Field, OldValue, NewValue) tuples.
        /// </summary>
        public List<(string Field, string OldValue, string NewValue)> ChangedFields { get; set; } = new();

        public string ChangedFieldsSummary =>
            ChangedFields.Count == 0
                ? string.Empty
                : string.Join(", ", ChangedFields.ConvertAll(f => $"{f.Field}: {f.OldValue} → {f.NewValue}"));
    }
}
