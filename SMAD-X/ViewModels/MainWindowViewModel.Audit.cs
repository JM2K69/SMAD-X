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
        /// <summary>Faux pour un outil que le formateur a fermé pendant l'examen en cours.</summary>
        private bool IsToolOpen(LabTools tool) => !(IsLabMode && CurrentLab?.Lab.IsClosed(tool) == true);

        public bool CanShowAudit => IsToolOpen(LabTools.Audit);
        public bool CanShowAttackPaths => IsToolOpen(LabTools.AttackPaths);
        public bool CanShowComparison => IsToolOpen(LabTools.Comparison);

        /// <summary>Après l'ouverture d'un exercice ou un changement de mode : menus, boutons et raccourcis.</summary>
        private void RefreshLabTools()
        {
            OnPropertyChanged(nameof(CanShowAudit));
            OnPropertyChanged(nameof(CanShowAttackPaths));
            OnPropertyChanged(nameof(CanShowComparison));
            ShowAuditCommand.NotifyCanExecuteChanged();
            ShowAttackPathsCommand.NotifyCanExecuteChanged();
            ShowComparisonCommand.NotifyCanExecuteChanged();
            MarkBaselineCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand(CanExecute = nameof(CanShowAudit))]
        private void ShowAudit()
        {
            if (RootObject == null) return;
            _dialogs.ShowAudit(new AuditViewModel(() => RootObject, SelectObject, ApplyAuditBadges,
                hideFixGuidance: !IsToolOpen(LabTools.FixGuidance)));
        }

        [RelayCommand(CanExecute = nameof(CanShowAttackPaths))]
        private void ShowAttackPaths()
        {
            if (RootObject == null) return;
            _dialogs.ShowAttackPaths(new AttackPathsViewModel(() => RootObject, SelectObject, _dialogs));
        }

        /// <summary>Badge sur chaque nœud : gravité du constat le plus grave et liste des règles en échec.</summary>
        public void ApplyAuditBadges(AuditReport report)
        {
            // Par identifiant : après Annuler, l'arbre porte de nouveaux objets, avec les mêmes identifiants
            var byObject = report.Findings.GroupBy(f => f.Object.Id).ToDictionary(g => g.Key, g => g.ToList());
            foreach (var node in RootNodes.SelectMany(AllNodes))
            {
                if (byObject.TryGetValue(node.Data.Id, out var findings))
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

        /// <summary>
        /// Sélectionne l'objet dans l'arbre en dépliant ses parents et en levant un éventuel filtre de recherche.
        /// L'objet est retrouvé par son identifiant : un constat calculé avant Annuler désigne encore le bon nœud.
        /// </summary>
        public void SelectObject(ADObject target)
        {
            var nodes = RootNodes.SelectMany(AllNodes).ToList();
            var node = nodes.FirstOrDefault(n => n.Data == target) ?? nodes.FirstOrDefault(n => n.Data.Id == target.Id);
            if (node == null)
            {
                StatusMessage = string.Format(Loc["Status.ObjectGone"], target.Name);
                return;
            }

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
