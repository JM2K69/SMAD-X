using System.Collections.Generic;

namespace SMADX.Models
{
    /// <summary>
    /// Lien de réplication entre deux sites AD ou plus (site link).
    /// Stocké sous CN=IP,CN=Inter-Site Transports,CN=Sites,CN=Configuration,...
    /// </summary>
    public class ADSiteLink
    {
        /// <summary>Nom du lien, ex : "DEFAULTIPSITELINK"</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Transport : "IP" (RPC/IP) ou "SMTP".</summary>
        public string Transport { get; set; } = "IP";

        /// <summary>Noms des sites inclus dans ce lien (≥ 2).</summary>
        public List<string> SiteNames { get; set; } = new();

        /// <summary>Coût de réplication (100 par défaut). Plus c'est bas, plus le lien est préféré.</summary>
        public int Cost { get; set; } = 100;

        /// <summary>Intervalle de réplication en minutes (180 par défaut = 3 h).</summary>
        public int ReplicationIntervalMinutes { get; set; } = 180;

        /// <summary>Planification de réplication, ex : "Always" ou description horaire.</summary>
        public string ReplicationSchedule { get; set; } = "Always";

        /// <summary>True = choix automatique du serveur tête-de-pont.</summary>
        public bool BridgeheadAuto { get; set; } = true;

        /// <summary>Description libre.</summary>
        public string Description { get; set; } = string.Empty;

        public override string ToString() => $"{Name}  [{string.Join(" ↔ ", SiteNames)}]  cost={Cost}";
    }
}
