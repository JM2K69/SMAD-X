using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using SMADX.Models;

namespace SMADX.Services.Labs
{
    /// <summary>Fichier d'exercice illisible, d'un autre format ou d'une version plus récente.</summary>
    public sealed class LabFormatException : Exception
    {
        public LabFormatException(string message, Exception? inner = null) : base(message, inner)
        {
        }
    }

    /// <summary>
    /// Lecture, écriture et préparation des exercices .smadx-lab.json. Les exercices fournis avec
    /// l'application sont embarqués dans l'exécutable (dossier Labs/ du projet).
    /// </summary>
    public sealed class LabService
    {
        public const string FileExtension = ".smadx-lab.json";
        private const string EmbeddedPrefix = "Labs/";

        private static JsonSerializerOptions Options => ADDataService.SerializerOptions;

        /// <summary>Lit un exercice depuis son JSON ; le domaine de départ est migré vers le format courant.</summary>
        /// <exception cref="LabFormatException">Pas un exercice SMAD-X, ou format plus récent.</exception>
        public LabDefinition Parse(string json)
        {
            try
            {
                if (JsonNode.Parse(json) is not JsonObject root || (string?)root["Format"] != "smadx-lab")
                    throw new LabFormatException("Ce fichier n'est pas un exercice SMAD-X (clé \"Format\": \"smadx-lab\" absente).");

                var version = root["FormatVersion"]?.GetValue<int>() ?? 0;
                if (version > LabDefinition.CurrentFormatVersion)
                    throw new LabFormatException($"Exercice au format v{version}, plus récent que le format v{LabDefinition.CurrentFormatVersion} pris en charge. Mettez SMAD-X à jour.");

                if (root["Start"] is not JsonObject start)
                    throw new LabFormatException("L'exercice n'a pas de domaine de départ (clé \"Start\").");
                root["Start"] = ADDocumentMigrator.MigrateToCurrent((JsonObject)start.DeepClone(), out _);

                var lab = root.Deserialize<LabDefinition>(Options)
                          ?? throw new LabFormatException("Exercice vide.");
                if (lab.Start?.Domain == null)
                    throw new LabFormatException("Le domaine de départ de l'exercice est vide.");
                ADDataService.RestoreParentReferences(lab.Start.Domain, null);
                return lab;
            }
            catch (Exception ex) when (ex is JsonException or FormatException or InvalidOperationException or DocumentTooNewException)
            {
                throw new LabFormatException($"Exercice illisible : {ex.Message}", ex);
            }
        }

        public LabDefinition LoadFile(string path) => Parse(File.ReadAllText(path));

        public string Serialize(LabDefinition lab) => JsonSerializer.Serialize(lab, Options);

        public void SaveFile(LabDefinition lab, string path) => File.WriteAllText(path, Serialize(lab));

        /// <summary>Exercices fournis avec l'application, triés par niveau puis par titre.</summary>
        public IReadOnlyList<LabDefinition> BuiltInLabs()
        {
            var assembly = typeof(LabService).Assembly;
            return assembly.GetManifestResourceNames()
                .Where(r => r.Replace('\\', '/').StartsWith(EmbeddedPrefix, StringComparison.Ordinal) && r.EndsWith(FileExtension, StringComparison.OrdinalIgnoreCase))
                .Select(r =>
                {
                    using var reader = new StreamReader(assembly.GetManifestResourceStream(r)!);
                    return Parse(reader.ReadToEnd());
                })
                .OrderBy(l => l.Level)
                .ThenBy(l => l.Title.Resolve("fr-FR"), StringComparer.CurrentCulture)
                .ToList();
        }

        /// <summary>
        /// Copie de travail du domaine de départ, dates décalées pour que l'exercice se joue « aujourd'hui ».
        /// Le domaine de départ de l'exercice reste intact (il sert à la comparaison et à « Recommencer »).
        /// </summary>
        public ADRootDocument PrepareStart(LabDefinition lab, DateTime now)
        {
            var copy = JsonSerializer.Deserialize<ADRootDocument>(JsonSerializer.Serialize(lab.Start, Options), Options)!;
            ADDataService.RestoreParentReferences(copy.Domain, null);

            var shift = now.Date - lab.ReferenceDate.Date;
            foreach (var obj in Flatten(copy.Domain))
            {
                obj.CreatedDate += shift;
                obj.ModifiedDate += shift;
                obj.LastLogonTimestamp += shift;
                obj.PwdLastSet += shift;
            }
            return copy;
        }

        private static IEnumerable<ADObject> Flatten(ADObject root) =>
            new[] { root }.Concat(root.Children.SelectMany(Flatten));
    }
}
