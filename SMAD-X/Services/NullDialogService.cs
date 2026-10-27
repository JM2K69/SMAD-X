using System.Threading.Tasks;
using SMADX.Models;
using SMADX.ViewModels;

namespace SMADX.Services
{
    /// <summary>
    /// Service de dialogues inerte : n'ouvre rien et répond « annulé ».
    /// Utilisé par le concepteur XAML (Design.DataContext), qui ne peut pas fournir de fenêtre.
    /// </summary>
    public sealed class NullDialogService : IDialogService
    {
        public Task<string?> PickSaveFileAsync(string title, string suggestedFileName, string defaultExtension, params FileFilter[] filters) => Task.FromResult<string?>(null);
        public Task<string?> PickOpenFileAsync(string title, params FileFilter[] filters) => Task.FromResult<string?>(null);
        public Task<NewDomainRequest?> ShowNewDomainAsync() => Task.FromResult<NewDomainRequest?>(null);
        public Task ShowRelationsAsync(RelationsViewModel viewModel) => Task.CompletedTask;
        public void ShowGraph(GraphViewModel viewModel) { }
        public void ShowAudit(AuditViewModel viewModel) { }
        public void ShowLab(LabViewModel viewModel) { }
        public void ShowComparison(ComparisonViewModel viewModel) { }
        public void ShowAttackPaths(AttackPathsViewModel viewModel) { }
        public Task ShowTierConfigurationAsync() => Task.CompletedTask;
        public Task ShowTimelineAsync() => Task.CompletedTask;
        public Task ShowDelegationsAsync(ADObject? root) => Task.CompletedTask;
        public Task ShowSitesAsync(ADSitesTopology? topology) => Task.CompletedTask;
        public Task<string?> ShowExportReportAsync(ADRootDocument document) => Task.FromResult<string?>(null);
        public Task ShowAboutAsync() => Task.CompletedTask;
        public bool OpenInStudio(string domainFile, out string studioPath) { studioPath = string.Empty; return false; }
        public Task ShowShortcutsAsync() => Task.CompletedTask;
        public Task<UnsavedChoice> AskUnsavedAsync(bool submission) => Task.FromResult(UnsavedChoice.Cancel);
        public Task<bool> ConfirmAsync(string title, string message, string yes, string no) => Task.FromResult(false);
    }
}
