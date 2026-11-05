using SMARX.Models;
using SMARX.Samples;
using SMARX.Services.Linux;
using static SMARX.Services.Linux.LinuxOperation;

namespace SMARX.Tests;

/// <summary>Explications d'un accès, et les droits tels que les montrent ls -l et getfacl.</summary>
public class LinuxExplainerTests
{
    private readonly LinuxHost _host = LinuxSamples.FileServer();

    private IReadOnlyList<string> Explain(string user, string path, LinuxOperation operation, string culture = "fr-FR") =>
        LinuxExplainer.Explain(LinuxAccess.Check(_host, _host.User(user)!, path, operation), culture);

    [Fact]
    public void EveryStep_IsExplained_ThenTheResult()
    {
        var lines = Explain("bob", "/srv/compta/budget.ods", Read);

        Assert.Equal(
        [
            "Traverser / : bob relève des autres (r-x) → x accordé.",
            "Traverser /srv : bob relève des autres (r-x) → x accordé.",
            "Traverser /srv/compta : bob est membre du groupe propriétaire « compta » (rwx) → x accordé.",
            "Lire /srv/compta/budget.ods : bob est membre du groupe propriétaire « compta » (r--) → r accordé.",
            "Résultat : bob peut lire /srv/compta/budget.ods."
        ], lines);
    }

    [Fact]
    public void OwnerDenial_SaysTheGroupDoesNotApplyToTheOwner()
    {
        var lines = Explain("alice", "/srv/compta/notes.txt", Write);

        Assert.Contains(lines, l => l.Contains("alice en est le propriétaire (r--)") && l.Contains("ne s'appliquent pas au propriétaire") && l.EndsWith("il faut w : refusé."));
        Assert.Equal("Résultat : alice ne peut pas modifier le contenu de /srv/compta/notes.txt.", lines[^1]);
    }

    [Fact]
    public void MaskAndStickyBit_AreExplained_InEnglishToo()
    {
        Assert.Contains(Explain("bob", "/srv/partage", Create, "en-US"),
            l => l.Contains("the ACL names bob (rwx, limited by the mask (r-x) to r-x)") && l.Contains("wx needed: denied"));
        Assert.Contains(Explain("bob", "/tmp/alice.txt", Delete),
            l => l.StartsWith("Sticky bit sur /tmp : seuls le propriétaire du fichier (alice), celui du dossier (root) et root peuvent supprimer → refusé"));
    }

    [Fact]
    public void Listing_ShowsSpecialBitsAndAcls_LikeLsAndGetfacl()
    {
        Assert.Equal("drwxrws---", LinuxListing.Mode(_host.Find("/srv/compta")!));
        Assert.Equal("drwxrwxrwt", LinuxListing.Mode(_host.Find("/tmp")!));
        Assert.Equal("drwxr-x---+", LinuxListing.Mode(_host.Find("/srv/partage")!));
        Assert.Equal("-rw-r--r--", LinuxListing.Mode(_host.Find("/opt/noexec.sh")!));

        var acl = LinuxListing.Getfacl(_host.Find("/srv/partage")!, "/srv/partage");
        Assert.Contains("user:bob:rwx\t#effective:r-x", acl);
        Assert.Contains("mask::r-x", acl);
        Assert.Contains("default:group:rh:r-x", LinuxListing.Getfacl(_host.Find("/srv/projets")!, "/srv/projets"));
        Assert.Contains("# flags: -s-", LinuxListing.Getfacl(_host.Find("/srv/projets")!, "/srv/projets"));
    }

    [Fact]
    public void Perms_AndModes_RoundTrip()
    {
        Assert.Equal(Perm.Read | Perm.Execute, Perms.Parse("r-x"));
        Assert.Equal("rw-", Perms.Format(Perms.Parse("rw")));
        Assert.Throws<FormatException>(() => Perms.Parse("rwz"));

        var node = new LinuxNode { Mode = "2770" };
        Assert.True(node.SetGid);
        node.ModeBits = 0x3FF; // 1777
        Assert.Equal("1777", node.Mode);
    }
}
