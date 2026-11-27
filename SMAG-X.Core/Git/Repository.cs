namespace SMAGX.Git;

/// <summary>Une entrée de l'index : chemin (avec « / »), mode, contenu, et étape d'un conflit (0 hors conflit, 1 base, 2 nous, 3 eux).</summary>
public sealed record IndexEntry(string Path, string Mode, ObjectId Id, int Stage = 0);

/// <summary>L'index (la zone de préparation) : ce qui partira dans le prochain commit.</summary>
public sealed class GitIndex
{
    private readonly SortedDictionary<string, IndexEntry> _entries = new(StringComparer.Ordinal);
    private readonly SortedDictionary<string, IndexEntry?[]> _conflicts = new(StringComparer.Ordinal);

    /// <summary>Les entrées hors conflit, dans l'ordre des chemins.</summary>
    public IEnumerable<IndexEntry> Entries => _entries.Values;

    /// <summary>Les chemins en conflit, avec leurs étapes 1, 2 et 3 (null si absente de ce côté).</summary>
    public IReadOnlyDictionary<string, IndexEntry?[]> Conflicts => _conflicts;

    public bool HasConflicts => _conflicts.Count > 0;

    public bool Contains(string path) => _entries.ContainsKey(path) || _conflicts.ContainsKey(path);

    public IndexEntry? Get(string path) => _entries.GetValueOrDefault(path);

    public void Set(IndexEntry entry)
    {
        _conflicts.Remove(entry.Path);
        _entries[entry.Path] = entry with { Stage = 0 };
    }

    public void Remove(string path)
    {
        _entries.Remove(path);
        _conflicts.Remove(path);
    }

    /// <summary>Un chemin en conflit : ses trois versions remplacent l'entrée normale.</summary>
    public void SetConflict(string path, IndexEntry? @base, IndexEntry? ours, IndexEntry? theirs)
    {
        _entries.Remove(path);
        _conflicts[path] = [@base, ours, theirs];
    }

    public void Clear()
    {
        _entries.Clear();
        _conflicts.Clear();
    }

    /// <summary>Le contenu de l'index comme dictionnaire chemin → entrée (hors conflits).</summary>
    public Dictionary<string, IndexEntry> Snapshot() => new(_entries, StringComparer.Ordinal);

    public void Load(IEnumerable<IndexEntry> entries)
    {
        Clear();
        foreach (var entry in entries)
            _entries[entry.Path] = entry;
    }
}

/// <summary>Une ligne du journal des références (reflog) : ancien et nouveau commit, auteur, message.</summary>
public sealed record ReflogEntry(ObjectId? Old, ObjectId New, Signature Who, string Message);

/// <summary>Un dépôt Git : objets, références, HEAD, index et configuration locale. Le répertoire de travail est sur le poste.</summary>
public sealed class Repository(string root, bool bare = false)
{
    /// <summary>Le dossier du dépôt sur le poste (« /home/alice/projet »), ou le dépôt nu sur la forge.</summary>
    public string Root { get; } = root;

    public bool Bare { get; } = bare;

    public ObjectStore Objects { get; } = new();

    /// <summary>Les références, par nom complet : refs/heads/main, refs/remotes/origin/main, refs/tags/v1.</summary>
    public SortedDictionary<string, ObjectId> Refs { get; } = new(StringComparer.Ordinal);

    /// <summary>HEAD attachée à une branche (son nom complet), ou null si HEAD est détachée.</summary>
    public string? HeadRef { get; set; } = "refs/heads/main";

    /// <summary>Le commit de HEAD détachée.</summary>
    public ObjectId? DetachedHead { get; set; }

    /// <summary>Le commit où HEAD a été détachée (« HEAD detached at », puis « detached from » après de nouveaux commits).</summary>
    public ObjectId? DetachedAt { get; set; }

    public GitIndex Index { get; } = new();

    /// <summary>La configuration du dépôt (.git/config), clés en minuscules (« remote.origin.url »).</summary>
    public Dictionary<string, string> Config { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Fusion en cours : le commit fusionné (MERGE_HEAD) et le message préparé (MERGE_MSG).</summary>
    public ObjectId? MergeHead { get; set; }
    public string? MergeMessage { get; set; }

    /// <summary>La branche par défaut de chaque dépôt distant (« origin » → « main »), que suit refs/remotes/origin/HEAD.</summary>
    public Dictionary<string, string> RemoteHeads { get; } = new(StringComparer.Ordinal);

    /// <summary>La branche quittée au dernier changement (pour « git switch - »).</summary>
    public string? PreviousBranch { get; set; }

    public Dictionary<string, List<ReflogEntry>> Reflogs { get; } = new(StringComparer.Ordinal);

    /// <summary>Le commit de HEAD, ou null sur une branche encore vide.</summary>
    public ObjectId? Head => HeadRef is null ? DetachedHead : Refs.TryGetValue(HeadRef, out var id) ? id : null;

    /// <summary>Le nom court de la branche courante, ou null si HEAD est détachée.</summary>
    public string? CurrentBranch => HeadRef is { } r && r.StartsWith("refs/heads/") ? r["refs/heads/".Length..] : null;

    public Commit GetCommit(ObjectId id) => Objects.Get<Commit>(id);

    /// <summary>Le contenu d'un commit, à plat : chemin → entrée (mode et objet).</summary>
    public Dictionary<string, TreeEntry> Files(ObjectId? commit)
    {
        var files = new Dictionary<string, TreeEntry>(StringComparer.Ordinal);
        if (commit is { } id)
            Flatten(GetCommit(id).Tree, "", files);
        return files;
    }

    private void Flatten(ObjectId treeId, string prefix, Dictionary<string, TreeEntry> files)
    {
        foreach (var entry in Objects.Get<Tree>(treeId).Entries)
        {
            var path = prefix + entry.Name;
            if (entry.IsTree) Flatten(entry.Id, path + "/", files);
            else files[path] = entry with { Name = path };
        }
    }

    /// <summary>Écrit les arbres d'un ensemble de fichiers (chemin → mode et objet) et rend l'arbre racine.</summary>
    public ObjectId WriteTree(IEnumerable<(string Path, string Mode, ObjectId Id)> files)
    {
        var root = new Folder();
        foreach (var (path, mode, id) in files)
        {
            var parts = path.Split('/');
            var folder = root;
            foreach (var part in parts[..^1])
            {
                if (!folder.Folders.TryGetValue(part, out var child))
                    folder.Folders[part] = child = new Folder();
                folder = child;
            }
            folder.Files[parts[^1]] = (mode, id);
        }
        return Write(root);
    }

    private ObjectId Write(Folder folder)
    {
        var entries = folder.Files.Select(f => new TreeEntry(f.Value.Mode, f.Key, f.Value.Id))
            .Concat(folder.Folders.Select(f => new TreeEntry(TreeEntry.TreeMode, f.Key, Write(f.Value))));
        return Objects.Put(new Tree(entries));
    }

    private sealed class Folder
    {
        public Dictionary<string, Folder> Folders { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, (string Mode, ObjectId Id)> Files { get; } = new(StringComparer.Ordinal);
    }

    /// <summary>Déplace une référence et l'inscrit dans son reflog (et dans celui de HEAD si c'est la branche courante).</summary>
    public void UpdateRef(string name, ObjectId id, Signature who, string message)
    {
        var old = Refs.TryGetValue(name, out var o) ? o : (ObjectId?)null;
        Refs[name] = id;
        Log(name, old, id, who, message);
        if (name == HeadRef) Log("HEAD", old, id, who, message);
    }

    public void Log(string name, ObjectId? old, ObjectId id, Signature who, string message)
    {
        if (!Reflogs.TryGetValue(name, out var list))
            Reflogs[name] = list = [];
        list.Add(new ReflogEntry(old, id, who, message));
    }

    /// <summary>Les commits accessibles depuis ceux-là (eux compris).</summary>
    public HashSet<ObjectId> Ancestors(IEnumerable<ObjectId> starts)
    {
        var seen = new HashSet<ObjectId>();
        var stack = new Stack<ObjectId>(starts);
        while (stack.Count > 0)
        {
            var id = stack.Pop();
            if (!seen.Add(id)) continue;
            foreach (var parent in GetCommit(id).Parents) stack.Push(parent);
        }
        return seen;
    }

    public bool IsAncestor(ObjectId ancestor, ObjectId of) => Ancestors([of]).Contains(ancestor);

    /// <summary>Les meilleurs ancêtres communs de deux commits (merge-base).</summary>
    public IReadOnlyList<ObjectId> MergeBases(ObjectId a, ObjectId b)
    {
        var common = Ancestors([a]);
        common.IntersectWith(Ancestors([b]));
        // Un ancêtre commun qui est l'ancêtre d'un autre ancêtre commun n'est pas le meilleur
        return common.Where(c => !common.Any(o => !o.Equals(c) && IsAncestor(c, o))).ToList();
    }
}
