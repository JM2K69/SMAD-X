using System.Security.Cryptography;
using System.Text;

namespace SMAGX.Ssh;

/// <summary>
/// Une clé publique SSH, au format d'une ligne de « id_ed25519.pub » ou de « known_hosts » : type, blob en base64,
/// commentaire. Les clés des postes sont fictives (des octets tirés au hasard, pas un vrai point de courbe) ; leur
/// empreinte et leur dessin se calculent exactement comme ssh-keygen (vérifiés sur des clés d'OpenSSH 10.3).
/// </summary>
public sealed class SshPublicKey(string type, byte[] blob, string comment)
{
    /// <summary>« ssh-ed25519 », « ssh-rsa » ou « ecdsa-sha2-nistp256 ».</summary>
    public string Type { get; } = type;

    public byte[] Blob { get; } = blob;

    public string Comment { get; } = comment;

    /// <summary>La ligne du fichier .pub : « ssh-ed25519 AAAA… alice@lycee.example ».</summary>
    public string Line => Comment.Length > 0 ? $"{Type} {Base64} {Comment}" : $"{Type} {Base64}";

    public string Base64 => Convert.ToBase64String(Blob);

    /// <summary>« SHA256: » suivi de l'empreinte SHA-256 du blob, en base64 sans « = », comme ssh-keygen -l.</summary>
    public string Fingerprint => "SHA256:" + Convert.ToBase64String(SHA256.HashData(Blob)).TrimEnd('=');

    /// <summary>Le nom du type entre crochets et en fin de ligne de ssh-keygen -l : « ED25519 », « RSA », « ECDSA ».</summary>
    public string Label => Type switch { "ssh-ed25519" => "ED25519", "ssh-rsa" => "RSA", _ => "ECDSA" };

    /// <summary>La taille en bits : 256 pour ED25519 et ECDSA P-256, la longueur du module pour RSA.</summary>
    public int Bits
    {
        get
        {
            if (Type != "ssh-rsa") return 256;
            var reader = new SshReader(Blob);
            reader.String();
            reader.Bytes();
            var n = reader.Bytes();
            var i = 0;
            while (i < n.Length && n[i] == 0) i++;
            return n.Length == i ? 0 : (n.Length - i - 1) * 8 + (int)Math.Log2(n[i]) + 1;
        }
    }

    /// <summary>« 256 SHA256:… commentaire (ED25519) », la ligne de ssh-keygen -l.</summary>
    public string FingerprintLine => $"{Bits} {Fingerprint} {(Comment.Length > 0 ? Comment : "no comment")} ({Label})";

    /// <summary>Le dessin de l'empreinte (« randomart »), l'algorithme de l'évêque ivre d'OpenSSH (sshkey.c).</summary>
    public string Randomart => SshRandomart.Draw(SHA256.HashData(Blob), $"[{Label} {Bits}]", "[SHA256]");

    public bool SameKey(SshPublicKey other) => Type == other.Type && Blob.AsSpan().SequenceEqual(other.Blob);

    /// <summary>Lit « type base64 [commentaire] » ; null si ce n'est pas une clé publique (blob illisible ou type différent).</summary>
    public static SshPublicKey? Parse(string line)
    {
        var parts = line.Trim().Split([' ', '\t'], 3, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 || parts[0] is not ("ssh-ed25519" or "ssh-rsa" or "ecdsa-sha2-nistp256")) return null;
        try
        {
            var blob = Convert.FromBase64String(parts[1]);
            var reader = new SshReader(blob);
            if (reader.String() != parts[0]) return null;
            switch (parts[0])
            {
                case "ssh-ed25519":
                    if (reader.Bytes().Length != 32) return null;
                    break;
                case "ssh-rsa":
                    reader.Bytes();
                    reader.Bytes();
                    break;
                default:
                    reader.String();
                    reader.Bytes();
                    break;
            }
            return reader.AtEnd ? new SshPublicKey(parts[0], blob, parts.Length > 2 ? parts[2].Trim() : "") : null;
        }
        catch (Exception e) when (e is FormatException or ArgumentOutOfRangeException or IndexOutOfRangeException)
        {
            return null;
        }
    }

    /// <summary>La clé publique de la forge (« clé d'hôte »), la même pour tous les SMAG-X : tirée de son nom.</summary>
    public static SshPublicKey HostKey(string host) =>
        Ed25519(SHA256.HashData(Encoding.UTF8.GetBytes("SMAG-X host key " + host)), "");

    public static SshPublicKey Ed25519(byte[] point, string comment)
    {
        var w = new SshWriter();
        w.String("ssh-ed25519");
        w.Bytes(point);
        return new SshPublicKey("ssh-ed25519", w.ToArray(), comment);
    }
}

/// <summary>
/// Une paire de clés fictive et son fichier privé au format d'OpenSSH (« -----BEGIN OPENSSH PRIVATE KEY----- »,
/// openssh-key-v1, sans chiffrement), dont ssh retrouve la clé publique.
/// </summary>
public static class SshKeyFile
{
    private static readonly byte[] Magic = "openssh-key-v1\0"u8.ToArray();

    /// <summary>Le type demandé à ssh-keygen -t, sous son nom de type SSH, ou null s'il est inconnu.</summary>
    public static string? TypeOf(string name) => name.ToLowerInvariant() switch
    {
        "ed25519" or "ssh-ed25519" => "ssh-ed25519",
        "rsa" or "ssh-rsa" => "ssh-rsa",
        "ecdsa" or "ecdsa-sha2-nistp256" => "ecdsa-sha2-nistp256",
        _ => null
    };

    /// <summary>Une paire neuve : la clé publique et le texte du fichier privé.</summary>
    public static (SshPublicKey Public, string PrivateFile) Generate(string type, int bits, string comment, Random random)
    {
        var pub = new SshWriter();
        var priv = new SshWriter();
        pub.String(type);
        priv.String(type);
        switch (type)
        {
            case "ssh-ed25519":
            {
                var point = Bytes(random, 32);
                var seed = Bytes(random, 32);
                pub.Bytes(point);
                priv.Bytes(point);
                priv.Bytes([.. seed, .. point]);
                break;
            }
            case "ssh-rsa":
            {
                var n = Bytes(random, bits / 8);
                n[0] |= 0x80;
                n[^1] |= 1;
                byte[] e = [0x01, 0x00, 0x01];
                pub.Mpint(e);
                pub.Mpint(n);
                priv.Mpint(n);
                priv.Mpint(e);
                priv.Mpint(Bytes(random, bits / 8));
                priv.Mpint(Bytes(random, bits / 16));
                priv.Mpint(Bytes(random, bits / 16));
                priv.Mpint(Bytes(random, bits / 16));
                break;
            }
            default:
            {
                byte[] q = [0x04, .. Bytes(random, 64)];
                pub.String("nistp256");
                pub.Bytes(q);
                priv.String("nistp256");
                priv.Bytes(q);
                priv.Mpint(Bytes(random, 32));
                break;
            }
        }
        var blob = pub.ToArray();
        var check = (uint)random.Next();
        var section = new SshWriter();
        section.UInt32(check);
        section.UInt32(check);
        section.Raw(priv.ToArray());
        section.String(comment);
        for (byte pad = 1; section.Length % 8 != 0; pad++) section.Raw([pad]);

        var file = new SshWriter();
        file.Raw(Magic);
        file.String("none");
        file.String("none");
        file.String("");
        file.UInt32(1);
        file.Bytes(blob);
        file.Bytes(section.ToArray());
        var base64 = Convert.ToBase64String(file.ToArray());
        var text = new StringBuilder("-----BEGIN OPENSSH PRIVATE KEY-----\n");
        for (var i = 0; i < base64.Length; i += 70) text.Append(base64.AsSpan(i, Math.Min(70, base64.Length - i))).Append('\n');
        text.Append("-----END OPENSSH PRIVATE KEY-----\n");
        return (new SshPublicKey(type, blob, comment), text.ToString());
    }

    /// <summary>La clé publique d'un fichier privé (avec son commentaire), ou null si le fichier n'en est pas un.</summary>
    public static SshPublicKey? ReadPrivate(string text)
    {
        var lines = text.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length < 3 || lines[0] != "-----BEGIN OPENSSH PRIVATE KEY-----" || lines[^1] != "-----END OPENSSH PRIVATE KEY-----") return null;
        try
        {
            var data = Convert.FromBase64String(string.Concat(lines[1..^1]));
            if (!data.AsSpan(0, Magic.Length).SequenceEqual(Magic)) return null;
            var reader = new SshReader(data[Magic.Length..]);
            var cipher = reader.String();
            reader.String();
            reader.Bytes();
            if (reader.UInt32() != 1) return null;
            var blob = reader.Bytes();
            var type = new SshReader(blob).String();
            var comment = "";
            if (cipher == "none")
            {
                var section = new SshReader(reader.Bytes());
                section.UInt32();
                section.UInt32();
                section.String();
                switch (type)
                {
                    case "ssh-ed25519": section.Bytes(); section.Bytes(); break;
                    case "ssh-rsa": for (var i = 0; i < 6; i++) section.Bytes(); break;
                    default: section.String(); section.Bytes(); section.Bytes(); break;
                }
                comment = section.String();
            }
            return SshPublicKey.Parse($"{type} {Convert.ToBase64String(blob)} {comment}");
        }
        catch (Exception e) when (e is FormatException or ArgumentOutOfRangeException or IndexOutOfRangeException or ArgumentException)
        {
            return null;
        }
    }

    private static byte[] Bytes(Random random, int count)
    {
        var bytes = new byte[count];
        random.NextBytes(bytes);
        return bytes;
    }
}

/// <summary>Le dessin d'une empreinte : un évêque part du centre d'un champ de 17 × 9 et suit les bits de l'empreinte.</summary>
public static class SshRandomart
{
    private const string Augmentation = " .o+=*BOX@%&#/^SE";
    private const int SizeX = 17, SizeY = 9;

    public static string Draw(byte[] digest, string title, string hash)
    {
        var field = new int[SizeX, SizeY];
        var len = Augmentation.Length - 1;
        int x = SizeX / 2, y = SizeY / 2;
        foreach (var value in digest)
        {
            int input = value;
            for (var b = 0; b < 4; b++)
            {
                x += (input & 1) != 0 ? 1 : -1;
                y += (input & 2) != 0 ? 1 : -1;
                x = Math.Clamp(x, 0, SizeX - 1);
                y = Math.Clamp(y, 0, SizeY - 1);
                if (field[x, y] < len - 2) field[x, y]++;
                input >>= 2;
            }
        }
        field[SizeX / 2, SizeY / 2] = len - 1;
        field[x, y] = len;

        var text = new StringBuilder();
        text.Append('+').Append(Border(title)).Append("+\n");
        for (var row = 0; row < SizeY; row++)
        {
            text.Append('|');
            for (var col = 0; col < SizeX; col++) text.Append(Augmentation[Math.Min(field[col, row], len)]);
            text.Append("|\n");
        }
        text.Append('+').Append(Border(hash)).Append('+');
        return text.ToString();
    }

    private static string Border(string label)
    {
        var left = (SizeX - label.Length) / 2;
        return new string('-', left) + label + new string('-', SizeX - left - label.Length);
    }
}

/// <summary>
/// Le fichier ~/.ssh/known_hosts : les clés des serveurs déjà acceptés. Comme sur Debian (HashKnownHosts yes), le nom
/// du serveur y est haché : « |1|sel|empreinte ssh-ed25519 AAAA… ».
/// </summary>
public static class KnownHosts
{
    /// <summary>La clé connue pour ce serveur (nom en clair ou haché), ou null.</summary>
    public static SshPublicKey? Find(string text, string host)
    {
        foreach (var line in Lines(text))
        {
            if (Matches(line.Host, host) && SshPublicKey.Parse(line.Key) is { } key) return key;
        }
        return null;
    }

    /// <summary>La ligne ajoutée pour un serveur accepté, son nom haché avec un sel tiré au hasard.</summary>
    public static string Line(string host, SshPublicKey key, Random random)
    {
        var salt = new byte[20];
        random.NextBytes(salt);
        var hash = HMACSHA1.HashData(salt, Encoding.UTF8.GetBytes(host));
        return $"|1|{Convert.ToBase64String(salt)}|{Convert.ToBase64String(hash)} {key.Type} {key.Base64}\n";
    }

    /// <summary>Le fichier sans les lignes de ce serveur (ssh-keygen -R), et les numéros des lignes retirées.</summary>
    public static (string Text, List<int> Removed) Remove(string text, string host)
    {
        var kept = new StringBuilder();
        var removed = new List<int>();
        var number = 0;
        foreach (var raw in text.Split('\n'))
        {
            if (raw.Length == 0) continue;
            number++;
            var parts = raw.Split(' ', 2);
            if (parts.Length == 2 && !raw.StartsWith('#') && Matches(parts[0], host)) removed.Add(number);
            else kept.Append(raw).Append('\n');
        }
        return (kept.ToString(), removed);
    }

    private static IEnumerable<(string Host, string Key)> Lines(string text)
    {
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var parts = line.Split(' ', 2);
            if (parts.Length == 2) yield return (parts[0], parts[1]);
        }
    }

    private static bool Matches(string field, string host)
    {
        if (!field.StartsWith("|1|")) return field.Split(',').Contains(host);
        var parts = field.Split('|');
        if (parts.Length != 4) return false;
        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            return Convert.ToBase64String(HMACSHA1.HashData(salt, Encoding.UTF8.GetBytes(host))) == parts[3];
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

/// <summary>Écrit les champs d'un message SSH (RFC 4251) : entiers 32 bits, chaînes et entiers multiprécision.</summary>
internal sealed class SshWriter
{
    private readonly MemoryStream _data = new();

    public long Length => _data.Length;

    public void UInt32(uint value) => _data.Write([(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value]);

    public void Raw(byte[] bytes) => _data.Write(bytes);

    public void Bytes(byte[] bytes)
    {
        UInt32((uint)bytes.Length);
        _data.Write(bytes);
    }

    public void String(string text) => Bytes(Encoding.UTF8.GetBytes(text));

    public void Mpint(byte[] value)
    {
        var i = 0;
        while (i < value.Length && value[i] == 0) i++;
        var trimmed = value[i..];
        Bytes(trimmed.Length > 0 && (trimmed[0] & 0x80) != 0 ? [0, .. trimmed] : trimmed);
    }

    public byte[] ToArray() => _data.ToArray();
}

internal sealed class SshReader(byte[] data)
{
    private int _position;

    public bool AtEnd => _position == data.Length;

    public uint UInt32()
    {
        var value = (uint)(data[_position] << 24 | data[_position + 1] << 16 | data[_position + 2] << 8 | data[_position + 3]);
        _position += 4;
        return value;
    }

    public byte[] Bytes()
    {
        var length = (int)UInt32();
        if (length < 0 || _position + length > data.Length) throw new FormatException("champ trop long");
        var bytes = data[_position..(_position + length)];
        _position += length;
        return bytes;
    }

    public string String() => Encoding.UTF8.GetString(Bytes());
}
