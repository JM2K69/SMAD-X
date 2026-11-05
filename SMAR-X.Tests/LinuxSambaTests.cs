using SMARX.Models;
using SMARX.Samples;
using SMARX.Services.Linux;
using static SMARX.Services.Linux.LinuxOperation;

namespace SMARX.Tests;

/// <summary>
/// Les partages Samba du serveur d'exemple : Samba décide d'abord (valid users, read only, write list), puis le noyau,
/// sous l'identité Unix que Samba choisit (force group) ; hide unreadable cache ce qu'on ne peut pas lire.
/// </summary>
public class LinuxSambaTests
{
    private readonly LinuxHost _host = LinuxSamples.FileServer();

    private SambaAccessResult Try(string user, string share, string path, LinuxOperation operation) =>
        SambaAccess.Check(_host, _host.User(user)!, _host.Share(share)!, path, operation);

    [Fact]
    public void ValidUsers_AndEnteringTheShare_DecideTheConnection()
    {
        Assert.Equal(SambaRefusal.NotValidUser, Try("carol", "compta", "/srv/compta", List).Refusal);       // pas dans @compta
        Assert.Equal(SambaRefusal.CannotEnterShare, Try("alice", "partage", "/srv/partage", List).Refusal); // pas de x sur /srv/partage
        Assert.True(Try("carol", "partage", "/srv/partage", List).Allowed);                                  // rh : r-x
    }

    [Fact]
    public void WriteList_GivesNoUnixRight()
    {
        // bob est dans la write list de partage (en lecture seule), mais l'ACL du dossier, bridée par le masque r-x, ne
        // lui laisse pas créer ; carol, qui écrirait doc.txt par rh, n'est pas dans la write list
        var bob = Try("bob", "partage", "/srv/partage", Create);
        Assert.True(bob.Writable);
        Assert.False(bob.Allowed);
        Assert.Equal(SambaRefusal.None, bob.Refusal);
        Assert.Equal(SambaRefusal.ReadOnly, Try("carol", "partage", "/srv/partage/doc.txt", Write).Refusal);
    }

    [Fact]
    public void WritingAFile_OpensItReadWrite()
    {
        // doc.txt : bob lit par compta (r--) et écrit par rh (-w-) ; une même entrée doit donner r et w
        Assert.False(Try("bob", "partage", "/srv/partage/doc.txt", Write).Allowed);
        Assert.True(LinuxAccess.Check(_host, _host.User("bob")!, "/srv/partage/doc.txt", Write).Allowed);   // en local, w suffit
    }

    [Fact]
    public void HideUnreadable_JudgesLikeWindows()
    {
        // annonce.txt (0604, groupe compta) : alice, du groupe, ne peut pas la lire, mais « autres : r » la rend visible
        var visible = SambaAccess.List(_host, _host.User("alice")!, _host.Share("public")!, "/srv").Where(e => e.Visible).Select(e => e.Node.Name);
        Assert.Equal(["annonce.txt", "compta", "projets"], visible.Order(StringComparer.Ordinal));
        Assert.False(Try("alice", "public", "/srv/annonce.txt", Read).Allowed);
    }

    [Fact]
    public void Creation_UsesTheShareMasks_ForceGroup_AndNoUmask()
    {
        var dave = SambaAccess.Create(_host, _host.User("dave")!, _host.Share("depot")!, "/tmp", "a.txt", LinuxNodeKind.File).Created!.Node!;
        Assert.Equal(("0744", "dave", "dave"), (dave.Mode, dave.Owner, dave.Group));   // umask 077 de dave ignorée ; x par l'attribut archive

        var alice = SambaAccess.Create(_host, _host.User("alice")!, _host.Share("projets")!, "/srv/projets", "b.txt", LinuxNodeKind.File).Created!.Node!;
        Assert.Equal(("0660", "projet"), (alice.Mode, alice.Group));
        Assert.Equal(0x1B0, SambaAccess.RequestedMode(_host.Share("compta")!, LinuxNodeKind.File));   // (0666 | 0100) & 0660
    }
}
