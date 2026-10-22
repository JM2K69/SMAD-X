using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using SMADX.Models;
using SMADX.Services;

namespace SMADX.ViewModels
{
    // Nouveau domaine, ouverture, sauvegarde et exports
    public partial class MainWindowViewModel
    {
        private static LocalizationService Loc => LocalizationService.Instance;

        private static FileFilter SmadxFilter     => new(Loc["FileType.SMADXJSON"], "*.smad-x.json");
        private static FileFilter JsonFilter      => new(Loc["FileType.JSON"], "*.json");
        private static FileFilter PowerShellFilter => new(Loc["FileType.PowerShell"], "*.ps1");
        private static FileFilter MarkdownFilter  => new(Loc["FileType.Markdown"], "*.md");
        private static FileFilter AllFilesFilter  => new(Loc["FileType.AllFiles"], "*");

        [RelayCommand]
        private void InitializeSampleData()
        {
            TierConfigurationService.Instance.ResetToDefaults();
            ShowDomain(new ADRootDocument
            {
                Version = 2,
                Domain = _dataService.CreateSampleStructure(),
                SitesTopology = _dataService.CreateSampleTopology()
            }, expandDefaultNodes: true);
            StatusMessage = Loc["Status.SampleLoaded"];
        }

        [RelayCommand]
        private async Task CreateNewDomain()
        {
            var request = await _dialogs.ShowNewDomainAsync();
            if (request == null) return;

            // Un nouveau domaine ne doit rien hériter du document précédent (sites, tiers personnalisés)
            TierConfigurationService.Instance.ResetToDefaults();
            ShowDomain(new ADRootDocument
            {
                Version = 2,
                Domain = _dataService.CreateFreshDomainStructure(request.DomainName, request.EnableTiering),
                SitesTopology = _dataService.CreateFreshTopology(request.DomainName)
            }, expandDefaultNodes: true);

            var tieringStatus = Loc[request.EnableTiering ? "Status.WithTiering" : "Status.WithoutTiering"];
            StatusMessage = string.Format(Loc["Status.DomainCreated"], request.DomainName, tieringStatus);
        }

        [RelayCommand]
        private Task SaveToFile() => RunFileCommandAsync(async () =>
        {
            if (RootObject == null) return;
            var path = await _dialogs.PickSaveFileAsync(Loc["Dialog.Save.Title"], GetSmadxFileName(), "smad-x.json",
                SmadxFilter, AllFilesFilter);
            if (path == null) return;

            // Build a v2 document preserving SitesTopology if loaded
            var doc = _currentDocument ?? new ADRootDocument();
            doc.Version = 2;
            doc.Domain = RootObject;
            doc.SitesTopology = SitesTopology;
            var success = await _dataService.SaveDocumentAsync(doc, path);
            StatusMessage = success ? string.Format(Loc["Status.Saved"], path) : Loc["Status.ErrorSave"];
        });

        [RelayCommand]
        private Task LoadFromFile() => RunFileCommandAsync(async () =>
        {
            var path = await _dialogs.PickOpenFileAsync(Loc["Dialog.Load.Title"], SmadxFilter, JsonFilter, AllFilesFilter);
            if (path == null) return;

            var result = await _dataService.LoadDocumentAsync(path);
            if (result.Document == null)
            {
                StatusMessage = result.Status switch
                {
                    DocumentLoadStatus.TooNew        => string.Format(Loc["Status.ErrorLoadTooNew"], result.FileVersion, ADRootDocument.CurrentVersion),
                    DocumentLoadStatus.InvalidFormat => string.Format(Loc["Status.ErrorLoadInvalid"], result.Error),
                    _                                => result.Error is null ? Loc["Status.ErrorLoad"] : $"{Loc["Status.ErrorLoad"]} : {result.Error}"
                };
                return;
            }

            _dataService.ApplyTierConfiguration(result.Document);
            ShowDomain(result.Document, expandDefaultNodes: false);
            var siteInfo = SitesTopology != null ? $" (+{SitesTopology.Sites.Count} sites)" : string.Empty;
            var migration = result.WasMigrated
                ? string.Format(Loc["Status.Loaded.Migrated"], result.FileVersion, ADRootDocument.CurrentVersion)
                : string.Empty;
            StatusMessage = string.Format(Loc["Status.Loaded"], path) + siteInfo + migration;
        });

        [RelayCommand]
        private Task ExportToJson() => RunFileCommandAsync(async () =>
        {
            if (RootObject == null) return;
            var path = await _dialogs.PickSaveFileAsync(Loc["Dialog.ExportJson.Title"], GetSmadxFileName(), "smad-x.json",
                SmadxFilter, AllFilesFilter);
            if (path == null) return;

            var success = await _dataService.SaveToFileAsync(RootObject, path);
            StatusMessage = success ? string.Format(Loc["Status.ExportedJson"], path) : Loc["Status.ErrorExportJson"];
        });

        [RelayCommand]
        private Task ExportToPowerShell() => RunFileCommandAsync(async () =>
        {
            if (RootObject == null) return;
            var path = await _dialogs.PickSaveFileAsync(Loc["Dialog.ExportPowerShell.Title"],
                $"Create-ADStructure_{DateTime.Now:yyyyMMdd_HHmmss}.ps1", "ps1", PowerShellFilter, AllFilesFilter);
            if (path == null) return;

            var success = await _powerShellExportService.ExportToPowerShellAsync(RootObject, path);
            StatusMessage = success ? string.Format(Loc["Status.ExportedPowerShell"], path) : Loc["Status.ErrorExportPowerShell"];
        });

        [RelayCommand]
        private Task GenerateImportScript() => RunFileCommandAsync(async () =>
        {
            var path = await _dialogs.PickSaveFileAsync(Loc["Dialog.GenerateImportScript.Title"],
                "Export-ADToSMAD-X.ps1", "ps1", PowerShellFilter, AllFilesFilter);
            if (path == null) return;

            var success = await _importPowerShellService.GenerateImportScriptAsync(path);
            StatusMessage = success
                ? string.Format(Loc["Status.GeneratedImportScript"], path)
                : Loc["Status.ErrorGenerateImportScript"];
        });

        [RelayCommand]
        private async Task ExportReport()
        {
            if (RootObject == null) return;

            var result = await _dialogs.ShowExportReportAsync(new ADRootDocument
            {
                Version = 2,
                Domain = RootObject,
                SitesTopology = SitesTopology
            });
            if (result != null)
                StatusMessage = $"{Loc["Report.Success"]} {result}";
        }

        /// <summary>Remplace la description de l'objet sélectionné par le contenu d'un fichier Markdown.</summary>
        [RelayCommand]
        private Task ImportDescription() => RunFileCommandAsync(async () =>
        {
            if (SelectedNode == null)
            {
                StatusMessage = Loc["Report.Import.NoSelection"];
                return;
            }

            var path = await _dialogs.PickOpenFileAsync(Loc["Report.Import.Title"], MarkdownFilter);
            if (path == null) return;

            var content = await _reportService.ImportMarkdownAsync(path);
            if (content != null)
            {
                SelectedObjectDescription = content;
                RecordHistory();
                StatusMessage = Loc["Report.Import.Success"];
            }
            else
            {
                StatusMessage = Loc["Report.Import.Error"];
            }
        });

        /// <summary>Affiche dans la barre d'état toute erreur inattendue d'une commande de fichier.</summary>
        private async Task RunFileCommandAsync(Func<Task> command)
        {
            try
            {
                await command();
            }
            catch (Exception ex)
            {
                StatusMessage = string.Format(Loc["Status.Error"], ex.Message);
            }
        }

        /// <summary>
        /// Génère un nom de fichier basé sur le nom de domaine courant et la date d'export.
        /// Format : &lt;domaine&gt;_&lt;yyyyMMdd_HHmmss&gt;.smad-x.json
        /// </summary>
        private string GetSmadxFileName()
        {
            var domainName = RootObject?.Name ?? "smad-x";
            // Remplacer les caractères invalides pour un nom de fichier
            foreach (var c in System.IO.Path.GetInvalidFileNameChars())
                domainName = domainName.Replace(c, '_');
            return $"{domainName}_{DateTime.Now:yyyyMMdd_HHmmss}.smad-x.json";
        }
    }
}
