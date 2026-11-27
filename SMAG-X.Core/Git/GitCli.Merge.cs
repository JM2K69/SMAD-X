using System.Text;
using SMAGX.Machine;

namespace SMAGX.Git;

public static partial class GitCli
{
    // ── git merge ───────────────────────────────────────────────────────────

    private static int Merge(GitContext c)
    {
        var repo = c.Repo;
        var args = SplitFlags(c.Args, "m");
        if (args.Remove("--abort")) return MergeAbort(c);
        if (args.Remove("--continue")) return CommitCommand(new GitContext(c.Ws, c.Out, []));
        var noFf = args.Remove("--no-ff");
        var pullMessage = args.FirstOrDefault(a => a.StartsWith("--pull-message="));
        if (pullMessage is not null) args.Remove(pullMessage);
        var ffOnly = args.Remove("--ff-only");
        args.Remove("--no-edit");
        args.Remove("--edit");
        string? message = null;
        var m = args.IndexOf("-m");
        if (m >= 0 && m + 1 < args.Count) { message = args[m + 1]; args.RemoveRange(m, 2); }
        if (args.Count == 0) return c.Out.Fatal("No remote for the current branch.");
        var name = args[0];

        if (repo.MergeHead is not null)
        {
            c.Out.Error("error: Merging is not possible because you have unmerged files.\n");
            c.Hint("Fix them up in the work tree, and then use 'git add/rm <file>'\nas appropriate to mark resolution and make a commit.");
            return c.Out.Fatal("Exiting because of an unresolved conflict.");
        }
        if (Resolve(repo, name) is not { } theirs)
        {
            c.Out.Error($"merge: {name} - not something we can merge\n");
            return 1;
        }
        var who = c.Identity();
        var head = repo.Head;

        if (head is null)
        {
            if (!SwitchTree(c, new(StringComparer.Ordinal), repo.Files(theirs), "merge")) return 2;
            MoveHead(repo, theirs, who ?? Fallback(c), $"merge {name}: Fast-forward");
            return 0;
        }
        if (repo.IsAncestor(theirs, head.Value))
        {
            c.Out.Write("Already up to date.\n");
            return 0;
        }
        var fastForward = repo.IsAncestor(head.Value, theirs);
        if (fastForward && !noFf)
        {
            c.Out.Write($"Updating {head.Value.Short}..{theirs.Short}\nFast-forward\n");
            var before = repo.Files(head);
            if (!SwitchTree(c, before, repo.Files(theirs), "merge")) return 1;
            MoveHead(repo, theirs, who ?? Fallback(c), $"merge {name}: Fast-forward");
            c.Out.Write(Stat(repo, before, repo.Files(theirs)));
            return 0;
        }
        if (ffOnly)
        {
            c.Out.Error("hint: Diverging branches can't be fast-forwarded, you need to either:\n");
            return c.Out.Fatal("Not possible to fast-forward, aborting.");
        }
        if (who is null) return c.IdentityMissing();

        var bases = repo.MergeBases(head.Value, theirs);
        var baseFiles = bases.Count > 0 ? repo.Files(bases[0]) : new Dictionary<string, TreeEntry>(StringComparer.Ordinal);
        var ours = repo.Files(head);
        var theirsFiles = repo.Files(theirs);
        var result = new Dictionary<string, TreeEntry>(StringComparer.Ordinal);
        var conflicts = new Dictionary<string, (TreeEntry? Base, TreeEntry? Ours, TreeEntry? Theirs, byte[]? Content)>(StringComparer.Ordinal);
        var messages = new StringBuilder();

        foreach (var path in baseFiles.Keys.Union(ours.Keys).Union(theirsFiles.Keys).Order(StringComparer.Ordinal))
        {
            var b = baseFiles.GetValueOrDefault(path);
            var o = ours.GetValueOrDefault(path);
            var t = theirsFiles.GetValueOrDefault(path);
            if (Same(o, t)) { if (o is not null) result[path] = o; continue; }
            if (Same(o, b)) { if (t is not null) result[path] = t; continue; }
            if (Same(t, b)) { if (o is not null) result[path] = o; continue; }
            if (o is not null && t is not null)
            {
                messages.Append($"Auto-merging {path}\n");
                var merged = ThreeWayMerge.Merge(b is null ? null : Data(repo, b), Data(repo, o), Data(repo, t), "HEAD", name);
                if (merged.Conflict)
                {
                    messages.Append(b is null ? $"CONFLICT (add/add): Merge conflict in {path}\n" : $"CONFLICT (content): Merge conflict in {path}\n");
                    conflicts[path] = (b, o, t, merged.Content);
                }
                else result[path] = new TreeEntry(o.Mode, path, repo.Objects.Put(new Blob(merged.Content)));
                continue;
            }
            // Modifié d'un côté, supprimé de l'autre
            var kept = o ?? t!;
            messages.Append(o is null
                ? $"CONFLICT (modify/delete): {path} deleted in HEAD and modified in {name}.  Version {name} of {path} left in tree.\n"
                : $"CONFLICT (modify/delete): {path} deleted in {name} and modified in HEAD.  Version HEAD of {path} left in tree.\n");
            conflicts[path] = (b, o, t, Data(repo, kept));
        }

        // Le répertoire de travail ne doit rien perdre : les chemins que la fusion touche doivent être propres
        var touched = ours.Keys.Union(result.Keys).Union(conflicts.Keys)
            .Where(p => conflicts.ContainsKey(p) || !Same(ours.GetValueOrDefault(p), result.GetValueOrDefault(p))).ToList();
        var target = new Dictionary<string, TreeEntry>(result, StringComparer.Ordinal);
        foreach (var (path, conflict) in conflicts)
            target[path] = new TreeEntry((conflict.Ours ?? conflict.Theirs)!.Mode, path, repo.Objects.Put(new Blob(conflict.Content!)));
        if (!SwitchTree(c, ours, target, "merge")) return 2;

        c.Out.Write(messages.ToString());
        var defaultMessage = pullMessage?["--pull-message=".Length..] ?? MergeMessage(repo, name);
        if (conflicts.Count > 0)
        {
            foreach (var (path, conflict) in conflicts)
            {
                IndexEntry? Stage(TreeEntry? e) => e is null ? null : new IndexEntry(path, e.Mode, e.Id);
                repo.Index.SetConflict(path, Stage(conflict.Base), Stage(conflict.Ours), Stage(conflict.Theirs));
            }
            repo.MergeHead = theirs;
            repo.MergeMessage = (message is null ? defaultMessage : message + "\n") + "\n# Conflicts:\n" + string.Concat(conflicts.Keys.Select(p => "#\t" + p + "\n"));
            c.Out.Write("Automatic merge failed; fix conflicts and then commit the result.\n");
            return 1;
        }

        var commitMessage = message is not null ? CleanMessage(message) : defaultMessage;
        if (message is null && c.Ws.Editor is { } editor && editor(defaultMessage) is { } edited)
            commitMessage = CleanMessage(StripComments(edited));
        var tree = repo.WriteTree(result.Select(r => (r.Key, r.Value.Mode, r.Value.Id)));
        var commit = new Commit(tree, [head.Value, theirs], who, who, commitMessage);
        var id = repo.Objects.Put(commit);
        MoveHead(repo, id, who, $"merge {name}: Merge made by the 'ort' strategy.");
        c.Out.Write("Merge made by the 'ort' strategy.\n");
        c.Out.Write(Stat(repo, ours, repo.Files(id)));
        return 0;
    }

    /// <summary>Le message d'une fusion : « Merge branch 'feature' », suivi de « into dev » hors de la branche principale.</summary>
    private static string MergeMessage(Repository repo, string name)
    {
        var kind = repo.Refs.ContainsKey("refs/heads/" + name) ? $"branch '{name}'"
            : repo.Refs.ContainsKey("refs/remotes/" + name) ? $"remote-tracking branch '{name}'"
            : repo.Refs.ContainsKey("refs/tags/" + name) ? $"tag '{name}'"
            : $"commit '{name}'";
        var into = repo.CurrentBranch is { } branch && branch is not ("main" or "master") ? $" into {branch}" : "";
        return $"Merge {kind}{into}\n";
    }

    private static byte[] Data(Repository repo, TreeEntry entry) => repo.Objects.Get<Blob>(entry.Id).Data;

    private static Signature Fallback(GitContext c) => new(c.Ws.User, $"{c.Ws.User}@{c.Ws.Host}", c.Ws.Clock());

    private static int MergeAbort(GitContext c)
    {
        var repo = c.Repo;
        if (repo.MergeHead is null) return c.Out.Fatal("There is no merge to abort (MERGE_HEAD missing).");
        ResetHard(c, repo.Head);
        repo.MergeHead = null;
        repo.MergeMessage = null;
        return 0;
    }

    /// <summary>Index et répertoire de travail ramenés à un commit (les fichiers non suivis restent).</summary>
    private static void ResetHard(GitContext c, ObjectId? commit)
    {
        var repo = c.Repo;
        var files = repo.Files(commit);
        var tracked = repo.Index.Entries.Select(e => e.Path).Concat(repo.Index.Conflicts.Keys).Concat(repo.Files(repo.Head).Keys).Distinct().ToList();
        foreach (var path in tracked.Where(p => !files.ContainsKey(p)))
            c.Ws.DeleteFile(Workstation.Combine(repo.Root, path));
        repo.Index.Clear();
        foreach (var (path, entry) in files)
        {
            repo.Index.Set(new IndexEntry(path, entry.Mode, entry.Id));
            c.Ws.WriteFile(Workstation.Combine(repo.Root, path), Data(repo, entry));
        }
    }

    // ── git restore ─────────────────────────────────────────────────────────

    private static int Restore(GitContext c)
    {
        var repo = c.Repo;
        var args = c.Args.Where(a => a != "--").ToList();
        var staged = args.Remove("--staged") | args.Remove("-S");
        var worktree = args.Remove("--worktree") | args.Remove("-W");
        if (!staged) worktree = true;
        string? source = null;
        for (var i = 0; i < args.Count; i++)
        {
            if (args[i] is "-s" or "--source" && i + 1 < args.Count) { source = args[i + 1]; args.RemoveRange(i, 2); break; }
            if (args[i].StartsWith("--source=")) { source = args[i]["--source=".Length..]; args.RemoveAt(i); break; }
        }
        if (args.Count == 0) return c.Out.Fatal("you must specify path(s) to restore");

        Dictionary<string, TreeEntry>? sourceFiles = null;
        if (source is not null)
        {
            if (Resolve(repo, source) is not { } id) return c.Out.Fatal($"could not resolve {source}");
            sourceFiles = repo.Files(id);
        }
        else if (staged) sourceFiles = repo.Files(repo.Head);

        foreach (var typed in args)
        {
            var spec = c.RepoPath(typed);
            bool Match(string p) => spec.Length == 0 || p == spec || p.StartsWith(spec + "/", StringComparison.Ordinal);
            var known = repo.Index.Entries.Select(e => e.Path).Concat(repo.Index.Conflicts.Keys)
                .Concat(sourceFiles?.Keys ?? Enumerable.Empty<string>()).Where(Match).Distinct().ToList();
            if (known.Count == 0)
            {
                c.Out.Error($"error: pathspec '{typed}' did not match any file(s) known to git\n");
                return 1;
            }
            foreach (var path in known)
            {
                if (staged)
                {
                    if (sourceFiles!.TryGetValue(path, out var e)) repo.Index.Set(new IndexEntry(path, e.Mode, e.Id));
                    else repo.Index.Remove(path);
                }
                if (worktree)
                {
                    var from = sourceFiles is not null && !staged ? sourceFiles.GetValueOrDefault(path)
                        : staged ? sourceFiles!.GetValueOrDefault(path)
                        : repo.Index.Get(path) is { } ie ? new TreeEntry(ie.Mode, path, ie.Id) : null;
                    var absolute = Workstation.Combine(repo.Root, path);
                    if (from is null) c.Ws.DeleteFile(absolute);
                    else c.Ws.WriteFile(absolute, Data(repo, from));
                }
            }
        }
        return 0;
    }

    // ── git reset ───────────────────────────────────────────────────────────

    private static int Reset(GitContext c)
    {
        var repo = c.Repo;
        var args = c.Args.Where(a => a != "--").ToList();
        var hard = args.Remove("--hard");
        var soft = args.Remove("--soft");
        args.Remove("--mixed");
        var who = c.Identity() ?? Fallback(c);

        // « git reset fichier » : retire le fichier de l'index
        var revision = args.Count > 0 && Resolve(repo, args[0]) is not null ? args[0] : null;
        var paths = revision is null ? args : args.Skip(1).ToList();
        if (paths.Count > 0)
        {
            var source = repo.Files(revision is null ? repo.Head : Resolve(repo, revision));
            foreach (var typed in paths)
            {
                var spec = c.RepoPath(typed);
                bool Match(string p) => p == spec || p.StartsWith(spec + "/", StringComparison.Ordinal);
                foreach (var path in repo.Index.Entries.Select(e => e.Path).Concat(source.Keys).Where(Match).Distinct().ToList())
                {
                    if (source.TryGetValue(path, out var e)) repo.Index.Set(new IndexEntry(path, e.Mode, e.Id));
                    else repo.Index.Remove(path);
                }
            }
            UnstagedAfterReset(c);
            return 0;
        }

        var target = revision is null ? repo.Head : Resolve(repo, revision);
        if (target is null) return c.Out.Fatal("Failed to resolve 'HEAD' as a valid ref.");
        if (hard) ResetHard(c, target);
        else if (!soft)
        {
            repo.Index.Clear();
            foreach (var (path, entry) in repo.Files(target)) repo.Index.Set(new IndexEntry(path, entry.Mode, entry.Id));
        }
        MoveHead(repo, target.Value, who, $"reset: moving to {revision ?? "HEAD"}");
        repo.MergeHead = null;
        repo.MergeMessage = null;
        if (hard) c.Out.Write($"HEAD is now at {target.Value.Short} {repo.GetCommit(target.Value).Subject}\n");
        else if (!soft) UnstagedAfterReset(c);
        return 0;
    }

    private static void UnstagedAfterReset(GitContext c)
    {
        var repo = c.Repo;
        var work = Status.WorkTree(c.Ws, repo);
        var lines = new StringBuilder();
        foreach (var entry in repo.Index.Entries)
        {
            if (!work.TryGetValue(entry.Path, out var data)) lines.Append("D\t").Append(entry.Path).Append('\n');
            else if (!Status.BlobId(data).Equals(entry.Id)) lines.Append("M\t").Append(entry.Path).Append('\n');
        }
        if (lines.Length > 0) c.Out.Write("Unstaged changes after reset:\n" + lines);
    }

    // ── git rm ──────────────────────────────────────────────────────────────

    private static int Remove(GitContext c)
    {
        var repo = c.Repo;
        var args = SplitFlags(c.Args.Where(a => a != "--").ToList(), "");
        var cached = args.Remove("--cached");
        var force = args.Remove("-f") | args.Remove("--force");
        var recursive = args.Remove("-r");
        if (args.Count == 0)
        {
            c.Out.Error("usage: git rm [-f | --force] [-n] [-r] [--cached] [--ignore-unmatch]\n");
            return 129;
        }
        var head = repo.Files(repo.Head);
        var work = Status.WorkTree(c.Ws, repo);
        var targets = new List<string>();
        foreach (var typed in args)
        {
            var spec = c.RepoPath(typed);
            var exact = repo.Index.Get(spec) is not null || repo.Index.Conflicts.ContainsKey(spec);
            var under = repo.Index.Entries.Where(e => e.Path.StartsWith(spec + "/", StringComparison.Ordinal)).Select(e => e.Path).ToList();
            if (!exact && under.Count == 0) return c.Out.Fatal($"pathspec '{typed}' did not match any files");
            if (!exact && !recursive) return c.Out.Fatal($"not removing '{typed}' recursively without -r");
            targets.AddRange(exact ? [spec] : under);
        }
        if (!force)
        {
            var staged = targets.Where(p => repo.Index.Get(p) is { } e && (!head.TryGetValue(p, out var h) || !h.Id.Equals(e.Id))).ToList();
            var modified = targets.Where(p => repo.Index.Get(p) is { } e && work.TryGetValue(p, out var d) && !Status.BlobId(d).Equals(e.Id)).ToList();
            var both = cached ? [] : staged.Intersect(modified).ToList();
            if (both.Count > 0) return RmRefuse(c, both, "has staged content different from both the\nfile and the HEAD", "(use -f to force removal)");
            if (!cached && staged.Count > 0) return RmRefuse(c, staged, "has changes staged in the index", "(use --cached to keep the file, or -f to force removal)");
            if (!cached && modified.Count > 0) return RmRefuse(c, modified, "has local modifications", "(use --cached to keep the file, or -f to force removal)");
        }
        foreach (var path in targets)
        {
            repo.Index.Remove(path);
            if (!cached) c.Ws.DeleteFile(Workstation.Combine(repo.Root, path));
            c.Out.Write($"rm '{path}'\n");
        }
        return 0;
    }

    private static int RmRefuse(GitContext c, List<string> paths, string what, string advice)
    {
        var plural = paths.Count > 1;
        c.Out.Error($"error: the following file{(plural ? "s" : "")} {(plural ? what.Replace("has ", "have ") : what)}:\n" +
                    string.Concat(paths.Select(p => "    " + p + "\n")) + advice + "\n");
        return 1;
    }
}
