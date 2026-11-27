using SMAGX.Git;

namespace SMAGX.Machine;

/// <summary>Le résultat d'une tabulation : la ligne complétée, la position du curseur, et les choix possibles.</summary>
public sealed record Completion(string Line, int Caret, IReadOnlyList<string> Candidates)
{
    /// <summary>Plusieurs choix et rien à compléter : une deuxième tabulation les affiche, comme bash.</summary>
    public bool Ambiguous => Candidates.Count > 1;
}

/// <summary>
/// La complétion par tabulation du terminal, comme bash avec le script de complétion de Git : le nom d'une commande,
/// les sous-commandes et options de git, les branches (locales, distantes), les fichiers que la commande peut
/// toucher (modifiés pour git add, en préparation pour git restore --staged…), les dossiers pour cd, les serveurs
/// pour ssh. Un seul choix : il est écrit, suivi d'une espace (ou d'un « / » pour un dossier) ; plusieurs : leur début
/// commun.
/// </summary>
public static class Completer
{
    private static readonly string[] ShellCommands = ["cat", "cd", "clear", "echo", "git", "ls", "mkdir", "pwd", "rm", "ssh", "ssh-keygen", "touch"];

    /// <summary>Les commandes de Git que SMAG-X simule.</summary>
    private static readonly string[] GitCommands =
        ["add", "branch", "checkout", "clone", "commit", "config", "fetch", "init", "log", "merge", "pull", "push", "remote", "reset", "restore", "rm", "status", "switch", "version"];

    private static readonly Dictionary<string, string[]> GitOptions = new()
    {
        ["add"] = ["--all", "-A"],
        ["branch"] = ["--all", "--delete", "--move", "--verbose", "-D", "-a", "-d", "-m", "-v", "-vv"],
        ["checkout"] = ["-b", "--detach"],
        ["commit"] = ["--all", "--amend", "--message", "-a", "-m"],
        ["config"] = ["--get", "--global", "--list", "--unset"],
        ["fetch"] = ["--all"],
        ["log"] = ["--all", "--graph", "--oneline", "-n"],
        ["merge"] = ["--abort", "--ff-only", "--no-ff", "-m"],
        ["pull"] = ["--ff-only", "--no-rebase"],
        ["push"] = ["--force", "--set-upstream", "-u"],
        ["remote"] = ["-v"],
        ["reset"] = ["--hard", "--mixed", "--soft"],
        ["restore"] = ["--source", "--staged", "-S"],
        ["rm"] = ["--cached", "-f", "-r"],
        ["status"] = ["--short", "-s"],
        ["switch"] = ["--create", "--detach", "-C", "-c"]
    };

    private static readonly string[] ConfigKeys = ["color.ui", "core.editor", "init.defaultBranch", "pull.rebase", "user.email", "user.name"];

    public static Completion Complete(Workstation ws, string line, int caret)
    {
        caret = Math.Clamp(caret, 0, line.Length);
        var before = line[..caret];
        var start = WordStart(before);
        var word = Unescape(before[start..]);
        List<string> previous;
        try { previous = Shell.Tokenize(before[..start]); }
        catch (FormatException) { return new Completion(line, caret, []); }

        var choices = Choices(ws, previous, word).Where(c => c.Value.StartsWith(word, StringComparison.Ordinal))
            .DistinctBy(c => c.Value).OrderBy(c => c.Value, StringComparer.Ordinal).ToList();
        if (choices.Count == 0) return new Completion(line, caret, []);

        string completed;
        if (choices.Count == 1) completed = Escape(choices[0].Value) + choices[0].Suffix;
        else
        {
            var common = choices.Select(c => c.Value).Aggregate(CommonPrefix);
            completed = Escape(common);
        }
        // Les choix affichés : le dernier élément d'un chemin, avec « / » pour un dossier, comme bash
        var shown = choices.Select(c => (c.Path ? Shown(c.Value, word) : c.Value) + (c.Suffix == "/" ? "/" : "")).ToList();
        var after = line[caret..];
        if (completed.EndsWith(' ') && after.StartsWith(' ')) completed = completed[..^1];
        var text = before[..start] + completed;
        return new Completion(text + after, text.Length, choices.Count == 1 ? [] : shown);
    }

    private readonly record struct Choice(string Value, string Suffix, bool Path = false);

    private static IEnumerable<Choice> Words(IEnumerable<string> words) => words.Select(w => new Choice(w, " "));

    private static IEnumerable<Choice> Choices(Workstation ws, List<string> previous, string word)
    {
        if (previous.Count == 0) return Words(ShellCommands);
        var command = previous[0];
        var last = previous[^1];
        switch (command)
        {
            case "cd":
                return Paths(ws, word, directoriesOnly: true);
            case "ssh":
                if (word.StartsWith('-')) return Words(["-T", "-i", "-l", "-p", "-v"]);
                return last == "-i" ? Paths(ws, word, false) : Words(ws.Network.Select(f => "git@" + f.Host));
            case "ssh-keygen":
                if (last == "-t") return Words(["ecdsa", "ed25519", "rsa"]);
                if (last == "-f") return Paths(ws, word, false);
                if (last == "-R") return Words(ws.Network.Select(f => f.Host));
                return word.StartsWith('-') ? Words(["-C", "-N", "-R", "-b", "-f", "-l", "-q", "-t", "-v", "-y"]) : [];
            case "git":
                return GitChoices(ws, previous, word);
            case "echo" or "pwd" or "clear":
                return last.EndsWith('>') ? Paths(ws, word, false) : [];
            default:
                return Paths(ws, word, false);
        }
    }

    private static IEnumerable<Choice> GitChoices(Workstation ws, List<string> previous, string word)
    {
        if (previous.Count == 1) return Words(GitCommands);
        var sub = previous[1];
        var args = previous.Skip(2).ToList();
        var last = previous[^1];
        if (word.StartsWith('-')) return Words(GitOptions.GetValueOrDefault(sub) ?? []);

        var repo = ws.FindRepository(ws.Cwd);
        var positional = args.Count(a => !a.StartsWith('-'));
        switch (sub)
        {
            case "add":
                return repo is null ? [] : Changed(ws, repo, staged: false, untracked: true);
            case "restore":
                if (repo is null) return [];
                return args.Contains("--staged") || args.Contains("-S") ? Changed(ws, repo, staged: true, untracked: false) : Changed(ws, repo, staged: false, untracked: false);
            case "rm":
                return repo is null ? [] : Tracked(ws, repo);
            case "switch":
                return repo is null || last is "-c" or "-C" or "--create" ? [] : Words(LocalBranches(repo).Concat(RemoteNames(repo)));
            case "checkout":
                if (repo is null || last == "-b") return [];
                return args.Contains("--") ? Changed(ws, repo, staged: false, untracked: false)
                    : Words(LocalBranches(repo).Concat(RemoteBranches(repo)).Concat(RemoteNames(repo)));
            case "branch":
                if (repo is null) return [];
                return args.Any(a => a is "-d" or "-D" or "-m" or "--delete" or "--move") || positional >= 1
                    ? Words(LocalBranches(repo)) : [];
            case "merge" or "log" or "reset":
                return repo is null ? [] : Words(LocalBranches(repo).Concat(RemoteBranches(repo)).Append("HEAD"));
            case "push":
                if (repo is null) return [];
                return positional == 0 ? Words(Remotes(repo)) : Words(LocalBranches(repo));
            case "pull" or "fetch":
                if (repo is null) return [];
                return positional == 0 ? Words(Remotes(repo)) : Words(RemoteBranchesOf(repo, args.First(a => !a.StartsWith('-'))));
            case "remote":
                if (positional == 0) return Words(["add", "get-url", "remove", "set-url"]);
                return repo is not null && positional == 1 && args.First(a => !a.StartsWith('-')) is "remove" or "set-url" or "get-url"
                    ? Words(Remotes(repo)) : [];
            case "config":
                return positional == 0 ? Words(ConfigKeys) : [];
            case "init":
                return Paths(ws, word, directoriesOnly: true);
            default:
                return [];
        }
    }

    // ── Branches et distants ────────────────────────────────────────────────

    private static IEnumerable<string> LocalBranches(Repository repo) =>
        repo.Refs.Keys.Where(r => r.StartsWith("refs/heads/")).Select(r => r["refs/heads/".Length..]);

    private static IEnumerable<string> RemoteBranches(Repository repo) =>
        repo.Refs.Keys.Where(r => r.StartsWith("refs/remotes/") && !r.EndsWith("/HEAD")).Select(r => r["refs/remotes/".Length..]);

    // Une branche distante qu'aucune branche locale ne porte encore : « git switch tarifs » la crée en la suivant
    private static IEnumerable<string> RemoteNames(Repository repo)
    {
        var local = LocalBranches(repo).ToHashSet();
        return RemoteBranches(repo).Select(r => r[(r.IndexOf('/') + 1)..]).GroupBy(n => n).Where(g => g.Count() == 1 && !local.Contains(g.Key)).Select(g => g.Key);
    }

    private static IEnumerable<string> RemoteBranchesOf(Repository repo, string remote) =>
        RemoteBranches(repo).Where(r => r.StartsWith(remote + "/")).Select(r => r[(remote.Length + 1)..]);

    private static IEnumerable<string> Remotes(Repository repo) =>
        repo.Config.Keys.Where(k => k.StartsWith("remote.") && k.EndsWith(".url")).Select(k => k["remote.".Length..^".url".Length]);

    // ── Fichiers ────────────────────────────────────────────────────────────

    // Les fichiers modifiés (ou en préparation), et non suivis : ceux que git add ou git restore peuvent toucher
    private static IEnumerable<Choice> Changed(Workstation ws, Repository repo, bool staged, bool untracked)
    {
        var status = Status.Compute(ws, repo);
        var paths = staged ? status.Staged.Select(c => c.Path)
            : status.Unstaged.Select(c => c.Path).Concat(status.Conflicts.Select(c => c.Path)).Concat(untracked ? status.Untracked : []);
        return paths.Select(p => Status.Show(ws, repo, p)).Select(p => p.EndsWith('/') ? new Choice(p[..^1], "/", true) : new Choice(p, " ", true));
    }

    private static IEnumerable<Choice> Tracked(Workstation ws, Repository repo) =>
        repo.Index.Entries.Select(e => new Choice(Status.Show(ws, repo, e.Path), " ", true));

    // Les fichiers et dossiers du dossier tapé (« src/ma » : ceux de src qui commencent par « ma »)
    private static IEnumerable<Choice> Paths(Workstation ws, string word, bool directoriesOnly)
    {
        var slash = word.LastIndexOf('/');
        var folder = slash < 0 ? "" : word[..(slash + 1)];
        var name = word[(slash + 1)..];
        var dir = ws.Resolve(folder.Length == 0 ? "." : folder);
        if (!ws.DirectoryExists(dir)) yield break;
        var prefix = dir == "/" ? "/" : dir + "/";
        var entries = ws.Directories.Where(d => d != dir && Workstation.Parent(d) == dir).Select(d => (Name: d[prefix.Length..], Directory: true))
            .Concat(directoriesOnly ? [] : ws.Files.Keys.Where(f => Workstation.Parent(f) == dir).Select(f => (Name: f[prefix.Length..], Directory: false)));
        foreach (var (entry, isDirectory) in entries)
        {
            if (entry == ".git" || (entry.StartsWith('.') && !name.StartsWith('.'))) continue;
            yield return new Choice(folder + entry, isDirectory ? "/" : " ", true);
        }
    }

    // ── Mots ────────────────────────────────────────────────────────────────

    // Le début du mot sous le curseur : après la dernière espace non échappée (et hors guillemets)
    private static int WordStart(string text)
    {
        var start = 0;
        var quote = '\0';
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (quote != '\0') { if (c == quote) quote = '\0'; continue; }
            if (c is '"' or '\'') quote = c;
            else if (c == '\\') i++;
            else if (c == ' ') start = i + 1;
        }
        return start;
    }

    private static string Unescape(string word) => word.Replace("\\ ", " ").Trim('"', '\'');

    private static string Escape(string value) => value.Replace(" ", "\\ ");

    private static string Shown(string value, string word) =>
        word.Contains('/') && value.LastIndexOf('/') is var i and >= 0 ? value[(i + 1)..] : value;

    private static string CommonPrefix(string a, string b)
    {
        var n = 0;
        while (n < a.Length && n < b.Length && a[n] == b[n]) n++;
        return a[..n];
    }
}
