using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SMADX.Models
{
    /// <summary>
    /// Copie d'un apprenant au format .smadx-submission.json : l'exercice tel qu'il l'a reçu, son domaine dans
    /// l'état où il l'a laissé et les indices consultés. Elle sert à reprendre le travail dans SMAD-X et à le
    /// rendre au formateur, qui la corrige dans SMAD-X Studio avec son propre exemplaire de l'exercice.
    /// </summary>
    public sealed class LabSubmission
    {
        /// <summary>
        /// Version du format des copies écrite par cette version de SMAD-X. v2 : version du moteur et temps passé.
        /// v3 : réponses aux questions et identifiant de l'apprenant.
        /// </summary>
        public const int CurrentFormatVersion = 3;

        public string Format { get; set; } = "smadx-submission";
        public int FormatVersion { get; set; } = CurrentFormatVersion;

        public string Student { get; set; } = string.Empty;

        /// <summary>Identifiant de l'apprenant (numéro, adresse) pour l'export vers Moodle ou l'ENT ; facultatif.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? StudentId { get; set; }

        public string LabId { get; set; } = string.Empty;

        /// <summary>Session de travail : les identifiants des objets du domaine en sont dérivés.</summary>
        public Guid SessionId { get; set; }

        /// <summary>Date de préparation du domaine de départ : la correction le reprépare à cette date.</summary>
        public DateTime StartedOn { get; set; }

        /// <summary>Dernier enregistrement de la copie : la correction se fait à cette date.</summary>
        public DateTime SavedAt { get; set; }

        /// <summary>Temps passé sur l'exercice, en minutes, sessions reprises comprises (null avant la 0.9).</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? ElapsedMinutes { get; set; }

        /// <summary>Version de SMAD-X qui a écrit la copie, et empreinte de ses règles d'audit (null avant la 0.9).</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? EngineVersion { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? RulesVersion { get; set; }

        /// <summary>Identifiants des objectifs (et des questions) dont l'indice a été consulté.</summary>
        public List<string> HintsUsed { get; set; } = new();

        /// <summary>Réponses aux questions, par identifiant de question.</summary>
        public Dictionary<string, LabAnswer> Answers { get; set; } = new();

        /// <summary>Exercice reçu par l'apprenant (pour reprendre le travail ; la correction utilise celui du formateur).</summary>
        public LabDefinition Lab { get; set; } = null!;

        /// <summary>Domaine de l'apprenant.</summary>
        public ADRootDocument Work { get; set; } = null!;

        /// <summary>Sceau : SHA-256 de la copie sans le sceau, écrit par SMAD-X à chaque enregistrement.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Seal { get; set; }

        /// <summary>État du sceau constaté à la lecture du fichier.</summary>
        [JsonIgnore]
        public SubmissionSeal SealCheck { get; set; }
    }

    public enum SubmissionSeal
    {
        /// <summary>Pas de sceau : la copie n'a pas été enregistrée par SMAD-X.</summary>
        Missing,
        Valid,
        /// <summary>Le contenu a changé depuis l'enregistrement par SMAD-X.</summary>
        Broken
    }
}
