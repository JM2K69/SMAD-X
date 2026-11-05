using SMARX.Models;
using SMARX.Samples;
using SMARX.Services.Linux;

namespace SMARX.Tests;

/// <summary>Ce que reçoit un fichier ou un dossier créé : umask(2), le bit setgid d'un dossier, l'ACL par défaut (acl(5)).</summary>
public class LinuxCreationTests
{
    private readonly LinuxHost _host = LinuxSamples.FileServer();

    private CreationResult Create(string user, string directory, string name, LinuxNodeKind kind = LinuxNodeKind.File) =>
        LinuxCreation.Create(_host, _host.User(user)!, directory, name, kind);

    [Fact]
    public void Umask_IsTakenFromTheRequestedMode_AndTheGroupIsThePrimaryGroup()
    {
        var file = Create("alice", "/tmp", "a.txt").Node!;
        Assert.Equal(("alice", "alice", "0644"), (file.Owner, file.Group, file.Mode));

        var directory = Create("alice", "/tmp", "d", LinuxNodeKind.Directory).Node!;
        Assert.Equal("0755", directory.Mode);

        // dave : umask 077
        Assert.Equal("0600", Create("dave", "/home/dave", "secret.txt").Node!.Mode);
    }

    [Fact]
    public void SetGidDirectory_GivesItsGroup_AndPassesTheBitToSubdirectories()
    {
        var file = Create("alice", "/srv/compta", "facture.pdf");
        Assert.True(file.GroupFromSetGid);
        Assert.Equal(("compta", "0644"), (file.Node!.Group, file.Node.Mode));

        var directory = Create("alice", "/srv/compta", "2026", LinuxNodeKind.Directory).Node!;
        Assert.Equal(("compta", "2755"), (directory.Group, directory.Mode));
    }

    [Fact]
    public void DefaultAcl_ReplacesTheUmask_AndBecomesTheNewAcl()
    {
        // projets : ACL par défaut user::rwx, group::rwx, group:rh:r-x, mask::rwx, other::---
        var file = Create("dave", "/srv/projets", "x");
        Assert.False(file.Access.Allowed); // dave n'est pas du groupe projet

        var created = Create("alice", "/srv/projets", "plan.md");
        Assert.True(created.FromDefaultAcl);
        Assert.False(created.UmaskApplied);
        var node = created.Node!;
        // touch demande 0666 : le propriétaire et le masque perdent x, les autres restent à ---
        Assert.Equal(("projet", "0660"), (node.Group, node.Mode));
        Assert.Equal("rwx", node.Acl!.GroupObj);
        Assert.Equal("r-x", node.Acl.Groups.Single(g => g.Name == "rh").Perm);
        Assert.Null(node.DefaultAcl);

        var directory = Create("alice", "/srv/projets", "lot1", LinuxNodeKind.Directory).Node!;
        Assert.Equal("2770", directory.Mode);
        Assert.NotNull(directory.DefaultAcl);
    }

    [Fact]
    public void DefaultAcl_WithAnUmaskOf077_StillOpensToTheGroup()
    {
        _host.User("alice")!.Umask = "077";

        Assert.Equal("0660", Create("alice", "/srv/projets", "plan.md").Node!.Mode);
    }

    [Fact]
    public void Creating_NeedsWriteAndExecuteOnTheDirectory()
    {
        var result = Create("carol", "/srv/rh", "x.txt");

        Assert.False(result.Access.Allowed);
        Assert.Null(result.Node);
    }
}
