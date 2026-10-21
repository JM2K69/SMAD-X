using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SMADX.Models;

namespace SMADX.ViewModels
{
    // Recherche dans l'arbre, déplier/replier et panneau des sites
    public partial class MainWindowViewModel
    {
        // ─── Recherche ────────────────────────────────────────────────────────────

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                    ApplySearch();
            }
        }

        [RelayCommand]
        private void ClearSearch() => SearchText = string.Empty;

        /// <summary>
        /// Applique le filtre de recherche : met à jour IsVisible sur les nœuds,
        /// et expand les parents des nœuds correspondants.
        /// </summary>
        private void ApplySearch()
        {
            var query = _searchText.Trim();
            foreach (var root in RootNodes)
                ApplySearchToNode(root, query);
        }

        private static bool ApplySearchToNode(ADTreeNode node, string query)
        {
            bool selfMatch = string.IsNullOrEmpty(query) ||
                             node.Data.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                             node.Data.Type.ToString().Contains(query, StringComparison.OrdinalIgnoreCase) ||
                             (!string.IsNullOrEmpty(node.Data.Description) &&
                              node.Data.Description.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                             (!string.IsNullOrEmpty(node.Data.Tier) &&
                              node.Data.Tier.Contains(query, StringComparison.OrdinalIgnoreCase));

            bool anyChildMatch = false;
            foreach (var child in node.Children)
                if (ApplySearchToNode(child, query))
                    anyChildMatch = true;

            node.IsVisible = selfMatch || anyChildMatch;
            if (anyChildMatch && !string.IsNullOrEmpty(query))
                node.IsExpanded = true;

            return node.IsVisible;
        }

        // ─── Déplier / replier ────────────────────────────────────────────────────

        [RelayCommand]
        private void ExpandAll()
        {
            foreach (var root in RootNodes)
                SetExpandedRecursive(root, true);
        }

        [RelayCommand]
        private void CollapseAll()
        {
            foreach (var root in RootNodes)
                SetExpandedRecursive(root, false);
        }

        private static void SetExpandedRecursive(ADTreeNode node, bool expanded)
        {
            node.IsExpanded = expanded;
            foreach (var child in node.Children)
                SetExpandedRecursive(child, expanded);
        }

        // ─── Panneau des sites ────────────────────────────────────────────────────

        /// <summary>Flat list of site tree nodes shown in the Sites panel of the main window.</summary>
        public ObservableCollection<SiteTreeNode> SiteRootNodes { get; } = new();

        [ObservableProperty]
        private bool _isSitesExpanded = true;

        [RelayCommand]
        private void ToggleSitesExpanded() => IsSitesExpanded = !IsSitesExpanded;

        partial void OnSitesTopologyChanged(ADSitesTopology? value) => RebuildSiteTree();

        private void RebuildSiteTree()
        {
            SiteRootNodes.Clear();
            if (SitesTopology == null) return;
            foreach (var site in SitesTopology.Sites)
                SiteRootNodes.Add(new SiteTreeNode(site));
            UpdateObjectCounts();
        }
    }
}
