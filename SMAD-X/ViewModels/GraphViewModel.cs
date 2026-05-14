using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SMADX.Models;

namespace SMADX.ViewModels
{
    public partial class GraphViewModel : ViewModelBase
    {
        public ADObject Root { get; }

        // Filtres
        private bool _showMemberOf = true;
        public bool ShowMemberOf
        {
            get => _showMemberOf;
            set { SetProperty(ref _showMemberOf, value); RequestRefresh(); }
        }

        private bool _showGpoLinks = true;
        public bool ShowGpoLinks
        {
            get => _showGpoLinks;
            set { SetProperty(ref _showGpoLinks, value); RequestRefresh(); }
        }

        private bool _showGpoInheritance = true;
        public bool ShowGpoInheritance
        {
            get => _showGpoInheritance;
            set { SetProperty(ref _showGpoInheritance, value); RequestRefresh(); }
        }

        private bool _showPsoLinks = true;
        public bool ShowPsoLinks
        {
            get => _showPsoLinks;
            set { SetProperty(ref _showPsoLinks, value); RequestRefresh(); }
        }

        private bool _showHierarchy = false;
        public bool ShowHierarchy
        {
            get => _showHierarchy;
            set { SetProperty(ref _showHierarchy, value); RequestRefresh(); }
        }

        private bool _showIsolated = false;
        public bool ShowIsolated
        {
            get => _showIsolated;
            set { SetProperty(ref _showIsolated, value); RequestRefresh(); }
        }

        // Infos nœud sélectionné
        private string _selectedNodeInfo = string.Empty;
        public string SelectedNodeInfo
        {
            get => _selectedNodeInfo;
            set => SetProperty(ref _selectedNodeInfo, value);
        }

        // Stats
        private string _statsText = string.Empty;
        public string StatsText
        {
            get => _statsText;
            set => SetProperty(ref _statsText, value);
        }

        // Trigger pour recharger le graphe (observé par la View)
        private int _refreshToken;
        public int RefreshToken
        {
            get => _refreshToken;
            private set => SetProperty(ref _refreshToken, value);
        }

        public GraphViewModel(ADObject root)
        {
            Root = root;
        }

        public Graph.GraphFilter BuildFilter() => new Graph.GraphFilter
        {
            ShowMemberOf       = ShowMemberOf,
            ShowGpoLinks       = ShowGpoLinks,
            ShowGpoInheritance = ShowGpoInheritance,
            ShowPsoLinks       = ShowPsoLinks,
            ShowHierarchy      = ShowHierarchy,
            ShowIsolated       = ShowIsolated,
        };

        public void SetSelectedNode(Graph.GraphNode? node)
        {
            if (node == null)
            {
                SelectedNodeInfo = string.Empty;
                return;
            }
            SelectedNodeInfo =
                $"{GetIcon(node.NodeType)} {node.Label}  |  Type : {node.NodeType}" +
                (string.IsNullOrEmpty(node.Tier) ? "" : $"  |  Tier : {node.Tier}");
        }

        public void UpdateStats(int nodeCount, int edgeCount)
        {
            StatsText = $"Nœuds : {nodeCount}   Arêtes : {edgeCount}";
        }

        private void RequestRefresh() => RefreshToken++;

        private static string GetIcon(ADObjectType t) => t switch
        {
            ADObjectType.Domain                  => "🌐",
            ADObjectType.OrganizationalUnit      => "📁",
            ADObjectType.User                    => "👤",
            ADObjectType.Group                   => "👥",
            ADObjectType.Computer                => "💻",
            ADObjectType.Policy                  => "📋",
            ADObjectType.PasswordSettingsObject  => "🔑",
            ADObjectType.GMSA                    => "🔐",
            ADObjectType.Container               => "📦",
            _                                    => "◉"
        };
    }
}
