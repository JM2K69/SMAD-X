using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SMAGX.Git;

/// <summary>
/// Les objets Git sous leur forme brute (type et contenu, ceux que Git hache), pour les faire voyager d'un SMAG-X à l'autre :
/// relus, ils redonnent exactement le même identifiant.
/// </summary>
public static partial class ObjectCodec
{
    public static GitObject Decode(string type, byte[] body) => type switch
    {
        "blob" => new Blob(body),
        "tree" => DecodeTree(body),
        "commit" => DecodeCommit(body),
        _ => throw new FormatException($"type d'objet inconnu : {type}")
    };

    private static Tree DecodeTree(byte[] body)
    {
        var entries = new List<TreeEntry>();
        var i = 0;
        while (i < body.Length)
        {
            var space = Array.IndexOf(body, (byte)' ', i);
            var zero = Array.IndexOf(body, (byte)0, space);
            var mode = Encoding.ASCII.GetString(body, i, space - i);
            var name = Encoding.UTF8.GetString(body, space + 1, zero - space - 1);
            var id = new ObjectId(Convert.ToHexStringLower(body, zero + 1, 20));
            entries.Add(new TreeEntry(mode, name, id));
            i = zero + 21;
        }
        return new Tree(entries);
    }

    private static Commit DecodeCommit(byte[] body)
    {
        var text = Encoding.UTF8.GetString(body);
        var split = text.IndexOf("\n\n", StringComparison.Ordinal);
        var header = text[..split].Split('\n');
        var message = text[(split + 2)..];
        ObjectId? tree = null;
        var parents = new List<ObjectId>();
        Signature? author = null, committer = null;
        foreach (var line in header)
        {
            if (line.StartsWith("tree ")) tree = new ObjectId(line[5..]);
            else if (line.StartsWith("parent ")) parents.Add(new ObjectId(line[7..]));
            else if (line.StartsWith("author ")) author = DecodeSignature(line[7..]);
            else if (line.StartsWith("committer ")) committer = DecodeSignature(line[10..]);
        }
        return new Commit(tree ?? throw new FormatException("commit sans arbre"), parents,
            author ?? throw new FormatException("commit sans auteur"), committer ?? author, message);
    }

    [GeneratedRegex(@"^(.*) <(.*)> (\d+) ([+-])(\d\d)(\d\d)$")]
    private static partial Regex SignatureLine();

    private static Signature DecodeSignature(string text)
    {
        var m = SignatureLine().Match(text);
        if (!m.Success) throw new FormatException($"signature illisible : {text}");
        var offset = new TimeSpan(int.Parse(m.Groups[5].Value), int.Parse(m.Groups[6].Value), 0);
        if (m.Groups[4].Value == "-") offset = -offset;
        var when = DateTimeOffset.FromUnixTimeSeconds(long.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture)).ToOffset(offset);
        return new Signature(m.Groups[1].Value, m.Groups[2].Value, when);
    }
}
