using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SMADX.Models;
using SMADX.Services.Labs;
using SMARX.Models;

namespace SMARX.Services.Labs
{
    /// <summary>
    /// Fichiers des exercices SMAR-X : l'exercice (.smarx-lab.json), le sujet à distribuer (sans corrigé, et sans
    /// indices pour un examen) et la copie scellée de l'apprenant (.smarx-submission.json).
    /// </summary>
    public static class SmarxLabService
    {
        private static JsonSerializerOptions Options => SmarxDocument.SerializerOptions;

        /// <summary>Version de SMAR-X écrite dans les exercices et les copies.</summary>
        public static string EngineVersion { get; } =
            typeof(SmarxLabService).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion?.Split('+')[0]
            ?? typeof(SmarxLabService).Assembly.GetName().Version?.ToString(3) ?? "0";

        public static string Serialize(SmarxLab lab)
        {
            lab.FormatVersion = SmarxLab.CurrentFormatVersion;
            lab.EngineVersion = EngineVersion;
            return JsonSerializer.Serialize(lab, Options);
        }

        /// <exception cref="InvalidDataException">Pas un exercice SMAR-X, ou d'un format plus récent.</exception>
        public static SmarxLab Parse(string json)
        {
            SmarxLab? lab;
            try { lab = JsonSerializer.Deserialize<SmarxLab>(json, Options); }
            catch (JsonException ex) { throw new InvalidDataException(ex.Message, ex); }
            if (lab is not { Format: "smarx-lab" })
                throw new InvalidDataException("Ce fichier n'est pas un exercice SMAR-X (clé \"Format\": \"smarx-lab\" absente).");
            if (lab.FormatVersion > SmarxLab.CurrentFormatVersion)
                throw new InvalidDataException($"Exercice au format v{lab.FormatVersion}, plus récent que le format v{SmarxLab.CurrentFormatVersion} pris en charge.");
            return lab;
        }

        /// <summary>
        /// Sujet à distribuer : l'exercice sans le corrigé des questions et, pour un examen, sans les indices. Les
        /// réponses de l'apprenant sont alors corrigées avec l'exemplaire du formateur.
        /// </summary>
        public static string SerializeSubject(SmarxLab lab)
        {
            var node = JsonNode.Parse(Serialize(lab))!.AsObject();
            foreach (var question in node["Questions"]!.AsArray().OfType<JsonObject>())
            {
                question.Remove("AcceptedAnswers");
                foreach (var choice in question["Choices"]!.AsArray().OfType<JsonObject>())
                    choice.Remove("Correct");
                if (question["Prompts"] is JsonArray prompts)
                {
                    foreach (var prompt in prompts.OfType<JsonObject>())
                        prompt.Remove("Match");
                    question["Choices"] = Shuffled((JsonArray)question["Choices"]!, $"{lab.Id}|{question["Id"]}");
                }
                if (lab.Exam) question["Hint"] = new JsonObject();
            }
            if (lab.Exam)
                foreach (var objective in node["Objectives"]!.AsArray().OfType<JsonObject>())
                    objective["Hint"] = new JsonObject();
            return node.ToJsonString(Options);
        }

        /// <summary>Mélange reproductible des propositions d'une association, jamais dans l'ordre de départ si possible.</summary>
        private static JsonArray Shuffled(JsonArray items, string seed)
        {
            var original = items.Select(i => i!.DeepClone()).ToList();
            var order = original.OrderBy(i => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(seed + "|" + i!["Id"])))).ToList();
            if (order.Count > 1 && order.Select(i => (string?)i!["Id"]).SequenceEqual(original.Select(i => (string?)i!["Id"])))
                order = [.. order.Skip(1), order[0]];
            return new JsonArray([.. order]);
        }

        /// <summary>La copie, scellée : le sceau couvre tout son contenu (nom, dates, indices, réponses, serveurs).</summary>
        public static string SerializeSubmission(SmarxSubmission submission)
        {
            submission.FormatVersion = SmarxSubmission.CurrentFormatVersion;
            submission.EngineVersion = EngineVersion;
            submission.Seal = null;
            var node = JsonSerializer.SerializeToNode(submission, Options)!.AsObject();
            node["Seal"] = SubmissionSealer.Compute(node);
            return node.ToJsonString(Options);
        }

        /// <exception cref="InvalidDataException">Pas une copie SMAR-X, ou d'un format plus récent.</exception>
        public static SmarxSubmission ParseSubmission(string json)
        {
            JsonObject node;
            try { node = JsonNode.Parse(json)?.AsObject() ?? throw new InvalidDataException("Copie vide."); }
            catch (JsonException ex) { throw new InvalidDataException(ex.Message, ex); }
            var seal = SubmissionSealer.Check(node);
            SmarxSubmission? submission;
            try { submission = node.Deserialize<SmarxSubmission>(Options); }
            catch (JsonException ex) { throw new InvalidDataException(ex.Message, ex); }
            if (submission is not { Format: "smarx-submission" })
                throw new InvalidDataException("Ce fichier n'est pas une copie SMAR-X (clé \"Format\": \"smarx-submission\" absente).");
            if (submission.FormatVersion > SmarxSubmission.CurrentFormatVersion)
                throw new InvalidDataException($"Copie au format v{submission.FormatVersion}, plus récent que le format v{SmarxSubmission.CurrentFormatVersion} pris en charge.");
            submission.SealCheck = seal;
            return submission;
        }

        /// <summary>Nom proposé pour la copie : « id-de-l-exercice.nom-de-l-apprenant.smarx-submission.json ».</summary>
        public static string SubmissionFileName(string labId, string student)
        {
            var safe = new string(student.Trim().Select(ch => char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '-').ToArray()).Trim('-');
            return $"{labId}.{(safe.Length > 0 ? safe : "copie")}{SmarxSubmission.FileExtension}";
        }
    }
}
