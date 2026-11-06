using SMARX.Models;
using SMARX.Samples;
using SMARX.Services.Windows;
using SMARX.ViewModels;

namespace SMARX.Tests;

/// <summary>
/// whoami : le jeton d'un compte tel que l'écrit « whoami /user /groups », bâti sur le jeton qu'utilise AccessCheck.
/// Les groupes globaux sont de type Group, ceux de domaine local des Alias « Local Group », et l'UAC laisse Administrators
/// en « refus seulement » sans élévation.
/// </summary>
[Collection(InterfaceTests.Name)]
public class WhoamiTests
{
    private readonly WindowsHost _host = WindowsSamples.FileServer();

    private static string Line(WhoamiToken token, string name) =>
        token.Text().Split('\n').Single(l => l.StartsWith(name + " "));

    [Fact]
    public void DomainAccount_ShowsItsGlobalGroups_AndTheDomainLocalGroupsItGetsThroughThem()
    {
        var bob = WindowsWhoami.Of(_host, _host.User("bob")!, network: false, elevated: false);

        Assert.Equal(@"corp\bob", bob.User);
        Assert.Equal(new[]
        {
            "Everyone", @"BUILTIN\Users", @"NT AUTHORITY\INTERACTIVE", @"NT AUTHORITY\Authenticated Users",
            @"CORP\G_Employes", @"CORP\G_Compta", @"CORP\G_RH",
            @"CORP\DL_Projets_LM", @"CORP\DL_Compta_LM", @"CORP\DL_Archives_Refuser", @"CORP\DL_RH_LM",
            @"Mandatory Label\Medium Mandatory Level"
        }, bob.Groups.Select(g => g.Name));
        var rh = bob.Groups.Single(g => g.Key == "DL_RH_LM");
        Assert.Equal(("Alias", "G_RH", WhoamiOrigin.Nested), (rh.Type, rh.Via, rh.Origin));
        Assert.Equal("Group", bob.Groups.Single(g => g.Key == "G_RH").Type);

        var text = bob.Text();
        Assert.StartsWith("USER INFORMATION\n----------------\n\nUser Name", text.Replace("\r", ""));
        Assert.Matches(@"^CORP\\DL_RH_LM +Alias +S-1-5-21-1-1-1-\d+ +Mandatory group, Enabled by default, Enabled group, Local Group$", Line(bob, @"CORP\DL_RH_LM"));
    }

    [Fact]
    public void LocalAdministrator_IsDenyOnlyUntilElevated_AndFilteredThroughAShare()
    {
        var carol = _host.User("carol")!;

        var local = WindowsWhoami.Of(_host, carol, network: false, elevated: false);
        Assert.Equal(@"srv-fic\carol", local.User);
        Assert.Matches(@"^BUILTIN\\Administrators +Alias +S-1-5-32-544 +Group used for deny only$", Line(local, @"BUILTIN\Administrators"));
        Assert.Equal(@"Mandatory Label\Medium Mandatory Level", local.Groups[^1].Name);

        var elevated = WindowsWhoami.Of(_host, carol, network: false, elevated: true);
        Assert.EndsWith("Enabled group, Group owner", Line(elevated, @"BUILTIN\Administrators"));
        Assert.Equal(@"Mandatory Label\High Mandatory Level", elevated.Groups[^1].Name);

        // Par un partage, un compte local administrateur est filtré (restrictions distantes de l'UAC)
        var network = WindowsWhoami.Of(_host, carol, network: true, elevated: true);
        Assert.Contains(network.Groups, g => g.Name == @"NT AUTHORITY\NETWORK");
        Assert.DoesNotContain(network.Groups, g => g.Name == @"NT AUTHORITY\INTERACTIVE");
        Assert.True(network.Groups.Single(g => g.Key == WellKnown.Administrators).DenyOnly);
    }

    /// <summary>
    /// dave, mis dans G_RH depuis l'onglet Membres de « Comptes et groupes » : le testeur le voit aussitôt ; la fenêtre whoami montre le jeton de
    /// la prochaine session (+ G_RH, + DL_RH_LM par G_RH), et whoami, dans la session ouverte, l'ancien, jusqu'à ce que
    /// dave ferme et rouvre sa session.
    /// </summary>
    [Fact]
    public void GroupMembersChange_ShowsInWhoami_UntilTheSessionIsOpenedAgain()
    {
        SmarxText.Instance.SetLanguage("fr-FR");
        var vm = new MainWindowViewModel().Windows;
        var whoami = new WhoamiViewModel(vm, "dave", network: false, elevated: false);
        Assert.False(whoami.HasChanges);

        using var directory = new DirectoryViewModel(vm, "G_RH");
        directory.AddMembers(["dave"]);

        Assert.Contains("G_RH", vm.Host.User("dave")!.Groups);
        Assert.Contains("Add-ADGroupMember -Identity G_RH -Members dave", directory.Commands);
        Assert.True(whoami.HasChanges);
        Assert.Equal(new[] { @"CORP\G_RH", @"CORP\DL_RH_LM" }, whoami.Rows.Where(r => r.Change == "+").Select(r => r.Name));
        Assert.Equal("par G_RH", whoami.Rows.Single(r => r.Name == @"CORP\DL_RH_LM").Meaning);
        Assert.DoesNotContain("G_RH", whoami.SessionText);

        whoami.RelogonCommand.Execute(null);
        Assert.False(whoami.HasChanges);
        Assert.Contains(@"CORP\G_RH", whoami.SessionText);
        whoami.Dispose();
    }
}
