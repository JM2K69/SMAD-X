using System.Threading.Tasks;
using SMADX.Models;
using SMADX.ViewModels;

namespace SMADX.Services
{
    /// <summary>Filtre d'un sélecteur de fichiers, ex. ("Fichier SMAD-X", "*.smad-x.json").</summary>
    public sealed record FileFilter(string Name, params string[] Patterns);

    /// <summary>Résultat de la boîte « Nouveau domaine ».</summary>
    public sealed record NewDomainRequest(string DomainName, bool EnableTiering);

    /// <summary>
    /// Fenêtres et sélecteurs de fichiers demandés par les ViewModels.
    /// Les ViewModels ne connaissent ainsi aucune vue Avalonia, et les tests
    /// peuvent fournir une implémentation qui répond sans afficher de fenêtre.
    /// </summary>
    public interface IDialogService
    {
        /// <summary>Chemin choisi, ou null si l'utilisateur annule.</summary>
        Task<string?> PickSaveFileAsync(string title, string suggestedFileName, string defaultExtension, params FileFilter[] filters);

        /// <summary>Chemin choisi, ou null si l'utilisateur annule.</summary>
        Task<string?> PickOpenFileAsync(string title, params FileFilter[] filters);

        /// <summary>Nom et options du domaine à créer, ou null si l'utilisateur annule.</summary>
        Task<NewDomainRequest?> ShowNewDomainAsync();

        Task ShowRelationsAsync(RelationsViewModel viewModel);

        /// <summary>Fenêtre non modale : l'utilisateur peut garder le graphe ouvert en travaillant.</summary>
        void ShowGraph(GraphViewModel viewModel);

        /// <summary>Fenêtre non modale : l'apprenant corrige dans la fenêtre principale puis relance l'audit.</summary>
        void ShowAudit(AuditViewModel viewModel);

        /// <summary>Fenêtre non modale : l'apprenant travaille dans la fenêtre principale et vérifie quand il veut.</summary>
        void ShowLab(LabViewModel viewModel);

        /// <summary>Fenêtre non modale « avant / après », actualisable pendant le travail.</summary>
        void ShowComparison(ComparisonViewModel viewModel);

        /// <summary>Fenêtre non modale des chemins d'attaque vers le contrôle du domaine.</summary>
        void ShowAttackPaths(AttackPathsViewModel viewModel);

        Task ShowTierConfigurationAsync();

        Task ShowTimelineAsync();

        Task ShowDelegationsAsync(ADObject? root);

        Task ShowSitesAsync(ADSitesTopology? topology);

        /// <summary>Dossier ou fichier exporté, ou null si l'utilisateur annule.</summary>
        Task<string?> ShowExportReportAsync(ADRootDocument document);

        Task ShowAboutAsync();

        /// <summary>
        /// Lance SMAD-X Studio, livré à côté de SMAD-X, sur un domaine enregistré. Faux s'il est introuvable ;
        /// <paramref name="studioPath"/> donne alors l'emplacement attendu.
        /// </summary>
        bool OpenInStudio(string domainFile, out string studioPath);

        Task ShowShortcutsAsync();
    }
}
