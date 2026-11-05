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
        Assert.Contains(vm.EffectiveRights, r => r.Label == "Suppression" && r.Granted);
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
