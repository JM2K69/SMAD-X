using SMARX.Models;
using SMARX.Samples;
using SMARX.Services.Linux;

namespace SMARX.Tests;

/// <summary>
/// Le moteur donne les mêmes résultats que le noyau Linux. Fidelity/linux-file-server.txt a été produit par
/// tools/SmarxKernelCheck sur le serveur d'exemple, dans un conteneur Debian : chaque opération tentée sous chaque
/// compte (ACCESS), et ce que reçoit un fichier ou un dossier créé dans chaque dossier (CREATE : mode, propriétaire,
/// groupe, ACL lue par getfacl).
/// </summary>
public class LinuxKernelFidelityTests
{
    private static readonly string[] Lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Fidelity", "linux-file-server.txt"));

    [Fact]
    public void EveryOperation_IsAllowedOrDenied_AsByTheKernel()
    {
        var host = LinuxSamples.FileServer();
        var accesses = Lines.Where(l => l.StartsWith("ACCESS ")).Select(l => l.Split(' ')).ToList();
        Assert.True(accesses.Count > 900, $"{accesses.Count} accès seulement");

        var differences = accesses
            .Where(a => LinuxAccess.Check(host, host.User(a[1])!, a[3], Enum.Parse<LinuxOperation>(a[2])).Allowed != (a[4] == "yes"))
            .Select(a => string.Join(' ', a))
            .ToList();
        Assert.True(differences.Count == 0, "Le noyau dit autrement :\n" + string.Join('\n', differences));
    }

    [Fact]
    public void EveryCreatedFileAndDirectory_GetsTheSameModeGroupAndAcl_AsOnTheKernel()
    {
        var host = LinuxSamples.FileServer();
        var creations = Lines.Where(l => l.StartsWith("CREATE ")).Select(l => l.Split(' ')).ToList();
        Assert.True(creations.Count > 100, $"{creations.Count} créations seulement");

        var differences = new List<string>();
        foreach (var c in creations)
        {
            var (user, kind, directory) = (host.User(c[1])!, Enum.Parse<LinuxNodeKind>(c[2]), c[3]);
            var created = LinuxCreation.Create(host, user, directory, ".new", kind).Node;
            var expected = c[4] == "denied" ? "denied" : $"{c[4].PadLeft(4, '0')} {c[5]} {c[6]} {c[7]}";
            var actual = created == null ? "denied" : $"{created.Mode} {created.Owner} {created.Group} {Acl(created, directory)}";
            if (actual != expected)
                differences.Add($"{c[1]} {c[2]} {directory} : noyau {expected}, SMAR-X {actual}");
        }
        Assert.True(differences.Count == 0, "Le noyau dit autrement :\n" + string.Join('\n', differences));
    }

    /// <summary>Entrées de getfacl -c, sans les commentaires « #effective », sur une ligne.</summary>
    private static string Acl(LinuxNode node, string directory) =>
        string.Join(',', LinuxListing.Getfacl(node, LinuxHost.Combine(directory, node.Name))
            .Where(l => !l.StartsWith('#'))
            .Select(l => l.Split('\t')[0]));
}
