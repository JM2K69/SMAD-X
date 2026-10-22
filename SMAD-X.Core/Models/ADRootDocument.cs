using System.Collections.Generic;

namespace SMADX.Models
{
    /// <summary>
    /// Document racine du fichier .smad-x.json.
    ///
    /// Historique du format :
    ///   - v1 : fichier = ADObject directement (pas de champ "Version")
    ///   - v2 : fichier = { "Version": 2, "Domain": {...}, "SitesTopology": {...}, "TierConfigurations": [...] }
    ///
    /// À la lecture, Services.ADDocumentMigrator fait passer un fichier ancien à la version
    /// courante et refuse un fichier écrit par une version plus récente de SMAD-X.
    /// </summary>
    public class ADRootDocument
    {
        /// <summary>
        /// Version du format écrite par cette version de SMAD-X. Une modification incompatible
        /// du format l'incrémente et ajoute l'étape correspondante dans ADDocumentMigrator.
        /// </summary>
        public const int CurrentVersion = 2;

        /// <summary>Version du format. Toujours <see cref="CurrentVersion"/> à l'écriture.</summary>
        public int Version { get; set; } = CurrentVersion;

        /// <summary>Arbre du domaine AD (ex-racine du fichier v1).</summary>
        public ADObject Domain { get; set; } = null!;

        /// <summary>
        /// Topologie de sites. Peut être null si le domaine n'a pas encore
        /// de sites configurés ou si le fichier a été migré depuis v1.
        /// </summary>
        public ADSitesTopology? SitesTopology { get; set; }

        /// <summary>
        /// Configurations de tiers personnalisées. Null ou vide = utiliser les tiers par défaut.
        /// Persisté dans le fichier .smad-x.json pour conserver les tiers ajoutés par l'utilisateur.
        /// </summary>
        public List<TierConfiguration>? TierConfigurations { get; set; }
    }
}
