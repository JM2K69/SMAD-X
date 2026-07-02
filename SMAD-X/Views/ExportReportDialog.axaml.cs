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
    /// <summary>
    /// Item shown in the individual-element ComboBox.
    /// Wraps ADObject, ADSite, or ADSiteLink.
    /// </summary>
    public class DocumentedElement
    {
        public string DisplayName { get; }
        public object Source      { get; }

        public DocumentedElement(ADObject obj)
        {
            Source      = obj;
            DisplayName = $"{TypeIcon(obj.Type)} {obj.Name}  [{obj.Type}]";
        }

        public DocumentedElement(ADSite site)
        {
            Source      = site;
            DisplayName = $"🏢 {site.Name}  [Site]";
        }

        public DocumentedElement(ADSiteLink link)
        {
            Source      = link;
            DisplayName = $"🔗 {link.Name}  [SiteLink]";
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

        public ExportReportDialog(ADRootDocument document)
        {
            _document = document;
            InitializeComponent();
            PopulateElements();
        }

        // ── Populate the individual-element combo ────────────────────────────

        private void PopulateElements()
        {
            var items = new List<DocumentedElement>();

            if (_document.Domain is not null)
            {
                foreach (var obj in FlattenTree(_document.Domain))
                    if (!string.IsNullOrWhiteSpace(obj.Description))
                        items.Add(new DocumentedElement(obj));
            }

            if (_document.SitesTopology is not null)
            {
                foreach (var site in _document.SitesTopology.Sites)
                    if (!string.IsNullOrWhiteSpace(site.Description))
                        items.Add(new DocumentedElement(site));

                foreach (var link in _document.SitesTopology.SiteLinks)
                    if (!string.IsNullOrWhiteSpace(link.Description))
                        items.Add(new DocumentedElement(link));
            }

            ElementComboBox.ItemsSource = items;
            if (items.Count > 0)
                ElementComboBox.SelectedIndex = 0;
        }

        // ── Scope radio toggle ───────────────────────────────────────────────

        private void OnScopeChanged(object? sender, RoutedEventArgs e)
        {
            if (IndividualPanel is not null && RadioIndividual is not null)
                IndividualPanel.IsVisible = RadioIndividual.IsChecked == true;
        }

        // ── Export ───────────────────────────────────────────────────────────

        private async void OnExportClick(object? sender, RoutedEventArgs e)
        {
            bool isCombined = RadioCombined?.IsChecked == true;
            bool isDocx     = RadioDocx?.IsChecked     == true;
            bool isPdf      = RadioPdf?.IsChecked      == true;
            string ext      = isDocx ? "docx" : isPdf ? "pdf" : "md";

            if (isCombined)
                await ExportCombined(ext);
            else
            {
                var selected = ElementComboBox?.SelectedItem as DocumentedElement;
                if (selected is not null)
                    await ExportIndividual(selected);
            }
        }

        private async Task ExportCombined(string ext)
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title             = _loc["Report.Dialog.Title"],
                SuggestedFileName = $"rapport-ad.{ext}",
                DefaultExtension  = ext,
                FileTypeChoices   = BuildFileTypeChoices(ext),
            });
            if (file is null) return;

            var path = file.Path.LocalPath;
            bool ok  = ext switch
            {
                "docx" => await _reportSvc.ExportDocxAsync(_document, path),
                "pdf"  => await _reportSvc.ExportPdfAsync(_document, path),
                _      => await _reportSvc.ExportMarkdownAsync(_document, path),
            };

            Close(ok ? path : null);
        }

        private async Task ExportIndividual(DocumentedElement element)
        {
            var suggestedName = SanitizeFileName(element.DisplayName) + ".md";

            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title             = _loc["Report.Dialog.Title"],
                SuggestedFileName = suggestedName,
                DefaultExtension  = "md",
                FileTypeChoices   = new List<FilePickerFileType>
                {
                    new(_loc["FileType.Markdown"]) { Patterns = new[] { "*.md" } }
                }
            });
            if (file is null) return;

            var path = file.Path.LocalPath;
            bool ok = element.Source switch
            {
                ADObject   obj  => await _reportSvc.ExportSingleObjectMarkdownAsync(obj, path),
                ADSite     site => await _reportSvc.ExportSingleSiteMarkdownAsync(site, path),
                ADSiteLink link => await _reportSvc.ExportSingleSiteLinkMarkdownAsync(link, path),
                _               => false
            };

            Close(ok ? path : null);
        }

        private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(null);

        // ── Helpers ──────────────────────────────────────────────────────────

        private List<FilePickerFileType> BuildFileTypeChoices(string preferredExt)
        {
            var list = new List<FilePickerFileType>();
            var mdType   = new FilePickerFileType(_loc["FileType.Markdown"]) { Patterns = new[] { "*.md" } };
            var docxType = new FilePickerFileType(_loc["FileType.Word"])     { Patterns = new[] { "*.docx" } };
            var pdfType  = new FilePickerFileType(_loc["FileType.Pdf"])      { Patterns = new[] { "*.pdf" } };

            if (preferredExt == "docx")      list.AddRange(new[] { docxType, mdType, pdfType });
            else if (preferredExt == "pdf")  list.AddRange(new[] { pdfType,  mdType, docxType });
            else                             list.AddRange(new[] { mdType,   docxType, pdfType });
            return list;
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
