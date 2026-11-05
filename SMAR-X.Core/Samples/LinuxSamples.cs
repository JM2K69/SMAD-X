using SMARX.Models;
using static SMARX.Models.LinuxNode;

namespace SMARX.Samples;

/// <summary>
/// Serveur de fichiers Linux d'exemple, qui montre chaque règle : la classe du propriétaire, le x d'un dossier,
/// le sticky bit, le bit setgid, une ACL et son masque, une ACL par défaut. Chaque compte a son groupe personnel
/// (comme Debian) ; alice et bob sont à la comptabilité, bob et carol aux ressources humaines, dave dans aucun
/// groupe de service. Le noyau Linux donne les mêmes résultats (SMAR-X.Tests/Fidelity).
/// </summary>
public static class LinuxSamples
{
    public static LinuxHost FileServer()
    {
        var host = new LinuxHost { Name = "srv-fic" };
        foreach (var (name, gid) in new[] { ("root", 0), ("alice", 1001), ("bob", 1002), ("carol", 1003), ("dave", 1004), ("compta", 2001), ("rh", 2002), ("projet", 2003) })
            host.Groups.Add(new LinuxGroup { Name = name, Gid = gid });
        host.Users.Add(new LinuxUser { Name = "root", Uid = 0, PrimaryGroup = "root" });
        host.Users.Add(new LinuxUser { Name = "alice", Uid = 1001, PrimaryGroup = "alice", Groups = ["compta", "projet"] });
        host.Users.Add(new LinuxUser { Name = "bob", Uid = 1002, PrimaryGroup = "bob", Groups = ["compta", "rh", "projet"] });
        host.Users.Add(new LinuxUser { Name = "carol", Uid = 1003, PrimaryGroup = "carol", Groups = ["rh"] });
        host.Users.Add(new LinuxUser { Name = "dave", Uid = 1004, PrimaryGroup = "dave", Umask = "077" });

        var partage = Directory("partage", "root", "root", "0750",
            File("doc.txt", "root", "root", "0664"));
        // ACL : bob rwx, limité par le masque r-x ; le groupe rh lit
        partage.Acl = new PosixAcl { Users = [AclEntry.Of("bob", "rwx")], GroupObj = "r-x", Groups = [AclEntry.Of("rh", "r-x")] };
        // doc.txt : compta lit, rh écrit, chacun séparément ; le masque rw- laisse passer les deux
        partage.Children[0].Acl = new PosixAcl { GroupObj = "---", Groups = [AclEntry.Of("compta", "r--"), AclEntry.Of("rh", "-w-")] };

        var projets = Directory("projets", "root", "projet", "2770");
        projets.DefaultAcl = new PosixDefaultAcl { UserObj = "rwx", GroupObj = "rwx", Groups = [AclEntry.Of("rh", "r-x")], Mask = "rwx", Other = "---" };

        host.Root = Directory("", "root", "root", "0755",
            Directory("srv", "root", "root", "0755",
                Directory("compta", "root", "compta", "2770",
                    File("budget.ods", "alice", "compta", "0640"),
                    File("notes.txt", "alice", "compta", "0460")),
                Directory("rh", "root", "rh", "0750",
                    File("salaires.ods", "carol", "rh", "0600")),
                File("annonce.txt", "root", "compta", "0604"),
                partage,
                projets),
            Directory("tmp", "root", "root", "1777",
                File("alice.txt", "alice", "alice", "0644")),
            Directory("home", "root", "root", "0755",
                Directory("alice", "alice", "alice", "0700",
                    File("journal.txt", "alice", "alice", "0600")),
                Directory("dave", "dave", "dave", "0711",
                    File("public.txt", "dave", "dave", "0644"),
                    Directory("vitrine", "dave", "dave", "0744",
                        File("photo.png", "dave", "dave", "0644")))),
            Directory("usr", "root", "root", "0755",
                Directory("bin", "root", "root", "0755",
                    File("outil", "root", "root", "0755"))),
            Directory("opt", "root", "root", "0755",
                File("noexec.sh", "root", "root", "0644")));
        return host;
    }
}
