using SMARX.Models;
using SMARX.Services.Linux;
using SMARX.Services.Windows;
using SMARX.ViewModels;

namespace SMARX.Tests;

/// <summary>
/// Les deux espaces de SMAR-X : modifier les groupes d'un compte, créer un compte ou un groupe, et, sous Windows,
/// tester une session élevée, ajouter ou modifier une ACE, désactiver l'héritage. Tout se recalcule aussitôt.
/// </summary>
[Collection(InterfaceTests.Name)]
public class WorkspaceEditingTests
{
    private static NodeItem FindLinux(NodeItem item, string path) =>
        item.Path == path ? item : item.Children.Select(c => FindLinuxOrNull(c, path)).First(f => f != null)!;

    private static NodeItem? FindLinuxOrNull(NodeItem item, string path) =>
        item.Path == path ? item : item.Children.Select(c => FindLinuxOrNull(c, path)).FirstOrDefault(f => f != null);

    private static NtfsItem FindWindows(NtfsItem item, string path) =>
        string.Equals(item.Path, path, StringComparison.OrdinalIgnoreCase) ? item : item.Children.Select(c => FindWindowsOrNull(c, path)).First(f => f != null)!;

    private static NtfsItem? FindWindowsOrNull(NtfsItem item, string path) =>
        string.Equals(item.Path, path, StringComparison.OrdinalIgnoreCase) ? item : item.Children.Select(c => FindWindowsOrNull(c, path)).FirstOrDefault(f => f != null);

    private static MainWindowViewModel Shell()
    {
        SmarxText.Instance.SetLanguage("fr-FR");
        return new MainWindowViewModel();
    }

    [Fact]
    public void Linux_AddingDaveToCompta_LetsHimListTheFolder()
    {
        var vm = Shell().Linux;
        vm.SelectedNode = FindLinux(vm.Tree[0], "/srv/compta");
        vm.SelectedUser = vm.Users.First(u => u.Name == "dave");
        vm.SelectedOperation = vm.Operations.First(o => Equals(o.Operation, LinuxOperation.List));
        Assert.False(vm.Allowed);

        vm.SelectedAccount = vm.Users.First(u => u.Name == "dave");
        vm.Memberships.First(m => m.Group == "compta").IsMember = true;

        Assert.Equal(["compta"], vm.Host.User("dave")!.Groups);
        Assert.True(vm.Allowed);
        Assert.Contains("usermod -g dave -G compta dave", vm.AccountCommands);
        Assert.Contains("2001(compta)", vm.Users.First(u => u.Name == "dave").IdLine);
    }

    [Fact]
    public void Linux_NewAccountAndGroup_GetTheNextFreeIds()
    {
        var vm = Shell().Linux;

        vm.NewGroupName = "direction";
        vm.AddGroupCommand.Execute(null);
        vm.NewUserName = "eve";
        vm.AddUserCommand.Execute(null);

        Assert.Equal(2004, vm.Host.Group("direction")!.Gid);
        Assert.Equal((1005, "eve"), (vm.Host.User("eve")!.Uid, vm.Host.User("eve")!.PrimaryGroup));
        Assert.Equal("eve", vm.SelectedAccount!.Name);
        Assert.Contains(vm.Memberships, m => m.Group == "direction");
    }

    [Fact]
    public void Windows_StartsOnDaveWritingThePlanning_ThroughHisExplicitAce()
    {
        var shell = Shell();
        shell.ShowWindowsCommand.Execute(null);
        var vm = shell.Windows;

        Assert.True(shell.IsWindows);
        Assert.Equal((@"D:\Partages\Projets\planning.xlsx", "dave", (object)WindowsOperation.Write), (vm.SelectedNode!.Path, vm.SelectedUser!.Name, vm.SelectedOperation!.Operation));
        Assert.True(vm.Allowed);
        Assert.Contains(vm.Steps, s => s.Title == "ACE 1, explicite" && s.Passed == true);
        // L'accès effectif est dans la fenêtre Avancé, ouverte sur le compte testé
        Assert.Contains(new AdvancedSecurityViewModel(vm.Host, vm.SelectedNode, vm.SelectedUser.Name).EffectiveRights, r => r.Label == "Suppression" && r.Granted);
    }

    [Fact]
    public void Windows_ElevatedSession_OpensTheAdminFolderToCarol()
    {
        var vm = Shell().Windows;
        vm.SelectedNode = FindWindows(vm.Tree[0], @"D:\Admin");
        vm.SelectedUser = vm.Users.First(u => u.Name == "carol");
        vm.SelectedOperation = vm.Operations.First(o => Equals(o.Operation, WindowsOperation.List));

        Assert.True(vm.CanElevate);
        Assert.False(vm.Allowed);
        vm.Elevated = true;
        Assert.True(vm.Allowed);
        Assert.True(vm.MatrixRows.Single(r => r.User.Name == "carol").Cells.First(c => Equals(c.Operation, WindowsOperation.List)).Allowed);

        vm.SelectedUser = vm.Users.First(u => u.Name == "dave");
        Assert.False(vm.Elevated); // pas d'élévation pour un compte qui n'est pas administrateur
    }

    [Fact]
    public void Windows_AddingAnAce_AndDisablingInheritance_RecomputeEverything()
    {
        var vm = Shell().Windows;
        vm.SelectedNode = FindWindows(vm.Tree[0], @"D:\Partages\Compta\budget.xlsx");
        vm.SelectedUser = vm.Users.First(u => u.Name == "dave");
        vm.SelectedOperation = vm.Operations.First(o => Equals(o.Operation, WindowsOperation.Read));
        Assert.False(vm.Allowed);

        vm.Editor.NewTrustee = vm.Editor.Principals.First(p => Equals(p.Value, "dave"));
        vm.Editor.NewRights = vm.Editor.NewRightsChoices.First(c => Equals(c.Value, NtfsRights.Read));
        vm.Editor.AddAceCommand.Execute(null);
        Assert.True(vm.Allowed);
        Assert.Contains("icacls \"D:\\Partages\\Compta\\budget.xlsx\" /grant:r \"SRV-FIC\\dave:(R)\"", vm.Editor.Commands);

        // L'ACE explicite devient un refus : il omet Synchroniser, comme l'onglet Sécurité
        var ace = vm.Editor.Aces.First(a => a.IsExplicit);
        ace.IsDeny = true;
        Assert.Equal("(RD,RA,REA,RC)", vm.SelectedNode!.Node.Aces.Single().Rights);
        Assert.False(vm.Allowed);

        // Sans les ACE héritées, alice (GL_Compta) perd l'accès au fichier
        vm.SelectedUser = vm.Users.First(u => u.Name == "alice");
        Assert.True(vm.Allowed);
        vm.Editor.DisableInheritanceRemoveCommand.Execute(null);
        Assert.False(vm.Allowed);
        Assert.True(vm.SelectedNode.IsProtected);
        vm.Editor.EnableInheritanceCommand.Execute(null);
        Assert.True(vm.Allowed);
    }

    /// <summary>
    /// L'onglet Sécurité : coches noires et grises ; la fenêtre Modifier change le serveur, Annuler le remet ; un compte
    /// hérité ne se supprime pas ; la fenêtre Avancé convertit l'héritage en ACE explicites.
    /// </summary>
    [Fact]
    public void Windows_SecurityTab_EditAndAdvancedWindows_ChangeTheServerLikeWindows()
    {
        var vm = Shell().Windows;
        vm.SelectedNode = FindWindows(vm.Tree[0], @"D:\Partages\Compta\Archives");
        vm.SelectedUser = vm.Users.First(u => u.Name == "alice");
        vm.SelectedOperation = vm.Operations.First(o => Equals(o.Operation, WindowsOperation.CreateFile));
        Assert.False(vm.Allowed);

        vm.Security.SelectedPrincipal = vm.Security.Principals.First(p => p.Trustee == "GL_Compta");
        var write = vm.Security.Rows.Single(r => r.Label == "Écriture");
        Assert.Equal(("✓", true), (write.AllowMark, write.AllowGray));   // héritée de Compta
        Assert.Equal(("✓", false), (write.DenyMark, write.DenyGray));    // posée sur Archives

        var edit = new PermissionsEditViewModel(vm.Host, vm.SelectedNode!);
        edit.SelectedPrincipal = edit.Principals.First(p => p.Trustee == "GL_Compta");
        var row = edit.Rows.Single(r => r.Permission == BasicPermission.Write);
        Assert.False(row.AllowEnabled);
        Assert.True(row.DenyEnabled);
        row.DenyChecked = false;
        vm.AfterSecurityWindow();
        Assert.True(vm.Allowed);
        Assert.DoesNotContain("/deny", edit.Commands);
        edit.Cancel();
        vm.AfterSecurityWindow();
        Assert.False(vm.Allowed);

        edit = new PermissionsEditViewModel(vm.Host, vm.SelectedNode!);
        edit.SelectedPrincipal = edit.Principals.First(p => p.Trustee == "GL_Compta");
        edit.RemoveCommand.Execute(null);
        Assert.True(edit.HasMessage);

        var advanced = new AdvancedSecurityViewModel(vm.Host, vm.SelectedNode!, "alice");
        Assert.Equal(3, advanced.Entries.Count);
        Assert.Equal(14, advanced.EffectiveRights.Count);
        advanced.DisableCommand.Execute(null);
        Assert.True(advanced.Asking);
        advanced.ConvertCommand.Execute(null);
        Assert.Equal(@"icacls ""D:\Partages\Compta\Archives"" /inheritance:d", advanced.Commands);
        Assert.All(advanced.Entries, e => Assert.Equal("Aucun", e.InheritedFrom));
        vm.AfterSecurityWindow();
        Assert.True(vm.SelectedNode!.IsProtected);
        Assert.False(vm.Allowed);
    }

    /// <summary>
    /// Par un partage : le testeur passe par Projets (Lecture), où dave ne peut plus écrire ; la fenêtre Partage avancé
    /// donne Modifier aux Utilisateurs authentifiés, et dave écrit ; Annuler remet le partage en Lecture.
    /// </summary>
    [Fact]
    public void Windows_ThroughAShare_TheMostRestrictiveWins_AndTheShareWindowChangesIt()
    {
        var vm = Shell().Windows;
        vm.SelectedNode = FindWindows(vm.Tree[0], @"D:\Partages\Projets\planning.xlsx");
        vm.SelectedUser = vm.Users.First(u => u.Name == "dave");
        vm.SelectedOperation = vm.Operations.First(o => Equals(o.Operation, WindowsOperation.Write));
        Assert.True(vm.Allowed);
        Assert.Equal(["en local, sur le serveur", @"par le partage \\SRV-FIC\Projets\planning.xlsx", @"par le partage \\SRV-FIC\Partages\Projets\planning.xlsx"],
            vm.Routes.Select(r => r.Label));

        vm.SelectedRoute = vm.Routes.First(r => r.Share?.Name == "Projets");
        Assert.False(vm.Allowed);
        Assert.Equal(@"\\SRV-FIC\Projets\planning.xlsx", vm.TargetText);
        Assert.EndsWith("NTFS l'accorde, le partage non.", vm.Verdict);
        Assert.Contains(vm.Steps, s => s.Title == "Jeton réseau de dave");
        Assert.False(vm.MatrixRows.Single(r => r.User.Name == "dave").Cells.Single(c => Equals(c.Operation, WindowsOperation.Write)).Allowed);

        var share = new ShareSettingsViewModel(vm.Host, FindWindows(vm.Tree[0], @"D:\Partages\Projets"));
        Assert.True(share.IsShared);
        share.Candidate = share.Candidates.First(c => c.Trustee == WellKnown.AuthenticatedUsers);
        share.AddPrincipalCommand.Execute(null);
        share.Rows.Single(r => r.Right == ShareRight.Change).AllowChecked = true;
        Assert.Contains(@"-ChangeAccess ""NT AUTHORITY\Authenticated Users""", share.Commands);
        vm.AfterSecurityWindow();
        Assert.True(vm.Allowed);

        share.Cancel();
        vm.AfterSecurityWindow();
        Assert.False(vm.Allowed);
    }

    /// <summary>
    /// En passant par un partage, choisir un objet hors de ce partage : la route revient en local, sans que le testeur
    /// calcule l'accès du nouvel objet par l'ancien partage (SmbAccess levait « … n'est pas dans le partage Projets »).
    /// Un objet du même partage garde la route.
    /// </summary>
    [Fact]
    public void Windows_ThroughAShare_PickingAnObjectOutsideIt_FallsBackToLocal()
    {
        var vm = Shell().Windows;
        vm.SelectedNode = FindWindows(vm.Tree[0], @"D:\Partages\Projets\planning.xlsx");
        vm.SelectedUser = vm.Users.First(u => u.Name == "dave");
        vm.SelectedOperation = vm.Operations.First(o => Equals(o.Operation, WindowsOperation.Write));
        vm.SelectedRoute = vm.Routes.First(r => r.Share?.Name == "Projets");

        vm.SelectedNode = FindWindows(vm.Tree[0], @"D:\Partages\Projets\notes.txt");
        Assert.Equal("Projets", vm.SelectedRoute?.Share?.Name);
        Assert.Equal(@"\\SRV-FIC\Projets\notes.txt", vm.TargetText);

        // Écrire ne s'applique pas à un dossier : l'opération change en même temps que l'objet
        vm.SelectedNode = FindWindows(vm.Tree[0], @"D:\Partages\Compta\Archives");
        Assert.True(vm.SelectedRoute?.IsLocal);
        Assert.True(vm.HasResult);
        Assert.Equal(@"D:\Partages\Compta\Archives", vm.TargetText);
        Assert.Contains(vm.Steps, s => s.Title == "Jeton de dave");
    }

    [Fact]
    public void Linux_ThroughASambaShare_PickingAnObjectOutsideIt_FallsBackToLocal()
    {
        var vm = Shell().Linux;
        vm.SelectedNode = FindLinux(vm.Tree[0], "/srv/partage/doc.txt");
        vm.SelectedUser = vm.Users.First(u => u.Name == "carol");
        vm.SelectedOperation = vm.Operations.First(o => Equals(o.Operation, LinuxOperation.Write));
        vm.SelectedRoute = vm.Routes.First(r => r.Share?.Name == "partage");

        vm.SelectedNode = FindLinux(vm.Tree[0], "/home/alice");
        Assert.True(vm.SelectedRoute?.IsLocal);
        Assert.True(vm.HasResult);
        Assert.Equal("/home/alice", vm.TargetText);
    }

    /// <summary>
    /// Par un partage Samba : carol écrit doc.txt en local (rh : -w-), pas par partage (lecture seule) ; ajoutée à la
    /// write list, Samba la laisse passer, mais le noyau refuse : le client ouvre en lecture-écriture.
    /// </summary>
    [Fact]
    public void Linux_ThroughASambaShare_SambaDecidesThenTheKernel_AndTheShareWindowChangesIt()
    {
        var vm = Shell().Linux;
        vm.SelectedNode = FindLinux(vm.Tree[0], "/srv/partage/doc.txt");
        vm.SelectedUser = vm.Users.First(u => u.Name == "carol");
        vm.SelectedOperation = vm.Operations.First(o => Equals(o.Operation, LinuxOperation.Write));
        Assert.True(vm.Allowed);
        Assert.Equal(["en local, sur le serveur", @"par le partage \\srv-fic\partage\doc.txt", @"par le partage \\srv-fic\public\partage\doc.txt"],
            vm.Routes.Select(r => r.Label));

        vm.SelectedRoute = vm.Routes.First(r => r.Share?.Name == "partage");
        Assert.Equal([LinuxOperation.Read, LinuxOperation.Write, LinuxOperation.Delete], vm.Operations.Select(o => (LinuxOperation)o.Operation));
        Assert.False(vm.Allowed);
        Assert.EndsWith("Samba refuse avant le noyau.", vm.Verdict);
        Assert.Equal(@"\\srv-fic\partage\doc.txt", vm.TargetText);

        var share = new SambaShareSettingsViewModel(vm.Host, FindLinux(vm.Tree[0], "/srv/partage"));
        share.WriteList = "bob carol";
        Assert.Contains("write list = bob carol", share.Config);
        vm.AfterShareWindow();
        Assert.False(vm.Allowed);
        Assert.EndsWith("Samba laisse passer, le noyau refuse.", vm.Verdict);
        Assert.Contains(vm.Steps, s => s.Title == "Ouvrir en lecture-écriture");

        share.Cancel();
        vm.AfterShareWindow();
        Assert.EndsWith("Samba refuse avant le noyau.", vm.Verdict);
    }

    [Fact]
    public void Windows_DisablingInheritanceByCopying_KeepsTheSameAccess()
    {
        var vm = Shell().Windows;
        vm.SelectedNode = FindWindows(vm.Tree[0], @"D:\Partages\Compta\budget.xlsx");
        vm.SelectedUser = vm.Users.First(u => u.Name == "alice");
        vm.SelectedOperation = vm.Operations.First(o => Equals(o.Operation, WindowsOperation.Write));

        vm.Editor.DisableInheritanceCopyCommand.Execute(null);

        Assert.True(vm.Allowed);
        Assert.All(vm.Editor.Aces, a => Assert.True(a.IsExplicit));
        Assert.DoesNotContain("(I)", vm.Editor.Icacls);
    }

    [Fact]
    public void Windows_AddingDaveToGlCompta_LetsHimWriteTheBudget()
    {
        var vm = Shell().Windows;
        vm.SelectedNode = FindWindows(vm.Tree[0], @"D:\Partages\Compta\budget.xlsx");
        vm.SelectedUser = vm.Users.First(u => u.Name == "dave");
        vm.SelectedOperation = vm.Operations.First(o => Equals(o.Operation, WindowsOperation.Write));
        Assert.False(vm.Allowed);

        vm.SelectedAccount = vm.Users.First(u => u.Name == "dave");
        vm.Memberships.First(m => m.Group == "GL_Compta").IsMember = true;

        Assert.True(vm.Allowed);
        Assert.Contains("net localgroup \"GL_Compta\" dave /add", vm.AccountCommands);
    }
}
