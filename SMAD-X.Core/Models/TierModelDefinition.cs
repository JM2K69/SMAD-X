using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SMADX.Models
{
    /// <summary>
    /// Modèle de cloisonnement en tiers publié par un tiers (Microsoft, HardenAD) : ce qu'il crée dans l'annuaire.
    /// Les fichiers SMAD-X.Core/TierModels/*.json sont produits par tools/TierModelGenerator à partir de la source
    /// officielle du modèle ; ils ne s'écrivent pas à la main.
    /// </summary>
    public sealed class TierModelDefinition
    {
        /// <summary>Identifiant stable, enregistré sur le domaine : « microsoft », « hardenad ».</summary>
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public TierModelSource Source { get; set; } = new();

        /// <summary>Tiers propres au modèle (HardenAD ajoute des tiers « Legacy »).</summary>
        public List<TierModelTier> Tiers { get; set; } = new();

        public List<TierModelOu> OrganizationalUnits { get; set; } = new();

        public List<TierModelGroup> Groups { get; set; } = new();

        public List<TierModelAccount> Accounts { get; set; } = new();

        public List<TierModelDelegation> Delegations { get; set; } = new();

        public List<TierModelGpo> GroupPolicies { get; set; } = new();

        /// <summary>Membres que le modèle ajoute à des groupes qui existent déjà (Enterprise Admins, Protected Users…).</summary>
        public List<TierModelMembership> DefaultMembers { get; set; } = new();
    }

    /// <summary>D'où vient le modèle : à citer, et à suivre quand il évolue.</summary>
    public sealed class TierModelSource
    {
        public string Repository { get; set; } = string.Empty;

        /// <summary>Version publiée (étiquette Git) dont le modèle est tiré.</summary>
        public string Version { get; set; } = string.Empty;

        public string Commit { get; set; } = string.Empty;

        public string License { get; set; } = string.Empty;

        public string Copyright { get; set; } = string.Empty;

        /// <summary>Fichiers de la source lus par le générateur.</summary>
        public List<string> Files { get; set; } = new();

        /// <summary>Ce que SMAD-X ne reproduit pas (composants optionnels, paramètres des GPO…).</summary>
        public List<TierModelNote> Notes { get; set; } = new();
    }

    public sealed class TierModelNote
    {
        public string Fr { get; set; } = string.Empty;
        public string En { get; set; } = string.Empty;

        public string Resolve(string language) => language.StartsWith("en", System.StringComparison.OrdinalIgnoreCase) ? En : Fr;
    }

    public sealed class TierModelTier
    {
        public string Name { get; set; } = string.Empty;
        public int Level { get; set; }
        public string Color { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    /// <summary>Rôle d'une OU dans le modèle ; les OU de transit attendent qu'un objet soit rangé dans son tier.</summary>
    public enum TierModelOuRole
    {
        Container,
        AdminAccounts,
        AdminGroups,
        ServiceAccounts,
        AdminWorkstations,
        Servers,
        Workstations,
        Users,
        Groups,
        Contacts,
        Disabled,
        Staging,
        Quarantine,
        Provisioning
    }

    public sealed class TierModelOu
    {
        /// <summary>Chemin depuis la racine du domaine, du parent à l'enfant : « Tier Model Administration/Tier 0 ».</summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>Tier des objets rangés dans l'OU ; null pour une OU de regroupement ou de transit.</summary>
        public string? Tier { get; set; }

        public TierModelOuRole Role { get; set; }

        public string Description { get; set; } = string.Empty;
    }

    public sealed class TierModelGroup
    {
        /// <summary>sAMAccountName, nom sous lequel SMAD-X désigne le groupe.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>OU du groupe, au format de <see cref="TierModelOu.Path"/>.</summary>
        public string Path { get; set; } = string.Empty;

        public string? Tier { get; set; }

        public ADGroupScope Scope { get; set; }

        public string Description { get; set; } = string.Empty;

        public List<string> MemberOf { get; set; } = new();
    }

    public sealed class TierModelAccount
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string? Tier { get; set; }
        public bool Enabled { get; set; }
        public string Description { get; set; } = string.Empty;
        public List<string> MemberOf { get; set; } = new();
    }

    public sealed class TierModelDelegation
    {
        public string Trustee { get; set; } = string.Empty;

        /// <summary>OU où l'ACE est posée, au format de <see cref="TierModelOu.Path"/>.</summary>
        public string Target { get; set; } = string.Empty;

        /// <summary>Droit tel que l'import le nomme (GenericAll, ResetPassword, CreateChild:computer…).</summary>
        public string Right { get; set; } = string.Empty;

        public RightCategory Category { get; set; }

        /// <summary>Droits de la source, pour la lecture humaine.</summary>
        public string SourceRights { get; set; } = string.Empty;
    }

    /// <summary>Ce que fait une GPO du modèle, pour les GPO qui comptent dans le cloisonnement.</summary>
    public enum TierModelGpoRole
    {
        /// <summary>Restreint l'ouverture de session (droits « Interdire l'ouverture de session… »).</summary>
        LogonRestrictions,
        /// <summary>Peuple le groupe local Administrateurs des machines.</summary>
        LocalAdministrators,
        /// <summary>Peuple le groupe local Utilisateurs du Bureau à distance.</summary>
        RemoteDesktopUsers,
        Laps,
        AuthenticationSilo,
        Baseline
    }

    public sealed class TierModelGpo
    {
        public string Name { get; set; } = string.Empty;

        public TierModelGpoRole Role { get; set; }

        /// <summary>Tier que la GPO protège ou administre ; null pour une GPO qui vaut pour tout le domaine.</summary>
        public string? Tier { get; set; }

        public List<TierModelGpoLink> Links { get; set; } = new();

        /// <summary>« Interdire l'ouverture d'une session locale » : comptes et groupes refusés ; null si la GPO ne définit pas ce droit.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? DenyInteractiveLogon { get; set; }

        /// <summary>« Interdire l'ouverture de session par les services Bureau à distance » ; null si la GPO ne le définit pas.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? DenyRemoteInteractiveLogon { get; set; }

        /// <summary>Membres que la GPO met dans le groupe local Administrateurs ; null si elle n'y touche pas.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string>? LocalAdmins { get; set; }

        /// <summary><see cref="LocalAdmins"/> remplace les membres du groupe au lieu de s'y ajouter.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool ReplaceLocalAdmins { get; set; }

        /// <summary>Filtre WMI de la GPO ; null = tous les ordinateurs de l'OU liée.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public GpoComputerFilter? ComputerFilter { get; set; }
    }

    public sealed class TierModelGpoLink
    {
        /// <summary>OU liée, au format de <see cref="TierModelOu.Path"/> ; vide pour la racine du domaine.</summary>
        public string Target { get; set; } = string.Empty;

        /// <summary>Le modèle crée certains liens désactivés, à activer après test.</summary>
        public bool Enabled { get; set; }
    }

    public sealed class TierModelMembership
    {
        public string Group { get; set; } = string.Empty;
        public List<string> Members { get; set; } = new();
    }
}
