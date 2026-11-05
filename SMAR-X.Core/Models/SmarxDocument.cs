using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SMARX.Models
{
    /// <summary>Fichier .smar-x.json : les serveurs simulés, avec leurs comptes, leurs groupes et leurs fichiers.</summary>
    public sealed class SmarxDocument
    {
        public const string FileExtension = ".smar-x.json";
        public const int CurrentFormatVersion = 1;

        public string Format { get; set; } = "smar-x";
        public int FormatVersion { get; set; } = CurrentFormatVersion;
        public List<LinuxHost> Linux { get; set; } = new();
        public List<WindowsHost> Windows { get; set; } = new();

        public static JsonSerializerOptions SerializerOptions { get; } = new()
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public string Serialize() => JsonSerializer.Serialize(this, SerializerOptions);

        /// <exception cref="InvalidDataException">Pas un fichier SMAR-X, ou d'un format plus récent.</exception>
        public static SmarxDocument Parse(string json)
        {
            SmarxDocument? document;
            try { document = JsonSerializer.Deserialize<SmarxDocument>(json, SerializerOptions); }
            catch (JsonException ex) { throw new InvalidDataException(ex.Message, ex); }
            if (document is not { Format: "smar-x" })
                throw new InvalidDataException("Ce fichier n'est pas un fichier SMAR-X (clé \"Format\": \"smar-x\" absente).");
            if (document.FormatVersion > CurrentFormatVersion)
                throw new InvalidDataException($"Fichier au format v{document.FormatVersion}, plus récent que le format v{CurrentFormatVersion} pris en charge.");
            return document;
        }
    }
}
