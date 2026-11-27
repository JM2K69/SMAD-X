using System.Text;
using SMAGX.Machine;

namespace SMAGX.Git;

public static partial class GitCli
{
    // ── git branch ──────────────────────────────────────────────────────────

    private static int Branch(GitContext c)
    {
        var repo = c.Repo;
        var args = SplitFlags(c.Args, "");
        var verbose = args.Count(a => a is "-v" or "--verbose") + args.Count(a => a == "-vv") * 2;
        args.RemoveAll(a => a is "-v" or "-vv" or "--verbose");
        var all = args.Remove("-a") | args.Remove("--all");
        var remotes = args.Remove("-r") | args.Remove("--remotes");
        var delete = args.Remove("-d") | args.Remove("--delete");
        var forceDelete = args.Remove("-D");
        var move = args.Remove("-m") | args.Remove("--move");
        var forceMove = args.Remove("-M");
        var showCurrent = args.Remove("--show-current");
        var force = args.Remove("-f") | args.Remove("--force");

        if (showCurrent)
        {
            if (repo.CurrentBranch is { } current) c.Out.Write(current + "\n");
            return 0;
        }
        if (delete || forceDelete) return DeleteBranches(c, args, forceDelete || (delete && force));
        if (move || forceMove) return RenameBranch(c, args, forceMove);
        if (args.Count == 0) return ListBranches(c, verbose, all, remotes);

        var name = args[0];
        var start = args.Count > 1 ? Resolve(repo, args[1]) : repo.Head;
        if (start is null)
            return c.Out.Fatal($"not a valid object name: '{(args.Count > 1 ? args[1] : repo.CurrentBranch)}'");
        if (!ValidBranchName(name)) return InvalidBranchName(c, name);
        if (repo.Refs.ContainsKey("refs/heads/" + name) && !force)
            return c.Out.Fatal($"a branch named '{name}' already exists");
        var who = c.Identity() ?? new Signature(c.Ws.User, $"{c.Ws.User}@{c.Ws.Host}", c.Ws.Clock());
        repo.UpdateRef("refs/heads/" + name, start.Value, who, $"branch: Created from {(args.Count > 1 ? args[1] : "HEAD")}");
        return 0;
    }

    internal static bool ValidBranchName(string name) =>
        name.Length > 0 && !name.StartsWith('-') && !name.Contains("..") && !name.EndsWith('/') && !name.EndsWith(".lock")
        && name.IndexOfAny([' ', '~', '^', ':', '?', '*', '[', '\\']) < 0 && name != "HEAD";

    private static int ListBranches(GitContext c, int verbose, bool all, bool remotesOnly)
    {
        var repo = c.Repo;
        var rows = new List<(bool Current, string Name, string Display, string Color, ObjectId Id)>();
        if (!remotesOnly && repo.HeadRef is null && repo.DetachedHead is { } detached)
            rows.Add((true, "", $"(HEAD detached at {detached.Short})", Ansi.Green, detached));
        if (!remotesOnly)
            foreach (var (name, id) in repo.Refs.Where(r => r.Key.StartsWith("refs/heads/")))
                rows.Add((name == repo.HeadRef, Short(name), Short(name), name == repo.HeadRef ? Ansi.Green : "", id));
        if (all || remotesOnly)
            foreach (var (name, id) in repo.Refs.Where(r => r.Key.StartsWith("refs/remotes/")))
                rows.Add((false, Short(name), (all ? "remotes/" : "") + Short(name), Ansi.Red, id));
        // « remotes/origin/HEAD -> origin/main » : la branche par défaut du dépôt distant (référence symbolique)
        string Target(string display) => display.EndsWith("/HEAD") && repo.RemoteHeads.TryGetValue(
            display.Replace("remotes/", "")[..^"/HEAD".Length], out var b) ? $" -> {display.Replace("remotes/", "")[..^"HEAD".Length]}{b}" : "";
        var width = rows.Count == 0 ? 0 : rows.Max(r => r.Display.Length);
        var text = new StringBuilder();
        foreach (var row in rows)
        {
            text.Append(row.Current ? "* " : "  ");
            var display = verbose > 0 ? row.Display.PadRight(width) : row.Display;
            text.Append(row.Color).Append(display).Append(Ansi.Reset);
            if (row.Color == Ansi.Red && Target(row.Display) is { Length: > 0 } arrow)
            {
                text.Append(arrow).Append('\n');
                continue;
            }
            if (verbose > 0)
            {
                var commit = repo.GetCommit(row.Id);
                var tag = row.Name.Length > 0 && row.Color != Ansi.Red ? TrackingTag(repo, row.Name, row.Id, verbose) : "";
                text.Append(' ').Append(row.Id.Short).Append(' ').Append(tag).Append(commit.Subject);
            }
            text.Append('\n');
        }
        c.Out.Write(text.ToString());
        return 0;
    }

    private static int DeleteBranches(GitContext c, List<string> names, bool force)
    {
        var repo = c.Repo;
        if (names.Count == 0) return c.Out.Fatal("branch name required");
        var code = 0;
        foreach (var name in names)
        {
            var full = "refs/heads/" + name;
            if (!repo.Refs.TryGetValue(full, out var id))
            {
                c.Out.Error($"error: branch '{name}' not found\n");
                code = 1;
                continue;
            }
            if (full == repo.HeadRef)
            {
                c.Out.Error($"error: cannot delete branch '{name}' used by worktree at '{repo.Root}'\n");
                code = 1;
                continue;
            }
            if (!force && repo.Head is { } head && !repo.IsAncestor(id, head))
            {
                c.Out.Error($"error: the branch '{name}' is not fully merged\n");
                c.Hint($"If you are sure you want to delete it, run 'git branch -D {name}'\nDisable this message with \"git config set advice.forceDeleteBranch false\"");
                code = 1;
                continue;
            }
            repo.Refs.Remove(full);
            repo.Reflogs.Remove(full);
            c.Out.Write($"Deleted branch {name} (was {id.Short}).\n");
        }
        return code;
    }

    private static int RenameBranch(GitContext c, List<string> args, bool force)
    {
        var repo = c.Repo;
        var (from, to) = args.Count switch
        {
            1 => (repo.CurrentBranch ?? "", args[0]),
            >= 2 => (args[0], args[1]),
            _ => ("", "")
        };
        if (to.Length == 0) return c.Out.Fatal("branch name required");
        var oldRef = "refs/heads/" + from;
        var newRef = "refs/heads/" + to;
        if (!ValidBranchName(to)) return InvalidBranchName(c, to);
        if (repo.Refs.ContainsKey(newRef) && !force && oldRef != newRef)
            return c.Out.Fatal($"a branch named '{to}' already exists");
        if (repo.Refs.TryGetValue(oldRef, out var id))
        {
            repo.Refs.Remove(oldRef);
            repo.Refs[newRef] = id;
            if (repo.Reflogs.Remove(oldRef, out var log)) repo.Reflogs[newRef] = log;
        }
        else if (oldRef != repo.HeadRef)
            return c.Out.Fatal($"no branch named '{from}'");
        if (repo.HeadRef == oldRef) repo.HeadRef = newRef;
        return 0;
    }

    // ── git switch ──────────────────────────────────────────────────────────

    private static int Switch(GitContext c)
    {
        var repo = c.Repo;
        var args = c.Args.ToList();
        string? create = null;
        var forceCreate = false;
        var detach = args.Remove("--detach") | args.Remove("-d");
        for (var i = 0; i < args.Count; i++)
        {
            if (args[i] is "-c" or "--create" or "-C" or "--force-create" && i + 1 < args.Count)
            {
                forceCreate = args[i] is "-C" or "--force-create";
                create = args[i + 1];
                args.RemoveRange(i, 2);
                break;
            }
        }
        if (create is not null)
            return SwitchToNewBranch(c, create, args.FirstOrDefault(), forceCreate, "switch");
        if (args.Count == 0) return c.Out.Fatal("missing branch or commit argument");
        var target = args[0] == "-" ? repo.PreviousBranch ?? "" : args[0];
        if (args[0] == "-" && repo.PreviousBranch is null) return c.Out.Fatal("invalid reference: @{-1}");
        if (!detach && repo.Refs.ContainsKey("refs/heads/" + target))
            return SwitchToBranch(c, target);
        if (!detach && TrackRemoteBranch(c, target) is { } dwim) return dwim;
        if (Resolve(repo, target) is { } commit)
        {
            if (!detach)
            {
                c.Out.Error($"fatal: a branch is expected, got {(repo.Refs.ContainsKey("refs/remotes/" + target) ? "remote branch" : repo.Refs.ContainsKey("refs/tags/" + target) ? "tag" : "commit")} '{target}'\n");
                c.Hint("If you want to detach HEAD at the commit, try again with the --detach option.", color: false);
                return 128;
            }
            return SwitchToCommit(c, commit, advice: false);
        }
        return c.Out.Fatal($"invalid reference: {target}");
    }

    /// <summary>« git switch nom » quand seule origin/nom existe : crée la branche locale qui la suit et s'y place.</summary>
    private static int? TrackRemoteBranch(GitContext c, string name)
    {
        var repo = c.Repo;
        var candidates = repo.Refs.Keys.Where(r => r.StartsWith("refs/remotes/") && r.EndsWith("/" + name) && !r.EndsWith("/HEAD")).ToList();
        if (candidates.Count != 1) return null;
        var tracking = candidates[0];
        var remote = tracking["refs/remotes/".Length..^(name.Length + 1)];
        var id = repo.Refs[tracking];
        if (repo.Index.HasConflicts) return NeedsMerge(c);
        if (!SwitchTree(c, repo.Files(repo.Head), repo.Files(id), "checkout")) return 1;
        var who = c.Identity() ?? new Signature(c.Ws.User, $"{c.Ws.User}@{c.Ws.Host}", c.Ws.Clock());
        var previous = repo.CurrentBranch;
        repo.UpdateRef("refs/heads/" + name, id, who, $"branch: Created from {remote}/{name}");
        repo.Log("HEAD", repo.Head, id, who, $"checkout: moving from {previous ?? repo.Head?.Hex} to {name}");
        repo.PreviousBranch = previous ?? repo.PreviousBranch;
        repo.HeadRef = "refs/heads/" + name;
        repo.DetachedHead = null;
        repo.DetachedAt = null;
        repo.Config[$"branch.{name}.remote"] = remote;
        repo.Config[$"branch.{name}.merge"] = "refs/heads/" + name;
        c.Out.Write($"branch '{name}' set up to track '{remote}/{name}'.\n");
        c.Out.Error($"Switched to a new branch '{name}'\n");
        return 0;
    }

    internal static int SwitchToNewBranch(GitContext c, string name, string? startSpec, bool force, string verb)
    {
        var repo = c.Repo;
        if (!ValidBranchName(name)) return InvalidBranchName(c, name);
        var exists = repo.Refs.ContainsKey("refs/heads/" + name);
        if (exists && !force) return c.Out.Fatal($"a branch named '{name}' already exists");
        var start = startSpec is null ? repo.Head : Resolve(repo, startSpec);
        if (startSpec is not null && start is null)
            return c.Out.Fatal(verb == "switch" ? $"invalid reference: {startSpec}" : $"'{startSpec}' is not a commit and a branch '{name}' cannot be created from it");
        var who = c.Identity() ?? new Signature(c.Ws.User, $"{c.Ws.User}@{c.Ws.Host}", c.Ws.Clock());
        if (start is { } id)
        {
            if (!SwitchTree(c, repo.Files(repo.Head), repo.Files(id), "checkout")) return 1;
            var previous = repo.CurrentBranch;
            repo.UpdateRef("refs/heads/" + name, id, who, $"branch: Created from {startSpec ?? "HEAD"}");
            repo.Log("HEAD", repo.Head, id, who, $"checkout: moving from {previous ?? repo.Head?.Hex} to {name}");
            repo.PreviousBranch = previous ?? repo.PreviousBranch;
        }
        // Une nouvelle branche au même commit ne touche pas aux fichiers : Git ne liste pas les modifications locales (mesuré)
        var moved = start is { } s && repo.Head is { } h && !s.Equals(h);
        repo.HeadRef = "refs/heads/" + name;
        repo.DetachedHead = null;
        repo.DetachedAt = null;
        if (moved) ShowLocalChanges(c);
        c.Out.Error(exists ? $"Switched to and reset branch '{name}'\n" : $"Switched to a new branch '{name}'\n");
        return 0;
    }

    internal static int SwitchToBranch(GitContext c, string name)
    {
        var repo = c.Repo;
        if (repo.Index.HasConflicts) return NeedsMerge(c);
        var full = "refs/heads/" + name;
        if (repo.HeadRef == full)
        {
            ShowLocalChanges(c);
            c.Out.Error($"Already on '{name}'\n");
            ReportTracking(c);
            return 0;
        }
        var target = repo.Refs[full];
        if (!SwitchTree(c, repo.Files(repo.Head), repo.Files(target), "checkout")) return 1;
        var who = c.Identity() ?? new Signature(c.Ws.User, $"{c.Ws.User}@{c.Ws.Host}", c.Ws.Clock());
        var from = repo.CurrentBranch ?? repo.Head?.Hex;
        if (repo.HeadRef is null && repo.DetachedHead is { } detached)
        {
            if (!LeavingCommits(c, detached))
                c.Out.Error($"Previous HEAD position was {detached.Short} {repo.GetCommit(detached).Subject}\n");
        }
        repo.PreviousBranch = repo.CurrentBranch ?? repo.PreviousBranch;
        repo.HeadRef = full;
        repo.DetachedHead = null;
        repo.DetachedAt = null;
        repo.Log("HEAD", null, target, who, $"checkout: moving from {from} to {name}");
        ShowLocalChanges(c);
        c.Out.Error($"Switched to branch '{name}'\n");
        ReportTracking(c);
        return 0;
    }

    internal static int SwitchToCommit(GitContext c, ObjectId commit, bool advice)
    {
        var repo = c.Repo;
        if (repo.Index.HasConflicts) return NeedsMerge(c);
        if (!SwitchTree(c, repo.Files(repo.Head), repo.Files(commit), "checkout")) return 1;
        var who = c.Identity() ?? new Signature(c.Ws.User, $"{c.Ws.User}@{c.Ws.Host}", c.Ws.Clock());
        var wasDetached = repo.HeadRef is null;
        if (wasDetached && repo.DetachedHead is { } old && !old.Equals(commit))
        {
            if (!LeavingCommits(c, old))
                c.Out.Error($"Previous HEAD position was {old.Short} {repo.GetCommit(old).Subject}\n");
        }
        repo.PreviousBranch = repo.CurrentBranch ?? repo.PreviousBranch;
        repo.Log("HEAD", repo.Head, commit, who, $"checkout: moving from {repo.CurrentBranch ?? repo.Head?.Hex} to {commit.Hex}");
        repo.HeadRef = null;
        repo.DetachedHead = commit;
        repo.DetachedAt = commit;
        ShowLocalChanges(c);
        if (advice && !wasDetached)
            c.Out.Error($"Note: switching to '{c.Args.FirstOrDefault(a => !a.StartsWith('-'))}'.\n\n" +
                        "You are in 'detached HEAD' state. You can look around, make experimental\n" +
                        "changes and commit them, and you can discard any commits you make in this\n" +
                        "state without impacting any branches by switching back to a branch.\n\n" +
                        "If you want to create a new branch to retain commits you create, you may\n" +
                        "do so (now or later) by using -c with the switch command. Example:\n\n" +
                        "  git switch -c <new-branch-name>\n\n" +
                        "Or undo this operation with:\n\n" +
                        "  git switch -\n\n" +
                        "Turn off this advice by setting config variable advice.detachedHead to false\n\n");
        c.Out.Error($"HEAD is now at {commit.Short} {repo.GetCommit(commit).Subject}\n");
        return 0;
    }

    /// <summary>Quitter une HEAD détachée : prévient des commits qu'aucune branche ne retient plus (vrai si c'est le cas).</summary>
    private static bool LeavingCommits(GitContext c, ObjectId detached)
    {
        var repo = c.Repo;
        var kept = repo.Ancestors(repo.Refs.Where(r => r.Key.StartsWith("refs/")).Select(r => r.Value));
        var lost = Walk(repo, [detached]).Where(id => !kept.Contains(id)).ToList();
        if (lost.Count == 0) return false;
        var text = new StringBuilder();
        text.Append($"Warning: you are leaving {lost.Count} commit{(lost.Count == 1 ? "" : "s")} behind, not connected to\nany of your branches:\n\n");
        foreach (var id in lost.Take(5)) text.Append($"  {id.Short} {repo.GetCommit(id).Subject}\n");
        if (lost.Count > 5) text.Append($" ... and {lost.Count - 4} more.\n");
        text.Append("\nIf you want to keep it by creating a new branch, this may be a good time\nto do so with:\n\n");
        text.Append($" git branch <new-branch-name> {detached.Short}\n\n");
        c.Out.Error(text.ToString());
        return true;
    }

    private static int NeedsMerge(GitContext c)
    {
        c.Out.Error("error: you need to resolve your current index first\n");
        foreach (var path in c.Repo.Index.Conflicts.Keys) c.Out.Error($"{path}: needs merge\n");
        return 1;
    }

    /// <summary>
    /// Remplace l'arbre « from » par « to » dans l'index et le répertoire de travail, en gardant les modifications locales
    /// des fichiers que le changement ne touche pas. Refuse (et l'écrit comme Git) si une modification locale ou un
    /// fichier non suivi serait écrasé.
    /// </summary>
    internal static bool SwitchTree(GitContext c, Dictionary<string, TreeEntry> from, Dictionary<string, TreeEntry> to, string operation)
    {
        var repo = c.Repo;
        var work = Status.WorkTree(c.Ws, repo);
        var index = repo.Index.Snapshot();
        var overwritten = new List<string>();
        var untracked = new List<string>();
        var changed = from.Keys.Union(to.Keys).Where(p => !Same(from.GetValueOrDefault(p), to.GetValueOrDefault(p)))
            .Order(StringComparer.Ordinal).ToList();
        foreach (var path in changed)
        {
            var f = from.GetValueOrDefault(path);
            var t = to.GetValueOrDefault(path);
            var i = index.GetValueOrDefault(path);
            var w = work.TryGetValue(path, out var data) ? Status.BlobId(data) : (ObjectId?)null;
            if (i is null && f is null)
            {
                if (w is { } untrackedId && t is not null && !untrackedId.Equals(t.Id)) untracked.Add(path);
                continue;
            }
            var clean = SameId(i?.Id, f?.Id) && SameId(w, i?.Id);
            var alreadyTarget = SameId(i?.Id, t?.Id) && SameId(w, t?.Id);
            if (!clean && !alreadyTarget) overwritten.Add(path);
        }
        if (overwritten.Count > 0)
        {
            c.Out.Error($"error: Your local changes to the following files would be overwritten by {operation}:\n" +
                        string.Concat(overwritten.Select(p => "\t" + p + "\n")) +
                        (operation == "merge" ? "Please commit your changes or stash them before you merge.\n" : "Please commit your changes or stash them before you switch branches.\n") +
                        "Aborting\n");
            return false;
        }
        if (untracked.Count > 0)
        {
            c.Out.Error($"error: The following untracked working tree files would be overwritten by {operation}:\n" +
                        string.Concat(untracked.Select(p => "\t" + p + "\n")) +
                        (operation == "merge" ? "Please move or remove them before you merge.\n" : "Please move or remove them before you switch branches.\n") +
                        "Aborting\n");
            return false;
        }
        foreach (var path in changed)
        {
            var t = to.GetValueOrDefault(path);
            var absolute = Workstation.Combine(repo.Root, path);
            if (t is null)
            {
                repo.Index.Remove(path);
                c.Ws.DeleteFile(absolute);
            }
            else
            {
                repo.Index.Set(new IndexEntry(path, t.Mode, t.Id));
                c.Ws.WriteFile(absolute, repo.Objects.Get<Blob>(t.Id).Data);
            }
        }
        return true;
    }

    private static bool Same(TreeEntry? a, TreeEntry? b) => a is null ? b is null : b is not null && a.Id.Equals(b.Id) && a.Mode == b.Mode;

    private static bool SameId(ObjectId? a, ObjectId? b) => a is null ? b is null : b is not null && a.Value.Equals(b.Value);

    /// <summary>Après un changement de branche, Git liste les fichiers qui gardent des modifications locales (« M	a.txt »).</summary>
    private static void ShowLocalChanges(GitContext c)
    {
        var repo = c.Repo;
        var head = repo.Files(repo.Head);
        var work = Status.WorkTree(c.Ws, repo);
        var text = new StringBuilder();
        var paths = head.Keys.Union(repo.Index.Entries.Select(e => e.Path)).Order(StringComparer.Ordinal);
        foreach (var path in paths)
        {
            var h = head.GetValueOrDefault(path);
            var i = repo.Index.Get(path);
            var w = work.TryGetValue(path, out var data) ? Status.BlobId(data) : (ObjectId?)null;
            if (i is null && h is not null) text.Append("D\t").Append(path).Append('\n');
            else if (i is not null && h is null) text.Append("A\t").Append(path).Append('\n');
            else if (i is not null && w is null) text.Append("D\t").Append(path).Append('\n');
            else if (i is not null && h is not null && (!i.Id.Equals(h.Id) || !w!.Value.Equals(h.Id))) text.Append("M\t").Append(path).Append('\n');
        }
        c.Out.Write(text.ToString());
    }

    // ── git checkout ────────────────────────────────────────────────────────

    private static int Checkout(GitContext c)
    {
        var repo = c.Repo;
        var args = c.Args.ToList();
        var dashDash = args.IndexOf("--");
        if (dashDash >= 0)
        {
            var source = dashDash > 0 ? args[0] : null;
            return CheckoutPaths(c, source, args.Skip(dashDash + 1).ToList());
        }
        for (var i = 0; i < args.Count; i++)
        {
            if (args[i] is "-b" or "-B" && i + 1 < args.Count)
                return SwitchToNewBranch(c, args[i + 1], args.Skip(i + 2).FirstOrDefault(), args[i] == "-B", "checkout");
        }
        var detach = args.Remove("--detach");
        if (args.Count == 0) return c.Out.Fatal("You are on a branch yet to be born");
        var target = args[0] == "-" ? repo.PreviousBranch ?? "" : args[0];
        if (!detach && repo.Refs.ContainsKey("refs/heads/" + target)) return SwitchToBranch(c, target);
        if (!detach && TrackRemoteBranch(c, target) is { } dwim) return dwim;
        if (Resolve(repo, target) is { } commit)
        {
            if (args.Count > 1) return CheckoutPaths(c, target, args.Skip(1).ToList());
            return SwitchToCommit(c, commit, advice: true);
        }
        // « git checkout fichier » : restaure depuis l'index
        if (c.Ws.FileExists(c.Ws.Resolve(target)) || repo.Index.Contains(c.RepoPath(target)))
            return CheckoutPaths(c, null, args);
        c.Out.Error($"error: pathspec '{target}' did not match any file(s) known to git\n");
        return 1;
    }

    /// <summary>« git checkout [commit] -- fichiers » : remplace les fichiers par ceux de l'index, ou du commit (index compris).</summary>
    private static int CheckoutPaths(GitContext c, string? source, List<string> paths)
    {
        var repo = c.Repo;
        Dictionary<string, TreeEntry>? files = null;
        if (source is not null)
        {
            if (Resolve(repo, source) is not { } commit)
            {
                c.Out.Error($"error: pathspec '{source}' did not match any file(s) known to git\n");
                return 1;
            }
            files = repo.Files(commit);
        }
        var count = 0;
        foreach (var typed in paths)
        {
            var spec = c.RepoPath(typed);
            bool Match(string p) => spec.Length == 0 || p == spec || p.StartsWith(spec + "/", StringComparison.Ordinal);
            var matched = files is not null
                ? files.Where(f => Match(f.Key)).Select(f => (f.Key, f.Value.Mode, f.Value.Id)).ToList()
                : repo.Index.Entries.Where(e => Match(e.Path)).Select(e => (e.Path, e.Mode, e.Id)).ToList();
            if (matched.Count == 0)
            {
                c.Out.Error($"error: pathspec '{typed}' did not match any file(s) known to git\n");
                return 1;
            }
            foreach (var (path, mode, id) in matched)
            {
                if (files is not null) repo.Index.Set(new IndexEntry(path, mode, id));
                c.Ws.WriteFile(Workstation.Combine(repo.Root, path), repo.Objects.Get<Blob>(id).Data);
                count++;
            }
        }
        // Affiché seulement dans un terminal (mesuré : absent quand la sortie d'erreur n'en est pas un)
        if (c.Ws.Interactive)
            c.Out.Error($"Updated {count} path{(count == 1 ? "" : "s")} from {(source is null ? "the index" : Resolve(repo, source)!.Value.Short)}\n");
        return 0;
    }
}
