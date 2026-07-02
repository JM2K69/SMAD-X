using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Media;
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

        [ObservableProperty]
        private ObservableCollection<SiteLinkLabelViewModel> _graphEdgeLabels = new();

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
            GraphEdgeLabels.Clear();

            foreach (var s in topology.Sites)
                Sites.Add(s);

            foreach (var l in topology.SiteLinks)
                SiteLinks.Add(l);

            HasData = Sites.Count > 0;
            StatusText = $"{Sites.Count} site(s) — {SiteLinks.Count} lien(s)";

            BuildGraph();
        }

        // ── Graph layout ────────────────────────────────────────────────────
        private void BuildGraph()
        {
            GraphNodes.Clear();
            GraphEdges.Clear();
            GraphEdgeLabels.Clear();

            if (_topology is null || _topology.Sites.Count == 0) return;

            int count = _topology.Sites.Count;

            // Canvas size matches AXAML: 1400 × 820
            double cx = 700, cy = 380;

            // Radius grows with node count; minimum keeps 2-node case readable
            double r = count switch
            {
                1 => 0,
                2 => 220,
                3 => 240,
                _ => Math.Min(300, 60 * count)
            };

            // Node size
            const double nodeW = 150, nodeH = 110;

            var nodeMap = new System.Collections.Generic.Dictionary<string, SiteNodeViewModel>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < count; i++)
            {
                double angle = 2 * Math.PI * i / count - Math.PI / 2;
                var site = _topology.Sites[i];
                var node = new SiteNodeViewModel
                {
                    Name              = site.Name,
                    X                 = cx + r * Math.Cos(angle) - nodeW / 2,
                    Y                 = cy + r * Math.Sin(angle) - nodeH / 2,
                    SubnetCount       = site.Subnets.Count,
                    DcCount           = site.DomainControllers.Count,
                    SubnetsText       = site.Subnets.Count > 0
                                            ? string.Join(", ", site.Subnets.Select(s => s.Cidr))
                                            : "—",
                    DomainControllersText = site.DomainControllers.Count > 0
                                            ? string.Join(", ", site.DomainControllers)
                                            : "—",
                    Location          = site.Location,
                    IsDefault         = site.Name.Equals("Default-First-Site-Name", StringComparison.OrdinalIgnoreCase),
                    LinkedGPOsText    = site.LinkedGPOs.Count > 0
                                            ? string.Join(", ", site.LinkedGPOs)
                                            : "—",
                };
                GraphNodes.Add(node);
                nodeMap[node.Name] = node;
            }

            // Second pass: resolve linked site names per node from site links
            foreach (var link in _topology.SiteLinks)
            {
                foreach (var siteName in link.SiteNames)
                {
                    if (!nodeMap.TryGetValue(siteName, out var selfNode)) continue;
                    var others = link.SiteNames
                        .Where(n => !n.Equals(siteName, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    if (others.Count == 0) continue;
                    var combined = string.IsNullOrEmpty(selfNode.LinkedSitesText) || selfNode.LinkedSitesText == "—"
                        ? string.Join(", ", others)
                        : selfNode.LinkedSitesText + ", " + string.Join(", ", others);
                    // deduplicate
                    selfNode.LinkedSitesText = string.Join(", ",
                        combined.Split(", ", StringSplitOptions.RemoveEmptyEntries)
                               .Distinct(StringComparer.OrdinalIgnoreCase));
                }
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

                        double x1 = n1.X + nodeW / 2;
                        double y1 = n1.Y + nodeH / 2;
                        double x2 = n2.X + nodeW / 2;
                        double y2 = n2.Y + nodeH / 2;

                        GraphEdges.Add(new SiteLinkLineViewModel
                        {
                            X1 = x1, Y1 = y1,
                            X2 = x2, Y2 = y2,
                            LinkName = link.Name,
                            Cost     = link.Cost,
                        });

                        // Label at midpoint
                        GraphEdgeLabels.Add(new SiteLinkLabelViewModel
                        {
                            X    = (x1 + x2) / 2 - 35,
                            Y    = (y1 + y2) / 2 - 10,
                            Text = $"{link.Name}  cost={link.Cost}  {link.ReplicationIntervalMinutes}min",
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

    // ── Graph node / edge models ────────────────────────────────────────────
    public class SiteNodeViewModel
    {
        public string Name                    { get; set; } = string.Empty;
        public double X                       { get; set; }
        public double Y                       { get; set; }
        public int    SubnetCount             { get; set; }
        public int    DcCount                 { get; set; }
        public string SubnetsText             { get; set; } = string.Empty;
        public string DomainControllersText   { get; set; } = string.Empty;
        public string Location                { get; set; } = string.Empty;
        public bool   IsDefault               { get; set; }
        public string LinkedSitesText         { get; set; } = "—";
        public string LinkedGPOsText          { get; set; } = "—";

        public IBrush NodeBorderBrush => IsDefault
            ? new SolidColorBrush(Color.Parse("#E3A21A"))
            : new SolidColorBrush(Color.Parse("#0078D4"));

        public string Label => $"{Name}\n🌐 {SubnetsText}\n🖥 {DomainControllersText}\n🔗 {LinkedSitesText}\n📄 GPO: {LinkedGPOsText}";
    }

    public class SiteLinkLineViewModel
    {
        public double X1       { get; set; }
        public double Y1       { get; set; }
        public double X2       { get; set; }
        public double Y2       { get; set; }
        public string LinkName { get; set; } = string.Empty;
        public int    Cost     { get; set; }

        public Point Start => new(X1, Y1);
        public Point End   => new(X2, Y2);
    }

    public class SiteLinkLabelViewModel
    {
        public double X    { get; set; }
        public double Y    { get; set; }
        public string Text { get; set; } = string.Empty;
    }
}
