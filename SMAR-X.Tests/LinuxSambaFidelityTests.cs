using SMARX.Models;
using SMARX.Samples;
using SMARX.Services.Linux;

namespace SMARX.Tests;

/// <summary>
/// L'accès par les partages Samba donne les mêmes résultats qu'un vrai Samba. Fidelity/linux-samba.txt a été produit par
/// tools/SmarxSambaCheck : le serveur d'exemple et ses partages recréés dans un conteneur Debian 13 (Samba 4.22), puis,
/// avec smbclient sous chaque compte et par chaque partage, chaque fichier lu, écrit et supprimé, chaque dossier listé et
/// supprimé (SMB), un fichier et un dossier créés dans chaque dossier (SCREATE : mode, propriétaire, groupe, ACL), et la
/// racine de chaque partage listée à la connexion (CONNECT).
/// </summary>
public class LinuxSambaFidelityTests
{
    private static readonly string[] Lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Fidelity", "linux-samba.txt"));
    private readonly LinuxHost _host = LinuxSamples.FileServer();

    /// <summary>« yes » ou « no » ; une liste réussie donne les noms visibles, séparés par « / ».</summary>
    private string Result(string user, string share, LinuxOperation operation, string path)
    {
        var result = SambaAccess.Check(_host, _host.User(user)!, _host.Share(share)!, path, operation);
        if (!result.Allowed) return "no";
        return operation == LinuxOperation.List
            ? string.Join('/', SambaAccess.List(_host, _host.User(user)!, _host.Share(share)!, path).Where(e => e.Visible).Select(e => e.Node.Name).Order(StringComparer.Ordinal))
            : "yes";
    }

    /// <summary>Samba répond NT_STATUS_ACCESS_DENIED ; smbclient « del » liste d'abord le dossier, et ne trouve pas un fichier caché.</summary>
    private static string Measured(string value) => value is "NT_STATUS_ACCESS_DENIED" or "NT_STATUS_NO_SUCH_FILE" ? "no" : value;

    [Fact]
    public void EveryOperationThroughEveryShare_IsWhatSambaDoes()
    {
        var operations = Lines.Where(l => l.StartsWith("SMB|")).Select(l => l.Split('|')).ToList();
        Assert.True(operations.Count >= 150, $"{operations.Count} opérations seulement");
        Assert.All(operations, o => Assert.True(o[5] == "yes" || !o[5].StartsWith("NT_") || Measured(o[5]) == "no", string.Join('|', o)));

        var differences = new List<string>();
        foreach (var o in operations)
        {
            var actual = Result(o[1], o[2], Enum.Parse<LinuxOperation>(o[3]), o[4]);
            if (actual != Measured(o[5]))
                differences.Add(string.Join('|', o) + $" ; SMAR-X : {actual}");
        }
        foreach (var c in Lines.Where(l => l.StartsWith("CONNECT|")).Select(l => l.Split('|')))
        {
            var root = _host.Share(c[2])!.Path;
            var actual = Result(c[1], c[2], LinuxOperation.List, root) == "no" ? "no" : "yes";
            if (actual != Measured(c[3]))
                differences.Add(string.Join('|', c) + $" ; SMAR-X : {actual}");
        }
        Assert.True(differences.Count == 0, "Samba dit autrement :\n" + string.Join('\n', differences));
    }

    [Fact]
    public void EveryFileAndDirectoryCreatedThroughAShare_GetsWhatSambaGives()
    {
        var creations = Lines.Where(l => l.StartsWith("SCREATE|")).Select(l => l.Split('|')).ToList();
        Assert.True(creations.Count >= 70, $"{creations.Count} créations seulement");

        var differences = new List<string>();
        foreach (var c in creations)
        {
            var (user, share, kind, folder) = (_host.User(c[1])!, _host.Share(c[2])!, Enum.Parse<LinuxNodeKind>(c[3]), c[4]);
            var created = SambaAccess.Create(_host, user, share, folder, ".new", kind).Created?.Node;
            var expected = c[5].StartsWith("NT_") ? "denied" : c[5];
            var actual = created == null ? "denied" : $"{Convert.ToString(created.ModeBits, 8)} {created.Owner} {created.Group} {Acl(created, folder)}";
            if (actual != expected)
                differences.Add($"{c[1]} {c[2]} {c[3]} {folder} : Samba {expected}, SMAR-X {actual}");
        }
        Assert.True(differences.Count == 0, "Samba crée autrement :\n" + string.Join('\n', differences));
    }

    /// <summary>Entrées de getfacl -c, sans les commentaires « #effective », sur une ligne.</summary>
    private static string Acl(LinuxNode node, string directory) =>
        string.Join(',', LinuxListing.Getfacl(node, LinuxHost.Combine(directory, node.Name))
            .Where(l => !l.StartsWith('#'))
            .Select(l => l.Split('\t')[0]));
}
