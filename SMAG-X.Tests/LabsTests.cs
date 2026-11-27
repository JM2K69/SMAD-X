using System.Net;
using SMAGX.Fidelity;
using SMAGX.Forge;
using SMAGX.Labs;
using SMAGX.Machine;

namespace SMAGX.Tests;

/// <summary>
/// Les scénarios fournis : leur départ se joue sans erreur, aucun objectif n'est atteint au départ (les gardes, si), et
/// une solution (ici, pas dans les scénarios) les atteint tous. Un scénario à deux se joue aussi sur deux SMAG-X.
/// </summary>
public class LabsTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 27, 8, 0, 0, TimeSpan.FromHours(1));

    // Les solutions : « @rôle », « $ commande », « write », « append », « key » (la clé publique ajoutée sur la forge)
    private static readonly Dictionary<string, string> Solutions = new()
    {
        ["premiers-pas"] = """
            $ git config --global user.name "Alice Martin"
            $ git config --global user.email alice@lycee.example
            $ git init
            write index.html <h1>Bonjour</h1>\n
            $ git add index.html
            $ git commit -m "Première page"
            append index.html <p>Suite</p>\n
            $ git commit -am "Deuxième version"
            """,
        ["trois-zones"] = """
            $ git add style.css
            $ git commit -m "Nouvelle couleur"
            $ git restore index.html
            write .gitignore brouillon.txt\n
            $ git add .gitignore
            $ git commit -m "Ignorer le brouillon"
            """,
        ["branches"] = """
            $ git switch contact
            write contact.html <p>Contact</p>\n
            $ git add contact.html
            $ git commit -m "Page contact"
            $ git switch main
            append index.html <a href="contact.html">Contact</a>\n
            $ git commit -am "Lien vers le contact"
            $ git merge contact
            $ git branch -d contact
            """,
        ["conflit"] = """
            $ git merge titre
            write index.html <h1>Agence Méridien Voyages</h1>\n<p>Bienvenue</p>\n
            $ git add index.html
            $ git commit -m "Titre retenu"
            """,
        ["publier"] = """
            $ ssh-keygen -t ed25519 -N "" -f ~/.ssh/id_ed25519
            key
            $ ssh -T git@gitlab.example
            $ git remote add origin git@gitlab.example:classe/portfolio.git
            $ git push -u origin main
            """,
        ["contribuer"] = """
            key
            $ git clone git@github.example:classe/recettes.git
            $ cd recettes
            $ git switch -c gaufres
            write recettes/gaufres.md # Gaufres\n
            $ git add recettes/gaufres.md
            $ git commit -m "Recette des gaufres"
            $ git push -u origin gaufres
            """,
        ["remonter-le-temps"] = """
            $ git reset HEAD~1
            write .gitignore secret.txt\n
            $ git add .gitignore
            $ git commit -m "Ignorer secret.txt"
            $ git branch v1 HEAD~3
            """,
        ["a-deux"] = """
            @alice
            $ git clone git@gitlab.example:classe/agence.git
            $ cd agence
            write equipe.html <h2>L'équipe</h2>\n
            $ git add equipe.html
            $ git commit -m "Page équipe"
            $ git push
            @bob
            $ git clone git@gitlab.example:classe/agence.git
            $ cd agence
            write tarifs.html <h2>Tarifs</h2>\n
            $ git add tarifs.html
            $ git commit -m "Page tarifs"
            $ git push
            $ git pull --no-rebase
            $ git push
            @alice
            $ git pull
            """,
        ["conflit-a-deux"] = """
            @alice
            write index.html <h1>Agence Méridien, voyages</h1>\n<p>Bienvenue</p>\n
            $ git commit -am "Titre d'Alice"
            $ git push
            @bob
            write index.html <h1>Méridien : séjours</h1>\n<p>Bienvenue</p>\n
            $ git commit -am "Titre de Bob"
            $ git push
            $ git pull --no-rebase
            write index.html <h1>Agence Méridien : voyages et séjours</h1>\n<p>Bienvenue</p>\n
            $ git add index.html
            $ git commit -m "Titre retenu"
            $ git push
            @alice
            $ git pull
            """,
        ["branche-relue"] = """
            @bob
            $ git switch tarifs
            write tarifs.html <h2>Tarifs</h2>\n
            $ git add tarifs.html
            $ git commit -m "Page tarifs"
            $ git push -u origin tarifs
            @alice
            $ git fetch
            $ git log --oneline origin/tarifs
            $ git merge origin/tarifs
            $ git push
            @bob
            $ git switch main
            $ git pull
            $ git branch -d tarifs
            """
    };

    public static TheoryData<string> Labs()
    {
        var data = new TheoryData<string>();
        foreach (var lab in SmagxBuiltInLabs.All()) data.Add(lab.Id);
        return data;
    }

    private static SmagxLab Lab(string id) => SmagxBuiltInLabs.All().Single(l => l.Id == id);

    /// <summary>Le poste d'un rôle, comme l'application le crée : l'identité du rôle (« moi » : celle d'Alice Martin), ~/projet.</summary>
    private static Workstation Station(SmagxRole role, IEnumerable<IForge> forges)
    {
        var ws = new Workstation(role.Login ?? "alice", "poste")
        {
            FullName = role.Name ?? "Alice Martin", Email = role.Email ?? "alice@lycee.example",
            Interactive = false, Ask = (_, _) => "yes", Random = new Random(3)
        };
        ws.Network.AddRange(forges);
        ws.EnsureDirectory(ws.Home + "/projet");
        ws.Cwd = ws.Home + "/projet";
        return ws;
    }

    private static List<SMAGX.Forge.Forge> Forges() =>
        [new SMAGX.Forge.Forge("gitlab.example"), new SMAGX.Forge.Forge("github.example", ForgeKind.GitHub)];

    private static void Play(SmagxLab lab, string solution, IReadOnlyDictionary<string, Workstation> stations)
    {
        var shells = stations.ToDictionary(s => s.Key, s => new Shell(s.Value));
        var role = lab.Roles[0].Id;
        var minute = 0;
        foreach (var step in Transcript.Parse(solution))
        {
            if (step.Kind == "@") { role = step.Argument; continue; }
            if (!stations.TryGetValue(role, out var ws)) continue;
            switch (step.Kind)
            {
                case "$":
                    var date = new DateTimeOffset(2026, 11, 27, 10, 0, 0, TimeSpan.FromHours(1)).AddMinutes(minute++);
                    ws.Clock = () => date;
                    shells[role].Run(step.Argument);
                    break;
                case "write":
                    ws.WriteText(ws.Resolve(step.Argument), step.Content ?? "");
                    break;
                case "append":
                    var path = ws.Resolve(step.Argument);
                    ws.WriteText(path, ws.ReadText(path) + step.Content);
                    break;
                case "key":
                    foreach (var forge in ws.Network) forge.AddKey(ws.User, "", ws.ReadText(ws.Home + "/.ssh/id_ed25519.pub").Trim());
                    break;
            }
        }
    }

    [Theory]
    [MemberData(nameof(Labs))]
    public void Lab_StartsUnsolved_AndItsSolutionReachesEveryObjective(string id)
    {
        var lab = Lab(id);
        var forges = Forges();
        var stations = lab.Roles.ToDictionary(r => r.Id, r => Station(r, forges));
        Assert.Empty(SmagxLabSetup.Apply(lab, stations, forges, Start));

        var start = SmagxLabGrader.Grade(lab, stations, forges, null, "fr-FR");
        Assert.All(start.Objectives, r => Assert.Equal(r.Objective.Guard, r.Passed));
        Assert.Equal(0, start.Score);

        Play(lab, Solutions[id], stations);
        var end = SmagxLabGrader.Grade(lab, stations, forges, null, "fr-FR");
        Assert.All(end.Objectives, r => Assert.True(r.Passed, $"{r.Objective.Id} : {r.Feedback}"));
        Assert.True(end.Completed);
        Assert.Equal(lab.MaxPoints, end.Score);
    }

    [Fact]
    public void Labs_FormAProgressivePath()
    {
        var labs = SmagxBuiltInLabs.All();
        Assert.Equal(Enumerable.Range(1, labs.Count), labs.Select(l => l.Step));
        Assert.Equal(labs.Select(l => l.Level).Order(), labs.Where(l => !l.IsPair).Concat(labs.Where(l => l.IsPair)).Select(l => l.Level).Order());
        Assert.All(labs.Where(l => l.IsPair), l => Assert.Single(l.Roles, r => r.Serves));
        Assert.All(labs, l => Assert.Equal(l.Objectives.Count, l.Objectives.Select(o => o.Id).Distinct().Count()));
    }

    [Fact]
    public void PairLab_PlaysOnTwoSmagx()
    {
        var lab = Lab("a-deux");
        var forges = Forges();
        using var server = new ForgeServer(forges, "2468");
        server.Start(0, IPAddress.Loopback);
        var remotes = RemoteForge.Connect("127.0.0.1", server.Port, "2468");

        // Le SMAG-X d'Alice sert la forge et la prépare ; celui de Bob ne prépare que son poste
        var alice = new Dictionary<string, Workstation> { ["alice"] = Station(lab.Roles[0], forges) };
        var bob = new Dictionary<string, Workstation> { ["bob"] = Station(lab.Roles[1], remotes) };
        Assert.Empty(SmagxLabSetup.Apply(lab, alice, forges, Start));
        Assert.Empty(SmagxLabSetup.Apply(lab, bob, null, Start));

        var all = new Dictionary<string, Workstation> { ["alice"] = alice["alice"], ["bob"] = bob["bob"] };
        Play(lab, Solutions["a-deux"], all);

        var onAlice = SmagxLabGrader.Grade(lab, alice, forges, null, "fr-FR");
        var onBob = SmagxLabGrader.Grade(lab, bob, remotes, null, "fr-FR");
        foreach (var (result, other) in new[] { (onAlice, "bob"), (onBob, "alice") })
        {
            Assert.All(result.Objectives, r =>
            {
                if (r.Objective.Check.Role == other) Assert.Null(r.Passed);
                else Assert.True(r.Passed, $"{r.Objective.Id} : {r.Feedback}");
            });
            Assert.True(result.Completed);
        }
    }
}
