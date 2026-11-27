using SMAGX.Forge;
using SMAGX.Machine;
using SMAGX.Ssh;

namespace SMAGX.Tests;

/// <summary>
/// La partie SSH : empreintes et dessins identiques à ceux d'OpenSSH (Fidelity/ssh, relevés avec ssh-keygen 10.3),
/// puis le parcours de l'élève : clé générée, refus de la forge, clé ajoutée, serveur accepté, connexion.
/// </summary>
public class SshTests
{
    private static string Fixture(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fidelity", "ssh", name)).Replace("\r\n", "\n");

    [Theory]
    [InlineData("ed25519")]
    [InlineData("rsa")]
    public void FingerprintAndRandomart_MatchOpenSsh(string name)
    {
        var key = SshPublicKey.Parse(Fixture(name + ".pub.txt"))!;
        Assert.Equal(Fixture(name + ".lv.txt"), key.FingerprintLine + "\n" + key.Randomart + "\n");
    }

    [Fact]
    public void GeneratedPrivateKey_GivesBackItsPublicKey()
    {
        foreach (var type in new[] { "ssh-ed25519", "ssh-rsa", "ecdsa-sha2-nistp256" })
        {
            var (key, file) = SshKeyFile.Generate(type, type == "ssh-rsa" ? 3072 : 256, "alice@poste", new Random(7));
            var read = SshKeyFile.ReadPrivate(file)!;
            Assert.Equal(key.Line, read.Line);
            Assert.Equal(type == "ssh-rsa" ? 3072 : 256, read.Bits);
            Assert.All(file.Split('\n')[1..^2], line => Assert.True(line.Length <= 70));
        }
    }

    [Fact]
    public void KnownHosts_HashesTheHostName()
    {
        var key = SshPublicKey.HostKey("gitlab.example");
        var line = KnownHosts.Line("gitlab.example", key, new Random(1));
        Assert.StartsWith("|1|", line);
        Assert.DoesNotContain("gitlab.example", line);
        Assert.True(KnownHosts.Find(line, "gitlab.example")!.SameKey(key));
        Assert.Null(KnownHosts.Find(line, "github.example"));
    }

    private sealed class Student
    {
        public readonly Queue<string> Answers = new();
        public readonly List<string> Questions = [];
        public readonly Workstation Ws;
        public readonly Shell Shell;

        public Student(params IForge[] forges)
        {
            Ws = new Workstation("alice") { Interactive = false, Random = new Random(42) };
            Ws.Ask = (shown, _) =>
            {
                Questions.Add(shown[(shown.LastIndexOf('\n', shown.Length - 2) + 1)..]);
                return Answers.Count > 0 ? Answers.Dequeue() : "";
            };
            Ws.Network.AddRange(forges);
            Shell = new Shell(Ws);
            Shell.Run("git config --global user.name \"Alice Martin\"");
            Shell.Run("git config --global user.email alice@lycee.example");
        }

        public CommandResult Run(string line, params string[] answers)
        {
            foreach (var answer in answers) Answers.Enqueue(answer);
            return Shell.Run(line);
        }
    }

    [Fact]
    public void Keygen_AsksLikeOpenSsh()
    {
        var alice = new Student();
        var result = alice.Run("ssh-keygen -t ed25519 -C \"alice@lycee.example\"", "", "", "");
        Assert.Equal(0, result.ExitCode);
        var lines = result.Display.Split('\n');
        Assert.Equal("Generating public/private ed25519 key pair.", lines[0]);
        Assert.Equal("Enter file in which to save the key (/home/alice/.ssh/id_ed25519): ", lines[1]);
        Assert.Equal("Created directory '/home/alice/.ssh'.", lines[2]);
        Assert.Equal("Enter passphrase for \"/home/alice/.ssh/id_ed25519\" (empty for no passphrase): ", lines[3]);
        Assert.Equal("Enter same passphrase again: ", lines[4]);
        Assert.Equal("Your identification has been saved in /home/alice/.ssh/id_ed25519", lines[5]);
        Assert.Equal("Your public key has been saved in /home/alice/.ssh/id_ed25519.pub", lines[6]);
        Assert.Equal("The key fingerprint is:", lines[7]);
        Assert.Matches("^SHA256:[A-Za-z0-9+/]{43} alice@lycee.example$", lines[8]);
        Assert.Equal("+--[ED25519 256]--+", lines[10]);
        Assert.StartsWith("ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAI", alice.Ws.ReadText("/home/alice/.ssh/id_ed25519.pub"));

        // Une deuxième fois : le fichier existe, ssh-keygen demande avant d'écraser
        var again = alice.Run("ssh-keygen -t ed25519", "", "n");
        Assert.Equal(1, again.ExitCode);
        Assert.Contains("/home/alice/.ssh/id_ed25519 already exists.\nOverwrite (y/n)? n\n", again.Display);

        var check = alice.Run("ssh-keygen -lf .ssh/id_ed25519.pub");
        Assert.Matches(@"^256 SHA256:\S+ alice@lycee.example \(ED25519\)\n$", check.Out);
    }

    [Fact]
    public void Clone_NeedsTheServerAcceptedAndAKeyOnTheForge()
    {
        var forge = new SMAGX.Forge.Forge("gitlab.example");
        forge.Create("classe/agence");
        var alice = new Student(forge);

        // Pas de clé : la forge accepte le serveur, puis refuse l'élève
        var refused = alice.Run("git clone git@gitlab.example:classe/agence.git", "yes");
        Assert.Equal(128, refused.ExitCode);
        Assert.Equal("Cloning into 'agence'...\n"
                     + "The authenticity of host 'gitlab.example (203.0.113.10)' can't be established.\n"
                     + $"ED25519 key fingerprint is: {SshPublicKey.HostKey("gitlab.example").Fingerprint}\n"
                     + "This key is not known by any other names.\n"
                     + "Are you sure you want to continue connecting (yes/no/[fingerprint])? yes\n"
                     + "Warning: Permanently added 'gitlab.example' (ED25519) to the list of known hosts.\n"
                     + "git@gitlab.example: Permission denied (publickey).\n"
                     + SshClient.RemoteFatal, refused.Display);
        Assert.StartsWith("|1|", alice.Ws.ReadText("/home/alice/.ssh/known_hosts"));

        alice.Run("ssh-keygen -t ed25519 -N \"\" -f /home/alice/.ssh/id_ed25519 -q");
        Assert.Null(forge.AddKey("alice", "Poste du lycée", alice.Ws.ReadText("/home/alice/.ssh/id_ed25519.pub")));
        Assert.Equal("taken", forge.AddKey("bob", "", alice.Ws.ReadText("/home/alice/.ssh/id_ed25519.pub")));
        Assert.Equal("invalid", forge.AddKey("alice", "", "ssh-ed25519 pas-une-cle"));

        var test = alice.Run("ssh -T git@gitlab.example");
        Assert.Equal(0, test.ExitCode);
        Assert.Equal("Welcome to GitLab, @alice!\n", test.Display);

        var clone = alice.Run("git clone git@gitlab.example:classe/agence.git");
        Assert.Equal(0, clone.ExitCode);
        Assert.Contains("You appear to have cloned an empty repository.", clone.Display);
        Assert.Single(alice.Questions);
    }

    [Fact]
    public void Ssh_GreetsLikeEachForge_AndRefusesOtherUsers()
    {
        var github = new SMAGX.Forge.Forge("github.example", ForgeKind.GitHub);
        var alice = new Student(github);
        alice.Run("ssh-keygen -q -N \"\" -f /home/alice/.ssh/id_ed25519");
        github.AddKey("alice", "", alice.Ws.ReadText("/home/alice/.ssh/id_ed25519.pub"));

        var test = alice.Run("ssh -T git@github.example", "yes");
        Assert.Equal(1, test.ExitCode);
        Assert.EndsWith("Hi alice! You've successfully authenticated, but GitHub does not provide shell access.\n", test.Display);

        var pty = alice.Run("ssh git@github.example");
        Assert.Equal("PTY allocation request failed on channel 0\nHi alice! You've successfully authenticated, but GitHub does not provide shell access.\nConnection to github.example closed.\n", pty.Display);

        var other = alice.Run("ssh -T alice@github.example");
        Assert.Equal(255, other.ExitCode);
        Assert.Equal("alice@github.example: Permission denied (publickey).\n", other.Display);

        var unknown = alice.Run("ssh -T git@gitlab.example");
        Assert.Equal("ssh: Could not resolve hostname gitlab.example: Name or service not known\n", unknown.Display);
    }

    [Fact]
    public void HostKey_RefusedThenForgottenThenChanged()
    {
        var forge = new SMAGX.Forge.Forge("gitlab.example");
        var alice = new Student(forge);
        var no = alice.Run("ssh -T git@gitlab.example", "peut-être", "no");
        Assert.Equal(255, no.ExitCode);
        Assert.EndsWith("(yes/no/[fingerprint])? peut-être\nPlease type 'yes', 'no' or the fingerprint: no\nHost key verification failed.\n", no.Display);

        alice.Run("ssh -T git@gitlab.example", SshPublicKey.HostKey("gitlab.example").Fingerprint);
        var forget = alice.Run("ssh-keygen -R gitlab.example");
        Assert.Equal("# Host gitlab.example found: line 1\n/home/alice/.ssh/known_hosts updated.\nOriginal contents retained as /home/alice/.ssh/known_hosts.old\n", forget.Display);

        // Une autre clé enregistrée pour ce nom : ssh refuse sans rien demander
        alice.Ws.WriteText("/home/alice/.ssh/known_hosts", $"gitlab.example {SshPublicKey.HostKey("autre").Type} {SshPublicKey.HostKey("autre").Base64}\n");
        var changed = alice.Run("ssh -T git@gitlab.example");
        Assert.Equal(255, changed.ExitCode);
        Assert.StartsWith("@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@\n@    WARNING: REMOTE HOST IDENTIFICATION HAS CHANGED!     @\n", changed.Display);
        Assert.Contains("Offending ED25519 key in /home/alice/.ssh/known_hosts:1\n", changed.Display);
        Assert.EndsWith("Host key verification failed.\n", changed.Display);
    }

    [Fact]
    public void Passphrase_IsAskedAtEachConnection()
    {
        var forge = new SMAGX.Forge.Forge("gitlab.example");
        var alice = new Student(forge);
        var keygen = alice.Run("ssh-keygen -f /home/alice/.ssh/id_ed25519", "secret", "autre", "secret", "secret");
        Assert.Contains("Passphrases do not match.  Try again.\n", keygen.Display);
        forge.AddKey("alice", "", alice.Ws.ReadText("/home/alice/.ssh/id_ed25519.pub"));

        var wrong = alice.Run("ssh -T git@gitlab.example", "yes", "a", "b", "c");
        Assert.Equal(255, wrong.ExitCode);
        Assert.Contains("Enter passphrase for key '/home/alice/.ssh/id_ed25519': \n", wrong.Display);
        Assert.DoesNotContain("a\n", wrong.Display.Replace("yes\n", ""));

        var right = alice.Run("ssh -T git@gitlab.example", "secret");
        Assert.Equal(0, right.ExitCode);
        Assert.Equal("Enter passphrase for key '/home/alice/.ssh/id_ed25519': \nWelcome to GitLab, @alice!\n", right.Display);
    }
}
