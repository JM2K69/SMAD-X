using System;

namespace SMADX.Models
{
    /// <summary>
    /// Represents a point-in-time snapshot of an AD domain loaded from a .smad-x.json file.
    /// </summary>
    public class DomainSnapshot
    {
        /// <summary>Full path to the source .smad-x.json file.</summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>Root domain name (e.g. contoso.com).</summary>
        public string DomainName { get; set; } = string.Empty;

        /// <summary>Snapshot date — taken from the root ADObject.ModifiedDate.</summary>
        public DateTime SnapshotDate { get; set; }

        /// <summary>Fully deserialized AD object tree.</summary>
        public ADObject Root { get; set; } = null!;

        public override string ToString() => $"{DomainName}  —  {SnapshotDate:yyyy-MM-dd HH:mm}";
    }
}
