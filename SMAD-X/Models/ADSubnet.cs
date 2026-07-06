namespace SMADX.Models
{
    /// <summary>
    /// Sous-réseau IP associé à un site Active Directory.
    /// Ex : "10.0.1.0/24" → site "Paris-HQ"
    /// </summary>
    public class ADSubnet
    {
        /// <summary>Notation CIDR, ex : "192.168.1.0/24"</summary>
        public string Cidr { get; set; } = string.Empty;

        /// <summary>Nom du site auquel ce sous-réseau est rattaché.</summary>
        public string SiteName { get; set; } = string.Empty;

        /// <summary>Description libre.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Localisation physique (bâtiment, salle, pays…).</summary>
        public string Location { get; set; } = string.Empty;

        public override string ToString() => $"{Cidr}  →  {SiteName}";
    }
}
