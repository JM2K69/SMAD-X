using SMADX.Models;

namespace SMADX.Services
{
    public enum DocumentLoadStatus
    {
        Loaded,
        NotFound,
        /// <summary>Fichier illisible sur le disque (droits, verrou…).</summary>
        Unreadable,
        /// <summary>Pas du JSON, ou pas un document SMAD-X.</summary>
        InvalidFormat,
        /// <summary>Écrit par une version plus récente de SMAD-X.</summary>
        TooNew
    }

    /// <summary>Résultat de <see cref="ADDataService.LoadDocumentAsync"/>.</summary>
    /// <param name="FileVersion">Version du format lue dans le fichier (0 si elle n'a pas pu être lue).</param>
    /// <param name="Error">Détail technique de l'échec, pour la barre d'état.</param>
    public sealed record DocumentLoadResult(
        DocumentLoadStatus Status,
        ADRootDocument? Document = null,
        int FileVersion = 0,
        string? Error = null)
    {
        /// <summary>Le fichier était dans un format plus ancien et a été converti à la lecture.</summary>
        public bool WasMigrated => Status == DocumentLoadStatus.Loaded && FileVersion < ADRootDocument.CurrentVersion;

        public static DocumentLoadResult Failed(DocumentLoadStatus status, int fileVersion = 0, string? error = null) =>
            new(status, null, fileVersion, error);
    }
}
