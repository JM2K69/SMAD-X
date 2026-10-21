using System;

namespace SMADX.Models
{
    /// <summary>
    /// Bits de userAccountControl utiles en sécurité (valeurs identiques à AD, pour l'import/export).
    /// Les bits de type de compte (NORMAL_ACCOUNT, SERVER_TRUST_ACCOUNT…) découlent de ADObjectType.
    /// </summary>
    [Flags]
    public enum ADAccountFlags
    {
        None = 0,

        /// <summary>ACCOUNTDISABLE (0x2) : le compte ne peut pas ouvrir de session.</summary>
        AccountDisabled = 0x2,

        /// <summary>PASSWD_NOTREQD (0x20) : un mot de passe vide est accepté.</summary>
        PasswordNotRequired = 0x20,

        /// <summary>DONT_EXPIRE_PASSWORD (0x10000) : le mot de passe n'expire jamais.</summary>
        PasswordNeverExpires = 0x10000,

        /// <summary>
        /// TRUSTED_FOR_DELEGATION (0x80000) : délégation non contrainte. Le compte reçoit le TGT de
        /// quiconque s'y authentifie ; normal sur un contrôleur de domaine, dangereux ailleurs.
        /// </summary>
        TrustedForDelegation = 0x80000,

        /// <summary>NOT_DELEGATED (0x100000) : « compte sensible, ne peut pas être délégué ».</summary>
        NotDelegated = 0x100000,

        /// <summary>
        /// DONT_REQ_PREAUTH (0x400000) : pas de pré-authentification Kerberos, ce qui rend le compte
        /// vulnérable à l'AS-REP roasting.
        /// </summary>
        DontRequirePreauth = 0x400000,

        /// <summary>
        /// TRUSTED_TO_AUTH_FOR_DELEGATION (0x1000000) : délégation contrainte avec transition de
        /// protocole (S4U2Self), vers les services listés dans AllowedToDelegateTo.
        /// </summary>
        TrustedToAuthForDelegation = 0x1000000
    }

    /// <summary>Portée d'un groupe (bits 0x2 / 0x4 / 0x8 de groupType).</summary>
    public enum ADGroupScope
    {
        DomainLocal,
        Global,
        Universal
    }

    /// <summary>Catégorie d'un groupe (bit 0x80000000 de groupType = sécurité).</summary>
    public enum ADGroupCategory
    {
        Security,
        Distribution
    }
}
