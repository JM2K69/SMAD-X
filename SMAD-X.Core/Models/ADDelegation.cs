namespace SMADX.Models
{
    public enum TrusteeType
    {
        User,
        Group,
        Computer
    }

    public enum RightCategory
    {
        PasswordReset,
        ComputerManagement,
        AccountUnlock,
        AttributeWrite,
        FullControl,
        Other
    }

    /// <summary>
    /// Represents an AD delegation (ACE) found on an OU or Container.
    /// </summary>
    public class ADDelegation
    {
        /// <summary>SAMAccountName of the trustee (user or group).</summary>
        public string TrusteeName { get; set; } = string.Empty;

        public TrusteeType TrusteeType { get; set; }

        /// <summary>Distinguished name of the OU/Container on which the ACE is set.</summary>
        public string TargetDN { get; set; } = string.Empty;

        /// <summary>Human-readable right, e.g. ResetPassword, CreateChild:computer.</summary>
        public string Right { get; set; } = string.Empty;

        public RightCategory RightCategory { get; set; }

        /// <summary>True when the ACE is inherited from a parent OU.</summary>
        public bool IsInherited { get; set; }

        /// <summary>Tier context of the target object, if known.</summary>
        public string? Tier { get; set; }
    }
}
