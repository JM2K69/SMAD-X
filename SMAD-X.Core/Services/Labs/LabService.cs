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
                if (version < 2)
                    MarkGuards(lab);
                lab.FormatVersion = LabDefinition.CurrentFormatVersion;
                return lab;
            }
            catch (Exception ex) when (ex is JsonException or FormatException or InvalidOperationException or DocumentTooNewException)
            {
                throw new LabFormatException($"Exercice illisible : {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Format v1 : les gardes n'étaient pas marquées. Une vérification de garde (l'objet existe, reste
        /// membre, garde sa délégation…) déjà atteinte au départ en est une ; elle ne rapporte plus de point.
        /// </summary>
        private static void MarkGuards(LabDefinition lab)
        {
            foreach (var o in lab.Objectives.Where(o => LabAuthoring.CanBeGuard(o.Check)))
                if (LabGrader.IsMet(o.Check, lab.Start.Domain, lab.ReferenceDate))
                {
                    o.Guard = true;
                    o.Points = 0;
                }
        }

        public LabDefinition LoadFile(string path) => Parse(File.ReadAllText(path));

        /// <summary>
        /// Écrit l'exercice complet, avec la version du moteur : c'est l'exemplaire du formateur, indices et corrigé
        /// des questions compris. Un examen se distribue sous forme de sujet (<see cref="SerializeSubject"/>).
        /// </summary>
        public string Serialize(LabDefinition lab)
        {
            lab.FormatVersion = LabDefinition.CurrentFormatVersion;
            lab.EngineVersion = EngineInfo.Version;
            return JsonSerializer.Serialize(lab, Options);
        }

        /// <summary>
        /// Sujet à distribuer : l'exercice sans le corrigé des questions (bons choix, réponses acceptées) et, pour un
        /// examen, sans les indices. L'apprenant ne peut rien en lire dans le fichier ; ses réponses sont corrigées
        /// dans Studio avec l'exemplaire du formateur. L'exercice en mémoire garde tout.
        /// </summary>
        public string SerializeSubject(LabDefinition lab)
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
                    // Les propositions saisies en face de leur élément trahiraient les associations : on les mélange
                    question["Choices"] = Shuffled((JsonArray)question["Choices"]!, $"{lab.Id}|{question["Id"]}");
                }
                if (lab.Exam) question["Hint"] = new JsonObject();
            }
            if (lab.Exam)
                foreach (var objective in node["Objectives"]!.AsArray().OfType<JsonObject>())
                    objective["Hint"] = new JsonObject();
            return node.ToJsonString(Options);
        }

        /// <summary>Mélange reproductible (le même sujet à chaque export), jamais dans l'ordre de départ quand c'est possible.</summary>
        private static JsonArray Shuffled(JsonArray items, string seed)
        {
            var original = items.Select(i => i!.DeepClone()).ToList();
            var order = original.OrderBy(i => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(seed + "|" + i!["Id"])))).ToList();
            if (order.Count > 1 && order.Select(i => (string?)i!["Id"]).SequenceEqual(original.Select(i => (string?)i!["Id"])))
                order = [.. order.Skip(1), order[0]];
            return new JsonArray([.. order]);
        }

        public void SaveFile(LabDefinition lab, string path) => File.WriteAllText(path, Serialize(lab));

        public void SaveSubject(LabDefinition lab, string path) => File.WriteAllText(path, SerializeSubject(lab));

        /// <summary>Nom proposé pour le sujet : « id.sujet.smadx-lab.json » (« id.subject… » en anglais).</summary>
        public static string SubjectFileName(string labId, string culture) =>
            labId + (culture.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? ".subject" : ".sujet") + FileExtension;

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
        public ADRootDocument PrepareStart(LabDefinition lab, DateTime now) => Shifted(lab.Start, lab.ReferenceDate, now);

        /// <summary>
        /// Domaine corrigé écrit par Studio (dates figées comme le départ, à la date de référence de l'exercice),
        /// décalé comme le départ : il se compare au domaine de départ préparé à la même date.
        /// </summary>
        public ADRootDocument PrepareSolution(LabDefinition lab, ADRootDocument solution, DateTime now) => Shifted(solution, lab.ReferenceDate, now);

        /// <summary>
        /// Domaine corrigé à la main dans SMAD-X, ramené aux dates du départ pour être gardé avec l'exercice. Ouvert dans
        /// SMAD-X, l'exercice a décalé toutes ses dates d'un même nombre de jours : on le retrouve sur les objets du départ
        /// (même DN), par leur date de création, et on l'annule. Un domaine qui n'a rien de commun avec le départ reste tel quel.
        /// </summary>
        public ADRootDocument NormalizeSolution(LabDefinition lab, ADRootDocument solution)
        {
            var created = Flatten(lab.Start.Domain)
                .GroupBy(o => o.DistinguishedName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().CreatedDate.Date, StringComparer.OrdinalIgnoreCase);
            var days = Flatten(solution.Domain)
                .Where(o => created.ContainsKey(o.DistinguishedName))
                .GroupBy(o => (o.CreatedDate.Date - created[o.DistinguishedName]).Days)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .FirstOrDefault();
            return Shifted(solution, lab.ReferenceDate.AddDays(days), lab.ReferenceDate);
        }

        private static ADRootDocument Shifted(ADRootDocument source, DateTime reference, DateTime now)
        {
            var copy = JsonSerializer.Deserialize<ADRootDocument>(JsonSerializer.Serialize(source, Options), Options)!;
            ADDataService.RestoreParentReferences(copy.Domain, null);

            var shift = now.Date - reference.Date;
            foreach (var obj in Flatten(copy.Domain))
            {
                obj.CreatedDate += shift;
                obj.ModifiedDate += shift;
                obj.LastLogonTimestamp += shift;
                obj.PwdLastSet += shift;
                obj.LapsPasswordExpiration += shift;
            }
            return copy;
        }

        private static IEnumerable<ADObject> Flatten(ADObject root) =>
            new[] { root }.Concat(root.Children.SelectMany(Flatten));
    }
}
