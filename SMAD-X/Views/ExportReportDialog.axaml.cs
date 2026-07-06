using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SMADX.Models;
using SMADX.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SMADX.Views
{
    // ── Type filter item (one per distinct type label) ───────────────────────

    public class TypeFilterItem
    {
        public string Label     { get; }
        public bool   IsChecked { get; set; } = true;
        public TypeFilterItem(string label) => Label = label;
    }

    public class DocumentedElement
    {
        public string  DisplayName { get; }
        public bool    IsChecked   { get; set; } = true;
        public string  TypeLabel   { get; }
        public object  Source      { get; }

        public DocumentedElement(ADObject obj)
        {
            Source      = obj;
            TypeLabel   = obj.Type.ToString();
            DisplayName = $"{TypeIcon(obj.Type)} {obj.Name}";
        }

        public DocumentedElement(ADSite site)
        {
            Source      = site;
            TypeLabel   = "Site";
            DisplayName = $"🏢 {site.Name}";
        }

        public DocumentedElement(ADSiteLink link)
        {
            Source      = link;
            TypeLabel   = "SiteLink";
            DisplayName = $"🔗 {link.Name}";
        }

        private static string TypeIcon(ADObjectType t) => t switch
        {
            ADObjectType.Domain                 => "🌐",
            ADObjectType.OrganizationalUnit     => "📁",
            ADObjectType.Container              => "📦",
            ADObjectType.User                   => "👤",
            ADObjectType.Group                  => "👥",
            ADObjectType.Computer               => "🖥",
            ADObjectType.GMSA                   => "🔧",
            ADObjectType.Policy                 => "📋",
            ADObjectType.PasswordSettingsObject => "🔑",
            _                                   => "•"
        };
    }

    public partial class ExportReportDialog : Window
    {
        private readonly ADRootDocument          _document;
        private readonly ADDocumentReportService _reportSvc = new();
        private readonly LocalizationService     _loc       = LocalizationService.Instance;

        private List<DocumentedElement> _allElements  = new();
        private List<DocumentedElement> _filtered     = new();
        private List<TypeFilterItem>    _typeFilters  = new();
        private bool                    _updatingTypeFilter = false;

        public ExportReportDialog(ADRootDocument document)
        {
            _document = document;
            InitializeComponent();
            BuildElementList();
            PopulateTypeFilter();
            ApplyTypeFilter();
            UpdateCount();
        }

        // ── Build master list ────────────────────────────────────────────────

        private void BuildElementList()
        {
            _allElements.Clear();

            if (_document.Domain is not null)
                foreach (var obj in FlattenTree(_document.Domain))
                    if (!string.IsNullOrWhiteSpace(obj.Description))
                        _allElements.Add(new DocumentedElement(obj));

            if (_document.SitesTopology is not null)
            {
                foreach (var site in _document.SitesTopology.Sites)
                    if (!string.IsNullOrWhiteSpace(site.Description))
                        _allElements.Add(new DocumentedElement(site));

                foreach (var link in _document.SitesTopology.SiteLinks)
                    if (!string.IsNullOrWhiteSpace(link.Description))
                        _allElements.Add(new DocumentedElement(link));
            }
        }

        // ── Type filter checkboxes ───────────────────────────────────────────

        private void PopulateTypeFilter()
        {
            // Always include all known AD types so the filter is predictable,
            // but mark only the ones that actually have documented items.
            var typesWithDocs = _allElements
                .Select(e => e.TypeLabel)
                .ToHashSet();

            // Build a stable ordered list that always includes Computer
            var allKnownTypes = new[]
            {
                "Computer", "Container", "Domain", "GMSA", "Group",
                "OrganizationalUnit", "PasswordSettingsObject", "Policy",
                "Site", "SiteLink", "User"
            };

            _typeFilters = allKnownTypes
                .Where(t => typesWithDocs.Contains(t))  // only show types that have items
                .Select(t => new TypeFilterItem(t) { IsChecked = true })
                .ToList();

            TypeFilterList.ItemsSource = _typeFilters;
        }

        private void OnTypeFilterCheckChanged(object? sender, RoutedEventArgs e)
        {
            if (_updatingTypeFilter) return;

            // Identify which filter was just toggled
            if (sender is not CheckBox cb || cb.DataContext is not TypeFilterItem toggled) return;

            _updatingTypeFilter = true;
            try
            {
                if (toggled.IsChecked)
                {
                    // Checking a type → show ONLY that type, uncheck all others
                    foreach (var f in _typeFilters)
                        f.IsChecked = f == toggled;
                }
                else
                {
                    // Unchecking a type → check all others (show everything except this)
                    foreach (var f in _typeFilters)
                        f.IsChecked = f != toggled;
                }

                // Refresh the type-filter checkboxes in the UI
                TypeFilterList.ItemsSource = null;
                TypeFilterList.ItemsSource = _typeFilters;
            }
            finally
            {
                _updatingTypeFilter = false;
            }

            ApplyTypeFilter();
        }

        private void ApplyTypeFilter()
        {
            var activeTypes = _typeFilters
                .Where(f => f.IsChecked)
                .Select(f => f.Label)
                .ToHashSet();

            _filtered = activeTypes.Count == 0
                ? _allElements.ToList()
                : _allElements.Where(x => activeTypes.Contains(x.TypeLabel)).ToList();

            ElementList.ItemsSource = null;
            ElementList.ItemsSource = _filtered;
            UpdateCount();
        }

        private void UpdateCount()
        {
            int count = _filtered.Count(e => e.IsChecked);
            if (SelectedCountLabel is not null)
                SelectedCountLabel.Text = string.Format(_loc["Report.Scope.SelectedCount"], count);
        }

        // ── Select all / none ────────────────────────────────────────────────

        private void OnSelectAll(object? sender, RoutedEventArgs e)
        {
            foreach (var item in _filtered) item.IsChecked = true;
            ApplyTypeFilter();
        }

        private void OnSelectNone(object? sender, RoutedEventArgs e)
        {
            foreach (var item in _filtered) item.IsChecked = false;
            ApplyTypeFilter();
        }

        private void RefreshCheckboxes()
        {
            ElementList.ItemsSource = null;
            ElementList.ItemsSource = _filtered;
            UpdateCount();
        }

        // ── Scope radio toggle ───────────────────────────────────────────────

        private void OnScopeChanged(object? sender, RoutedEventArgs e)
        {
            bool isIndividual = RadioIndividual?.IsChecked == true;
            if (IndividualPanel is not null) IndividualPanel.IsVisible = isIndividual;
        }

        // ── Export ───────────────────────────────────────────────────────────

        private async void OnExportClick(object? sender, RoutedEventArgs e)
        {
            if (RadioCombined?.IsChecked == true)
            {
                // Collect all checked formats – MD is always included
                var formats = new List<string> { "md" };
                if (ChkDocx?.IsChecked == true) formats.Add("docx");
                if (ChkPdf?.IsChecked  == true) formats.Add("pdf");
                await ExportCombinedMulti(formats);
            }
            else
            {
                await ExportIndividualChecked();
            }
        }

        private async Task ExportCombinedMulti(List<string> formats)
        {
            // Ask for a base path (without extension) using the first format as default
            var firstExt = formats[0];
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title             = _loc["Report.Dialog.Title"],
                SuggestedFileName = $"rapport-ad.{firstExt}",
                DefaultExtension  = firstExt,
                FileTypeChoices   = BuildFileTypeChoices(firstExt),
            });
            if (file is null) return;

            var basePath = file.Path.LocalPath;
            var stem     = Path.Combine(
                               Path.GetDirectoryName(basePath)!,
                               Path.GetFileNameWithoutExtension(basePath));

            int ok = 0;
            foreach (var ext in formats)
            {
                var path   = stem + "." + ext;
                bool saved = ext switch
                {
                    "docx" => await _reportSvc.ExportDocxAsync(_document, path),
                    "pdf"  => await _reportSvc.ExportPdfAsync(_document, path),
                    _      => await _reportSvc.ExportMarkdownAsync(_document, path),
                };
                if (saved) ok++;
            }
            Close(ok > 0 ? stem : null);
        }

        private async Task ExportIndividualChecked()
        {
            var toExport = _allElements.Where(e => e.IsChecked).ToList();
            if (toExport.Count == 0) { Close(null); return; }

            bool wantDocx = ChkDocx?.IsChecked == true;
            bool wantPdf  = ChkPdf?.IsChecked  == true;

            var domainName = _document.Domain?.Name ?? "domain";
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title         = _loc["Report.Export.FolderTitle"],
                AllowMultiple = false,
            });
            if (folders.Count == 0) return;

            // Root = selected folder / domain name
            var rootDir = Path.Combine(folders[0].Path.LocalPath, SanitizeFileName(domainName));

            int saved = 0;
            foreach (var element in toExport)
            {
                // Always create a subfolder per type under the domain folder
                var targetDir = Path.Combine(rootDir, SanitizeFileName(element.TypeLabel));
                Directory.CreateDirectory(targetDir);
                var stem = Path.Combine(targetDir, SanitizeFileName(element.DisplayName));

                if (await ExportElementMd(element, stem + ".md")) saved++;

                if (wantDocx)
                    await _reportSvc.ExportSingleElementDocxAsync(
                        element.DisplayName, element.TypeLabel,
                        GetDescription(element), stem + ".docx");

                if (wantPdf)
                    await _reportSvc.ExportSingleElementPdfAsync(
                        element.DisplayName, element.TypeLabel,
                        GetDescription(element), stem + ".pdf");
            }
            Close(string.Format(_loc["Report.Export.MultiDone"], saved) + " " + rootDir);
        }

        private Task<bool> ExportElementMd(DocumentedElement element, string path) =>
            element.Source switch
            {
                ADObject   obj  => _reportSvc.ExportSingleObjectMarkdownAsync(obj, path),
                ADSite     site => _reportSvc.ExportSingleSiteMarkdownAsync(site, path),
                ADSiteLink link => _reportSvc.ExportSingleSiteLinkMarkdownAsync(link, path),
                _               => Task.FromResult(false)
            };

        private static string? GetDescription(DocumentedElement element) =>
            element.Source switch
            {
                ADObject   obj  => obj.Description,
                ADSite     site => site.Description,
                ADSiteLink link => link.Description,
                _               => null
            };

        private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(null);

        // ── Helpers ──────────────────────────────────────────────────────────

        private List<FilePickerFileType> BuildFileTypeChoices(string preferredExt)
        {
            var mdType   = new FilePickerFileType(_loc["FileType.Markdown"]) { Patterns = new[] { "*.md" } };
            var docxType = new FilePickerFileType(_loc["FileType.Word"])     { Patterns = new[] { "*.docx" } };
            var pdfType  = new FilePickerFileType(_loc["FileType.Pdf"])      { Patterns = new[] { "*.pdf" } };

            return preferredExt switch
            {
                "docx" => new List<FilePickerFileType> { docxType, mdType, pdfType },
                "pdf"  => new List<FilePickerFileType> { pdfType,  mdType, docxType },
                _      => new List<FilePickerFileType> { mdType,   docxType, pdfType },
            };
        }

        private static string SanitizeFileName(string name)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Replace(' ', '-').ToLowerInvariant();
        }

        private static IEnumerable<ADObject> FlattenTree(ADObject node)
        {
            yield return node;
            foreach (var child in node.Children)
                foreach (var n in FlattenTree(child))
                    yield return n;
        }
    }
}
