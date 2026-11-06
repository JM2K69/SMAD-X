using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using SMADX.Models;

namespace SMARX.Models
{
    /// <summary>
    /// Exercice SMAR-X (.smarx-lab.json) : un énoncé, des serveurs de départ et des objectifs vérifiés par les
    /// moteurs de SMAR-X (noyau Linux, Samba, NTFS, SMB), plus des questions de compréhension communes avec SMAD-X.
    /// L'idée : donner aux comptes les droits dont ils ont besoin, et seulement ceux-là.
    /// </summary>
    public sealed class SmarxLab
    {
        public const string FileExtension = ".smarx-lab.json";
        public const int CurrentFormatVersion = 1;

        public string Format { get; set; } = "smarx-lab";
        public int FormatVersion { get; set; } = CurrentFormatVersion;
        /// <summary>Identifiant stable, ex. « linux-dossier-equipe » (sert aussi au nom de la copie).</summary>
        public string Id { get; set; } = string.Empty;
        public LocalizedText Title { get; set; } = new();
        public LabLevel Level { get; set; } = LabLevel.Beginner;
        public int DurationMinutes { get; set; } = 20;
        /// <summary>Mode examen : ni indice, ni vérification (sauf <see cref="ExamCheck"/>), ni « Recommencer ».</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool Exam { get; set; }
        /// <summary>En examen, « Vérifier » reste disponible, sans score ni explication.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool ExamCheck { get; set; }
        /// <summary>Version de SMAR-X qui a écrit l'exercice.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? EngineVersion { get; set; }
        /// <summary>Énoncé en Markdown.</summary>
        public LocalizedText Statement { get; set; } = new();
        /// <summary>Serveurs de départ ; l'apprenant travaille sur une copie.</summary>
        public SmarxDocument Start { get; set; } = new();
        public List<SmarxObjective> Objectives { get; set; } = new();
        public List<LabQuestion> Questions { get; set; } = new();

        /// <summary>Total des points, objectifs et questions ; une garde n'en rapporte pas.</summary>
        public int MaxPoints => Objectives.Where(o => !o.Guard).Sum(o => o.Points) + Questions.Sum(q => q.Points);

        /// <summary>Exemplaire du formateur : un examen qui contient encore des indices ou le corrigé de ses questions.</summary>
        [JsonIgnore]
        public bool IsTrainerCopy => Exam && (Questions.Any(q => q.HasAnswerKey || HasText(q.Hint)) || Objectives.Any(o => HasText(o.Hint)));

        private static bool HasText(LocalizedText text) => text.Values.Any(t => !string.IsNullOrWhiteSpace(t));
    }

    public sealed class SmarxObjective
    {
        public string Id { get; set; } = string.Empty;
        public LocalizedText Title { get; set; } = new();
        public LocalizedText Hint { get; set; } = new();
        public int Points { get; set; } = 1;
        /// <summary>
        /// Garde : atteinte au départ, elle ne rapporte rien et interdit une solution de facilité (ajouter le
        /// compte à un groupe, ouvrir un dossier à tous). Violée, elle annule les objectifs qui portent sur le même
        /// compte ou le même chemin.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool Guard { get; set; }
        /// <summary>
        /// Crédit partiel (« exactement les accès nécessaires ») : chaque case juste rapporte sa part des points. Sans lui,
        /// l'objectif n'est atteint que si toutes les cases sont justes : les refus déjà justes au départ ne rapportent rien.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool Partial { get; set; }
        public SmarxCheck Check { get; set; } = new();
    }

    /// <summary>Ce que vérifie un objectif.</summary>
    public enum SmarxCheckType
    {
        /// <summary>Account peut faire Operation sur Path (en local, ou par le partage Share).</summary>
        AccessAllowed,
        /// <summary>Account ne peut pas faire Operation sur Path.</summary>
        AccessDenied,
        /// <summary>
        /// Rien de plus que nécessaire : pour chaque compte de Accounts, chaque chemin de Paths et chaque opération
        /// de Operations, l'accès est permis si et seulement s'il figure dans Needs. Avec le crédit partiel, chaque case
        /// juste rapporte sa part des points ; sans lui, toutes doivent l'être.
        /// </summary>
        ExactAccess,
        /// <summary>Ce que crée Account dans le dossier Path, Other peut lui faire Operation (setgid, ACL par défaut, héritage).</summary>
        CreatedAllows,
        /// <summary>Règle d'hygiène Rule respectée sous Path (ou pour le partage Share).</summary>
        Hygiene,
        /// <summary>Le compte Account (« * » : tous les comptes) et ses groupes n'ont pas changé.</summary>
        AccountUnchanged,
        /// <summary>Le propriétaire et les droits de Path n'ont pas changé.</summary>
        PathUnchanged
    }

    /// <summary>Règles d'hygiène, chacune liée à une bonne pratique documentée.</summary>
    public enum SmarxHygieneRule
    {
        /// <summary>Linux : aucun fichier ni dossier modifiable par les autres, sauf un dossier sticky (comme /tmp).</summary>
        NoWorldWritable,
        /// <summary>Linux : aucun programme setuid ou setgid.</summary>
        NoSetuid,
        /// <summary>Windows : aucune autorisation Modification ou Contrôle total pour Tout le monde, Utilisateurs authentifiés ou Utilisateurs.</summary>
        NoBroadWrite,
        /// <summary>
        /// Windows, partage : la pratique conseillée par Microsoft, un partage large dont NTFS fait le tri. Contrôle total
        /// aux seuls Administrateurs, et Tout le monde au plus en Lecture ; Utilisateurs authentifiés : Modifier convient.
        /// </summary>
        ShareStandard,
        /// <summary>
        /// Windows, AGDLP : les autorisations NTFS ne nomment ni compte ni groupe global (ni groupe local créé sur le
        /// serveur) ; seulement des groupes de domaine local et les identités intégrées (Administrateurs, Système,
        /// Utilisateurs, Créateur propriétaire…).
        /// </summary>
        DomainLocalOnly
    }

    /// <summary>Vérification déclarative ; seuls les champs utiles au type sont renseignés.</summary>
    public sealed class SmarxCheck
    {
        public SmarxCheckType Type { get; set; }
        /// <summary>Le serveur Linux ou Windows concerné.</summary>
        public SmarxSystem System { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Account { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Other { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Path { get; set; }
        /// <summary>Nom de l'opération (LinuxOperation ou WindowsOperation).</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Operation { get; set; }
        /// <summary>Partage par lequel passe l'accès ; null : en local, sur le serveur.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Share { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public List<string>? Accounts { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public List<string>? Paths { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public List<string>? Operations { get; set; }
        /// <summary>Accès nécessaires, « compte opération chemin » (ExactAccess).</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public List<string>? Needs { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public SmarxHygieneRule? Rule { get; set; }

        /// <summary>Les comptes sur lesquels porte la vérification (une garde violée annule les objectifs qui les citent).</summary>
        [JsonIgnore]
        public IEnumerable<string> AccountsInvolved =>
            new[] { Account, Other }.OfType<string>().Concat(Accounts ?? []).Where(a => a != "*");

        /// <summary>Les chemins sur lesquels porte la vérification.</summary>
        [JsonIgnore]
        public IEnumerable<string> PathsInvolved => new[] { Path }.OfType<string>().Concat(Paths ?? []);
    }

    public enum SmarxSystem
    {
        Linux,
        Windows
    }

    /// <summary>
    /// Copie d'un apprenant (.smarx-submission.json) : l'exercice tel qu'il l'a reçu, ses serveurs à la fin, ses
    /// réponses et les indices consultés, scellés (voir <see cref="SMADX.Services.Labs.SubmissionSealer"/>).
    /// </summary>
    public sealed class SmarxSubmission
    {
        public const string FileExtension = ".smarx-submission.json";
        public const int CurrentFormatVersion = 1;

        public string Format { get; set; } = "smarx-submission";
        public int FormatVersion { get; set; } = CurrentFormatVersion;
        public string Student { get; set; } = string.Empty;
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? StudentId { get; set; }
        public string LabId { get; set; } = string.Empty;
        public Guid SessionId { get; set; }
        public DateTime StartedOn { get; set; }
        public DateTime SavedAt { get; set; }
        public int? ElapsedMinutes { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? EngineVersion { get; set; }
        public List<string> HintsUsed { get; set; } = new();
        public Dictionary<string, LabAnswer> Answers { get; set; } = new();
        public SmarxLab Lab { get; set; } = new();
        public SmarxDocument Work { get; set; } = new();
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Seal { get; set; }
        /// <summary>État du sceau à la lecture (pas écrit).</summary>
        [JsonIgnore] public SubmissionSeal SealCheck { get; set; }
    }
}
