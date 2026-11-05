using SMARX.Models;
using SMARX.Samples;
using SMARX.Services.Linux;
using static SMARX.Services.Linux.LinuxOperation;

namespace SMARX.Tests;

/// <summary>
/// Accès local à un serveur Linux, cas par cas, d'après le comportement documenté du noyau : path_resolution(7),
/// acl(5), unlink(2) et capabilities(7).
/// </summary>
public class LinuxAccessTests
{
    private readonly LinuxHost _host = LinuxSamples.FileServer();

    private AccessResult Try(string user, string path, LinuxOperation operation) => LinuxAccess.Check(_host, _host.User(user)!, path, operation);

    private bool Can(string user, string path, LinuxOperation operation) => Try(user, path, operation).Allowed;

    [Fact]
    public void Owner_GetsOnlyTheOwnerPermissions_EvenWhenTheGroupHasMore()
    {
        // notes.txt : alice r--, compta rw-
        Assert.False(Can("alice", "/srv/compta/notes.txt", Write));
        Assert.True(Can("bob", "/srv/compta/notes.txt", Write));
        var check = Try("alice", "/srv/compta/notes.txt", Write).Steps[^1].Check!;
        Assert.Equal(AccessClass.Owner, check.Entry.Class);
    }

    [Fact]
    public void GroupMember_GetsOnlyTheGroupPermissions_EvenWhenOthersHaveMore()
    {
        // annonce.txt : groupe compta ---, autres r--
        Assert.False(Can("bob", "/srv/annonce.txt", Read));
        Assert.True(Can("carol", "/srv/annonce.txt", Read));
    }

    [Fact]
    public void EveryDirectoryOfThePath_MustBeTraversable()
    {
        var result = Try("carol", "/srv/compta/budget.ods", Read);

        Assert.False(result.Allowed);
        Assert.Equal(AccessStepKind.Traverse, result.Steps[^1].Kind);
        Assert.Equal("/srv/compta", result.Steps[^1].Path);
        Assert.Equal(["/", "/srv", "/srv/compta"], result.Steps.Select(s => s.Path));
    }

    [Fact]
    public void SupplementaryGroups_Count()
    {
        // bob traverse /srv/rh grâce à son groupe secondaire rh, mais salaires.ods ne donne rien au groupe
        Assert.True(Can("bob", "/srv/rh", Enter));
        Assert.False(Can("bob", "/srv/rh/salaires.ods", Read));
        Assert.True(Can("carol", "/srv/rh/salaires.ods", Read));
    }

    [Fact]
    public void ExecuteOnADirectory_LetsYouThrough_ReadLetsYouListIt()
    {
        // /home/dave : 0711, on traverse sans lister
        Assert.True(Can("alice", "/home/dave/public.txt", Read));
        Assert.False(Can("alice", "/home/dave", List));
        // /home/dave/vitrine : 0744, on liste les noms sans pouvoir y entrer
        Assert.True(Can("alice", "/home/dave/vitrine", List));
        Assert.False(Can("alice", "/home/dave/vitrine/photo.png", Read));
    }

    [Fact]
    public void Deleting_DependsOnTheDirectory_NotOnTheFile()
    {
        // budget.ods : bob ne peut pas l'écrire, mais compta a rwx sur le dossier
        Assert.False(Can("bob", "/srv/compta/budget.ods", Write));
        Assert.True(Can("bob", "/srv/compta/budget.ods", Delete));
    }

    [Fact]
    public void StickyBit_OnlyTheOwnersAndRootDelete()
    {
        Assert.False(Can("bob", "/tmp/alice.txt", Delete));
        Assert.True(Can("alice", "/tmp/alice.txt", Delete));
        Assert.True(Can("root", "/tmp/alice.txt", Delete));
        Assert.Equal(AccessStepKind.StickyBit, Try("bob", "/tmp/alice.txt", Delete).Steps[^1].Kind);
    }

    [Fact]
    public void ADirectory_IsDeletedOnlyWhenEmpty()
    {
        var result = Try("root", "/home/dave", Delete);

        Assert.False(result.Allowed);
        Assert.Equal(AccessStepKind.NotEmpty, result.Steps[^1].Kind);
    }

    [Fact]
    public void Permissions_AreChangedByTheOwnerOrRoot_WhateverTheRights()
    {
        Assert.True(Can("alice", "/srv/compta/notes.txt", ChangePermissions));
        Assert.False(Can("bob", "/srv/compta/notes.txt", ChangePermissions)); // il peut l'écrire, pas changer ses droits
        Assert.True(Can("root", "/srv/compta/notes.txt", ChangePermissions));
        Assert.True(Can("alice", "/srv/compta/notes.txt", ChangeGroup));
        Assert.False(Can("alice", "/srv/compta/notes.txt", ChangeOwner));
        Assert.True(Can("root", "/srv/compta/notes.txt", ChangeOwner));
    }

    [Fact]
    public void Root_OverridesThePermissions_ButCannotExecuteAFileWithoutAnyXBit()
    {
        Assert.True(Can("root", "/home/alice/journal.txt", Write));
        Assert.True(Try("root", "/home/alice/journal.txt", Write).Steps[^1].Check!.RootOverride);
        Assert.True(Can("root", "/usr/bin/outil", Execute));
        Assert.False(Can("root", "/opt/noexec.sh", Execute));
        Assert.False(Can("root", "/opt/noexec.sh", ExecuteScript));
    }

    [Fact]
    public void AclNamedUser_IsLimitedByTheMask()
    {
        // partage : bob rwx dans l'ACL, masque r-x
        Assert.True(Can("bob", "/srv/partage", List));
        Assert.False(Can("bob", "/srv/partage", Create));
        var check = Try("bob", "/srv/partage", Create).Steps[^1].Check!;
        Assert.Equal(AccessClass.NamedUser, check.Entry.Class);
        Assert.Equal(Perm.Read | Perm.Execute, check.Effective);
    }

    [Fact]
    public void AclGroups_EachEntryMustBeEnoughOnItsOwn_AndOthersNoLongerApply()
    {
        var bob = _host.User("bob")!;
        var doc = _host.Find("/srv/partage/doc.txt")!;

        // compta donne r--, rh donne -w- : bob lit, bob écrit, mais pas les deux d'un coup
        Assert.True(LinuxPermissions.Check(bob, doc, "/srv/partage/doc.txt", Perm.Read).Granted);
        Assert.True(LinuxPermissions.Check(bob, doc, "/srv/partage/doc.txt", Perm.Write).Granted);
        var both = LinuxPermissions.Check(bob, doc, "/srv/partage/doc.txt", Perm.Read | Perm.Write);
        Assert.False(both.Granted);
        Assert.Equal(2, both.Groups.Count);

        // carol (rh) : son entrée de groupe ne donne pas r, et les autres (r--) ne s'appliquent plus à elle
        var carol = _host.User("carol")!;
        Assert.False(LinuxPermissions.Check(carol, doc, "/srv/partage/doc.txt", Perm.Read).Granted);
        var dave = _host.User("dave")!;
        Assert.True(LinuxPermissions.Check(dave, doc, "/srv/partage/doc.txt", Perm.Read).Granted);
    }

    [Fact]
    public void Problems_AreReported_NotTreatedAsDenials()
    {
        Assert.Equal(AccessProblem.NotFound, Try("alice", "/srv/absent", Read).Problem);
        Assert.Equal(AccessProblem.NotApplicable, Try("alice", "/srv/compta", Read).Problem);
        Assert.Equal(AccessProblem.NotApplicable, Try("root", "/", Delete).Problem);
    }

    [Fact]
    public void Matrix_ShowsEveryAccountOnTheUsualOperations()
    {
        var rows = LinuxAccess.Matrix(_host, "/tmp/alice.txt");

        Assert.Equal(_host.Users.Select(u => u.Name), rows.Select(r => r.User.Name));
        Assert.Equal(LinuxAccess.FileOperations, rows[0].Results.Keys);
        Assert.True(rows.Single(r => r.User.Name == "alice").Results[Delete].Allowed);
        Assert.False(rows.Single(r => r.User.Name == "bob").Results[Delete].Allowed);
        Assert.True(rows.Single(r => r.User.Name == "bob").Results[Read].Allowed);
    }
}
