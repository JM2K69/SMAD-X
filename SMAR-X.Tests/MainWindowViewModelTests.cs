using SMARX.Models;
using SMARX.Services.Linux;
using SMARX.ViewModels;

namespace SMARX.Tests;

/// <summary>La fenêtre de SMAR-X : chaque choix et chaque modification des droits recalcule le testeur et la matrice.</summary>
[Collection(InterfaceTests.Name)]
public class MainWindowViewModelTests
{
    private static LinuxWorkspaceViewModel Open(string path, string user, LinuxOperation operation)
    {
        SmarxText.Instance.SetLanguage("fr-FR");
        var vm = new MainWindowViewModel().Linux;
        vm.SelectedNode = Find(vm.Tree[0], path);
        vm.SelectedUser = vm.Users.First(u => u.Name == user);
        vm.SelectedOperation = vm.Operations.First(o => Equals(o.Operation, operation));
        return vm;
    }

    private static NodeItem Find(NodeItem item, string path) =>
        item.Path == path ? item : item.Children.Select(c => FindOrNull(c, path)).First(f => f != null)!;

    private static NodeItem? FindOrNull(NodeItem item, string path) =>
        item.Path == path ? item : item.Children.Select(c => FindOrNull(c, path)).FirstOrDefault(f => f != null);

    [Fact]
    public void Opening_TestsBobReadingTheBudget_StepByStep()
    {
        SmarxText.Instance.SetLanguage("fr-FR");
        var shell = new MainWindowViewModel();
        var vm = shell.Linux;

        Assert.True(shell.IsLinux);
        Assert.Equal(("/srv/compta/budget.ods", "bob", (object)LinuxOperation.Read), (vm.SelectedNode!.Path, vm.SelectedUser!.Name, vm.SelectedOperation!.Operation));
        Assert.True(vm.Allowed);
        Assert.Equal(["Traverser /", "Traverser /srv", "Traverser /srv/compta", "Lire /srv/compta/budget.ods"], vm.Steps.Select(s => s.Title));
        Assert.Equal(["/", "srv", "compta", "budget.ods"], vm.PathSegments.Select(s => s.Name));
        Assert.Equal(["Lire", "Écrire", "Exécuter", "Supprimer", "Changer les droits"], vm.MatrixColumns);
    }

    [Fact]
    public void ChangingTheMode_RecomputesTheTesterAndTheMatrix()
    {
        var vm = Open("/srv/compta/budget.ods", "bob", LinuxOperation.Read);

        vm.Editor.GroupBits.R = false; // chmod g-r

        Assert.Equal("0600", vm.SelectedNode!.Node.Mode);
        Assert.False(vm.Allowed);
        Assert.False(vm.MatrixRows.Single(r => r.User.Name == "bob").Cells.First(c => Equals(c.Operation, LinuxOperation.Read)).Allowed);
        Assert.Contains("chmod 0600 /srv/compta/budget.ods", vm.Editor.Commands);
        Assert.Equal("-rw-------", vm.SelectedNode.Mode);
    }

    [Fact]
    public void AddingAnAclEntry_RecalculatesTheMask_LikeSetfacl()
    {
        var vm = Open("/srv/rh/salaires.ods", "dave", LinuxOperation.Read);
        Assert.False(vm.Allowed);

        vm.Editor.UserToAdd = "dave";
        vm.Editor.AddUserCommand.Execute(null);

        var node = vm.SelectedNode!.Node;
        Assert.True(vm.Editor.HasAcl);
        Assert.Equal("dave", node.Acl!.Users.Single().Name);
        Assert.Equal(Perm.Read, node.GroupPerm); // masque = groupe propriétaire (---) ∪ dave (r--)
        Assert.Equal("Masque", vm.Editor.GroupRowLabel);
        Assert.EndsWith("+", vm.SelectedNode.Mode);
        // dave ne traverse toujours pas /srv/rh
        Assert.False(vm.Allowed);
        Assert.Equal("Traverser /srv/rh", vm.Steps[^1].Title);

        vm.Editor.RemoveAclCommand.Execute(null);
        Assert.Null(node.Acl);
        Assert.Equal(Perm.None, node.GroupPerm);
    }

    [Fact]
    public void ClickingAMatrixCell_TestsThatAccountAndOperation()
    {
        var vm = Open("/tmp/alice.txt", "bob", LinuxOperation.Read);

        var cell = vm.MatrixRows.Single(r => r.User.Name == "carol").Cells.Single(c => Equals(c.Operation, LinuxOperation.Delete));
        vm.SelectCellCommand.Execute(cell);

        Assert.Equal(("carol", (object)LinuxOperation.Delete), (vm.SelectedUser!.Name, vm.SelectedOperation!.Operation));
        Assert.False(vm.Allowed);
        Assert.Equal("Sticky bit sur /tmp", vm.Steps[^1].Title);
        Assert.True(vm.MatrixRows.Single(r => r.User.Name == "carol").Cells.Single(c => Equals(c.Operation, LinuxOperation.Delete)).IsSelected);
    }

    [Fact]
    public void ADirectory_ShowsWhatANewFileWouldGet_AndWhy()
    {
        var vm = Open("/srv/compta", "alice", LinuxOperation.Create);

        Assert.True(vm.ShowCreation);
        Assert.Equal("-rw-r--r--  alice  compta  fichier", vm.NewFile);
        Assert.Equal("drwxr-sr-x  alice  compta  dossier", vm.NewDirectory);
        Assert.Contains(vm.CreationReasons, r => r.Contains("setgid"));
    }

    [Fact]
    public void SwitchingToEnglish_TranslatesOperationsAndExplanations()
    {
        SmarxText.Instance.SetLanguage("fr-FR");
        var shell = new MainWindowViewModel();
        var vm = shell.Linux;
        try
        {
            shell.ToggleLanguageCommand.Execute(null);

            Assert.Equal("Read", vm.SelectedOperation!.Label);
            Assert.Equal("Result: bob can read /srv/compta/budget.ods.", vm.Verdict);
            Assert.Equal("Traverse /", vm.Steps[0].Title);
            Assert.Equal("Allowed", vm.VerdictTitle);
        }
        finally
        {
            SmarxText.Instance.SetLanguage("fr-FR");
        }
    }

    [Fact]
    public void TheDocument_RoundTrips()
    {
        var json = new MainWindowViewModel().ToDocument().Serialize();

        var copy = SmarxDocument.Parse(json);
        Assert.Equal("srv-fic", copy.Linux.Single().Name);
        Assert.Equal("SRV-FIC", copy.Windows.Single().Name);
        Assert.Equal("dave", copy.Windows[0].Find(@"D:\Partages\Projets\planning.xlsx")!.Aces.Single().Trustee);
        Assert.Equal("bob", copy.Linux[0].Find("/srv/partage")!.Acl!.Users.Single().Name);
        Assert.Throws<InvalidDataException>(() => SmarxDocument.Parse("{\"Format\":\"autre\"}"));
    }
}
