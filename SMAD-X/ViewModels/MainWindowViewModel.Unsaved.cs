using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using SMADX.Models;
using SMADX.Services;
using SMADX.Services.Labs;

namespace SMADX.ViewModels
{
    // Travail non enregistré : confirmation à la fermeture et, en mode exercice, copie de secours
    public partial class MainWindowViewModel
    {
        /// <summary>Dossier des copies de secours ; les tests le remplacent par un dossier temporaire.</summary>
        public static string DefaultRecoveryFolder { get; set; } =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SMAD-X", "Recovery");

        public string RecoveryFolder { get; set; } = DefaultRecoveryFolder;

        /// <summary>État du document au dernier enregistrement (fichier ou copie), ou à son ouverture.</summary>
        private string? _savedState;

        /// <summary>État écrit dans la dernière copie de secours.</summary>
        private string? _recoveredState;

        public bool HasUnsavedChanges => RootObject != null && _savedState != null && SerializeDocument() != _savedState;

        private void MarkSaved() => _savedState = RootObject != null ? SerializeDocument() : null;

        private string RecoveryPath(LabViewModel lab)
        {
            var id = string.Concat(lab.Lab.Id.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
            return Path.Combine(RecoveryFolder, $"{id}_{lab.SessionId:N}{LabSubmissionService.FileExtension}");
        }

        /// <summary>
        /// Copie de secours, appelée chaque minute par la fenêtre et avant sa fermeture : en mode exercice, le
        /// travail non enregistré est écrit, scellé, dans le dossier des copies de secours. Si SMAD-X se ferme
        /// mal, la prochaine ouverture de l'exercice propose de le reprendre.
        /// </summary>
        public void AutoSave(DateTime? now = null)
        {
            if (!IsLabMode || CurrentLab == null || CurrentDocument() is not { } work || !HasUnsavedChanges) return;
            var state = SerializeDocument();
            if (state == _recoveredState) return;
            try
            {
                Directory.CreateDirectory(RecoveryFolder);
                File.WriteAllText(RecoveryPath(CurrentLab), new LabSubmissionService().Serialize(CurrentLab.Snapshot(work, now ?? DateTime.Now)));
                _recoveredState = state;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                ErrorReporter.Log("Copie de secours", ex);
            }
        }

        private void DeleteRecovery(LabViewModel lab)
        {
            try
            {
                File.Delete(RecoveryPath(lab));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                ErrorReporter.Log("Copie de secours", ex);
            }
            _recoveredState = null;
        }

        /// <summary>
        /// Copie de secours à proposer pour cet exercice : la plus récente du même exercice (même domaine de
        /// départ), ou, pour une copie reprise, celle de sa session enregistrée après elle.
        /// </summary>
        private (LabSubmission Submission, string Path)? FindRecovery(LabDefinition lab, LabSubmission? resumed)
        {
            if (!Directory.Exists(RecoveryFolder)) return null;
            var fingerprint = LabSubmissionService.StartFingerprint(lab);
            var id = string.Concat(lab.Id.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
            (LabSubmission Submission, string Path)? best = null;
            foreach (var path in Directory.GetFiles(RecoveryFolder, $"{id}_*{LabSubmissionService.FileExtension}"))
            {
                LabSubmission candidate;
                try { candidate = new LabSubmissionService().LoadFile(path); }
                catch (Exception ex) when (ex is LabFormatException or IOException or UnauthorizedAccessException) { continue; }

                var matches = resumed != null
                    ? candidate.SessionId == resumed.SessionId && candidate.SavedAt > resumed.SavedAt
                    : LabSubmissionService.StartFingerprint(candidate.Lab) == fingerprint;
                if (matches && (best == null || candidate.SavedAt > best.Value.Submission.SavedAt))
                    best = (candidate, path);
            }
            return best;
        }

        /// <summary>
        /// Avant de fermer la fenêtre : vrai si elle peut se fermer. Des modifications non enregistrées font
        /// demander d'enregistrer le domaine, ou la copie en mode exercice.
        /// </summary>
        public async Task<bool> ConfirmCloseAsync()
        {
            if (!HasUnsavedChanges) return true;

            var submission = IsLabMode && CurrentLab != null;
            switch (await _dialogs.AskUnsavedAsync(submission))
            {
                case UnsavedChoice.Discard:
                    return true; // la copie de secours reste : la prochaine ouverture proposera de la reprendre
                case UnsavedChoice.Save when submission:
                    if (!CurrentLab!.HasStudentName)
                    {
                        _dialogs.ShowLab(CurrentLab);
                        StatusMessage = Loc["Lab.Submission.NeedName"];
                        return false;
                    }
                    await CurrentLab.SaveSubmissionCommand.ExecuteAsync(null);
                    return !HasUnsavedChanges;
                case UnsavedChoice.Save:
                    await SaveToFileCommand.ExecuteAsync(null);
                    return !HasUnsavedChanges; // sélecteur de fichier annulé : la fenêtre reste ouverte
                default:
                    return false;
            }
        }
    }
}
