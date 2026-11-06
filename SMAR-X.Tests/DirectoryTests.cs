using SMARX.Models;
using SMARX.ViewModels;

namespace SMARX.Tests;

/// <summary>
/// « Comptes et groupes », la fenêtre façon console Active Directory de l'espace Windows : l'arbre du domaine et du
/// serveur, les onglets Membre de, Membres et Imbrication, le sélecteur d'Ajouter…, et la création des comptes et des
/// groupes. Chaque changement passe aussitôt dans l'espace : comptes, testeur, whoami. Les textes de SMAR-X sont
/// partagés : ces tests changent la langue, ils passent avec les autres tests de l'interface, pas en même temps.
/// </summary>
[Collection(InterfaceTests.Name)]
public class DirectoryTests
{
    private static WindowsWorkspaceViewModel Workspace()
    {
        SmarxText.Instance.SetLanguage("fr-FR");
        return new MainWindowViewModel().Windows;
    }

    [Fact]
    public void Tree_ShowsTheDomainAndTheServer_LikeTheActiveDirectoryConsole()
    {
        using var directory = new DirectoryViewModel(Workspace());

        Assert.Equal(new[] { "CORP (domaine)", "SRV-FIC (serveur)" }, directory.Tree.Select(n => n.Display));
        var domain = directory.Tree[0];
        Assert.Equal(new[] { "Utilisateurs", "Groupes globaux (G_)", "Groupes de domaine local (DL_)" }, domain.Children.Select(c => c.Display));
        Assert.Equal(new[] { "alice", "bob", "dave" }, domain.Children[0].Children.Select(c => c.Name));
        Assert.Contains(domain.Children[1].Children, g => g.Name == "G_Compta" && g.IsGroup);
        Assert.All(domain.Children[2].Children, g => Assert.StartsWith("DL_", g.Name));
        var server = directory.Tree[1];
        Assert.Equal(new[] { "Administrator", "carol" }, server.Children[0].Children.Select(c => c.Name));
        Assert.Contains(server.Children[1].Children, g => g.Name == WellKnown.Administrators);
    }

    /// <summary>bob, ouvert sur son compte : ses groupes directs, Utilisateurs, et ce qu'il reçoit par imbrication.</summary>
    [Fact]
    public void Account_ShowsItsGroups_AndItsNesting_AsATreeAndAsPaths()
    {
        using var directory = new DirectoryViewModel(Workspace(), "bob");

        Assert.Equal("bob", directory.Title);
        Assert.Equal(@"CORP\bob", directory.Qualified);
        Assert.Equal(new[] { "G_Employes", "G_Compta", "G_RH", WellKnown.Users }, directory.MemberOf.Select(e => e.Name));
        var root = Assert.Single(directory.Nesting);
        Assert.True(root.IsUser);
        var compta = root.Children.Single(n => n.Display == "G_Compta");
        Assert.Equal(new[] { "DL_Compta_LM", "DL_Archives_Refuser" }, compta.Children.Select(n => n.Display));
        Assert.Contains("bob → G_Compta → DL_Archives_Refuser", directory.NestingPaths.Split('\n'));
        Assert.Contains("bob → G_RH → DL_RH_LM", directory.NestingPaths.Split('\n'));
        Assert.Contains(directory.Nested, e => e.Name == "DL_RH_LM" && e.Detail == "par G_RH");
        Assert.Contains("Add-ADGroupMember -Identity G_RH -Members bob", directory.Commands);
    }

    /// <summary>Un groupe : ses membres, comptes et groupes ; Supprimer retire le lien, et dave perd l'accès aussitôt.</summary>
    [Fact]
    public void RemovingAMember_ChangesTheTesterAtOnce()
    {
        var vm = Workspace();
        using var directory = new DirectoryViewModel(vm, "DL_Projets_LM");
        Assert.Equal(new[] { "G_Employes" }, directory.Members.Select(m => m.Name));
        Assert.True(directory.CanEditMembers);

        directory.SelectedMember = directory.Members[0];
        directory.RemoveMemberCommand.Execute(null);

        Assert.Empty(directory.Members);
        Assert.DoesNotContain("DL_Projets_LM", vm.Host.Group("G_Employes")!.Groups);
        Assert.DoesNotContain("DL_Projets_LM", vm.Users.First(u => u.Name == "dave").NestedLine);
    }

    /// <summary>
    /// Ajouter… propose tous les comptes et les groupes ; ce qu'Active Directory refuserait est grisé, avec sa raison :
    /// un membre déjà là, un compte local du serveur dans un groupe du domaine, un groupe de domaine local dans un global.
    /// </summary>
    [Fact]
    public void MembersPicker_GreysWhatActiveDirectoryWouldRefuse()
    {
        using var directory = new DirectoryViewModel(Workspace(), "G_RH");
        var picker = directory.MembersPicker();

        Assert.Equal("Ajouter des membres à G_RH", picker.Title);
        Assert.Equal("Déjà membre.", picker.Items.Single(c => c.Name == "bob").Reason);
        Assert.False(picker.Items.Single(c => c.Name == "carol").IsEnabled);
        Assert.False(picker.Items.Single(c => c.Name == "DL_RH_LM").IsEnabled);
        Assert.True(picker.Items.Single(c => c.Name == "dave").IsEnabled);

        picker.Filter = "da";
        Assert.Equal(new[] { "dave" }, picker.Items.Select(c => c.Name));
        picker.Items[0].IsChecked = true;
        picker.Filter = "";
        picker.Items.Single(c => c.Name == "carol").IsChecked = true;
        Assert.Equal(new[] { "dave" }, picker.Chosen);

        directory.AddMembers(picker.Chosen);
        Assert.Contains(directory.Members, m => m.Name == "dave");
    }

    /// <summary>Utilisateurs : tous les comptes en sont membres, on n'y ajoute personne.</summary>
    [Fact]
    public void Users_HasEveryAccount_AndCannotBeEdited()
    {
        using var directory = new DirectoryViewModel(Workspace(), WellKnown.Users);

        Assert.False(directory.CanEditMembers);
        Assert.True(directory.HasMembersNote);
        directory.Select("alice");
        directory.SelectedMemberOf = directory.MemberOf.Single(e => e.Name == WellKnown.Users);
        Assert.False(directory.RemoveMemberOfCommand.CanExecute(null));
    }

    /// <summary>Nouveau groupe global : il entre dans l'arbre, choisi ; un nom déjà pris est refusé.</summary>
    [Fact]
    public void Create_AddsTheObject_AndSelectsIt()
    {
        var vm = Workspace();
        using var directory = new DirectoryViewModel(vm);
        directory.NewKind = directory.Kinds.Single(k => Equals(k.Value, DirectoryViewModel.CreatableKind.GlobalGroup));
        directory.NewName = "G_Direction";
        directory.CreateCommand.Execute(null);

        Assert.Equal(WindowsGroupScope.Global, vm.Host.Group("G_Direction")!.Scope);
        Assert.Equal("G_Direction", directory.SelectedNode?.Name);
        Assert.Contains("New-ADGroup", directory.Commands);

        directory.NewName = "alice";
        directory.CreateCommand.Execute(null);
        Assert.Equal("alice existe déjà.", directory.Message);
    }

    /// <summary>Un serveur hors domaine : ni domaine dans l'arbre, ni comptes ou groupes du domaine à créer.</summary>
    [Fact]
    public void ServerOutsideADomain_HasNoDomainNode()
    {
        var vm = Workspace();
        vm.Host.Domain = null;
        using var directory = new DirectoryViewModel(vm);

        Assert.Single(directory.Tree);
        Assert.True(directory.Tree[0].IsServer);
        Assert.All(directory.Kinds, k => Assert.True(k.Value is DirectoryViewModel.CreatableKind.LocalUser or DirectoryViewModel.CreatableKind.LocalGroup));
    }
}
