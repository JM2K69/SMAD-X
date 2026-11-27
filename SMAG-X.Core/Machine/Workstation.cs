using System.Text;
using SMAGX.Git;

namespace SMAGX.Machine;

/// <summary>
/// Le poste de travail d'un élève : son identité système, ses fichiers (en mémoire), son dossier courant, sa
/// configuration Git globale (~/.gitconfig) et les dépôts qu'il a créés ou clonés.
/// </summary>
public sealed class Workstation
{
    public Workstation(string user = "alice", string host = "poste")
    {
        User = user;
        Host = host;
        Home = $"/home/{user}";
        Cwd = Home;
        Directories.Add("/");
        Directories.Add("/home");
        Directories.Add(Home);
    }

    public string User { get; }
    public string Host { get; }
    public string Home { get; }

    /// <summary>Le nom complet et l'adresse du compte de l'élève (pas ceux de Git : il les lui donne avec git config).</summary>
    public string? FullName { get; init; }
    public string? Email { get; init; }

    /// <summary>Le dossier courant, en chemin absolu (« /home/alice/projet »).</summary>
    public string Cwd { get; set; }

    /// <summary>Les fichiers du poste : chemin absolu → contenu.</summary>
    public SortedDictionary<string, byte[]> Files { get; } = new(StringComparer.Ordinal);

    public HashSet<string> Directories { get; } = new(StringComparer.Ordinal);

    /// <summary>La configuration Git globale (~/.gitconfig), clés en minuscules (« user.name »).</summary>
    public Dictionary<string, string> GlobalConfig { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Les dépôts du poste, par dossier racine.</summary>
    public Dictionary<string, Repository> Repositories { get; } = new(StringComparer.Ordinal);

    /// <summary>Les forges que le poste joint par le réseau (la sienne, ou celle du SMAG-X qui sert de serveur).</summary>
    public List<SMAGX.Forge.IForge> Network { get; } = [];

    public SMAGX.Forge.IForge? FindForge(string host) => Network.FirstOrDefault(f => f.Host == host);

    /// <summary>L'heure du poste : l'heure réelle, ou celle que fixe un scénario ou un test.</summary>
    public Func<DateTimeOffset> Clock { get; set; } = () => DateTimeOffset.Now;

    /// <summary>
    /// L'éditeur de texte que Git ouvre (message de commit sans -m, fusion) : reçoit le texte proposé, rend le texte
    /// enregistré, ou null si l'élève abandonne. Sans éditeur, Git garde le texte proposé.
    /// </summary>
    public Func<string, string?>? Editor { get; set; }

    /// <summary>
    /// Une question posée dans le terminal (ssh-keygen, l'authenticité d'un serveur, une phrase de passe) : reçoit tout
    /// ce que la commande a écrit jusqu'ici, question comprise, et si la saisie doit rester cachée ; rend la ligne tapée
    /// par l'élève. Sans terminal (null, ou une réponse null), ssh refuse ce qu'il ne peut pas demander.
    /// </summary>
    public Func<string, bool, string?>? Ask { get; set; }

    /// <summary>Le hasard des clés fictives et des sels de known_hosts (fixé par les tests).</summary>
    public Random Random { get; set; } = new();

    /// <summary>Les phrases de passe des clés privées qui en ont une (chemin → phrase), demandées par ssh à chaque usage.</summary>
    public Dictionary<string, string> KeyPassphrases { get; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Vrai pour le terminal de l'élève : Git y écrit ce qu'il n'écrit que dans un terminal (« Updated 1 path from the
    /// index », progression). Faux pour la comparaison avec le vrai Git, mesuré hors terminal.
    /// </summary>
    public bool Interactive { get; set; } = true;

    // ── Chemins ─────────────────────────────────────────────────────────────

    /// <summary>Un chemin tapé par l'élève, rendu absolu et normalisé (« . », « .. », « ~ »).</summary>
    public string Resolve(string path)
    {
        if (path == "~" || path.StartsWith("~/")) path = Home + path[1..];
        var start = path.StartsWith('/') ? "" : Cwd;
        var parts = new List<string>();
        foreach (var part in (start + "/" + path).Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == ".") continue;
            if (part == "..") { if (parts.Count > 0) parts.RemoveAt(parts.Count - 1); continue; }
            parts.Add(part);
        }
        return "/" + string.Join('/', parts);
    }

    public static string Parent(string path) => path.LastIndexOf('/') is var i and > 0 ? path[..i] : "/";

    public static string Combine(string dir, string relative) => dir == "/" ? "/" + relative : dir + "/" + relative;

    /// <summary>Chemin relatif de « from » vers « to » (deux chemins absolus), comme l'écrit Git (« ../a.txt »).</summary>
    public static string Relative(string from, string to)
    {
        var a = from.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var b = to.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var common = 0;
        while (common < a.Length && common < b.Length && a[common] == b[common]) common++;
        var parts = Enumerable.Repeat("..", a.Length - common).Concat(b[common..]);
        return string.Join('/', parts);
    }

    // ── Fichiers ────────────────────────────────────────────────────────────

    public bool FileExists(string path) => Files.ContainsKey(path);

    public bool DirectoryExists(string path) => Directories.Contains(path);

    public byte[] ReadFile(string path) => Files[path];

    public string ReadText(string path) => Encoding.UTF8.GetString(Files[path]);

    public void WriteFile(string path, byte[] content)
    {
        EnsureDirectory(Parent(path));
        Files[path] = content;
    }

    public void WriteText(string path, string text) => WriteFile(path, Encoding.UTF8.GetBytes(text));

    public void DeleteFile(string path) => Files.Remove(path);

    public void EnsureDirectory(string path)
    {
        while (Directories.Add(path) && path != "/")
            path = Parent(path);
    }

    /// <summary>Les fichiers sous un dossier, en chemins relatifs à lui (avec « / »), hors dossier .git.</summary>
    public IEnumerable<string> FilesUnder(string dir)
    {
        var prefix = dir == "/" ? "/" : dir + "/";
        foreach (var path in Files.Keys)
        {
            if (!path.StartsWith(prefix, StringComparison.Ordinal)) continue;
            var relative = path[prefix.Length..];
            if (relative == ".git" || relative.StartsWith(".git/")) continue;
            yield return relative;
        }
    }

    /// <summary>Le dépôt qui contient ce dossier (le plus proche en remontant), ou null.</summary>
    public Repository? FindRepository(string dir)
    {
        for (var d = dir; ; d = Parent(d))
        {
            if (Repositories.TryGetValue(d, out var repo)) return repo;
            if (d == "/") return null;
        }
    }

    /// <summary>Valeur d'une configuration : celle du dépôt, sinon la globale.</summary>
    public string? Config(Repository? repo, string key) =>
        repo is not null && repo.Config.TryGetValue(key, out var local) ? local
        : GlobalConfig.TryGetValue(key, out var global) ? global : null;
}
