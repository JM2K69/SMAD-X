using System.Collections.Generic;
using System.Linq;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using SMADX.Models;
using SMADX.Services;
using SMADX.Services.Audit;

namespace SMADX.ViewModels
{
    // Audit de sécurité : fenêtre, badges dans l'arbre, navigation vers l'objet d'un constat
    public partial class MainWindowViewModel
    {
        [RelayCommand]
        private void ShowAudit()
        {
            if (RootObject == null) return;
            _dialogs.ShowAudit(new AuditViewModel(() => RootObject, SelectObject, ApplyAuditBadges));
        }

        /// <summary>Badge sur chaque nœud : gravité du constat le plus grave et liste des règles en échec.</summary>
        public void ApplyAuditBadges(AuditReport report)
        {
            var byObject = report.Findings.GroupBy(f => f.Object).ToDictionary(g => g.Key, g => g.ToList());
            foreach (var node in RootNodes.SelectMany(AllNodes))
            {
                if (byObject.TryGetValue(node.Data, out var findings))
                {
                    var worst = findings.Min(f => f.Severity);
                    node.AuditSeverity = worst;
                    node.AuditBadgeColor = AuditViewModel.SeverityColor(worst);
                    node.AuditTooltip = string.Format(Loc["Audit.Badge.Tooltip"], findings.Count, AuditViewModel.SeverityLabel(worst))
                        + "\n" + string.Join("\n", findings.Select(f => $"• {f.Rule.Id} {f.Rule.Title}"));
                }
                else
                {
                    node.AuditSeverity = null;
                }
            }
        }

        /// <summary>Sélectionne l'objet dans l'arbre en dépliant ses parents et en levant un éventuel filtre de recherche.</summary>
        public void SelectObject(ADObject target)
        {
            var node = RootNodes.SelectMany(AllNodes).FirstOrDefault(n => n.Data == target);
            if (node == null) return;

            if (!node.IsVisible)
                SearchText = string.Empty;
            for (var parent = node.Parent; parent != null; parent = parent.Parent)
                parent.IsExpanded = true;
            SelectedNode = node;

            // Les parents viennent d'être dépliés : le TreeViewItem n'existe pas encore et le TreeView
            // remettrait la sélection à null. On la réapplique une fois la mise en page faite.
            Dispatcher.UIThread.Post(() =>
            {
                node.IsSelected = true;
                SelectedNode = node;
            }, DispatcherPriority.Background);
        }

        private static IEnumerable<ADTreeNode> AllNodes(ADTreeNode node) =>
            new[] { node }.Concat(node.Children.SelectMany(AllNodes));
    }
}
