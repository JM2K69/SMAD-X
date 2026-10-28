namespace SMADX.Models
{
    public enum TrusteeType
    {
        User,
        Group,
        Computer
    }

    /// <summary>
    /// Ce que permet une délégation (une ACE). Les six premières valeurs existent depuis la 0.3 ; les suivantes
    /// (0.11) nomment les droits que BloodHound sait exploiter. Sérialisées par leur nom.
    /// </summary>
    public enum RightCategory
    {
        /// <summary>Réinitialiser le mot de passe (User-Force-Change-Password).</summary>
        PasswordReset,
        /// <summary>Créer ou gérer des comptes d'ordinateur.</summary>
        ComputerManagement,
        /// <summary>Déverrouiller les comptes (lockoutTime).</summary>
        AccountUnlock,
        /// <summary>Écrire un attribut ; « WriteProperty:member » et « GenericWrite » se lisent comme WriteMembers et GenericWrite.</summary>
        AttributeWrite,
        /// <summary>Contrôle total (GenericAll).</summary>
        FullControl,
        Other,
        /// <summary>Écrire tous les attributs : membres d'un groupe, SPN, msDS-KeyCredentialLink, délégation…</summary>
        GenericWrite,
        /// <summary>Modifier les permissions de l'objet : on s'y accorde le contrôle total.</summary>
        WriteDacl,
        /// <summary>Changer le propriétaire de l'objet : le propriétaire réécrit les permissions.</summary>
        WriteOwner,
        /// <summary>Modifier les membres d'un groupe (WriteProperty sur member).</summary>
        WriteMembers,
        /// <summary>Répliquer les secrets du domaine (DS-Replication-Get-Changes et -All) : DCSync.</summary>
        Replication,
        /// <summary>Écrire msDS-KeyCredentialLink : Shadow Credentials.</summary>
        WriteKeyCredentialLink,
        /// <summary>Écrire msDS-AllowedToActOnBehalfOfOtherIdentity : délégation contrainte basée sur la ressource (RBCD).</summary>
        WriteAllowedToAct,
        /// <summary>Écrire servicePrincipalName : Kerberoasting ciblé.</summary>
        WriteSpn,
        /// <summary>Lire le mot de passe administrateur local géré par LAPS.</summary>
        ReadLapsPassword,
        /// <summary>Lire le mot de passe d'un compte de service géré (gMSA).</summary>
        ReadGmsaPassword,
        /// <summary>Écrire gPLink : lier une GPO à l'OU ou au domaine.</summary>
        WriteGPLink,
        /// <summary>Tous les droits étendus : réinitialiser un mot de passe, lire LAPS…</summary>
        AllExtendedRights
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

        /// <summary>
        /// Ce que permet réellement la délégation. Avant la 0.11, l'import rangeait WriteDacl et WriteOwner sous
        /// FullControl, GenericWrite et member sous AttributeWrite, la réplication sous Other et AllExtendedRights
        /// sous PasswordReset : le nom du droit les distingue.
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public RightCategory Effective => RightCategory switch
        {
            RightCategory.FullControl when Is("WriteDacl") => RightCategory.WriteDacl,
            RightCategory.FullControl when Is("WriteOwner") => RightCategory.WriteOwner,
            RightCategory.AttributeWrite when Is("GenericWrite") => RightCategory.GenericWrite,
            RightCategory.AttributeWrite when Right.EndsWith(":member", System.StringComparison.OrdinalIgnoreCase) => RightCategory.WriteMembers,
            RightCategory.Other when Is("DS-Replication-Get-Changes-All") => RightCategory.Replication,
            RightCategory.PasswordReset when Is("AllExtendedRights") => RightCategory.AllExtendedRights,
            _ => RightCategory
        };

        private bool Is(string right) => string.Equals(Right, right, System.StringComparison.OrdinalIgnoreCase);

        /// <summary>Nom du droit d'une catégorie, tel que l'import le lit dans les ACL d'un vrai AD.</summary>
        public static string DefaultRight(RightCategory category) => category switch
        {
            RightCategory.FullControl => "GenericAll",
            RightCategory.PasswordReset => "ResetPassword",
            RightCategory.AccountUnlock => "WriteProperty:lockoutTime",
            RightCategory.ComputerManagement => "CreateChild:computer",
            RightCategory.AttributeWrite => "WriteProperty",
            RightCategory.GenericWrite => "GenericWrite",
            RightCategory.WriteDacl => "WriteDacl",
            RightCategory.WriteOwner => "WriteOwner",
            RightCategory.WriteMembers => "WriteProperty:member",
            RightCategory.Replication => "DS-Replication-Get-Changes-All",
            RightCategory.WriteKeyCredentialLink => "WriteProperty:msDS-KeyCredentialLink",
            RightCategory.WriteAllowedToAct => "WriteProperty:msDS-AllowedToActOnBehalfOfOtherIdentity",
            RightCategory.WriteSpn => "WriteProperty:servicePrincipalName",
            RightCategory.ReadLapsPassword => "ReadProperty:msLAPS-Password",
            RightCategory.ReadGmsaPassword => "ReadProperty:msDS-ManagedPassword",
            RightCategory.WriteGPLink => "WriteProperty:gPLink",
            RightCategory.AllExtendedRights => "AllExtendedRights",
            _ => "ExtendedRight"
        };
    }
}
