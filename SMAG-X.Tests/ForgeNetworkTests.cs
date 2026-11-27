using System.Net;
using SMAGX.Forge;
using SMAGX.Machine;

namespace SMAGX.Tests;

/// <summary>
/// Deux élèves, deux SMAG-X : Alice sert ses forges sur le réseau (en boucle locale ici, sans demande du pare-feu), Bob
/// les joint avec l'adresse et le code. Ce que l'un pousse, l'autre le récupère, avec les mêmes identifiants de commit ;
/// la clé SSH de Bob est enregistrée sur la forge d'Alice par le réseau.
/// </summary>
public class ForgeNetworkTests
{
    private static int _minute;

    /// <summary>Un poste avec Git configuré et une clé SSH enregistrée sur la forge ; il accepte la clé du serveur.</summary>
    private static (Workstation Ws, Shell Shell) Station(string user, IForge forge)
    {
        var ws = new Workstation(user)
        {
            Interactive = false, Ask = (_, _) => "yes",
            Clock = () => new DateTimeOffset(2026, 11, 27, 10, Interlocked.Increment(ref _minute) % 60, 0, TimeSpan.FromHours(1))
        };
        ws.Network.Add(forge);
        var shell = new Shell(ws);
        shell.Run($"git config --global user.name \"{user}\"");
        shell.Run($"git config --global user.email \"{user}@example.com\"");
        shell.Run($"ssh-keygen -q -N \"\" -f /home/{user}/.ssh/id_ed25519");
        Assert.Null(forge.AddKey(user, "", ws.ReadText($"/home/{user}/.ssh/id_ed25519.pub")));
        return (ws, shell);
    }

    private static string Run(Shell shell, string line)
    {
        var result = shell.Run(line);
        Assert.True(result.ExitCode == 0, $"$ {line}\n{result.Display}");
        return result.Out + result.Err;
    }

    private static (SMAGX.Forge.Forge GitLab, ForgeServer Server, IForge Remote) Serve(string code)
    {
        var gitlab = new SMAGX.Forge.Forge("gitlab.example");
        var github = new SMAGX.Forge.Forge("github.example", ForgeKind.GitHub);
        gitlab.Create("classe/agence");
        var server = new ForgeServer([gitlab, github], code);
        server.Start(0, IPAddress.Loopback);
        var remotes = RemoteForge.Connect("127.0.0.1", server.Port, code);
        Assert.Equal(["gitlab.example", "github.example"], remotes.Select(r => r.Host));
        Assert.Equal(ForgeKind.GitHub, remotes[1].Kind);
        return (gitlab, server, remotes[0]);
    }

    [Fact]
    public void PushedOnOneStation_IsPulledOnTheOther()
    {
        var (forge, server, remote) = Serve("4821");
        using var _ = server;
        var (alice, a) = Station("alice", forge);
        var (bob, b) = Station("bob", remote);
        Assert.Single(forge.Keys("bob"));

        Run(a, "git clone git@gitlab.example:classe/agence.git");
        a.Run("cd agence");
        a.Run("echo \"Agence\" > index.html");
        Run(a, "git add index.html");
        Run(a, "git commit -m \"Accueil\"");
        var push = Run(a, "git push");
        Assert.Contains("[new branch]", push);

        var clone = Run(b, "git clone git@gitlab.example:classe/agence.git");
        Assert.Contains("Cloning into 'agence'", clone);
        Assert.Contains("Permanently added 'gitlab.example'", clone);
        b.Run("cd agence");
        Assert.Equal("Agence\n", bob.ReadText("/home/bob/agence/index.html"));
        Assert.Equal(alice.FindRepository("/home/alice/agence")!.Head, bob.FindRepository("/home/bob/agence")!.Head);

        b.Run("echo \"Contact\" > contact.html");
        Run(b, "git add contact.html");
        Run(b, "git commit -m \"Contact\"");
        Run(b, "git push");

        Run(a, "git pull");
        Assert.Equal("Contact\n", alice.ReadText("/home/alice/agence/contact.html"));
        Assert.Equal(alice.FindRepository("/home/alice/agence")!.Head, bob.FindRepository("/home/bob/agence")!.Head);
        Assert.Contains(forge.Projects(), p => p.Path == "classe/agence" && p.Branches.Any(x => x.Branch == "main"));
        Assert.Equal("Welcome to GitLab, @bob!\n", Run(b, "ssh -T git@gitlab.example"));
    }

    [Fact]
    public void PushRacingAnotherStation_IsRejectedFetchFirst()
    {
        var (forge, server, remote) = Serve("1111");
        using var _ = server;
        var (_, a) = Station("alice", forge);
        var (_, b) = Station("bob", remote);

        Run(a, "git clone git@gitlab.example:classe/agence.git");
        a.Run("cd agence");
        a.Run("echo un > a.txt");
        Run(a, "git add a.txt");
        Run(a, "git commit -m un");
        Run(a, "git push");

        Run(b, "git clone git@gitlab.example:classe/agence.git");
        b.Run("cd agence");
        a.Run("echo deux >> a.txt");
        Run(a, "git commit -am deux");
        Run(a, "git push");

        b.Run("echo trois > b.txt");
        Run(b, "git add b.txt");
        Run(b, "git commit -m trois");
        var push = b.Run("git push");
        Assert.Equal(1, push.ExitCode);
        Assert.Contains("(fetch first)", push.Err);
    }

    [Fact]
    public void WrongCode_LooksLikeARefusedKey()
    {
        var (_, server, _) = Serve("1234");
        using var _ = server;
        Assert.Throws<ForgeRefusedException>(() => RemoteForge.Connect("127.0.0.1", server.Port, "9999"));

        var ws = new Workstation("bob") { Ask = (_, _) => "yes" };
        ws.Network.Add(new RemoteForge("127.0.0.1", server.Port, "9999"));
        var clone = new Shell(ws).Run("git clone git@gitlab.example:classe/agence.git");
        Assert.Equal(128, clone.ExitCode);
        Assert.Contains("Permission denied (publickey)", clone.Err);
        Assert.DoesNotContain("authenticity", clone.Err);
    }

    [Fact]
    public void StoppedServer_RefusesTheConnection()
    {
        var (_, server, remote) = Serve("1234");
        server.Dispose();

        var ws = new Workstation("bob") { Ask = (_, _) => "yes" };
        ws.Network.Add(remote);
        var clone = new Shell(ws).Run("git clone git@gitlab.example:classe/agence.git");
        Assert.Equal(128, clone.ExitCode);
        Assert.Equal("Cloning into 'agence'...\nssh: connect to host gitlab.example port 22: Connection refused\n" + SMAGX.Ssh.SshClient.RemoteFatal, clone.Display);
    }
}
