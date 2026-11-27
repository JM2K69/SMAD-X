using SMAGX.Forge;
using SMAGX.Machine;

namespace SMAGX.Tests;

/// <summary>La tabulation du terminal, comme bash avec la complétion de Git.</summary>
public class CompletionTests
{
    private static (Workstation Ws, Shell Shell) Station()
    {
        var ws = new Workstation("alice") { Interactive = false, Ask = (_, _) => "yes" };
        // Forges sans SSH : ici, seule la complétion compte
        ws.Network.Add(new SMAGX.Forge.Forge("gitlab.example") { Ssh = false });
        ws.Network.Add(new SMAGX.Forge.Forge("github.example", ForgeKind.GitHub) { Ssh = false });
        ws.EnsureDirectory("/home/alice/projet");
        ws.Cwd = "/home/alice/projet";
        var shell = new Shell(ws);
        foreach (var line in new[]
                 {
                     "git config --global user.name \"Alice Martin\"", "git config --global user.email alice@lycee.example",
                     "git config --global init.defaultBranch main", "git init", "echo un > index.html", "echo deux > style.css",
                     "git add index.html style.css", "git commit -m Accueil", "git branch contact", "git branch correctifs",
                     "echo trois >> style.css", "echo quatre > tarifs.html", "mkdir images"
                 })
            shell.Run(line);
        return (ws, shell);
    }

    private static Completion Tab(Workstation ws, string line) => Completer.Complete(ws, line, line.Length);

    [Theory]
    [InlineData("gi", "git ")]
    [InlineData("git sw", "git switch ")]
    [InlineData("git switch cont", "git switch contact ")]
    [InlineData("git switch co", "git switch co")]
    [InlineData("git commit --am", "git commit --amend ")]
    [InlineData("git add ", "git add ")]
    [InlineData("git add s", "git add style.css ")]
    [InlineData("git add t", "git add tarifs.html ")]
    [InlineData("git rm s", "git rm style.css ")]
    [InlineData("cd im", "cd images/")]
    [InlineData("cat ind", "cat index.html ")]
    [InlineData("git config user.e", "git config user.email ")]
    [InlineData("ssh git@gith", "ssh git@github.example ")]
    [InlineData("ssh-keygen -t ed", "ssh-keygen -t ed25519 ")]
    public void Tab_CompletesLikeBash(string typed, string expected) => Assert.Equal(expected, Tab(Station().Ws, typed).Line);

    [Fact]
    public void Ambiguous_KeepsTheLine_AndListsTheChoices()
    {
        var (ws, _) = Station();
        var branches = Tab(ws, "git switch co");
        Assert.True(branches.Ambiguous);
        Assert.Equal(["contact", "correctifs"], branches.Candidates);

        // git add ne propose que ce qui a changé : style.css (modifié) et tarifs.html (non suivi), pas index.html
        var add = Tab(ws, "git add ");
        Assert.Equal(["style.css", "tarifs.html"], add.Candidates);
    }

    [Fact]
    public void Tab_FindsTheSshPublicKey()
    {
        var (ws, shell) = Station();
        shell.Run("ssh-keygen -q -N \"\" -f /home/alice/.ssh/id_ed25519");
        // Le dossier caché se complète dès qu'on tape son point ; la clé privée et la publique partagent leur début
        Assert.Equal("cat ~/.ssh/", Tab(ws, "cat ~/.s").Line);
        var key = Tab(ws, "cat ~/.ssh/i");
        Assert.Equal("cat ~/.ssh/id_ed25519", key.Line);
        Assert.Equal(["id_ed25519", "id_ed25519.pub"], Tab(ws, key.Line).Candidates);
        Assert.Equal("cat ~/.ssh/id_ed25519.pub ", Tab(ws, "cat ~/.ssh/id_ed25519.").Line);
    }

    [Fact]
    public void Tab_InTheMiddle_KeepsTheRestOfTheLine()
    {
        var (ws, _) = Station();
        var completion = Completer.Complete(ws, "git sw contact", "git sw".Length);
        Assert.Equal("git switch contact", completion.Line);
        Assert.Equal("git switch".Length, completion.Caret);
    }

    [Fact]
    public void RemoteBranches_AreOfferedAfterAFetch()
    {
        var (ws, shell) = Station();
        ((SMAGX.Forge.Forge)ws.Network[0]).Create("classe/agence");
        shell.Run("git remote add origin git@gitlab.example:classe/agence.git");
        shell.Run("git push -u origin main contact");
        Assert.Equal("git push origin ", Tab(ws, "git push or").Line);
        Assert.Equal("git pull origin contact ", Tab(ws, "git pull origin cont").Line);
        Assert.Contains("origin/contact", Tab(ws, "git merge origin/").Candidates);
    }
}
