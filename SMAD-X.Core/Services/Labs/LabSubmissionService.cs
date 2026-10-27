using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SMADX.Models;

namespace SMADX.Services.Labs
{
    /// <summary>
    /// Lecture et écriture des copies .smadx-submission.json. Chaque enregistrement scelle la copie :
    /// le sceau est le SHA-256 de son contenu sans le sceau, sous forme compacte. Il ne dépend pas de la
    /// mise en page (indentation, fins de ligne) mais du moindre changement de contenu : nom, dates,
    /// indices ou domaine modifiés à la main se voient à la correction.
    /// </summary>
    public sealed class LabSubmissionService
    {
        public const string FileExtension = ".smadx-submission.json";

        private static JsonSerializerOptions Options => ADDataService.SerializerOptions;

        private static string T(string fr, string en) =>
            LocalizationService.Instance.CurrentCulture.TwoLetterISOLanguageName == "fr" ? fr : en;

        /// <summary>Écrit la copie et son sceau.</summary>
        public string Serialize(LabSubmission submission)
        {
            submission.Seal = null;
            var node = JsonNode.Parse(JsonSerializer.Serialize(submission, Options))!.AsObject();
            submission.Seal = ComputeSeal(node);
            submission.SealCheck = SubmissionSeal.Valid;
            node["Seal"] = submission.Seal;
            return node.ToJsonString(Options);
        }

        /// <summary>SHA-256 de la copie sans son sceau, sous forme compacte.</summary>
        public static string ComputeSeal(JsonObject submission)
        {
            var content = (JsonObject)submission.DeepClone();
            content.Remove("Seal");
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content.ToJsonString())));
        }

        public void SaveFile(LabSubmission submission, string path) => File.WriteAllText(path, Serialize(submission));

        public LabSubmission LoadFile(string path) => Parse(File.ReadAllText(path));

        /// <summary>Lit une copie ; l'exercice et le domaine sont migrés vers le format courant.</summary>
        /// <exception cref="LabFormatException">Pas une copie SMAD-X, ou format plus récent.</exception>
        public LabSubmission Parse(string json)
        {
            try
            {
                if (JsonNode.Parse(json) is not JsonObject root || (string?)root["Format"] != "smadx-submission")
                    throw new LabFormatException(T("Ce fichier n'est pas une copie SMAD-X (clé \"Format\": \"smadx-submission\" absente).",
                                                   "This file is not a SMAD-X submission (key \"Format\": \"smadx-submission\" missing)."));

                var version = root["FormatVersion"]?.GetValue<int>() ?? 0;
                if (version > LabSubmission.CurrentFormatVersion)
                    throw new LabFormatException(T($"Copie au format v{version}, plus récent que le format v{LabSubmission.CurrentFormatVersion} pris en charge. Mettez SMAD-X à jour.",
                                                   $"Submission in format v{version}, newer than the supported format v{LabSubmission.CurrentFormatVersion}. Update SMAD-X."));

                if (root["Lab"] is not JsonObject labNode)
                    throw new LabFormatException(T("La copie ne contient pas l'exercice (clé \"Lab\").", "The submission does not contain the lab (key \"Lab\")."));
                if (root["Work"] is not JsonObject work)
                    throw new LabFormatException(T("La copie ne contient pas le domaine de l'apprenant (clé \"Work\").", "The submission does not contain the learner's domain (key \"Work\")."));

                // Le sceau se vérifie sur le fichier tel qu'il a été lu, avant toute migration
                var seal = root["Seal"]?.GetValue<string>();
                var sealCheck = seal == null ? SubmissionSeal.Missing
                    : string.Equals(seal, ComputeSeal(root), StringComparison.OrdinalIgnoreCase) ? SubmissionSeal.Valid
                    : SubmissionSeal.Broken;

                var lab = new LabService().Parse(labNode.ToJsonString());
                root.Remove("Lab");
                root["Work"] = ADDocumentMigrator.MigrateToCurrent((JsonObject)work.DeepClone(), out _);

                var submission = root.Deserialize<LabSubmission>(Options)
                                 ?? throw new LabFormatException(T("Copie vide.", "Empty submission."));
                submission.Lab = lab;
                submission.SealCheck = sealCheck;
                if (submission.Work?.Domain == null)
                    throw new LabFormatException(T("Le domaine de la copie est vide.", "The submission's domain is empty."));
                ADDataService.RestoreParentReferences(submission.Work.Domain, null);
                return submission;
            }
            catch (Exception ex) when (ex is JsonException or FormatException or InvalidOperationException or DocumentTooNewException)
            {
                throw new LabFormatException(T($"Copie illisible : {ex.Message}", $"Unreadable submission: {ex.Message}"), ex);
            }
        }

        /// <summary>
        /// Empreinte du domaine de départ d'un exercice : une copie faite sur un autre domaine de départ
        /// que celui du formateur ne peut pas être corrigée de façon fiable.
        /// </summary>
        public static string StartFingerprint(LabDefinition lab) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(lab.Start, Options).ReplaceLineEndings("\n"))));

        /// <summary>Nom de fichier proposé : « id_Prénom Nom.smadx-submission.json », sans caractère interdit.</summary>
        public static string SuggestedFileName(string labId, string student)
        {
            var name = string.IsNullOrWhiteSpace(student) ? "copie" : student.Trim();
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return $"{labId}_{name}{FileExtension}";
        }
    }
}
