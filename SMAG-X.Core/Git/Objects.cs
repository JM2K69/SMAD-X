using System.Security.Cryptography;
using System.Text;

namespace SMAGX.Git;

/// <summary>Identifiant d'un objet Git : le SHA-1 de son en-tête et de son contenu, en hexadécimal.</summary>
public readonly record struct ObjectId(string Hex)
{
    /// <summary>Les 7 premiers caractères, comme Git les affiche par défaut.</summary>
    public string Short => Hex[..7];

    public byte[] Raw => Convert.FromHexString(Hex);

    public override string ToString() => Hex;
}

public enum ObjectKind { Blob, Tree, Commit }

/// <summary>Un objet Git : son type et son contenu, tels que Git les hache (« type taille\0contenu »).</summary>
public abstract class GitObject
{
    public abstract ObjectKind Kind { get; }

    public abstract byte[] Body();

    public string TypeName => Kind switch
    {
        ObjectKind.Blob => "blob",
        ObjectKind.Tree => "tree",
        _ => "commit"
    };
}

/// <summary>Le contenu d'un fichier.</summary>
public sealed class Blob(byte[] data) : GitObject
{
    public byte[] Data { get; } = data;

    public override ObjectKind Kind => ObjectKind.Blob;

    public override byte[] Body() => Data;

    public string Text => Encoding.UTF8.GetString(Data);
}

/// <summary>Une entrée d'arbre : mode (100644 fichier, 100755 exécutable, 40000 dossier), nom et objet.</summary>
public sealed record TreeEntry(string Mode, string Name, ObjectId Id)
{
    public const string FileMode = "100644";
    public const string ExecutableMode = "100755";
    public const string TreeMode = "40000";

    public bool IsTree => Mode == TreeMode;
}

/// <summary>Un dossier : ses entrées, dans l'ordre de Git (octets du nom, un dossier comparé avec un « / » final).</summary>
public sealed class Tree : GitObject
{
    public Tree(IEnumerable<TreeEntry> entries)
    {
        var list = entries.ToList();
        list.Sort(Compare);
        Entries = list;
    }

    public IReadOnlyList<TreeEntry> Entries { get; }

    public override ObjectKind Kind => ObjectKind.Tree;

    public override byte[] Body()
    {
        using var stream = new MemoryStream();
        foreach (var entry in Entries)
        {
            var head = Encoding.UTF8.GetBytes($"{entry.Mode} {entry.Name}\0");
            stream.Write(head);
            stream.Write(entry.Id.Raw);
        }
        return stream.ToArray();
    }

    /// <summary>L'ordre de Git (base_name_compare) : un dossier se compare comme si son nom finissait par « / ».</summary>
    public static int Compare(TreeEntry a, TreeEntry b)
    {
        var x = Encoding.UTF8.GetBytes(a.Name + (a.IsTree ? "/" : ""));
        var y = Encoding.UTF8.GetBytes(b.Name + (b.IsTree ? "/" : ""));
        return x.AsSpan().SequenceCompareTo(y);
    }
}

/// <summary>Auteur ou committeur : nom, adresse et date, écrits comme dans un commit (« Nom &lt;adresse&gt; 1764234000 +0100 »).</summary>
public sealed record Signature(string Name, string Email, DateTimeOffset When)
{
    public string Raw => $"{Name} <{Email}> {When.ToUnixTimeSeconds()} {Zone(When.Offset)}";

    public static string Zone(TimeSpan offset) =>
        (offset < TimeSpan.Zero ? "-" : "+") + $"{Math.Abs(offset.Hours):00}{Math.Abs(offset.Minutes):00}";

    /// <summary>La date comme l'affiche « git log » : « Fri Nov 27 10:00:00 2026 +0100 ».</summary>
    public string LogDate =>
        $"{When.ToString("ddd MMM", System.Globalization.CultureInfo.InvariantCulture)} {When.Day} {When:HH:mm:ss} {When.Year} {Zone(When.Offset)}";
}

/// <summary>Un commit : l'arbre du projet, ses parents, l'auteur, le committeur et le message.</summary>
public sealed class Commit(ObjectId tree, IReadOnlyList<ObjectId> parents, Signature author, Signature committer, string message) : GitObject
{
    public ObjectId Tree { get; } = tree;
    public IReadOnlyList<ObjectId> Parents { get; } = parents;
    public Signature Author { get; } = author;
    public Signature Committer { get; } = committer;
    public string Message { get; } = message;

    /// <summary>La première ligne du message.</summary>
    public string Subject => Message.Split('\n')[0];

    public override ObjectKind Kind => ObjectKind.Commit;

    public override byte[] Body()
    {
        var text = new StringBuilder();
        text.Append("tree ").Append(Tree.Hex).Append('\n');
        foreach (var parent in Parents)
            text.Append("parent ").Append(parent.Hex).Append('\n');
        text.Append("author ").Append(Author.Raw).Append('\n');
        text.Append("committer ").Append(Committer.Raw).Append('\n');
        text.Append('\n').Append(Message);
        return Encoding.UTF8.GetBytes(text.ToString());
    }
}

/// <summary>La base d'objets d'un dépôt (le dossier .git/objects), en mémoire.</summary>
public sealed class ObjectStore
{
    private readonly Dictionary<string, GitObject> _objects = new(StringComparer.Ordinal);

    public int Count => _objects.Count;

    public IEnumerable<ObjectId> Ids => _objects.Keys.Select(k => new ObjectId(k));

    public static ObjectId Hash(GitObject obj)
    {
        var body = obj.Body();
        var header = Encoding.ASCII.GetBytes($"{obj.TypeName} {body.Length}\0");
        var data = new byte[header.Length + body.Length];
        header.CopyTo(data, 0);
        body.CopyTo(data, header.Length);
        return new ObjectId(Convert.ToHexStringLower(SHA1.HashData(data)));
    }

    public ObjectId Put(GitObject obj)
    {
        var id = Hash(obj);
        _objects.TryAdd(id.Hex, obj);
        return id;
    }

    public bool Contains(ObjectId id) => _objects.ContainsKey(id.Hex);

    public GitObject Get(ObjectId id) => _objects[id.Hex];

    public T Get<T>(ObjectId id) where T : GitObject => (T)_objects[id.Hex];

    public bool TryGet<T>(ObjectId id, out T obj) where T : GitObject
    {
        if (_objects.TryGetValue(id.Hex, out var found) && found is T typed)
        {
            obj = typed;
            return true;
        }
        obj = null!;
        return false;
    }

    /// <summary>Les objets dont l'identifiant commence par ce préfixe (abréviation tapée par l'utilisateur).</summary>
    public IReadOnlyList<ObjectId> WithPrefix(string prefix) =>
        _objects.Keys.Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).Select(k => new ObjectId(k)).ToList();

    /// <summary>Copie les objets manquants d'une autre base (clone, fetch, push).</summary>
    public void CopyFrom(ObjectStore other)
    {
        foreach (var (key, value) in other._objects)
            _objects.TryAdd(key, value);
    }
}
