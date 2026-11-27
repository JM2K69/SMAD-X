using SMAGX.Git;
using SMAGX.Ssh;

namespace SMAGX.Forge;

public enum ForgeKind { GitLab, GitHub }

/// <summary>Une adresse de dépôt distant : « git@gitlab.example:classe/projet.git », « ssh://git@hôte/chemin », « https://hôte/chemin ».</summary>
public sealed record RemoteUrl(string Original, string Host, string Path, bool Ssh)
{
    /// <summary>Le chemin du dépôt sur la forge, sans « .git » : « classe/projet ».</summary>
    public string Project => Path.EndsWith(".git") ? Path[..^4] : Path;

    /// <summary>L'utilisateur SSH de l'adresse (« git » dans « git@gitlab.example:… »), ou null s'il n'y en a pas.</summary>
    public string? User
    {
        get
        {
            var rest = Original.StartsWith("ssh://") ? Original["ssh://".Length..] : Original;
            var end = Original.StartsWith("ssh://") ? rest.IndexOf('/') : rest.IndexOf(':');
            var at = rest.IndexOf('@');
            return Ssh && at > 0 && (end < 0 || at < end) ? rest[..at] : null;
        }
    }

    /// <summary>L'adresse sans l'utilisateur, comme Git l'écrit après « To » (« gitlab.example:classe/projet.git »).</summary>
    public string Anonymized => Ssh ? $"{Host}:{Path}" : $"https://{Host}/{Path}";

    /// <summary>L'adresse après « From » dans « git fetch » : anonymisée et sans « .git ».</summary>
    public string FromDisplay => Anonymized.EndsWith(".git") ? Anonymized[..^4] : Anonymized;

    public static RemoteUrl? Parse(string url)
    {
        url = url.Trim();
        if (url.StartsWith("ssh://"))
        {
            var rest = url["ssh://".Length..];
            var at = rest.IndexOf('@');
            if (at >= 0) rest = rest[(at + 1)..];
            var slash = rest.IndexOf('/');
            return slash <= 0 ? null : new RemoteUrl(url, rest[..slash].Split(':')[0], rest[(slash + 1)..], true);
        }
        if (url.StartsWith("https://") || url.StartsWith("http://"))
        {
            var rest = url[(url.IndexOf("://") + 3)..];
            var slash = rest.IndexOf('/');
            return slash <= 0 ? null : new RemoteUrl(url, rest[..slash], rest[(slash + 1)..], false);
        }
        // Forme scp : utilisateur@hôte:chemin
        var colon = url.IndexOf(':');
        if (colon <= 0 || url.Contains('/') && url.IndexOf('/') < colon) return null;
        var hostPart = url[..colon];
        var host = hostPart.Contains('@') ? hostPart[(hostPart.IndexOf('@') + 1)..] : hostPart;
        return new RemoteUrl(url, host, url[(colon + 1)..].TrimStart('/'), true);
    }
}

/// <summary>
/// La forge simulée (façon GitLab ou GitHub) : ses dépôts nus par chemin « groupe/projet ». Elle vit dans un SMAG-X :
/// celui de l'élève, ou celui qui sert de serveur à deux élèves ; ses opérations sont protégées par un verrou, car les
/// postes et le serveur réseau l'utilisent depuis plusieurs fils.
/// </summary>
public sealed class Forge(string host = "gitlab.example", ForgeKind kind = ForgeKind.GitLab) : IForge
{
    private readonly object _sync = new();
    private readonly SortedDictionary<string, Repository> _repositories = new(StringComparer.Ordinal);

    public string Host { get; } = host;
    public ForgeKind Kind { get; } = kind;

    /// <summary>Faux pour la comparaison avec le vrai Git (dépôt file://, sans message du serveur) ; vrai dans l'application.</summary>
    public bool ServerMessages { get; init; } = true;

    /// <summary>Faux pour la comparaison avec le vrai Git (file://, sans SSH) ; vrai dans l'application.</summary>
    public bool Ssh { get; init; } = true;

    private readonly Dictionary<string, List<(string Title, SshPublicKey Key, DateTimeOffset Added)>> _keys = new(StringComparer.Ordinal);

    public void Ping() { }

    public string? Authenticate(string publicKey)
    {
        if (SshPublicKey.Parse(publicKey) is not { } key) return null;
        lock (_sync)
            return _keys.FirstOrDefault(a => a.Value.Any(k => k.Key.SameKey(key))).Key;
    }

    public IReadOnlyList<ForgeKey> Keys(string account)
    {
        lock (_sync)
            return _keys.TryGetValue(account, out var keys)
                ? keys.Select(k => new ForgeKey(k.Title, k.Key.Label, k.Key.Fingerprint, k.Added)).ToList()
                : [];
    }

    public string? AddKey(string account, string title, string publicKey)
    {
        if (SshPublicKey.Parse(publicKey) is not { } key) return "invalid";
        lock (_sync)
        {
            // Une clé désigne un seul compte : la forge refuse de l'enregistrer deux fois
            if (_keys.Values.Any(list => list.Any(k => k.Key.SameKey(key)))) return "taken";
            if (!_keys.TryGetValue(account, out var keys)) _keys[account] = keys = [];
            keys.Add((title.Trim().Length > 0 ? title.Trim() : key.Comment, key, DateTimeOffset.Now));
        }
        Changed?.Invoke();
        return null;
    }

    public void RemoveKey(string account, string fingerprint)
    {
        lock (_sync)
            if (_keys.TryGetValue(account, out var keys)) keys.RemoveAll(k => k.Key.Fingerprint == fingerprint);
        Changed?.Invoke();
    }

    /// <summary>Changé à chaque projet créé ou branche poussée (pour rafraîchir la carte Forge).</summary>
    public event Action? Changed;

    /// <summary>Un dépôt vide, comme « Nouveau projet » sur la forge : sa branche par défaut est main.</summary>
    public Repository Create(string project, string defaultBranch = "main")
    {
        Repository repo;
        lock (_sync)
        {
            if (_repositories.TryGetValue(project, out var existing)) return existing;
            repo = new Repository("/" + project + ".git", bare: true) { HeadRef = "refs/heads/" + defaultBranch };
            _repositories[project] = repo;
        }
        Changed?.Invoke();
        return repo;
    }

    bool IForge.Create(string project)
    {
        lock (_sync)
            if (_repositories.ContainsKey(project)) return false;
        Create(project);
        return true;
    }

    public Repository? Open(string project)
    {
        lock (_sync)
        {
            if (!_repositories.TryGetValue(project, out var repo)) return null;
            var copy = new Repository(repo.Root, bare: true) { HeadRef = repo.HeadRef };
            copy.Objects.CopyFrom(repo.Objects);
            foreach (var (name, id) in repo.Refs) copy.Refs[name] = id;
            return copy;
        }
    }

    public IReadOnlyList<string> Push(string project, ObjectStore objects, IReadOnlyList<RefUpdate> updates)
    {
        var rejected = new List<string>();
        lock (_sync)
        {
            if (!_repositories.TryGetValue(project, out var repo)) return updates.Select(u => u.Ref).ToList();
            repo.Objects.CopyFrom(objects);
            foreach (var update in updates)
            {
                var current = repo.Refs.TryGetValue(update.Ref, out var id) ? id : (ObjectId?)null;
                if (!Equals(current, update.Old)) { rejected.Add(update.Ref); continue; }
                repo.Refs[update.Ref] = update.New;
            }
        }
        Changed?.Invoke();
        return rejected;
    }

    public IReadOnlyList<ProjectInfo> Projects()
    {
        lock (_sync)
            return _repositories.Select(r => new ProjectInfo(r.Key, r.Value.Refs.Where(x => x.Key.StartsWith("refs/heads/"))
                .Select(x => (x.Key["refs/heads/".Length..], x.Value.Short)).ToList())).ToList();
    }

    /// <summary>Le message que la forge écrit après l'envoi d'une nouvelle branche (« remote: To create a merge request… »).</summary>
    public string NewBranchMessage(RemoteUrl url, string branch) => ServerMessages ? BranchMessage(Kind, Host, url, branch) : "";

    public static string BranchMessage(ForgeKind kind, string host, RemoteUrl url, string branch) => kind == ForgeKind.GitLab
        ? $"remote: \nremote: To create a merge request for {branch}, visit:\nremote:   https://{host}/{url.Project}/-/merge_requests/new?merge_request%5Bsource_branch%5D={Uri.EscapeDataString(branch)}\nremote: \n"
        : $"remote: \nremote: Create a pull request for '{branch}' on GitHub by visiting:\nremote:      https://{host}/{url.Project}/pull/new/{branch}\nremote: \n";
}
