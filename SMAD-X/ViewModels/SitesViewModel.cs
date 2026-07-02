using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SMADX.Models;

namespace SMADX.ViewModels
{
    public partial class SitesViewModel : ViewModelBase
    {
        // ── Source topology ─────────────────────────────────────────────────
        private ADSitesTopology? _topology;

        // ── Sites table ─────────────────────────────────────────────────────
        [ObservableProperty]
        private ObservableCollection<ADSite> _sites = new();

        [ObservableProperty]
        private ADSite? _selectedSite;

        // ── Site links table ────────────────────────────────────────────────
        [ObservableProperty]
        private ObservableCollection<ADSiteLink> _siteLinks = new();

        [ObservableProperty]
        private ADSiteLink? _selectedSiteLink;

        // ── Graph data ──────────────────────────────────────────────────────
        [ObservableProperty]
        private ObservableCollection<SiteNodeViewModel> _graphNodes = new();

        [ObservableProperty]
        private ObservableCollection<SiteLinkLineViewModel> _graphEdges = new();

        // ── Filter ──────────────────────────────────────────────────────────
        [ObservableProperty]
        private string _filterText = string.Empty;

        // ── Status ──────────────────────────────────────────────────────────
        [ObservableProperty]
        private string _statusText = string.Empty;

        [ObservableProperty]
        private bool _hasData;

        public SitesViewModel() { }

        public SitesViewModel(ADSitesTopology topology) => LoadTopology(topology);

        public void LoadTopology(ADSitesTopology topology)
        {
            _topology = topology;

            Sites.Clear();
            SiteLinks.Clear();
            GraphNodes.Clear();
            GraphEdges.Clear();

            foreach (var s in topology.Sites)
                Sites.Add(s);

            foreach (var l in topology.SiteLinks)
                SiteLinks.Add(l);

            HasData = Sites.Count > 0;
            StatusText = $"{Sites.Count} site(s) — {SiteLinks.Count} lien(s)";

            BuildGraph();
        }

        // ── Graph layout (simple circle) ────────────────────────────────────
        private void BuildGraph()
        {
            GraphNodes.Clear();
            GraphEdges.Clear();

            if (_topology is null || _topology.Sites.Count == 0) return;

            int count = _topology.Sites.Count;
            double cx = 450, cy = 320, r = Math.Min(280, count < 4 ? 150 : 280);

            var nodeMap = new System.Collections.Generic.Dictionary<string, SiteNodeViewModel>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < count; i++)
            {
                double angle = 2 * Math.PI * i / count - Math.PI / 2;
                var node = new SiteNodeViewModel
                {
                    Name       = _topology.Sites[i].Name,
                    X          = cx + r * Math.Cos(angle) - 55,
                    Y          = cy + r * Math.Sin(angle) - 22,
                    SubnetCount = _topology.Sites[i].Subnets.Count,
                    DcCount    = _topology.Sites[i].DomainControllers.Count,
                };
                GraphNodes.Add(node);
                nodeMap[node.Name] = node;
            }

            foreach (var link in _topology.SiteLinks)
            {
                if (link.SiteNames.Count < 2) continue;
                for (int i = 0; i < link.SiteNames.Count - 1; i++)
                {
                    for (int j = i + 1; j < link.SiteNames.Count; j++)
                    {
                        if (!nodeMap.TryGetValue(link.SiteNames[i], out var n1)) continue;
                        if (!nodeMap.TryGetValue(link.SiteNames[j], out var n2)) continue;
                        GraphEdges.Add(new SiteLinkLineViewModel
                        {
                            X1       = n1.X + 55,
                            Y1       = n1.Y + 22,
                            X2       = n2.X + 55,
                            Y2       = n2.Y + 22,
                            LinkName = link.Name,
                            Cost     = link.Cost,
                        });
                    }
                }
            }
        }

        partial void OnFilterTextChanged(string value) => ApplyFilter();

        [RelayCommand]
        private void ApplyFilter()
        {
            if (_topology is null) return;
            Sites.Clear();
            var q = string.IsNullOrWhiteSpace(FilterText) ? FilterText : FilterText.Trim();
            foreach (var s in _topology.Sites)
            {
                if (string.IsNullOrEmpty(q) ||
                    s.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                    s.Location.Contains(q, StringComparison.OrdinalIgnoreCase))
                    Sites.Add(s);
            }
        }
    }

    // ── Graph node / edge models (lightweight, no ObservableObject needed) ──
    public class SiteNodeViewModel
    {
        public string Name       { get; set; } = string.Empty;
        public double X          { get; set; }
        public double Y          { get; set; }
        public int    SubnetCount{ get; set; }
        public int    DcCount    { get; set; }

        public string Label => SubnetCount > 0 || DcCount > 0
            ? $"{Name}\n🌐{SubnetCount}  🖥{DcCount}"
            : Name;
    }

    public class SiteLinkLineViewModel
    {
        public double X1       { get; set; }
        public double Y1       { get; set; }
        public double X2       { get; set; }
        public double Y2       { get; set; }
        public string LinkName { get; set; } = string.Empty;
        public int    Cost     { get; set; }
    }
}
