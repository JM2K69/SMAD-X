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
    // ── Checkable item shown in the element list ─────────────────────────────

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

        // Full flat list of all documented elements
        private List<DocumentedElement> _allElements = new();
        // Currently visible subset (after type filter)
        private List<DocumentedElement> _filtered    = new();

        public ExportReportDialog(ADRootDocument document)
        {
            _document = document;
            InitializeComponent();
            BuildElementList();
            PopulateTypeFilter();
            RefreshList(null);
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

        // ── Type filter combo ────────────────────────────────────────────────

        private void PopulateTypeFilter()
        {
            var types = new List<string> { _loc["Report.Scope.TypeAll"] };
            types.AddRange(_allElements.Select(e => e.TypeLabel).Distinct().OrderBy(t => t));
            TypeFilterCombo.ItemsSource    = types;
            TypeFilterCombo.SelectedIndex  = 0;
        }

        private void OnTypeFilterChanged(object? sender, SelectionChangedEventArgs e)
        {
            var selected = TypeFilterCombo?.SelectedItem as string;
            RefreshList(selected == _loc["Report.Scope.TypeAll"] ? null : selected);
            UpdateCount();
        }

        private void RefreshList(string? typeFilter)
        {
            _filtered = typeFilter is null
                ? _allElements.ToList()
                : _allElements.Where(x => x.TypeLabel == typeFilter).ToList();

            ElementList.ItemsSource = null;
            ElementList.ItemsSource = _filtered;
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
            RefreshCheckboxes();
        }

        private void OnSelectNone(object? sender, RoutedEventArgs e)
        {
            foreach (var item in _filtered) item.IsChecked = false;
            RefreshCheckboxes();
        }

        private void RefreshCheckboxes()
        {
            // Force ItemsControl to re-render the bindings
            ElementList.ItemsSource = null;
            ElementList.ItemsSource = _filtered;
            UpdateCount();
        }

        // ── Scope radio toggle ───────────────────────────────────────────────

        private void OnScopeChanged(object? sender, RoutedEventArgs e)
        {
            bool isIndividual = RadioIndividual?.IsChecked == true;
            if (IndividualPanel  is not null) IndividualPanel.IsVisible  = isIndividual;
            if (FormatPanel      is not null) FormatPanel.IsVisible      = !isIndividual;
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

            // Single item → save-as picker
            if (toExport.Count == 1)
            {
                var element = toExport[0];
                var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title             = _loc["Report.Dialog.Title"],
                    SuggestedFileName = SanitizeFileName(element.DisplayName) + ".md",
                    DefaultExtension  = "md",
                    FileTypeChoices   = new List<FilePickerFileType>
                    {
                        new(_loc["FileType.Markdown"]) { Patterns = new[] { "*.md" } }
                    }
                });
                if (file is null) return;
                bool ok = await ExportElement(element, file.Path.LocalPath);
                Close(ok ? file.Path.LocalPath : null);
                return;
            }

            // Multiple items → folder picker
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title         = _loc["Report.Export.FolderTitle"],
                AllowMultiple = false,
            });
            if (folders.Count == 0) return;

            var dir   = folders[0].Path.LocalPath;
            int saved = 0;
            foreach (var element in toExport)
            {
                var path = Path.Combine(dir, SanitizeFileName(element.DisplayName) + ".md");
                if (await ExportElement(element, path)) saved++;
            }
            Close(string.Format(_loc["Report.Export.MultiDone"], saved) + " " + dir);
        }

        private Task<bool> ExportElement(DocumentedElement element, string path) =>
            element.Source switch
            {
                ADObject   obj  => _reportSvc.ExportSingleObjectMarkdownAsync(obj, path),
                ADSite     site => _reportSvc.ExportSingleSiteMarkdownAsync(site, path),
                ADSiteLink link => _reportSvc.ExportSingleSiteLinkMarkdownAsync(link, path),
                _               => Task.FromResult(false)
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
