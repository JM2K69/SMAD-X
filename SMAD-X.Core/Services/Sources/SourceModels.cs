using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SMADX.Services.Sources
{
    /// <summary>
    /// Un référentiel cité par SMAD-X : son nom, sa page, sa licence et ce qu'elle permet de reprendre, la date du relevé et
    /// celle de la publication relevée.
    /// </summary>
    public sealed record SourceFramework
    {
        public required string Id { get; init; }
        public required string NameFr { get; init; }
        public required string NameEn { get; init; }
        public required string Url { get; init; }
        public required string LicenseFr { get; init; }
        public required string LicenseEn { get; init; }
        /// <summary>Mention à reproduire avec le texte (MITRE ATT&amp;CK), sinon null.</summary>
        public string? Notice { get; init; }
        /// <summary>Version ou date de la publication relevée (en-tête Last-Modified, version du catalogue…).</summary>
        public string? Published { get; init; }
        public required string Retrieved { get; init; }
    }

    /// <summary>
    /// Un élément d'un référentiel cité par SMAD-X : un point de contrôle ANSSI, un contrôle NIST, une technique MITRE, une
    /// mesure CIS, une page Microsoft, un modèle de cloisonnement. Les textes officiels ne sont repris que si la licence le
    /// permet ; une source qui n'existe qu'en anglais garde son texte anglais dans les fiches françaises.
    /// </summary>
    public sealed record SourceItem
    {
        public required string Framework { get; init; }
        public required string Id { get; init; }
        public required string TitleFr { get; init; }
        public required string TitleEn { get; init; }
        public required string Url { get; init; }
        public string? UrlFr { get; init; }
        /// <summary>Niveaux officiels (ANSSI : 1 le plus critique ; un point peut en avoir plusieurs, chacun avec son seuil).</summary>
        public IReadOnlyList<int>? Levels { get; init; }
        /// <summary>Date d'ajout ou de dernière modification de l'élément dans sa source.</summary>
        public string? Dated { get; init; }
        public string? Version { get; init; }
        public string? TextFr { get; init; }
        public string? TextEn { get; init; }
        public string? RecoFr { get; init; }
        public string? RecoEn { get; init; }
        /// <summary>Les règles de SMAD-X qui citent cet élément.</summary>
        public required IReadOnlyList<string> UsedBy { get; init; }

        [JsonIgnore]
        public string Key => $"{Framework}|{Id}";
    }

    /// <summary>Le relevé complet, gardé dans sources/sources.json : les fiches s'en écrivent, et le relevé suivant s'y compare.</summary>
    public sealed record SourcesSnapshot
    {
        public required string Retrieved { get; init; }
        public required string Tool { get; init; }
        public required IReadOnlyList<SourceFramework> Frameworks { get; init; }
        public required IReadOnlyList<SourceItem> Items { get; init; }

        public static readonly JsonSerializerOptions Json = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        public static SourcesSnapshot? Load(string path) =>
            File.Exists(path) ? JsonSerializer.Deserialize<SourcesSnapshot>(File.ReadAllText(path), Json) : null;

        public void Save(string path) => File.WriteAllText(path, JsonSerializer.Serialize(this, Json).Replace("\r\n", "\n") + "\n");

        /// <summary>
        /// Le relevé livré avec cette version de SMAD-X : sources/sources.json, embarqué dans SMAD-X.Core. Studio en affiche
        /// et en exporte les fiches, sauf pour un référentiel qu'il a relevé depuis (<see cref="WithNewer"/>).
        /// </summary>
        public static SourcesSnapshot Embedded => EmbeddedSnapshot.Value;

        private static readonly Lazy<SourcesSnapshot> EmbeddedSnapshot = new(() =>
        {
            using var stream = typeof(SourcesSnapshot).Assembly.GetManifestResourceStream("Sources/sources.json")
                ?? throw new InvalidOperationException("Embedded resource Sources/sources.json is missing");
            return JsonSerializer.Deserialize<SourcesSnapshot>(stream, Json)!;
        });

        /// <summary>Ce relevé limité à quelques référentiels.</summary>
        public SourcesSnapshot Only(IReadOnlyCollection<string> frameworks) => this with
        {
            Frameworks = Frameworks.Where(f => frameworks.Contains(f.Id)).ToList(),
            Items = Items.Where(i => frameworks.Contains(i.Framework)).ToList()
        };

        /// <summary>Ce relevé, où les référentiels relus dans <paramref name="partial"/> remplacent les siens, avec tous leurs éléments.</summary>
        public SourcesSnapshot Merge(SourcesSnapshot partial)
        {
            var ids = partial.Frameworks.Select(f => f.Id).ToHashSet();
            return this with
            {
                Retrieved = string.CompareOrdinal(partial.Retrieved, Retrieved) > 0 ? partial.Retrieved : Retrieved,
                Frameworks = Frameworks.Where(f => !ids.Contains(f.Id)).Concat(partial.Frameworks).ToList(),
                Items = Items.Where(i => !ids.Contains(i.Framework)).Concat(partial.Items).ToList()
            };
        }

        /// <summary>
        /// Ce relevé, où chaque référentiel que <paramref name="newer"/> a relevé le même jour ou plus tard remplace le sien :
        /// sa date, sa publication et le contenu de ses éléments. La liste des éléments reste celle de ce relevé (ceux que
        /// cite cette version de SMAD-X, avec les règles qui les citent ici) ; un élément absent de l'autre relevé garde
        /// son contenu. Un relevé fait par une version plus ancienne de Studio ne remplace donc pas un relevé plus récent
        /// livré avec SMAD-X.
        /// </summary>
        public SourcesSnapshot WithNewer(SourcesSnapshot? newer)
        {
            if (newer == null) return this;
            var fresh = newer.Frameworks
                .Where(n => Frameworks.FirstOrDefault(f => f.Id == n.Id) is { } f && string.CompareOrdinal(n.Retrieved, f.Retrieved) >= 0)
                .ToDictionary(n => n.Id);
            if (fresh.Count == 0) return this;
            var items = newer.Items.Where(i => fresh.ContainsKey(i.Framework)).GroupBy(i => i.Key).ToDictionary(g => g.Key, g => g.First());
            return this with
            {
                Frameworks = Frameworks.Select(f => fresh.TryGetValue(f.Id, out var n) ? n : f).ToList(),
                Items = Items.Select(i => items.TryGetValue(i.Key, out var n) ? n with { UsedBy = i.UsedBy } : i).ToList()
            };
        }
    }
}
