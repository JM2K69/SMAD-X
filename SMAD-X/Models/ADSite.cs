using System;
using System.Collections.ObjectModel;

namespace SMADX.Models
{
    /// <summary>
    /// Représente un site Active Directory (CN=Sites,CN=Configuration,...).
    /// Les sites vivent dans le contexte de nommage Configuration, séparément
    /// de la hiérarchie du domaine.
    /// </summary>
    public class ADSite
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>Nom du site, ex : "Default-First-Site-Name"</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Description libre du site.</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Localisation physique (attribut Location d'AD).</summary>
        public string Location { get; set; } = string.Empty;

        /// <summary>Sous-réseaux IP rattachés à ce site.</summary>
        public ObservableCollection<ADSubnet> Subnets { get; set; } = new();

        /// <summary>Noms DNS des contrôleurs de domaine présents dans ce site.</summary>
        public ObservableCollection<string> DomainControllers { get; set; } = new();

        /// <summary>Niveau de tier hérité du contexte (optionnel).</summary>
        public string? Tier { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime ModifiedDate { get; set; } = DateTime.Now;

        public override string ToString() => Name;
    }
}
