using SMARX.Models;
using SMARX.Samples;
using SMARX.Services.Windows;
using static SMARX.Models.AceInheritance;
using static SMARX.Services.Windows.WindowsOperation;

namespace SMARX.Tests;

/// <summary>
/// Accès local à un serveur Windows, cas par cas, d'après le comportement documenté de NTFS : AccessCheck (ordre des
/// ACE, refus d'un droit pas encore accordé), héritage (OI, CI, IO, NP, CREATOR OWNER), droits implicites du
/// propriétaire et OWNER RIGHTS, UAC, privilèges « Contourner la vérification de parcours » et « Prendre possession ».
/// </summary>
public class WindowsAccessTests
{
    private readonly WindowsHost _host = WindowsSamples.FileServer();

    private WindowsAccessResult Try(string user, string path, WindowsOperation operation, bool elevated = false) =>
        WindowsAccess.Check(_host, _host.User(user)!, path, operation, elevated);

    private bool Can(string user, string path, WindowsOperation operation, bool elevated = false) => Try(user, path, operation, elevated).Allowed;

    [Fact]
    public void GroupPermission_ComesFromTheProtectedShare()
    {
        Assert.True(Can("alice", @"D:\Partages\Compta\budget.xlsx", Write));
        var dave = Try("dave", @"D:\Partages\Compta\budget.xlsx", Read);
        Assert.False(dave.Allowed);
        Assert.Equal(CheckStepKind.Missing, dave.Checks[0].Steps[^1].Kind);
    }

    [Fact]
    public void InheritedDeny_BlocksWhatItDenies_AndNothingElse()
    {
        Assert.False(Can("alice", @"D:\Partages\Compta\Archives\bilan-2025.xlsx", Write));
        Assert.True(Can("alice", @"D:\Partages\Compta\Archives\bilan-2025.xlsx", Read));
        var steps = Try("alice", @"D:\Partages\Compta\Archives\bilan-2025.xlsx", Write).Checks[0].Steps;
        Assert.Equal(CheckStepKind.DenyBlocks, steps[^1].Kind);
        Assert.Equal(@"D:\Partages\Compta\Archives", steps[^1].Ace!.InheritedFrom);
    }

    [Fact]
    public void ExplicitAllow_IsReadBeforeTheInheritedDeny()
    {
        Assert.True(Can("dave", @"D:\Partages\Projets\planning.xlsx", Write));
        Assert.False(Can("dave", @"D:\Partages\Projets\notes.txt", Write));
        // Refuser Écriture ne refuse pas Suppression : dave supprime ce qu'il ne peut pas modifier
        Assert.True(Can("dave", @"D:\Partages\Projets\notes.txt", Delete));
    }

    [Fact]
    public void CreatorOwner_GivesTheCreatorItsFile_AndNothingToTheOthers()
    {
        Assert.True(Can("alice", @"D:\Partages\Commun\rapport-alice.docx", Write));
        Assert.False(Can("bob", @"D:\Partages\Commun\rapport-alice.docx", Read));
        Assert.True(Can("bob", @"D:\Partages\Commun", List));
        Assert.True(Can("bob", @"D:\Partages\Commun", CreateFile));
        Assert.False(Can("bob", @"D:\Partages\Commun\rapport-alice.docx", Delete));

        var created = WindowsCreation.Create(_host, _host.User("bob")!, @"D:\Partages\Commun", "note-bob.txt", NtfsNodeKind.File);
        Assert.Equal("bob", created.Node!.Owner);
        Assert.True(Can("bob", created.Path, Write));
        Assert.False(Can("alice", created.Path, Read));
    }

    [Fact]
    public void FoldersOfThePath_AreNotChecked()
    {
        // alice ne peut rien lister dans D:\Partages\RH, mais lit le fichier qu'on lui ouvre
        Assert.False(Can("alice", @"D:\Partages\RH", List));
        Assert.True(Can("alice", @"D:\Partages\RH\annonce-rh.txt", Read));
    }

    [Fact]
    public void Uac_KeepsAdministratorsDenyOnly_UntilTheSessionIsElevated()
    {
        Assert.False(Can("carol", @"D:\Admin", List));
        Assert.True(Can("carol", @"D:\Admin", List, elevated: true));
        Assert.True(Can("Administrator", @"D:\Admin", List)); // le compte intégré n'est pas filtré
        Assert.Contains(Try("carol", @"D:\Admin", List).Token.Sids, s => s.Name == WellKnown.Administrators && s.DenyOnly);
    }

    [Fact]
    public void Owner_AlwaysChangesThePermissions_AndElevatedAdministratorsTakeOwnership()
    {
        Assert.True(Can("alice", @"D:\Partages\Compta\budget.xlsx", ChangePermissions));
        Assert.Equal(CheckStepKind.Owner, Try("alice", @"D:\Partages\Compta\budget.xlsx", ChangePermissions).Checks[0].Steps[0].Kind);
        Assert.False(Can("bob", @"D:\Partages\Compta\budget.xlsx", ChangePermissions));
        Assert.False(Can("alice", @"D:\Partages\Compta\budget.xlsx", TakeOwnership));
        Assert.True(Can("carol", @"D:\Partages\RH\salaires.xlsx", TakeOwnership, elevated: true));
    }

    [Fact]
    public void OwnerRights_ReplacesTheOwnersImplicitRights()
    {
        var host = new WindowsHost { Name = "S", Users = [new WindowsUser { Name = "alice" }] };
        host.Root = NtfsNode.Directory("D:", WellKnown.Administrators, NtfsAce.Allow(WellKnown.OwnerRights, "R")).With(NtfsNode.File("a.txt", "alice"));

        Assert.False(WindowsAccess.Check(host, host.User("alice")!, @"D:\a.txt", ChangePermissions).Allowed);
        Assert.True(WindowsAccess.Check(host, host.User("alice")!, @"D:\a.txt", Read).Allowed);
    }

    [Fact]
    public void Inheritance_FollowsObjectContainerInheritOnlyAndNoPropagate()
    {
        var host = new WindowsHost { Name = "S", Users = [new WindowsUser { Name = "u" }, new WindowsUser { Name = "v" }] };
        host.Root = NtfsNode.Directory("D:", WellKnown.Administrators,
                NtfsAce.Allow("u", "R", ObjectInherit),                 // les fichiers seulement, à tous les niveaux
                NtfsAce.Allow("v", "R", ThisFolderSubfoldersAndFiles | NoPropagate))
            .With(NtfsNode.Directory("A", WellKnown.Administrators).With(
                NtfsNode.File("f.txt", WellKnown.Administrators),
                NtfsNode.Directory("B", WellKnown.Administrators).With(NtfsNode.File("g.txt", WellKnown.Administrators))));
        bool Can2(string user, string path, WindowsOperation op) => WindowsAccess.Check(host, host.User(user)!, path, op).Allowed;

        Assert.True(Can2("u", @"D:\", List));
        Assert.False(Can2("u", @"D:\A", List));            // A n'en garde qu'une copie « héritage seulement »
        Assert.True(Can2("u", @"D:\A\B\g.txt", Read));     // transmise aux fichiers, même plus bas
        Assert.Contains("(I)(OI)(IO)(R)", WindowsListing.Icacls(host, @"D:\A").Single(l => l.Contains("S\\u:")));

        Assert.True(Can2("v", @"D:\A", List));             // (NP) : les enfants directs
        Assert.False(Can2("v", @"D:\A\f.txt", Read));    // f.txt n'est pas un enfant direct de D:
        Assert.False(Can2("v", @"D:\A\B", List));
    }

    [Fact]
    public void Icacls_ShowsInheritedAndDenyEntries_InCanonicalOrder()
    {
        var lines = WindowsListing.Icacls(_host, @"D:\Partages\Compta\Archives\bilan-2025.xlsx");

        Assert.StartsWith(@"D:\Partages\Compta\Archives\bilan-2025.xlsx SRV-FIC\GL_Compta:(I)(DENY)(WD,AD,WA,WEA)", lines[0]);
        Assert.Contains(lines, l => l.EndsWith(@"BUILTIN\Administrators:(I)(F)"));
        Assert.Contains(lines, l => l.EndsWith(@"SRV-FIC\GL_Compta:(I)(M)"));
        Assert.Contains("icacls \"D:\\Partages\\Compta\" /inheritance:r", WindowsListing.Commands(_host, _host.Find(@"D:\Partages\Compta")!, @"D:\Partages\Compta"));
    }

    [Fact]
    public void Explanation_ShowsTheTokenAndEachAceThatApplies()
    {
        var lines = WindowsExplainer.Steps(_host, Try("dave", @"D:\Partages\Projets\notes.txt", Write), "fr-FR");

        Assert.StartsWith("session locale : dave, Tout le monde, Utilisateurs authentifiés, INTERACTIF, Utilisateurs, GL_Stagiaires", lines[0].Detail);
        Assert.Contains(lines, s => s.Title == @"ACE 1, héritée de D:\Partages\Projets" && s.Detail.StartsWith("Refuser « Écriture » à GL_Stagiaires → refuse"));
        Assert.Contains("refus seulement", WindowsExplainer.Steps(_host, Try("carol", @"D:\Admin", List), "fr-FR")[0].Detail);
        Assert.Equal("Résultat : dave ne peut pas modifier le contenu de D:\\Partages\\Projets\\notes.txt.",
            WindowsExplainer.Verdict(Try("dave", @"D:\Partages\Projets\notes.txt", Write), "fr-FR"));
    }

    [Fact]
    public void Matrix_ShowsEveryAccountOnTheUsualOperations()
    {
        var rows = WindowsAccess.Matrix(_host, @"D:\Partages\Commun");

        Assert.Equal(_host.Users.Select(u => u.Name), rows.Select(r => r.User.Name));
        Assert.Equal(WindowsAccess.FolderOperations, rows[0].Results.Keys);
        Assert.True(rows.Single(r => r.User.Name == "dave").Results[CreateFile].Allowed);
        Assert.False(rows.Single(r => r.User.Name == "dave").Results[ChangePermissions].Allowed);
    }
}
