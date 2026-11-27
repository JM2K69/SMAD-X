using System.Text.Json.Serialization;
using SMADX.Models;

namespace SMAGX.Labs;

/// <summary>
/// Un scénario d'apprentissage SMAG-X (.smagx-lab.json) : un énoncé, un ou deux rôles (les postes), un départ (les
/// commandes jouées avant l'élève : projets sur la forge, commits d'un formateur, fichiers sur les postes) et des
/// objectifs vérifiés sur l'état réel des dépôts, des postes et de la forge, plus des questions de compréhension
/// communes avec SMAD-X. Un scénario à deux rôles se joue sur un seul SMAG-X (deux postes) ou sur deux SMAG-X en réseau.
/// </summary>
public sealed class SmagxLab
{
    public const string FileExtension = ".smagx-lab.json";
    public const int CurrentFormatVersion = 1;

    public string Format { get; set; } = "smagx-lab";
    public int FormatVersion { get; set; } = CurrentFormatVersion;

    /// <summary>Identifiant stable, ex. « premiers-pas ».</summary>
    public string Id { get; set; } = string.Empty;

    public LocalizedText Title { get; set; } = new();
    public LabLevel Level { get; set; } = LabLevel.Beginner;
    public int DurationMinutes { get; set; } = 20;

    /// <summary>La place dans le parcours (1, 2, 3…) : les scénarios s'enchaînent par difficulté croissante.</summary>
    public int Step { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Exam { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool ExamCheck { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EngineVersion { get; set; }

    /// <summary>Énoncé en Markdown.</summary>
    public LocalizedText Statement { get; set; } = new();

    /// <summary>Les rôles : un seul (l'élève, avec sa propre identité), ou deux élèves qui partagent la forge.</summary>
    public List<SmagxRole> Roles { get; set; } = [];

    /// <summary>
    /// Le départ, au format des scénarios de fidélité : « forge classe/projet » (projet vide sur gitlab.example,
    /// « forge github.example:moi/projet » sur GitHub), « @formateur » (un poste caché qui prépare la forge) ou « @rôle »,
    /// « $ commande », « write fichier contenu », « key » (la clé du poste enregistrée sur les forges), « trust hôte »
    /// (le serveur déjà accepté).
    /// </summary>
    public string Setup { get; set; } = string.Empty;

    public List<SmagxObjective> Objectives { get; set; } = [];
    public List<LabQuestion> Questions { get; set; } = [];

    [JsonIgnore]
    public int MaxPoints => Objectives.Where(o => !o.Guard).Sum(o => o.Points) + Questions.Sum(q => q.Points);

    /// <summary>Deux rôles : à deux sur un SMAG-X, ou sur deux SMAG-X en réseau.</summary>
    [JsonIgnore]
    public bool IsPair => Roles.Count > 1;

    [JsonIgnore]
    public bool IsTrainerCopy => Exam && (Questions.Any(q => q.HasAnswerKey || HasText(q.Hint)) || Objectives.Any(o => HasText(o.Hint)));

    private static bool HasText(LocalizedText text) => text.Values.Any(t => !string.IsNullOrWhiteSpace(t));
}

/// <summary>
/// Un rôle du scénario, joué sur un poste : son identifiant (« moi », « alice », « bob »), son identité (vide pour
/// « moi » : celle de l'élève) et ce qu'il a à faire.
/// </summary>
public sealed class SmagxRole
{
    public string Id { get; set; } = string.Empty;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Name { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Email { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Login { get; set; }

    /// <summary>Sur deux SMAG-X, le rôle dont le SMAG-X sert la forge (l'autre la rejoint).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Serves { get; set; }

    /// <summary>Ce que fait ce rôle, rappelé à celui qui le joue.</summary>
    public LocalizedText Brief { get; set; } = new();
}

public sealed class SmagxObjective
{
    public string Id { get; set; } = string.Empty;
    public LocalizedText Title { get; set; } = new();
    public LocalizedText Hint { get; set; } = new();
    public int Points { get; set; } = 1;

    /// <summary>Garde : vraie au départ, elle ne rapporte rien ; violée, elle annule les objectifs du même rôle.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Guard { get; set; }

    public SmagxCheck Check { get; set; } = new();
}

/// <summary>Ce que vérifie un objectif, sur le poste d'un rôle ou sur la forge.</summary>
public enum SmagxCheckType
{
    /// <summary>La configuration Key du poste vaut Value (« {name} », « {email} » : l'identité du rôle) ; sans Value, elle est renseignée.</summary>
    Config,
    /// <summary>Un dépôt existe dans le dossier Repo (relatif au dossier personnel : « projet »).</summary>
    Repository,
    /// <summary>La branche Branch (sinon HEAD) compte au moins Count commits.</summary>
    CommitCount,
    /// <summary>Le dernier commit de Branch (sinon HEAD) contient le fichier File (contenant Text, s'il est donné).</summary>
    FileInCommit,
    /// <summary>Le fichier File du répertoire de travail contient Text (sans Text : il existe).</summary>
    WorkFile,
    /// <summary>Rien à valider : index et répertoire de travail identiques au dernier commit, pas de fusion en cours.</summary>
    Clean,
    /// <summary>Le fichier File est dans le répertoire de travail, non suivi et ignoré par .gitignore.</summary>
    Ignored,
    /// <summary>La branche Branch existe.</summary>
    BranchExists,
    /// <summary>HEAD est sur la branche Branch.</summary>
    OnBranch,
    /// <summary>L'historique de Branch (sinon HEAD) contient un commit de fusion (deux parents).</summary>
    MergeCommit,
    /// <summary>Le dernier commit de Branch (sinon HEAD) a pour message Text.</summary>
    CommitMessage,
    /// <summary>L'historique de Branch (sinon HEAD) contient un commit de message Text.</summary>
    HistoryContains,
    /// <summary>Le dépôt distant Remote (« origin ») désigne le projet Project de la forge Host.</summary>
    Remote,
    /// <summary>La branche Branch suit une branche distante (git push -u, ou clone).</summary>
    Tracks,
    /// <summary>Le compte du rôle a au moins une clé SSH sur la forge Host.</summary>
    KeyOnForge,
    /// <summary>Le poste du rôle a accepté la clé du serveur Host (~/.ssh/known_hosts).</summary>
    KnownHost,
    /// <summary>Le projet Project de la forge a la branche Branch.</summary>
    ForgeBranch,
    /// <summary>Sur la forge, le dernier commit de Branch contient File (contenant Text, s'il est donné).</summary>
    ForgeFile,
    /// <summary>Sur la forge, l'historique de Branch contient un commit du rôle (son nom d'auteur).</summary>
    ForgeAuthor,
    /// <summary>Sur la forge, l'historique de Branch contient un commit de fusion.</summary>
    ForgeMergeCommit,
    /// <summary>La branche Branch du poste et celle de la forge pointent sur le même commit.</summary>
    Synced
}

/// <summary>Vérification déclarative ; seuls les champs utiles au type sont renseignés.</summary>
public sealed class SmagxCheck
{
    public SmagxCheckType Type { get; set; }

    /// <summary>Le rôle dont le poste est vérifié (null : la forge seule).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Role { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Repo { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Branch { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? File { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Text { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Key { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Value { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public int? Count { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Remote { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Project { get; set; }

    /// <summary>La forge : « gitlab.example » (par défaut) ou « github.example ».</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Host { get; set; }

    /// <summary>Inverse la vérification (« le fichier ne contient pas de marqueur de conflit »).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public bool Not { get; set; }
}
