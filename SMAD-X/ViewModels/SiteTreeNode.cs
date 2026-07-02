using System.Collections.ObjectModel;
using SMADX.Models;

namespace SMADX.ViewModels
{
    /// <summary>
    /// Lightweight tree-node for the Sites panel in the main window.
    /// Hierarchy: SiteTreeNode (site) → SiteTreeNode (subnet/DC/GPO rows)
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

            // Linked GPOs group
            if (site.LinkedGPOs.Count > 0)
            {
                var gpoGroup = new SiteTreeNode("📋 GPO Links");
                foreach (var gpo in site.LinkedGPOs)
                    gpoGroup.Children.Add(new SiteTreeNode($"📋 {gpo}", "📋"));
                Children.Add(gpoGroup);
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
