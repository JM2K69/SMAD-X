using System.Collections.ObjectModel;
using SMADX.Models;

namespace SMADX.ViewModels
{
    /// <summary>
    /// Lightweight tree-node for the Sites panel in the main window.
    /// Shows only Subnets and Domain Controllers — GPO Links are managed
    /// via the Relations window.
    /// </summary>
    public class SiteTreeNode
    {
        public string DisplayName { get; }
        public string Icon        { get; }

        public ObservableCollection<SiteTreeNode> Children { get; } = new();

        // ── Site root node ───────────────────────────────────────────────────
        public SiteTreeNode(ADSite site)
        {
            DisplayName = site.Name;
            Icon        = "🏢";

            // Subnets group
            if (site.Subnets.Count > 0)
            {
                var subnetGroup = new SiteTreeNode("🌐 Subnets");
                foreach (var s in site.Subnets)
                    subnetGroup.Children.Add(new SiteTreeNode($"🔵 {s.Cidr}", "🔵"));
                Children.Add(subnetGroup);
            }

            // Domain Controllers group
            if (site.DomainControllers.Count > 0)
            {
                var dcGroup = new SiteTreeNode("🖥️ Domain Controllers");
                foreach (var dc in site.DomainControllers)
                    dcGroup.Children.Add(new SiteTreeNode($"💻 {dc}", "💻"));
                Children.Add(dcGroup);
            }
        }

        // ── Group header node ────────────────────────────────────────────────
        public SiteTreeNode(string label)
        {
            DisplayName = label;
            Icon        = string.Empty;
        }

        // ── Leaf node ────────────────────────────────────────────────────────
        private SiteTreeNode(string label, string icon)
        {
            DisplayName = label;
            Icon        = icon;
        }
    }
}
