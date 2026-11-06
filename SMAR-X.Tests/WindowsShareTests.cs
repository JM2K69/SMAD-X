using SMARX.Models;
using SMARX.Samples;
using SMARX.Services.Windows;
using static SMARX.Services.Windows.WindowsOperation;

namespace SMARX.Tests;

/// <summary>
/// L'accès par les partages du serveur d'exemple, avec ses comptes locaux : le partage et NTFS doivent accorder tous deux,
/// l'UAC filtre à distance un administrateur local, l'énumération basée sur l'accès cache ce qu'on ne peut pas lire.
/// </summary>
public class WindowsShareTests
{
    private readonly WindowsHost _host = WindowsSamples.FileServer();

    private ShareAccessResult Try(string user, string share, string path, WindowsOperation operation) =>
        SmbAccess.Check(_host, _host.User(user)!, _host.Share(share)!, path, operation);

    [Fact]
    public void SameFile_TwoShares_TheMostRestrictiveWins()
    {
        // dave a Modification explicite sur planning.xlsx : Partages (Modifier) le laisse écrire, Projets (Lecture) non
        var throughPartages = Try("dave", "Partages", @"D:\Partages\Projets\planning.xlsx", Write);
        var throughProjets = Try("dave", "Projets", @"D:\Partages\Projets\planning.xlsx", Write);
        Assert.True(throughPartages.Allowed);
        Assert.False(throughProjets.Allowed);
        Assert.True(throughProjets.Ntfs.Allowed);          // NTFS l'accorde…
        Assert.False(throughProjets.ShareCheck.Granted);   // … le partage non
        Assert.Equal(@"\\SRV-FIC\Projets\planning.xlsx", throughProjets.Unc);
        Assert.True(WindowsAccess.Check(_host, _host.User("dave")!, @"D:\Partages\Projets\planning.xlsx", Write).Allowed);
    }

    [Fact]
    public void RemoteToken_HasNetwork_AndUacFiltersALocalAdministrator()
    {
        var carol = _host.User("carol")!;
        var token = WindowsToken.Remote(_host, carol);
        Assert.True(token.Has(WellKnown.Network, forAllow: true));
        Assert.False(token.Has(WellKnown.Interactive, forAllow: true));
        Assert.False(token.Has(WellKnown.Administrators, forAllow: true));   // compte local : refus seulement

        // Compta$ est large (Utilisateurs authentifiés) : carol s'y connecte, mais NTFS ne lui donne rien tant qu'elle est filtrée
        Assert.True(Try("carol", "Compta$", @"D:\Partages\Compta", List).Connected);
        Assert.False(Try("carol", "Compta$", @"D:\Partages\Compta", List).Allowed);
        Assert.True(Try("Administrator", "Compta$", @"D:\Partages\Compta", List).Allowed);   // compte intégré, non filtré

        carol.Domain = true;   // un compte du domaine n'est pas filtré à distance
        Assert.True(Try("carol", "Compta$", @"D:\Partages\Compta", List).Allowed);
    }

    /// <summary>
    /// Par SMB, Synchroniser seul se décide comme Attributs de lecture (mesuré) : par Compta$, dave lit les attributs de
    /// D:\Partages\Compta par son dossier parent, et l'ouverture avec Synchroniser réussit ; Parcours, non.
    /// </summary>
    [Fact]
    public void Synchronize_ThroughAShare_IsDecidedLikeReadAttributes()
    {
        var dave = WindowsToken.Remote(_host, _host.User("dave")!);
        var compta = _host.Share("Compta$")!;
        Assert.True(SmbAccess.Open(_host, dave, compta, @"D:\Partages\Compta", NtfsRights.ReadAttributes));
        Assert.True(SmbAccess.Open(_host, dave, compta, @"D:\Partages\Compta", NtfsRights.Synchronize));
        Assert.False(SmbAccess.Open(_host, dave, compta, @"D:\Partages\Compta", NtfsRights.ExecuteFile));
    }

    [Fact]
    public void AccessBasedEnumeration_HidesWhatTheAccountCannotRead_AndMakesItNotFound()
    {
        var alice = WindowsToken.Remote(_host, _host.User("alice")!);
        var partages = _host.Share("Partages")!;
        var visible = SmbAccess.List(_host, alice, partages, @"D:\Partages").Where(e => e.Visible).Select(e => e.Node.Name);
        Assert.Equal(new[] { "Commun", "Compta", "Projets" }, visible.Order());

        // RH est introuvable pour alice par ce partage, quel que soit le droit ; annonce-rh.txt, lisible, reste accessible
        Assert.True(Try("alice", "Partages", @"D:\Partages\RH", List).Hidden);
        Assert.False(SmbAccess.Open(_host, alice, partages, @"D:\Partages\RH", NtfsRights.ReadAttributes));
        Assert.True(Try("alice", "Partages", @"D:\Partages\RH\annonce-rh.txt", Read).Allowed);

        // Sans l'énumération basée sur l'accès, RH serait visible (et l'accès simplement refusé)
        partages.AccessBasedEnumeration = false;
        Assert.Contains(SmbAccess.List(_host, alice, partages, @"D:\Partages"), e => e.Node.Name == "RH" && e.Visible);
        Assert.False(Try("alice", "Partages", @"D:\Partages\RH", List).Hidden);
    }

    [Fact]
    public void SharedFolder_CannotBeDeleted_ThroughItsOwnShare()
    {
        var result = Try("Administrator", "Projets", @"D:\Partages\Projets", Delete);
        Assert.False(result.Allowed);
        Assert.False(result.InUse);   // Projets ne donne que Lecture : refusé avant d'être « en cours d'utilisation »
        var throughCompta = Try("Administrator", "Compta$", @"D:\Partages\Compta", Delete);
        Assert.True(throughCompta.InUse);
        Assert.False(throughCompta.Allowed);
    }
}
