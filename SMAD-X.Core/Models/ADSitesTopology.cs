using System.Collections.ObjectModel;

namespace SMADX.Models
{
    /// <summary>
    /// Conteneur racine de toute la topologie de sites d'un domaine.
    /// Sérialisé dans le fichier .smad-x.json (v2) sous la clé "SitesTopology".
    /// Null ou absent = domaine sans information de sites (fichiers v1).
    /// </summary>
    public class ADSitesTopology
    {
        public ObservableCollection<ADSite> Sites { get; set; } = new();
        public ObservableCollection<ADSiteLink> SiteLinks { get; set; } = new();
    }
}
