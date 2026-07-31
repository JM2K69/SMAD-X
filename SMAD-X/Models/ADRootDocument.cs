using System.Collections.Generic;

namespace SMADX.Models
{
    /// <summary>
    /// Document racine du fichier .smad-x.json version 2.
    ///
    /// Migration rétro-compatible :
    ///   - v1 : fichier = ADObject directement (pas de champ "Version")
    ///   - v2 : fichier = { "Version": 2, "Domain": {...}, "SitesTopology": {...} }
    ///
    /// ADDataService détecte automatiquement le format à la lecture :
    ///   si le JSON contient la clé "Version" → v2, sinon → v1 (Domain seul).
    /// </summary>
    public class ADRootDocument
    {
        /// <summary>Version du format. Toujours 2 pour les nouveaux fichiers.</summary>
        public int Version { get; set; } = 2;

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
