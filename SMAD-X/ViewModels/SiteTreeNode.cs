using System.Collections.ObjectModel;
using System.ComponentModel;
using SMADX.Models;
using SMADX.Services;

namespace SMADX.ViewModels
{
    /// <summary>
    /// Lightweight tree-node for the Sites panel in the main window.
    /// Shows only Subnets and Domain Controllers — GPO Links are managed
    /// via the Relations window.
    /// </summary>
    public class SiteTreeNode : INotifyPropertyChanged
    {
        private readonly string _label;

        /// <summary>Clé de traduction des en-têtes de groupe (« Sous-réseaux »…) ; null pour un nom venu du domaine.</summary>
        private readonly string? _labelKey;

        public string DisplayName => _labelKey != null ? LocalizationService.Instance[_labelKey] : _label;
        public string Icon        { get; }

        // Required by Avalonia TreeViewItem style bindings
        public bool IsExpanded { get; set; } = false;
        public bool IsSelected { get; set; } = false;
        public bool IsVisible  { get; set; } = true;

        public ObservableCollection<SiteTreeNode> Children { get; } = new();

        public event PropertyChangedEventHandler? PropertyChanged;

        // ── Site root node ───────────────────────────────────────────────────
        public SiteTreeNode(ADSite site)
        {
            _label = site.Name;
            Icon   = "🏢";

            // Subnets group
            if (site.Subnets.Count > 0)
            {
                var subnetGroup = Header("Sites.Tree.Subnets");
                foreach (var s in site.Subnets)
                    subnetGroup.Children.Add(new SiteTreeNode($"🔵 {s.Cidr}", "🔵"));
                Children.Add(subnetGroup);
            }

            // Domain Controllers group
            if (site.DomainControllers.Count > 0)
            {
                var dcGroup = Header("Sites.Tree.DomainControllers");
                foreach (var dc in site.DomainControllers)
                    dcGroup.Children.Add(new SiteTreeNode($"💻 {dc}", "💻"));
                Children.Add(dcGroup);
            }
        }

        // ── Group header node ────────────────────────────────────────────────
        public SiteTreeNode(string label)
        {
            _label = label;
            Icon   = string.Empty;
        }

        private SiteTreeNode(string? labelKey, string label, string icon)
        {
            _labelKey = labelKey;
            _label    = label;
            Icon      = icon;
        }

        /// <summary>En-tête de groupe traduit, relu à chaque changement de langue.</summary>
        private static SiteTreeNode Header(string labelKey) => new(labelKey, string.Empty, string.Empty);

        // ── Leaf node ────────────────────────────────────────────────────────
        private SiteTreeNode(string label, string icon) : this(null, label, icon)
        {
        }

        /// <summary>Après un changement de langue : les en-têtes de groupe relisent leur texte.</summary>
        public void Relocalize()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayName)));
            foreach (var child in Children)
                child.Relocalize();
        }
    }
}
