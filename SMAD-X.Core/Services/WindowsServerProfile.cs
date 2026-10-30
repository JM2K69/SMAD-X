using System.Collections.Generic;
using System.Linq;
using SMADX.Models;

namespace SMADX.Services
{
    /// <summary>
    /// Objet qu'une version de Windows Server crée en plus du tronc commun, dans le conteneur donné par son chemin depuis
    /// la racine (« Builtin », « System »…). Sans clé de description, l'objet n'a pas de description, comme dans AD.
    /// </summary>
    public sealed record VersionObject(string Name, ADObjectType Type, string[] Container, string? DescriptionKey, string? Tier);

    /// <summary>
    /// Ce qui distingue un domaine neuf promu par une version de Windows Server du tronc commun que construit
    /// <see cref="ADDataService"/>.
    /// <para>
    /// Une version est vérifiée quand l'export d'un vrai domaine neuf (SMAD-X.Tests/Fidelity) confirme son profil ; le
    /// test de fidélité compare alors le domaine neuf de SMAD-X à cet export, objet par objet et permission par permission.
    /// </para>
    /// </summary>
    /// <param name="LockoutMinutes">Durée de verrouillage d'un compte et de la fenêtre d'observation des échecs, par défaut.</param>
    public sealed record WindowsServerProfile(WindowsServerVersion Version, string Name, bool Verified, int LockoutMinutes, IReadOnlyList<VersionObject> Objects)
    {
        /// <summary>Version d'un nouveau domaine quand rien n'est précisé.</summary>
        public const WindowsServerVersion DefaultVersion = WindowsServerVersion.Server2022;

        // Exports de domaines neufs du 30/10/2026 : les trois versions partagent le même tronc commun, mêmes permissions
        // et mêmes comptes ; un compte reste verrouillé 30 minutes sous Windows Server 2019, 10 ensuite ; Windows Server
        // 2022 crée le conteneur SOA-Policies, Windows Server 2025 installe OpenSSH d'office et donne leurs groupes aux
        // comptes des approbations
        public static IReadOnlyList<WindowsServerProfile> All { get; } =
        [
            new(WindowsServerVersion.Server2019, "Windows Server 2019", true, 30, []),
            new(WindowsServerVersion.Server2022, "Windows Server 2022", true, 10,
                [new("SOA-Policies", ADObjectType.Container, ["System"], null, null)]),
            new(WindowsServerVersion.Server2025, "Windows Server 2025", true, 10,
                [
                    new("OpenSSH Users", ADObjectType.Group, ["Builtin"], "Desc.Builtin.OpenSshUsers", "Tier 0"),
                    new("External Trust Accounts", ADObjectType.Group, ["Users"], "Desc.Users.ExternalTrustAccounts", "Tier 0"),
                    new("Forest Trust Accounts", ADObjectType.Group, ["Users"], "Desc.Users.ForestTrustAccounts", "Tier 0")
                ])
        ];

        public static WindowsServerProfile For(WindowsServerVersion version) => All.Single(p => p.Version == version);

        /// <summary>« Windows Server 2022 », ou null pour un domaine dont la version est inconnue.</summary>
        public static string? NameOf(WindowsServerVersion? version) => version is { } v ? For(v).Name : null;
    }
}
