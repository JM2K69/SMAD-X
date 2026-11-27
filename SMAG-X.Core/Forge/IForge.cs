using SMAGX.Git;

namespace SMAGX.Forge;

/// <summary>Une mise à jour de branche envoyée par « git push » : la valeur attendue sur la forge (null si nouvelle) et la nouvelle.</summary>
public sealed record RefUpdate(string Ref, ObjectId? Old, ObjectId New);

/// <summary>Un projet de la forge, tel que l'affiche la carte Forge : son chemin et ses branches (nom, commit abrégé).</summary>
public sealed record ProjectInfo(string Path, IReadOnlyList<(string Branch, string Short)> Branches);

/// <summary>Une clé SSH enregistrée sur la forge : son titre, son type (« ED25519 »), son empreinte et le jour de son ajout.</summary>
public sealed record ForgeKey(string Title, string Label, string Fingerprint, DateTimeOffset Added);

/// <summary>La forge ne répond pas (serveur arrêté, adresse fausse) : Git l'écrit comme une connexion SSH impossible.</summary>
public class ForgeUnavailableException(string message, bool refused = false) : Exception(message)
{
    /// <summary>Vrai si le poste répond mais que rien n'écoute (SMAG-X fermé, serveur arrêté) : « Connection refused ».</summary>
    public bool ConnectionRefused { get; } = refused;
}

/// <summary>La forge du camarade refuse le code donné : Git l'écrit comme une clé SSH refusée.</summary>
public sealed class ForgeRefusedException() : ForgeUnavailableException("code refusé");

/// <summary>
/// Une forge, jointe sur place (celle de ce SMAG-X) ou par le réseau (celle du SMAG-X d'un camarade) : les commandes
/// de Git n'y voient pas de différence.
/// </summary>
public interface IForge
{
    string Host { get; }
    ForgeKind Kind { get; }

    /// <summary>
    /// Vrai : la forge se joint par SSH (sa clé de serveur, les clés des comptes). Faux pour la comparaison avec le
    /// vrai Git, qui la joint en file://, sans SSH.
    /// </summary>
    bool Ssh { get; }

    /// <summary>Joint la forge sans rien demander : lève ForgeUnavailableException si elle ne répond pas (ssh se connecte avant tout).</summary>
    void Ping();

    /// <summary>Le compte à qui appartient cette clé publique (ligne de .pub), ou null : c'est ainsi que la forge reconnaît l'élève.</summary>
    string? Authenticate(string publicKey);

    /// <summary>Les clés SSH d'un compte (page « Clés SSH » de la forge).</summary>
    IReadOnlyList<ForgeKey> Keys(string account);

    /// <summary>Ajoute une clé publique au compte : null si c'est fait, sinon « invalid » (pas une clé) ou « taken » (déjà enregistrée).</summary>
    string? AddKey(string account, string title, string publicKey);

    void RemoveKey(string account, string fingerprint);

    /// <summary>Une copie du dépôt (objets et branches), ou null s'il n'existe pas.</summary>
    Repository? Open(string project);

    /// <summary>
    /// Ajoute les objets et applique chaque mise à jour dont l'ancienne valeur est encore celle de la forge ; rend les
    /// références refusées (un autre poste a poussé entre-temps).
    /// </summary>
    IReadOnlyList<string> Push(string project, ObjectStore objects, IReadOnlyList<RefUpdate> updates);

    /// <summary>Le message de la forge après l'envoi d'une nouvelle branche (« remote: To create a merge request… »).</summary>
    string NewBranchMessage(RemoteUrl url, string branch);

    IReadOnlyList<ProjectInfo> Projects();

    /// <summary>« Nouveau projet » : un dépôt vide, branche par défaut main. Faux s'il existe déjà.</summary>
    bool Create(string project);
}
