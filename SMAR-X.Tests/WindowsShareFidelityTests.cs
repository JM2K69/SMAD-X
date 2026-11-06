using SMARX.Models;
using SMARX.Samples;
using SMARX.Services.Windows;

namespace SMARX.Tests;

/// <summary>
/// L'accès par les partages donne les mêmes résultats qu'un vrai Windows Server. Fidelity/windows-shares.txt a été produit
/// par tools/SmarxWindowsCheck --network : le serveur d'exemple et ses partages recréés sur un contrôleur de domaine
/// Windows Server 2025, puis, depuis un poste Windows 11 du domaine, chaque objet de chaque partage ouvert avec chacun des
/// 14 droits spéciaux sous l'identité réseau de chaque compte (NACCESS), et chaque dossier listé par le partage (NLIST).
/// Les comptes du contrôle sont des comptes du domaine : l'UAC ne filtre pas leur jeton à distance.
/// </summary>
public class WindowsShareFidelityTests
{
    private static readonly string[] Lines = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Fidelity", "windows-shares.txt"));

    private const string NotFound = "err-C0000034", InUse = "err-C0000043";

    private static WindowsHost Host()
    {
        var host = WindowsSamples.FileServer();
        foreach (var user in host.Users) user.Domain = true;
        return host;
    }

    [Fact]
    public void EveryRightThroughEveryShare_IsGrantedOrDenied_AsByWindows()
    {
        var host = Host();
        var accesses = Lines.Where(l => l.StartsWith("NACCESS|")).Select(l => l.Split('|')).ToList();
        Assert.True(accesses.Count >= 1500, $"{accesses.Count} accès seulement");
        // Deux erreurs seulement, chacune une règle : objet caché par l'ABE (introuvable), dossier partagé ouvert pour le
        // supprimer (violation de partage : l'accès est accordé, mais le serveur garde le dossier ouvert tant qu'un client
        // est connecté au partage ; toujours par ce partage-là, et pour le dossier d'un autre partage selon ses connexions)
        Assert.All(accesses.Where(a => a[5].StartsWith("err")), a => Assert.Contains(a[5], new[] { NotFound, InUse }));

        var differences = new List<string>();
        foreach (var a in accesses)
        {
            var (token, share, path) = (WindowsToken.Remote(host, host.User(a[1])!), host.Share(a[2])!, a[3]);
            var right = Ntfs.Special.Single(s => s.Code == a[4]).Right;
            var actual = SmbAccess.Hidden(host, token, share, path) ? NotFound
                : !SmbAccess.Open(host, token, share, path, right) ? "no"
                : right == NtfsRights.Delete && string.Equals(share.Path, path, StringComparison.OrdinalIgnoreCase) ? InUse
                : "yes";
            var otherShareInUse = actual == "yes" && a[5] == InUse && right == NtfsRights.Delete && SmbAccess.IsShared(host, path);
            if (actual != a[5] && !otherShareInUse)
                differences.Add(string.Join('|', a) + $" ; SMAR-X : {actual}");
        }
        Assert.True(differences.Count == 0, "Windows dit autrement :\n" + string.Join('\n', differences));
    }

    /// <summary>Lister un dossier par le partage : introuvable (caché), refusé, ou les noms visibles (l'énumération basée sur l'accès cache le reste).</summary>
    [Fact]
    public void ListingThroughEveryShare_ShowsWhatWindowsShows()
    {
        var host = Host();
        var listings = Lines.Where(l => l.StartsWith("NLIST|")).Select(l => l.Split('|')).ToList();
        Assert.NotEmpty(listings);

        var differences = new List<string>();
        foreach (var l in listings)
        {
            var (token, share, folder) = (WindowsToken.Remote(host, host.User(l[1])!), host.Share(l[2])!, l[3]);
            var shown = SmbAccess.Hidden(host, token, share, folder) ? "!DirectoryNotFoundException"
                : SmbAccess.Open(host, token, share, folder, NtfsRights.ReadData)
                ? string.Join('/', SmbAccess.List(host, token, share, folder).Where(e => e.Visible).Select(e => e.Node.Name).Order(StringComparer.OrdinalIgnoreCase))
                : "!denied";
            if (shown != l[4])
                differences.Add($"{l[1]} {l[2]} {folder}\n  Windows : {l[4]}\n  SMAR-X  : {shown}");
        }
        Assert.True(differences.Count == 0, "Windows liste autrement :\n" + string.Join('\n', differences));
    }
}
