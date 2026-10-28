using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace SMADX.Studio
{
    /// <summary>
    /// Ce que Studio retient d'une session à l'autre : le dossier où ranger les nouveaux exercices (celui du précédent) et
    /// les derniers exercices ouverts. Un fichier illisible ou absent vaut des réglages vides.
    /// </summary>
    public sealed class StudioSettings
    {
        /// <summary>Fichier des réglages ; les tests le remplacent par un fichier temporaire.</summary>
        public static string DefaultPath { get; set; } =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SMAD-X", "Studio", "settings.json");

        /// <summary>Dossier où Studio a rangé le dernier exercice créé : il y propose le suivant.</summary>
        public string? LabsFolder { get; set; }

        /// <summary>Les derniers exercices ouverts, le plus récent en tête.</summary>
        public List<RecentLab> RecentLabs { get; set; } = new();

        public static StudioSettings Load()
        {
            try
            {
                return File.Exists(DefaultPath) ? JsonSerializer.Deserialize<StudioSettings>(File.ReadAllText(DefaultPath)) ?? new() : new();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                return new();
            }
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(DefaultPath)!);
                File.WriteAllText(DefaultPath, JsonSerializer.Serialize(this));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Pas retenu : Studio proposera de nouveau le dossier Documents
            }
        }
    }

    /// <summary>Un exercice récent : son dossier (ou son fichier, s'il est enregistré seul) et son titre.</summary>
    public sealed class RecentLab
    {
        public string Path { get; set; } = "";
        public string Title { get; set; } = "";
    }
}
