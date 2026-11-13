using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SMADX.Services.Audit;
using SMADX.Services.Sources;

namespace SMADX.Studio.ViewModels
{
    /// <summary>Ce que la fenêtre des sources demande au système : enregistrer un PDF, choisir un dossier, ouvrir une page.</summary>
    public interface ISourceDialogs
    {
        Task<string?> SaveFileAsync(string title, string suggestedName, string filterName, string pattern);
        Task<string?> PickFolderAsync(string title);
        Task OpenUrlAsync(string url);
        Task ShowFolderAsync(string folder);
    }

    /// <summary>Une source citée par une règle : son référentiel et son identifiant (« ANSSI · vuln_spn_priv »), la clé de sa fiche.</summary>
    public sealed class SourceRefItem(AuditReference reference, string key)
    {
        public AuditReference Reference { get; } = reference;
        public string Key { get; } = key;

        public string Label => $"{Reference.FrameworkLabel} · {ShortId}";

        /// <summary>L'identifiant tel qu'on le cite : AC-6(5), 5.4, T1558.003 ; le titre d'une page Microsoft.</summary>
        public string ShortId => Reference.Framework switch
        {
            AuditFramework.Nist => Reference.Id.Replace("SP 800-53 Rev. 5 ", ""),
            AuditFramework.Cis => Reference.Id.Replace("CIS Controls v8 ", ""),
            AuditFramework.Microsoft => Reference.Title,
            _ => Reference.Id
        };

        public string Tip => Reference.Title;
    }

    /// <summary>Une règle de la liste : son identifiant, son titre dans la langue de l'interface, son point ANSSI et ses sources.</summary>
    public sealed partial class SourceRuleItem(AuditRule rule, string title, IReadOnlyList<SourceRefItem> references) : ObservableObject
    {
        public AuditRule Rule { get; } = rule;
        public string RuleId => Rule.Id;
        public string Title { get; } = title;
        public IReadOnlyList<SourceRefItem> References { get; } = references;
        public string Anssi => References.FirstOrDefault(r => r.Reference.Framework == AuditFramework.Anssi)?.ShortId ?? "—";
        public string Level => Rule.AnssiLevel is { } l ? $"N{l}" : "";
        public string SeverityColor => Rule.Severity switch
        {
            AuditSeverity.Critical => "#E74C3C",
            AuditSeverity.High => "#E67E22",
            AuditSeverity.Medium => "#F1C40F",
            _ => "#3498DB"
        };

        [ObservableProperty] private bool _isChecked;

        public bool Matches(string filter)
        {
            if (string.IsNullOrWhiteSpace(filter)) return true;
            var f = filter.Trim();
            return new[] { RuleId, Title }.Concat(References.SelectMany(r => new[] { r.Reference.Id, r.Reference.Title, r.ShortId }))
                .Any(t => t.Contains(f, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// Fenêtre « Fiches des sources » : les règles d'audit, comme dans les recettes ; pour la règle choisie, ses sources
    /// (ANSSI, NIST, CIS, MITRE ATT&amp;CK, Microsoft, modèles de cloisonnement) et la fiche de chacune, en français ou en
    /// anglais. On exporte une fiche, ou toutes celles des règles cochées, en PDF ; on ouvre la source officielle ; on la
    /// relit sur le web. Les fiches viennent du relevé livré avec SMAD-X ; un référentiel relu dans Studio est gardé dans
    /// le dossier des réglages et remplace le sien, tant que SMAD-X n'en livre pas un plus récent.
    /// </summary>
    public sealed partial class SourcesViewModel : ObservableObject
    {
        private readonly ISourceDialogs _dialogs;
        private readonly string _localPath;
        private readonly Func<DateTime> _clock;
        private readonly Func<IReadOnlyCollection<string>, string, Task<(SourcesSnapshot? Snapshot, List<string> Errors)>> _fetch;
        private readonly IReadOnlyDictionary<string, (string Fr, string En)> _ruleTitles = SourceCitations.RuleTitles();
        private readonly List<SourceRuleItem> _all;
        private SourcesSnapshot? _local;

        /// <summary>Le relevé gardé par Studio : à côté de ses réglages.</summary>
        public static string DefaultLocalPath => Path.Combine(Path.GetDirectoryName(StudioSettings.DefaultPath)!, "sources.json");

        /// <param name="ruleId">La règle à montrer d'abord (celle d'une recette), sinon la première.</param>
        /// <param name="fetch">Le relevé sur le web (les tests le remplacent) : référentiels à relire, date du jour.</param>
        public SourcesViewModel(ISourceDialogs dialogs, string? ruleId = null, string? localPath = null, Func<DateTime>? clock = null,
            Func<IReadOnlyCollection<string>, string, Task<(SourcesSnapshot?, List<string>)>>? fetch = null)
        {
            _dialogs = dialogs;
            _localPath = localPath ?? DefaultLocalPath;
            _clock = clock ?? (() => DateTime.Now);
            _fetch = fetch ?? ((only, today) => Task.Run(() => SourceUpdate.RunAsync(today, only, tool: "SMAD-X Studio")));
            _local = LoadLocal(_localPath);
            Snapshot = SourcesSnapshot.Embedded.WithNewer(_local);
            var culture = StudioText.Instance.Culture;
            _all = AuditRules.All
                .Select(rule => new SourceRuleItem(rule, SourceCitations.RuleTitle(rule.Id, culture), References(rule)))
                .Where(r => r.References.Count > 0)
                .OrderBy(r => r.Rule.Severity).ThenBy(r => r.RuleId, StringComparer.Ordinal)
                .ToList();
            foreach (var r in _all) Rules.Add(r);
            _language = Languages.First(l => l.Value == (StudioText.Instance.French ? "fr" : "en"));
            Selected = _all.FirstOrDefault(r => r.RuleId == ruleId) ?? _all.FirstOrDefault();
        }

        public ObservableCollection<SourceRuleItem> Rules { get; } = new();

        public IReadOnlyList<Choice<string>> Languages { get; } = [new("fr", "Français"), new("en", "English")];

        /// <summary>Le relevé affiché : celui livré avec SMAD-X, où chaque référentiel relu depuis dans Studio remplace le sien.</summary>
        public SourcesSnapshot Snapshot { get; private set; }

        [ObservableProperty] private SourceRuleItem? _selected;
        [ObservableProperty] private SourceRefItem? _selectedReference;
        [ObservableProperty] private Choice<string> _language;
        [ObservableProperty] private string _filter = "";
        /// <summary>La fiche affichée, en Markdown.</summary>
        [ObservableProperty] private string _sheet = "";
        /// <summary>D'où vient la fiche : le relevé livré avec SMAD-X, ou celui fait dans Studio.</summary>
        [ObservableProperty] private string _origin = "";
        /// <summary>Le résultat de la dernière action : export, relecture d'une source.</summary>
        [ObservableProperty] private string _message = "";
        [ObservableProperty] [NotifyCanExecuteChangedFor(nameof(CheckSourceCommand))] private bool _isBusy;
        [ObservableProperty] private bool _hasStudioRetrieval;

        private static string T(string fr, string en) => StudioText.Instance.T(fr, en);

        /// <summary>La version de Studio, inscrite dans les PDF.</summary>
        public static string Version =>
            (typeof(SourcesViewModel).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "?").Split('+')[0];

        private SourceItem? Item => SelectedReference is { } r ? Snapshot.Items.FirstOrDefault(i => i.Key == r.Key) : null;

        private SourceFramework? FrameworkOf(SourceItem item) => Snapshot.Frameworks.FirstOrDefault(f => f.Id == item.Framework);

        /// <summary>Les sources d'une règle, dans l'ordre des référentiels ; les points de la checklist ANSSI avant son guide.</summary>
        private IReadOnlyList<SourceRefItem> References(AuditRule rule)
        {
            var order = SourceSheets.Order.ToList();
            return rule.References
                .Select(r => (Ref: r, Item: SourceCitations.Find(Snapshot, r)))
                .Where(p => p.Item != null)
                .OrderBy(p => order.IndexOf(p.Item!.Framework)).ThenBy(p => p.Ref.Id.StartsWith("ANSSI-", StringComparison.Ordinal))
                .Select(p => new SourceRefItem(p.Ref, p.Item!.Key))
                .ToList();
        }

        partial void OnSelectedChanged(SourceRuleItem? value) => SelectedReference = value?.References.FirstOrDefault();

        partial void OnSelectedReferenceChanged(SourceRefItem? value) => Render();

        partial void OnLanguageChanged(Choice<string> value) => Render();

        partial void OnFilterChanged(string value)
        {
            Rules.Clear();
            foreach (var r in _all.Where(r => r.Matches(value))) Rules.Add(r);
            if (Selected == null || !Rules.Contains(Selected)) Selected = Rules.FirstOrDefault();
        }

        /// <summary>La fiche de la source choisie, dans la langue choisie, et sa provenance.</summary>
        private void Render()
        {
            HasStudioRetrieval = _local != null;
            if (Item is not { } item || FrameworkOf(item) is not { } framework)
            {
                Sheet = "";
                Origin = "";
                return;
            }
            Sheet = SourceSheets.Sheet(Language.Value, framework, item, _ruleTitles, links: false);
            var embedded = SourcesSnapshot.Embedded.Frameworks.First(f => f.Id == framework.Id);
            Origin = ReferenceEquals(framework, embedded)
                ? string.Format(T("Relevé du {0}, livré avec SMAD-X {1}.", "Retrieved on {0}, shipped with SMAD-X {1}."), framework.Retrieved, Version)
                : string.Format(T("Relu par Studio le {0}.", "Read again by Studio on {0}."), framework.Retrieved);
        }

        /// <summary>Montre une règle (celle d'une recette), quitte à vider le filtre qui la cachait.</summary>
        public void Select(string ruleId)
        {
            if (_all.FirstOrDefault(r => r.RuleId == ruleId) is not { } rule) return;
            if (!Rules.Contains(rule)) Filter = "";
            Selected = rule;
        }

        /// <summary>
        /// Le nom d'un PDF exporté, tel que Studio présente la source : la règle, puis le référentiel et l'identifiant
        /// (« ACL-03 - ANSSI vuln_permissions_naming_context »). Une règle cite plusieurs sources : la règle seule ne
        /// suffirait pas. Les caractères interdits dans un nom de fichier (« : » d'un titre Microsoft) deviennent des tirets.
        /// </summary>
        public static string ExportName(SourceRuleItem rule, SourceRefItem source)
        {
            var name = $"{rule.RuleId} - {source.Reference.FrameworkLabel} {source.ShortId}";
            var invalid = Path.GetInvalidFileNameChars().Concat("<>:\"/\\|?*").ToHashSet();
            return string.Concat(name.Select(c => invalid.Contains(c) ? '-' : c)).Trim().TrimEnd('.');
        }

        public void ClearChecks()
        {
            foreach (var r in _all) r.IsChecked = false;
        }

        /// <summary>La fiche affichée, en PDF.</summary>
        [RelayCommand]
        private async Task ExportPdfAsync()
        {
            if (Selected is not { } rule || SelectedReference is not { } source || Item is not { } item || FrameworkOf(item) is not { } framework) return;
            var file = await _dialogs.SaveFileAsync(T("Exporter la fiche en PDF", "Export the sheet as PDF"), ExportName(rule, source) + ".pdf", "PDF", "*.pdf");
            if (file == null) return;
            try
            {
                SourcePdf.Save(file, Version, framework, item, Language.Value, _ruleTitles);
                Message = string.Format(T("Fiche exportée : {0}", "Sheet exported: {0}"), file);
            }
            catch (IOException ex)
            {
                Message = string.Format(T("Le PDF n'a pas pu être écrit (est-il ouvert ailleurs ?) : {0}", "The PDF could not be written (is it open elsewhere?): {0}"), ex.Message);
            }
        }

        /// <summary>
        /// Les fiches des règles cochées (sans case cochée, de la règle choisie), en PDF, dans un dossier : un fichier par
        /// source de chaque règle, nommé comme Studio la présente (voir <see cref="ExportName"/>).
        /// </summary>
        [RelayCommand]
        private async Task ExportCheckedAsync()
        {
            var rules = _all.Where(r => r.IsChecked).ToList();
            if (rules.Count == 0 && Selected != null) rules.Add(Selected);
            if (rules.Count == 0) return;
            var folder = await _dialogs.PickFolderAsync(T("Dossier où exporter les fiches", "Folder to export the sheets to"));
            if (folder == null) return;
            var (written, failed) = ExportSheets(rules, folder);
            Message = failed.Count == 0
                ? string.Format(T("{0} fiche(s) exportée(s) dans {1}", "{0} sheet(s) exported to {1}"), written, folder)
                : string.Format(T("{0} fiche(s) exportée(s) dans {1} ; non écrites (ouvertes ailleurs ?) : {2}", "{0} sheet(s) exported to {1}; not written (open elsewhere?): {2}"),
                    written, folder, string.Join(", ", failed));
            if (written > 0) await _dialogs.ShowFolderAsync(folder);
        }

        /// <summary>Écrit les PDF des sources de ces règles dans <paramref name="folder"/>, un par source de chaque règle.</summary>
        public (int Written, List<string> Failed) ExportSheets(IEnumerable<SourceRuleItem> rules, string folder)
        {
            var written = 0;
            var failed = new List<string>();
            foreach (var (rule, source) in rules.SelectMany(r => r.References.Select(s => (r, s))))
            {
                var item = Snapshot.Items.First(i => i.Key == source.Key);
                var framework = FrameworkOf(item)!;
                var file = Path.Combine(folder, ExportName(rule, source) + ".pdf");
                try
                {
                    SourcePdf.Save(file, Version, framework, item, Language.Value, _ruleTitles);
                    written++;
                }
                catch (IOException)
                {
                    failed.Add(Path.GetFileName(file));
                }
            }
            return (written, failed);
        }

        /// <summary>La page officielle de la source, en français quand elle existe.</summary>
        [RelayCommand]
        private async Task OpenSourceAsync()
        {
            if (Item is not { } item) return;
            await _dialogs.OpenUrlAsync(Language.Value == "fr" && item.UrlFr != null ? item.UrlFr : item.Url);
        }

        private bool CanCheckSource() => !IsBusy;

        /// <summary>
        /// Relit sur le web le référentiel de la source choisie, dit ce qui a changé depuis le relevé affiché, et garde le
        /// nouveau relevé. Rien n'est gardé si un élément cité a disparu de sa source ou si elle est injoignable.
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanCheckSource))]
        private async Task CheckSourceAsync()
        {
            if (Item is not { } item || FrameworkOf(item) is not { } framework) return;
            var name = StudioText.Instance.French ? framework.NameFr : framework.NameEn;
            IsBusy = true;
            Message = string.Format(T("Relecture de {0} sur le web…", "Reading {0} again on the web…"), name)
                + (framework.Id == "mitre-attack" ? T(" (les données ATT&CK font environ 55 Mo)", " (the ATT&CK data is about 55 MB)") : "");
            try
            {
                var (partial, errors) = await _fetch([framework.Id], _clock().ToString("yyyy-MM-dd"));
                if (partial == null)
                {
                    Message = T("Rien n'est gardé : ", "Nothing is kept: ") + string.Join(" ", errors);
                    return;
                }
                var changes = SourceUpdate.Diff(Snapshot.Only([framework.Id]), partial, StudioText.Instance.French ? "fr" : "en");
                _local = _local == null ? partial : _local.Merge(partial);
                SaveLocal();
                Snapshot = SourcesSnapshot.Embedded.WithNewer(_local);
                Render();
                var head = string.Format(T("{0} relu le {1}", "{0} read again on {1}"), name, partial.Retrieved);
                Message = changes.Count == 0
                    ? head + T(" : rien n'a changé depuis le relevé affiché.", ": nothing changed since the retrieval shown.")
                    : head + string.Format(T(" : {0} changement(s).", ": {0} change(s).") + "\n", changes.Count) + string.Join("\n", changes);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
            {
                Message = string.Format(T("Source injoignable, rien n'est gardé : {0}", "Source unreachable, nothing is kept: {0}"), ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>Oublie les relevés faits dans Studio : les fiches redeviennent celles livrées avec SMAD-X.</summary>
        [RelayCommand]
        private void ResetToShipped()
        {
            try
            {
                if (File.Exists(_localPath)) File.Delete(_localPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Message = ex.Message;
                return;
            }
            _local = null;
            Snapshot = SourcesSnapshot.Embedded;
            Render();
            Message = string.Format(T("Fiches livrées avec SMAD-X {0}.", "Sheets shipped with SMAD-X {0}."), Version);
        }

        private static SourcesSnapshot? LoadLocal(string path)
        {
            try
            {
                return SourcesSnapshot.Load(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                return null;
            }
        }

        private void SaveLocal()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_localPath)!);
            File.WriteAllText(_localPath, JsonSerializer.Serialize(_local, SourcesSnapshot.Json), new UTF8Encoding(false));
        }
    }
}
