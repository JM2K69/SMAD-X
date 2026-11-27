using System.Text;
using SMAGX.Machine;

namespace SMAGX.Git;

/// <summary>Le contexte d'une commande git : le poste, la sortie, les arguments et le dépôt du dossier courant.</summary>
public sealed class GitContext(Workstation ws, CommandResult output, List<string> args)
{
    public Workstation Ws { get; } = ws;
    public CommandResult Out { get; } = output;
    public List<string> Args { get; } = args;

    private Repository? _repo;
    public Repository Repo => _repo ??= Ws.FindRepository(Ws.Cwd)!;
    public bool InRepository => Ws.FindRepository(Ws.Cwd) is not null;

    public string? Config(string key) => Ws.Config(Ws.FindRepository(Ws.Cwd), key);

    /// <summary>Le chemin, relatif à la racine du dépôt, d'un chemin tapé par l'élève.</summary>
    public string RepoPath(string typed)
    {
        var absolute = Ws.Resolve(typed);
        return absolute == Repo.Root ? "" : absolute[(Repo.Root.Length + 1)..];
    }

    public bool InsideRepository(string typed)
    {
        var absolute = Ws.Resolve(typed);
        return absolute == Repo.Root || absolute.StartsWith(Repo.Root + "/", StringComparison.Ordinal);
    }

    /// <summary>L'auteur des commits : user.name et user.email de la configuration, à l'heure du poste.</summary>
    public Signature? Identity()
    {
        var name = Config("user.name");
        var email = Config("user.email");
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email)) return null;
        return new Signature(name, email, Ws.Clock());
    }

    /// <summary>Le message de Git quand l'identité manque (user.name ou user.email).</summary>
    public int IdentityMissing()
    {
        var email = Config("user.email");
        Out.Error("Author identity unknown\n\n*** Please tell me who you are.\n\nRun\n\n" +
                  "  git config --global user.email \"you@example.com\"\n  git config --global user.name \"Your Name\"\n\n" +
                  "to set your account's default identity.\nOmit --global to set the identity only in this repository.\n\n");
        return Out.Fatal(string.IsNullOrWhiteSpace(Config("user.name")) && email is null
            ? $"unable to auto-detect email address (got '{Ws.User}@{Ws.Host}.(none)')"
            : string.IsNullOrWhiteSpace(Config("user.name")) ? "empty ident name (for <" + email + ">) not allowed"
            : $"unable to auto-detect email address (got '{Ws.User}@{Ws.Host}.(none)')");
    }

    /// <summary>
    /// Les lignes d'aide de Git (« hint: … »), en jaune : Git ne les colore qu'une fois sa configuration lue, pas celles
    /// de « git init » ni d'un nom de branche refusé (color: false, mesuré).
    /// </summary>
    public void Hint(string text, bool color = true)
    {
        foreach (var line in text.Split('\n'))
        {
            var hint = line.Length == 0 ? "hint:" : "hint: " + line;
            Out.Error((color ? Ansi.Paint(Ansi.Yellow, hint) : hint) + "\n");
        }
    }
}

/// <summary>La commande « git » du poste : les options globales, puis la sous-commande.</summary>
public static partial class GitCli
{
    public static int Run(Workstation ws, List<string> args, CommandResult r)
    {
        var i = 0;
        while (i < args.Count && args[i].StartsWith('-'))
        {
            if (args[i] == "-c" && i + 1 < args.Count) i += 2;
            else if (args[i] == "-C" && i + 1 < args.Count) { ws.Cwd = ws.Resolve(args[i + 1]); i += 2; }
            else if (args[i] is "--version" or "-v") { r.Write("git version 2.55.0\n"); return 0; }
            else break;
        }
        if (i >= args.Count)
        {
            r.Write(Usage);
            return r.ExitCode = 1;
        }
        var command = args[i];
        var ctx = new GitContext(ws, r, args.Skip(i + 1).ToList());
        // Une commande inconnue se signale même hors d'un dépôt (mesuré)
        if (!MainCommands.Contains(command)) return r.ExitCode = NotACommand(command, r);
        if (command is not ("init" or "clone" or "config" or "version" or "help") && !ctx.InRepository)
            return r.Fatal("not a git repository (or any of the parent directories): .git");
        r.ExitCode = command switch
        {
            "init" => Init(ctx),
            "config" => ConfigCommand(ctx),
            "status" => StatusCommand(ctx),
            "add" => Add(ctx),
            "commit" => CommitCommand(ctx),
            "log" => Log(ctx),
            "branch" => Branch(ctx),
            "switch" => Switch(ctx),
            "checkout" => Checkout(ctx),
            "merge" => Merge(ctx),
            "restore" => Restore(ctx),
            "reset" => Reset(ctx),
            "rm" => Remove(ctx),
            "remote" => Remote(ctx),
            "clone" => Clone(ctx),
            "fetch" => Fetch(ctx),
            "push" => Push(ctx),
            "pull" => Pull(ctx),
            "version" => Version(r),
            _ => NotSimulated(command, r)
        };
        return r.ExitCode;
    }

    /// <summary>Une vraie commande de Git que le simulateur ne reproduit pas (encore) : il le dit, en français.</summary>
    private static int NotSimulated(string command, CommandResult r)
    {
        r.Error($"SMAG-X : « git {command} » n'est pas reproduite par le simulateur.\n");
        return 1;
    }

    private const string Usage = "usage: git [-v | --version] [-h | --help] [-C <path>] [-c <name>=<value>]\n           <command> [<args>]\n";

    private static int Version(CommandResult r)
    {
        r.Write("git version 2.55.0\n");
        return 0;
    }


    // ── Révisions ───────────────────────────────────────────────────────────

    /// <summary>Le commit désigné par une révision : HEAD, branche, étiquette, branche distante, SHA abrégé, suivis de ~n ou ^n.</summary>
    public static ObjectId? Resolve(Repository repo, string spec)
    {
        var suffix = spec.IndexOfAny(['~', '^']);
        var name = suffix < 0 ? spec : spec[..suffix];
        var id = ResolveName(repo, name);
        if (id is null || suffix < 0) return id;
        var rest = spec[suffix..];
        var pos = 0;
        while (pos < rest.Length && id is not null)
        {
            var op = rest[pos++];
            var digits = 0;
            var start = pos;
            while (pos < rest.Length && char.IsDigit(rest[pos])) pos++;
            var n = pos > start ? int.Parse(rest[start..pos]) : 1;
            digits++;
            if (op == '~')
                for (var k = 0; k < n && id is not null; k++)
                    id = repo.GetCommit(id.Value).Parents is [var first, ..] ? first : null;
            else
                id = n == 0 ? id : repo.GetCommit(id.Value).Parents.Count >= n ? repo.GetCommit(id.Value).Parents[n - 1] : null;
        }
        return id;
    }

    private static ObjectId? ResolveName(Repository repo, string name)
    {
        if (name is "HEAD" or "@") return repo.Head;
        foreach (var full in new[] { name, "refs/" + name, "refs/tags/" + name, "refs/heads/" + name, "refs/remotes/" + name, "refs/remotes/" + name + "/HEAD" })
            if (repo.Refs.TryGetValue(full, out var id)) return id;
        if (name.Length >= 4 && name.All(Uri.IsHexDigit))
        {
            var matches = repo.Objects.WithPrefix(name).Where(id => repo.Objects.Get(id) is Commit).ToList();
            if (matches.Count == 1) return matches[0];
        }
        return null;
    }

    /// <summary>Les décorations d'un commit (« (HEAD -> main, origin/main) »), colorées comme Git : références triées puis inversées, HEAD en tête.</summary>
    public static string Decorations(Repository repo, ObjectId id)
    {
        var names = repo.Refs.Where(r => r.Value.Equals(id) && !r.Key.StartsWith("refs/stash"))
            .Select(r => r.Key).Reverse().ToList();
        var items = new List<string>();
        if (repo.HeadRef is { } head && names.Contains(head))
        {
            names.Remove(head);
            items.Add(Ansi.Paint(Ansi.BoldCyan, "HEAD") + Ansi.Paint(Ansi.Yellow, " -> ") + Ansi.Paint(Ansi.BoldGreen, Short(head)));
        }
        else if (repo.HeadRef is null && repo.DetachedHead is { } detached && detached.Equals(id))
            items.Add(Ansi.Paint(Ansi.BoldCyan, "HEAD"));
        foreach (var name in names)
            items.Add(name.StartsWith("refs/heads/") ? Ansi.Paint(Ansi.BoldGreen, Short(name))
                : name.StartsWith("refs/remotes/") ? Ansi.Paint(Ansi.BoldRed, Short(name))
                : name.StartsWith("refs/tags/") ? Ansi.Paint(Ansi.BoldYellow, "tag: " + Short(name))
                : Short(name));
        if (items.Count == 0) return "";
        return Ansi.Paint(Ansi.Yellow, " (") + string.Join(Ansi.Paint(Ansi.Yellow, ", "), items) + Ansi.Paint(Ansi.Yellow, ")");
    }

    public static string Short(string refName) =>
        refName.StartsWith("refs/heads/") ? refName["refs/heads/".Length..]
        : refName.StartsWith("refs/remotes/") ? refName["refs/remotes/".Length..]
        : refName.StartsWith("refs/tags/") ? refName["refs/tags/".Length..]
        : refName;

    /// <summary>Les options d'une commande : drapeaux courts regroupés (« -am » = « -a -m »), valeurs collées ou séparées.</summary>
    private static List<string> SplitFlags(List<string> args, string flagsWithValue)
    {
        var result = new List<string>();
        for (var i = 0; i < args.Count; i++)
        {
            var a = args[i];
            if (a == "--") { result.AddRange(args.Skip(i)); break; }
            if (a.Length > 2 && a[0] == '-' && a[1] != '-')
            {
                for (var k = 1; k < a.Length; k++)
                {
                    result.Add("-" + a[k]);
                    if (flagsWithValue.Contains(a[k]))
                    {
                        if (k + 1 < a.Length) result.Add(a[(k + 1)..]);
                        break;
                    }
                }
            }
            else result.Add(a);
        }
        return result;
    }

    /// <summary>Le message nettoyé comme « git commit -m » (mode whitespace) : espaces de fin, lignes vides en trop, fin de ligne.</summary>
    public static string CleanMessage(string message)
    {
        var lines = message.Replace("\r\n", "\n").Split('\n').Select(l => l.TrimEnd()).ToList();
        var result = new List<string>();
        foreach (var line in lines)
        {
            if (line.Length == 0 && (result.Count == 0 || result[^1].Length == 0)) continue;
            result.Add(line);
        }
        while (result.Count > 0 && result[^1].Length == 0) result.RemoveAt(result.Count - 1);
        return result.Count == 0 ? "" : string.Join('\n', result) + "\n";
    }
}
