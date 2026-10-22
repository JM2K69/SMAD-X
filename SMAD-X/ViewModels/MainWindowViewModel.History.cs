using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using CommunityToolkit.Mvvm.Input;
using SMADX.Models;
using SMADX.Services;

namespace SMADX.ViewModels
{
    // Annuler / rétablir : instantanés JSON du document, enregistrés après chaque action
    public partial class MainWindowViewModel
    {
        private readonly UndoHistory _history = new();

        /// <summary>Vrai pendant une restauration : les changements de sélection ne créent pas d'état.</summary>
        private bool _restoring;

        private string SerializeDocument() => JsonSerializer.Serialize(new ADRootDocument
        {
            Version = ADRootDocument.CurrentVersion,
            Domain = RootObject!,
            SitesTopology = SitesTopology
        }, ADDataService.SerializerOptions);

        /// <summary>Nouveau document affiché : l'historique repart de son état initial.</summary>
        private void ResetHistory()
        {
            if (RootObject == null) return;
            _history.Reset(SerializeDocument());
            RefreshHistoryCommands();
        }

        /// <summary>
        /// Enregistre l'état actuel s'il a changé. Appelé après chaque commande qui modifie le domaine,
        /// à la fermeture des fenêtres d'édition et à chaque changement de sélection (ce qui regroupe
        /// les modifications faites dans le panneau de détails sur un même objet).
        /// </summary>
        public void RecordHistory()
        {
            if (_restoring || RootObject == null) return;
            if (_history.Record(SerializeDocument()))
                RefreshHistoryCommands();
        }

        private bool CanUndo() => _history.CanUndo || (RootObject != null && _history.Current != SerializeDocument());
        private bool CanRedo() => _history.CanRedo;

        [RelayCommand(CanExecute = nameof(CanUndo))]
        private void Undo()
        {
            RecordHistory(); // une modification en cours dans le panneau devient annulable elle aussi
            if (_history.Undo() is { } state)
            {
                Restore(state);
                StatusMessage = Loc["Status.Undone"];
            }
        }

        [RelayCommand(CanExecute = nameof(CanRedo))]
        private void Redo()
        {
            if (_history.Redo() is { } state)
            {
                Restore(state);
                StatusMessage = Loc["Status.Redone"];
            }
        }

        private void RefreshHistoryCommands()
        {
            UndoCommand.NotifyCanExecuteChanged();
            RedoCommand.NotifyCanExecuteChanged();
        }

        /// <summary>Remplace le domaine par un état de l'historique, en gardant la sélection et les nœuds dépliés.</summary>
        private void Restore(string state)
        {
            var document = JsonSerializer.Deserialize<ADRootDocument>(state, ADDataService.SerializerOptions)!;
            ADDataService.RestoreParentReferences(document.Domain, null);

            // Repérage par identifiant : le DN change quand on annule un renommage ou un déplacement
            var selectedId = SelectedNode?.Data.Id;
            var expanded = RootNodes.SelectMany(AllNodes).Where(n => n.IsExpanded)
                .Select(n => n.Data.Id).ToHashSet();

            _restoring = true;
            try
            {
                if (_currentDocument != null)
                {
                    _currentDocument.Domain = document.Domain;
                    _currentDocument.SitesTopology = document.SitesTopology;
                }
                SitesTopology = document.SitesTopology;

                var rootNode = new ADTreeNode(document.Domain);
                foreach (var node in AllNodes(rootNode))
                    node.IsExpanded = expanded.Contains(node.Data.Id);
                SelectedNode = null;
                RootNodes.Clear();
                RootNodes.Add(rootNode);
                UpdateObjectCounts();
                SelectedNode = selectedId == null ? null : AllNodes(rootNode).FirstOrDefault(n => n.Data.Id == selectedId);
            }
            finally
            {
                _restoring = false;
            }
            RefreshHistoryCommands();
        }
    }
}
