using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using SMADX.Models;

namespace SMADX.Services
{
    /// <summary>Le fichier a été écrit par une version plus récente de SMAD-X.</summary>
    public sealed class DocumentTooNewException : Exception
    {
        public int FileVersion { get; }

        public DocumentTooNewException(int fileVersion)
            : base($"Format v{fileVersion} plus récent que le format v{ADRootDocument.CurrentVersion} pris en charge.")
            => FileVersion = fileVersion;
    }

    /// <summary>
    /// Fait passer le JSON d'un fichier .smad-x.json de sa version d'origine à
    /// <see cref="ADRootDocument.CurrentVersion"/>, une étape (N → N+1) à la fois.
    /// Travailler sur le JSON brut permet de renommer ou restructurer des champs
    /// que les classes actuelles ne connaissent plus.
    /// </summary>
    public static class ADDocumentMigrator
    {
        /// <summary>Étapes de migration, indexées par la version de départ.</summary>
        internal static readonly IReadOnlyDictionary<int, Func<JsonObject, JsonObject>> Steps =
            new Dictionary<int, Func<JsonObject, JsonObject>>
            {
                [1] = MigrateV1ToV2,
                // v2 → v3 : la structure ne change pas, seules de nouvelles valeurs et de nouveaux champs apparaissent
                [2] = document => document
            };

        /// <summary>
        /// Version du fichier : clé "Version" à la racine, ou 1 si la racine est directement
        /// un objet AD (format d'origine, aussi produit par l'export JSON simple).
        /// </summary>
        public static int DetectVersion(JsonObject root)
        {
            var versionNode = root["Version"] ?? root["version"];
            if (versionNode is null)
            {
                if (root.ContainsKey("Type") || root.ContainsKey("Children"))
                    return 1;
                throw new FormatException("Ni clé \"Version\" ni objet AD à la racine.");
            }

            if (versionNode is not JsonValue value || !value.TryGetValue(out int version) || version < 1)
                throw new FormatException($"Version de format invalide : {versionNode.ToJsonString()}.");
            return version;
        }

        /// <summary>
        /// Migre <paramref name="root"/> vers la version courante.
        /// </summary>
        /// <exception cref="DocumentTooNewException">Format plus récent que cette version de SMAD-X.</exception>
        /// <exception cref="FormatException">Racine ou version illisible.</exception>
        public static JsonObject MigrateToCurrent(JsonObject root, out int fileVersion)
        {
            fileVersion = DetectVersion(root);
            if (fileVersion > ADRootDocument.CurrentVersion)
                throw new DocumentTooNewException(fileVersion);

            for (var version = fileVersion; version < ADRootDocument.CurrentVersion; version++)
            {
                if (!Steps.TryGetValue(version, out var step))
                    throw new InvalidOperationException($"Aucune migration du format v{version} vers v{version + 1}.");
                root = step(root);
                root["Version"] = version + 1;
            }
            return root;
        }

        /// <summary>v1 → v2 : l'arbre AD seul devient le Domain d'un document qui porte aussi les sites.</summary>
        private static JsonObject MigrateV1ToV2(JsonObject domain) => new()
        {
            ["Version"] = 2,
            ["Domain"] = domain,
            ["SitesTopology"] = null
        };
    }
}
