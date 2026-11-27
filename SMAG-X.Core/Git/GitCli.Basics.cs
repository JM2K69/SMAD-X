using System.Text;
using SMAGX.Machine;

namespace SMAGX.Git;

public static partial class GitCli
{
    // ── git init ────────────────────────────────────────────────────────────

    private static int Init(GitContext c)
    {
        var args = c.Args;
        string? branch = null;
        string? dir = null;
        for (var i = 0; i < args.Count; i++)
        {
            if (args[i] is "-b" or "--initial-branch" && i + 1 < args.Count) branch = args[++i];
            else if (args[i].StartsWith("--initial-branch=")) branch = args[i]["--initial-branch=".Length..];
            else if (args[i] == "--bare") { }
            else if (!args[i].StartsWith('-')) dir = args[i];
        }
        var root = dir is null ? c.Ws.Cwd : c.Ws.Resolve(dir);
        c.Ws.EnsureDirectory(root);
        if (c.Ws.Repositories.ContainsKey(root))
        {
            c.Out.Write($"Reinitialized existing Git repository in {root}/.git/\n");
            return 0;
        }
        if (branch is null && c.Ws.Config(null, "init.defaultBranch") is { } configured) branch = configured;
        if (branch is null)
        {
            c.Hint("Using 'master' as the name for the initial branch. This default branch name\n" +
                   "will change to \"main\" in Git 3.0. To configure the initial branch name\n" +
                   "to use in all of your new repositories, which will suppress this warning,\ncall:\n\n" +
                   "\tgit config --global init.defaultBranch <name>\n\n" +
                   "Names commonly chosen instead of 'master' are 'main', 'trunk' and\n" +
                   "'development'. The just-created branch can be renamed via this command:\n\n" +
                   "\tgit branch -m <name>\n\n" +
                   "Disable this message with \"git config set advice.defaultBranchName false\"", color: false);
            branch = "master";
        }
        c.Ws.Repositories[root] = new Repository(root) { HeadRef = "refs/heads/" + branch };
        c.Out.Write($"Initialized empty Git repository in {root}/.git/\n");
        return 0;
    }

    // ── git config ──────────────────────────────────────────────────────────

    private static int ConfigCommand(GitContext c)
    {
        var args = c.Args.ToList();
        var global = args.Remove("--global");
        args.Remove("--local");
        var verb = args.Count > 0 && args[0] is "set" or "get" or "unset" or "list" ? args[0] : null;
        if (verb is not null) args.RemoveAt(0);
        var list = verb == "list" || args.Remove("--list") || args.Remove("-l");
        var unset = verb == "unset" || args.Remove("--unset");
        args.Remove("--get");

        var repo = c.Ws.FindRepository(c.Ws.Cwd);
        if (list)
        {
            var text = new StringBuilder();
            foreach (var (key, value) in c.Ws.GlobalConfig) text.Append(key).Append('=').Append(value).Append('\n');
            if (!global && repo is not null)
                foreach (var (key, value) in repo.Config) text.Append(key).Append('=').Append(value).Append('\n');
            c.Out.Write(text.ToString());
            return 0;
        }
        if (args.Count == 0)
        {
            c.Out.Error("error: wrong number of arguments, should be from 1 to 2\n");
            return 129;
        }
        var name = args[0];
        if (!name.Contains('.'))
        {
            c.Out.Error($"error: key does not contain a section: {name}\n");
            return 2;
        }
        Dictionary<string, string>? store = global ? c.Ws.GlobalConfig : repo?.Config;
        if (args.Count == 1 && !unset)
        {
            var value = global ? c.Ws.GlobalConfig.GetValueOrDefault(name) : c.Ws.Config(repo, name);
            if (value is null) return 1;
            c.Out.Write(value + "\n");
            return 0;
        }
        if (store is null) return c.Out.Fatal("not in a git directory");
        if (unset) return store.Remove(name) ? 0 : 5;
        store[name] = args[1];
        return 0;
    }

    // ── git status ──────────────────────────────────────────────────────────

    private static int StatusCommand(GitContext c)
    {
        var report = Status.Compute(c.Ws, c.Repo);
        c.Out.Write(c.Args.Any(a => a is "-s" or "--short")
            ? Status.Short(c.Ws, report)
            : Status.Long(c.Ws, report, Tracking(c.Repo)));
        return 0;
    }


    // ── git add ─────────────────────────────────────────────────────────────

    private static int Add(GitContext c)
    {
        var args = c.Args.Where(a => a != "--").ToList();
        var all = args.Remove("-A") | args.Remove("--all");
        var force = args.Remove("-f") | args.Remove("--force");
        if (args.Count == 0 && !all)
        {
            c.Out.Error("Nothing specified, nothing added.\n");
            c.Hint("Maybe you wanted to say 'git add .'?\nDisable this message with \"git config set advice.addEmptyPathspec false\"");
            return 0;
        }
        var repo = c.Repo;
        var work = Status.WorkTree(c.Ws, repo);
        var ignore = Ignore.Load(work);
        var specs = all && args.Count == 0 ? [""] : new List<string>();
        foreach (var arg in args)
        {
            if (!c.InsideRepository(arg))
                return c.Out.Fatal($"{arg}: '{arg}' is outside repository at '{repo.Root}'");
            specs.Add(c.RepoPath(arg));
        }

        var ignoredNamed = new List<string>();
        foreach (var (spec, typed) in specs.Zip(all && args.Count == 0 ? [""] : args))
        {
            bool Match(string path) => spec.Length == 0 || path == spec || path.StartsWith(spec + "/", StringComparison.Ordinal);
            var files = work.Keys.Where(Match).ToList();
            var tracked = repo.Index.Entries.Select(e => e.Path).Concat(repo.Index.Conflicts.Keys).Where(Match).ToList();
            if (files.Count == 0 && tracked.Count == 0)
                return c.Out.Fatal($"pathspec '{typed}' did not match any files");
            foreach (var path in files)
            {
                if (!repo.Index.Contains(path) && ignore.IsIgnored(path) && !force)
                {
                    if (path == spec) ignoredNamed.Add(typed);
                    continue;
                }
                var id = repo.Objects.Put(new Blob(work[path]));
                repo.Index.Set(new IndexEntry(path, TreeEntry.FileMode, id));
            }
            foreach (var path in tracked.Where(p => !work.ContainsKey(p)))
                repo.Index.Remove(path);
        }
        if (ignoredNamed.Count > 0)
        {
            c.Out.Error("The following paths are ignored by one of your .gitignore files:\n" + string.Join("", ignoredNamed.Select(p => p + "\n")));
            c.Hint("Use -f if you really want to add them.\nDisable this message with \"git config set advice.addIgnoredFile false\"");
            return 1;
        }
        return 0;
    }

    // ── git commit ──────────────────────────────────────────────────────────

    private static int CommitCommand(GitContext c)
    {
        var args = SplitFlags(c.Args, "m");
        var messages = new List<string>();
        bool all = false, amend = false, allowEmpty = false;
        for (var i = 0; i < args.Count; i++)
        {
            switch (args[i])
            {
                case "-m" or "--message" when i + 1 < args.Count: messages.Add(args[++i]); break;
                case "-a" or "--all": all = true; break;
                case "--amend": amend = true; break;
                case "--allow-empty": allowEmpty = true; break;
                default:
                    if (args[i].StartsWith("--message=")) messages.Add(args[i]["--message=".Length..]);
                    break;
            }
        }
        var repo = c.Repo;
        if (repo.Index.HasConflicts)
        {
            c.Out.Write(string.Concat(repo.Index.Conflicts.Keys.Select(p => "U\t" + p + "\n")));
            c.Out.Error("error: Committing is not possible because you have unmerged files.\n");
            c.Hint("Fix them up in the work tree, and then use 'git add/rm <file>'\nas appropriate to mark resolution and make a commit.");
            return c.Out.Fatal("Exiting because of an unresolved conflict.");
        }
        if (amend && repo.Head is null) return c.Out.Fatal("You have nothing to amend.");

        if (all)
        {
            var work = Status.WorkTree(c.Ws, repo);
            foreach (var entry in repo.Index.Entries.ToList())
            {
                if (!work.TryGetValue(entry.Path, out var data)) repo.Index.Remove(entry.Path);
                else repo.Index.Set(entry with { Id = repo.Objects.Put(new Blob(data)) });
            }
        }

        var parentFiles = amend
            ? repo.Files(repo.GetCommit(repo.Head!.Value).Parents is [var first, ..] ? first : null)
            : repo.Files(repo.Head);
        var tree = repo.WriteTree(repo.Index.Entries.Select(e => (e.Path, e.Mode, e.Id)));
        var headTree = repo.Head is { } h ? repo.GetCommit(h).Tree : (ObjectId?)null;
        if (!amend && !allowEmpty && repo.MergeHead is null && headTree is { } ht && ht.Equals(tree)
            || !amend && repo.Head is null && !repo.Index.Entries.Any() && !allowEmpty)
        {
            c.Out.Write(Status.Long(c.Ws, Status.Compute(c.Ws, repo), Tracking(repo)));
            return 1;
        }

        var author = c.Identity();
        if (author is null) return c.IdentityMissing();

        var message = messages.Count > 0 ? CleanMessage(string.Join("\n\n", messages))
            : amend ? repo.GetCommit(repo.Head!.Value).Message
            : repo.MergeMessage is { } merge ? CleanMessage(StripComments(merge))
            : null;
        if (message is null && c.Ws.Editor is { } editor)
            message = CleanMessage(StripComments(editor(CommitTemplate(c, repo)) ?? ""));
        if (string.IsNullOrEmpty(message))
        {
            c.Out.Error("Aborting commit due to empty commit message.\n");
            return 1;
        }

        IReadOnlyList<ObjectId> parents;
        Signature commitAuthor = author;
        if (amend)
        {
            var old = repo.GetCommit(repo.Head!.Value);
            parents = old.Parents;
            commitAuthor = old.Author;
        }
        else
        {
            var list = new List<ObjectId>();
            if (repo.Head is { } head) list.Add(head);
            if (repo.MergeHead is { } mergeHead) list.Add(mergeHead);
            parents = list;
        }
        var commit = new Commit(tree, parents, commitAuthor, author, message);
        var id = repo.Objects.Put(commit);
        var root = parents.Count == 0;
        var logMessage = (amend ? "commit (amend): " : root ? "commit (initial): " : repo.MergeHead is not null ? "commit (merge): " : "commit: ") + commit.Subject;
        MoveHead(repo, id, author, logMessage);
        repo.MergeHead = null;
        repo.MergeMessage = null;

        var branch = repo.CurrentBranch ?? "detached HEAD";
        c.Out.Write($"[{branch}{(root ? " (root-commit)" : "")} {id.Short}] {commit.Subject}\n");
        if (amend) c.Out.Write($" Date: {commitAuthor.LogDate}\n");
        if (parents.Count < 2) c.Out.Write(Summary(repo, parentFiles, repo.Files(id), modes: true));
        return 0;
    }

    /// <summary>Déplace HEAD : la branche courante, ou HEAD détachée.</summary>
    internal static void MoveHead(Repository repo, ObjectId id, Signature who, string message)
    {
        if (repo.HeadRef is { } head) repo.UpdateRef(head, id, who, message);
        else
        {
            repo.Log("HEAD", repo.DetachedHead, id, who, message);
            repo.DetachedHead = id;
        }
    }

    private static string CommitTemplate(GitContext c, Repository repo) =>
        "\n# Please enter the commit message for your changes. Lines starting\n# with '#' will be ignored, and an empty message aborts the commit.\n#\n" +
        string.Concat(Status.Long(c.Ws, Status.Compute(c.Ws, repo)).Split('\n').Select(l => l.Length == 0 ? "#\n" : "# " + l + "\n"));

    private static string StripComments(string text) =>
        string.Join('\n', text.Split('\n').Where(l => !l.StartsWith('#')));

    /// <summary>« 2 files changed, 3 insertions(+), 1 deletion(-) » puis les « create mode » et « delete mode ».</summary>
    internal static string Summary(Repository repo, Dictionary<string, TreeEntry> before, Dictionary<string, TreeEntry> after, bool modes)
    {
        var (files, insertions, deletions, created, deleted) = Changes(repo, before, after);
        var text = new StringBuilder();
        text.Append(SummaryLine(files.Count, insertions, deletions));
        if (modes) text.Append(ModeLines(before, after, created, deleted));
        return text.ToString();
    }

    /// <summary>Les « create mode » et « delete mode », dans l'ordre des chemins (mesuré).</summary>
    private static string ModeLines(Dictionary<string, TreeEntry> before, Dictionary<string, TreeEntry> after, List<string> created, List<string> deleted)
    {
        var text = new StringBuilder();
        foreach (var path in created.Concat(deleted).Order(StringComparer.Ordinal))
            text.Append(after.TryGetValue(path, out var a) && !before.ContainsKey(path)
                ? $" create mode {a.Mode} {path}\n"
                : $" delete mode {before[path].Mode} {path}\n");
        return text.ToString();
    }

    internal static string SummaryLine(int files, int insertions, int deletions)
    {
        var text = new StringBuilder($" {files} file{(files == 1 ? "" : "s")} changed");
        if (insertions > 0 || deletions == 0) text.Append($", {insertions} insertion{(insertions == 1 ? "" : "s")}(+)");
        if (deletions > 0 || insertions == 0) text.Append($", {deletions} deletion{(deletions == 1 ? "" : "s")}(-)");
        return text.Append('\n').ToString();
    }

    /// <summary>Les fichiers changés entre deux états, avec leurs lignes ajoutées et supprimées.</summary>
    internal static (List<(string Path, int Insertions, int Deletions)> Files, int Insertions, int Deletions, List<string> Created, List<string> Deleted)
        Changes(Repository repo, Dictionary<string, TreeEntry> before, Dictionary<string, TreeEntry> after)
    {
        var files = new List<(string, int, int)>();
        var created = new List<string>();
        var deleted = new List<string>();
        int ins = 0, del = 0;
        foreach (var path in before.Keys.Union(after.Keys).Order(StringComparer.Ordinal))
        {
            var a = before.GetValueOrDefault(path);
            var b = after.GetValueOrDefault(path);
            if (a is not null && b is not null && a.Id.Equals(b.Id) && a.Mode == b.Mode) continue;
            var (i, d) = LineDiff.Count(a is null ? null : repo.Objects.Get<Blob>(a.Id).Data, b is null ? null : repo.Objects.Get<Blob>(b.Id).Data);
            files.Add((path, i, d));
            ins += i;
            del += d;
            if (a is null) created.Add(path);
            if (b is null) deleted.Add(path);
        }
        return (files, ins, del, created, deleted);
    }

    /// <summary>Le diffstat de Git : « a.txt | 3 ++- », barres réduites pour tenir sur 80 colonnes, puis le résumé.</summary>
    internal static string Stat(Repository repo, Dictionary<string, TreeEntry> before, Dictionary<string, TreeEntry> after)
    {
        var (files, insertions, deletions, created, deleted) = Changes(repo, before, after);
        if (files.Count == 0) return "";
        const int width = 80;
        var maxChange = files.Max(f => f.Insertions + f.Deletions);
        var numberWidth = Math.Max(1, maxChange.ToString().Length);
        var nameWidth = files.Max(f => f.Path.Length);
        var graphWidth = maxChange;
        if (nameWidth + numberWidth + 6 + graphWidth > width)
        {
            if (graphWidth > width * 3 / 8 - numberWidth - 6)
                graphWidth = Math.Max(6, width * 3 / 8 - numberWidth - 6);
            if (nameWidth > width - numberWidth - 6 - graphWidth)
                nameWidth = width - numberWidth - 6 - graphWidth;
        }
        var text = new StringBuilder();
        foreach (var (path, ins, del) in files)
        {
            var name = path.Length > nameWidth ? "..." + path[^(nameWidth - 3)..] : path;
            int add = ins, rem = del;
            if (graphWidth < maxChange)
            {
                // Comme show_stats de Git : le total d'abord, puis le plus petit des deux côtés, le reste à l'autre
                var total = Scale(ins + del, graphWidth, maxChange);
                if (total < 2 && ins > 0 && del > 0) total = 2;
                if (ins < del) { add = Scale(ins, graphWidth, maxChange); rem = total - add; }
                else { rem = Scale(del, graphWidth, maxChange); add = total - rem; }
            }
            text.Append(' ').Append(name.PadRight(nameWidth)).Append(" | ").Append((ins + del).ToString().PadLeft(numberWidth));
            if (add + rem > 0)
            {
                text.Append(' ');
                if (add > 0) text.Append(Ansi.Paint(Ansi.Green, new string('+', add)));
                if (rem > 0) text.Append(Ansi.Paint(Ansi.Red, new string('-', rem)));
            }
            text.Append('\n');
        }
        text.Append(SummaryLine(files.Count, insertions, deletions));
        text.Append(ModeLines(before, after, created, deleted));
        return text.ToString();
    }

    private static int Scale(int value, int width, int max) => value == 0 ? 0 : 1 + value * (width - 1) / max;

    // ── git log ─────────────────────────────────────────────────────────────

    private static int Log(GitContext c)
    {
        var repo = c.Repo;
        var args = c.Args.ToList();
        var oneline = args.Remove("--oneline");
        var graph = args.Remove("--graph");
        var allRefs = args.Remove("--all");
        args.Remove("--decorate");
        int? limit = null;
        var revisions = new List<string>();
        for (var i = 0; i < args.Count; i++)
        {
            var a = args[i];
            if (a == "-n" && i + 1 < args.Count) limit = int.Parse(args[++i]);
            else if (a.StartsWith("-n") && int.TryParse(a[2..], out var n1)) limit = n1;
            else if (a.StartsWith("--max-count=") && int.TryParse(a["--max-count=".Length..], out var n2)) limit = n2;
            else if (a.Length > 1 && a[0] == '-' && int.TryParse(a[1..], out var n3)) limit = n3;
            else if (!a.StartsWith('-')) revisions.Add(a);
        }

        var starts = new List<ObjectId>();
        if (allRefs) starts.AddRange(repo.Refs.Where(r => !r.Key.StartsWith("refs/stash")).Select(r => r.Value));
        foreach (var rev in revisions)
        {
            if (Resolve(repo, rev) is { } id) starts.Add(id);
            else
            {
                c.Out.Error($"fatal: ambiguous argument '{rev}': unknown revision or path not in the working tree.\n" +
                            "Use '--' to separate paths from revisions, like this:\n'git <command> [<revision>...] -- [<file>...]'\n");
                return 128;
            }
        }
        if (revisions.Count == 0 && repo.Head is { } head) starts.Add(head);
        if (revisions.Count == 0 && repo.Head is null && !allRefs)
            return c.Out.Fatal($"your current branch '{repo.CurrentBranch}' does not have any commits yet");

        var commits = Walk(repo, starts);
        if (limit is { } max) commits = commits.Take(max).ToList();
        var text = new StringBuilder();
        var first = true;
        foreach (var id in commits)
        {
            var commit = repo.GetCommit(id);
            var prefix = graph ? "* " : "";
            if (oneline)
            {
                text.Append(prefix).Append(Ansi.Paint(Ansi.Yellow, id.Short)).Append(Decorations(repo, id)).Append(' ').Append(commit.Subject).Append('\n');
                continue;
            }
            if (!first) text.Append(graph ? "|\n" : "\n");
            first = false;
            text.Append(prefix).Append(Ansi.Paint(Ansi.Yellow, "commit " + id.Hex)).Append(Decorations(repo, id)).Append('\n');
            var bar = graph ? "| " : "";
            if (commit.Parents.Count > 1)
                text.Append(bar).Append("Merge: ").Append(string.Join(' ', commit.Parents.Select(p => p.Short))).Append('\n');
            text.Append(bar).Append($"Author: {commit.Author.Name} <{commit.Author.Email}>\n");
            text.Append(bar).Append($"Date:   {commit.Author.LogDate}\n");
            text.Append(bar.TrimEnd()).Append('\n');
            foreach (var line in commit.Message.TrimEnd('\n').Split('\n'))
                text.Append(bar).Append(line.Length == 0 ? "" : "    " + line).Append('\n');
        }
        c.Out.Write(text.ToString());
        return 0;
    }

    /// <summary>Les commits accessibles, du plus récent au plus ancien (date du committeur, à égalité dans l'ordre de découverte), comme « git log ».</summary>
    internal static List<ObjectId> Walk(Repository repo, IEnumerable<ObjectId> starts)
    {
        var queue = new List<ObjectId>();
        var seen = new HashSet<ObjectId>();
        void Insert(ObjectId id)
        {
            if (!seen.Add(id)) return;
            var date = repo.GetCommit(id).Committer.When;
            var at = queue.FindIndex(q => repo.GetCommit(q).Committer.When < date);
            if (at < 0) queue.Add(id); else queue.Insert(at, id);
        }
        foreach (var start in starts) Insert(start);
        var result = new List<ObjectId>();
        while (queue.Count > 0)
        {
            var id = queue[0];
            queue.RemoveAt(0);
            result.Add(id);
            foreach (var parent in repo.GetCommit(id).Parents) Insert(parent);
        }
        return result;
    }
}
