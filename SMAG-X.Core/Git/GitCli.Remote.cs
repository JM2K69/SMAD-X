using System.Text;
using SMAGX.Forge;
using SMAGX.Machine;
using SMAGX.Ssh;

namespace SMAGX.Git;

public static partial class GitCli
{
    // ── Branches suivies (upstream) ─────────────────────────────────────────

    /// <summary>La branche distante suivie par une branche locale : « origin » et « refs/remotes/origin/main », ou null.</summary>
    internal static (string Remote, string TrackingRef)? Upstream(Repository repo, string branch)
    {
        if (!repo.Config.TryGetValue($"branch.{branch}.remote", out var remote)
            || !repo.Config.TryGetValue($"branch.{branch}.merge", out var merge)) return null;
        var name = merge.StartsWith("refs/heads/") ? merge["refs/heads/".Length..] : merge;
        return (remote, $"refs/remotes/{remote}/{name}");
    }

    /// <summary>Commits de « a » absents de « b », et inversement (avance et retard).</summary>
    internal static (int Ahead, int Behind) AheadBehind(Repository repo, ObjectId local, ObjectId upstream)
    {
        var mine = repo.Ancestors([local]);
        var theirs = repo.Ancestors([upstream]);
        return (mine.Count(id => !theirs.Contains(id)), theirs.Count(id => !mine.Contains(id)));
    }

    /// <summary>Les lignes « Your branch is … » de git status (sans la ligne vide qui les suit), ou null sans branche suivie.</summary>
    private static string? Tracking(Repository repo)
    {
        if (repo.CurrentBranch is not { } branch || repo.Head is not { } head || Upstream(repo, branch) is not { } up) return null;
        var shown = Short(up.TrackingRef);
        if (!repo.Refs.TryGetValue(up.TrackingRef, out var upstream))
            return $"Your branch is based on '{shown}', but the upstream is gone.\n  (use \"git branch --unset-upstream\" to fixup)";
        var (ahead, behind) = AheadBehind(repo, head, upstream);
        string Commits(int n) => $"{n} commit{(n == 1 ? "" : "s")}";
        return (ahead, behind) switch
        {
            (0, 0) => $"Your branch is up to date with '{shown}'.",
            (_, 0) => $"Your branch is ahead of '{shown}' by {Commits(ahead)}.\n  (use \"git push\" to publish your local commits)",
            (0, _) => $"Your branch is behind '{shown}' by {Commits(behind)}, and can be fast-forwarded.\n  (use \"git pull\" to update your local branch)",
            _ => $"Your branch and '{shown}' have diverged,\nand have {ahead} and {behind} different commits each, respectively.\n  (use \"git pull\" if you want to integrate the remote branch with yours)"
        };
    }

    /// <summary>Après être passé sur une branche suivie, Git écrit où elle en est (« Your branch is up to date with … »).</summary>
    internal static void ReportTracking(GitContext c)
    {
        if (Tracking(c.Repo) is { } tracking) c.Out.Write(tracking + "\n");
    }

    /// <summary>L'indication de suivi de « git branch -v » (« [ahead 1] ») ou -vv (« [origin/main: ahead 1] », nom en bleu).</summary>
    private static string TrackingTag(Repository repo, string branch, ObjectId id, int verbose)
    {
        if (Upstream(repo, branch) is not { } up) return "";
        var name = Ansi.Paint("\u001b[34m", Short(up.TrackingRef));
        if (!repo.Refs.TryGetValue(up.TrackingRef, out var upstream))
            return verbose >= 2 ? $"[{name}: gone] " : "[gone] ";
        var (ahead, behind) = AheadBehind(repo, id, upstream);
        var parts = new List<string>();
        if (ahead > 0) parts.Add($"ahead {ahead}");
        if (behind > 0) parts.Add($"behind {behind}");
        var counts = string.Join(", ", parts);
        if (verbose >= 2) return counts.Length == 0 ? $"[{name}] " : $"[{name}: {counts}] ";
        return counts.Length == 0 ? "" : $"[{counts}] ";
    }

    /// <summary>Le dépôt distant nommé (« origin ») : son adresse et le dépôt de la forge, ou un message d'erreur de Git.</summary>
    private static (RemoteUrl Url, IForge Forge, Repository Remote)? Connect(GitContext c, string url, out string? error)
    {
        error = null;
        var parsed = RemoteUrl.Parse(url);
        var forge = parsed is null ? null : c.Ws.FindForge(parsed.Host);
        if (parsed is null || forge is null)
        {
            error = parsed is null
                ? $"fatal: '{url}' does not appear to be a git repository\nfatal: Could not read from remote repository.\n\nPlease make sure you have the correct access rights\nand the repository exists.\n"
                : $"ssh: Could not resolve hostname {parsed.Host}: Name or service not known\nfatal: Could not read from remote repository.\n\nPlease make sure you have the correct access rights\nand the repository exists.\n";
            return null;
        }
        Repository? remote;
        try
        {
            // Par SSH : la clé du serveur (demandée la première fois), puis les clés de l'élève ; ssh écrit ses refus
            if (parsed.Ssh && forge.Ssh && SshClient.Login(c.Ws, c.Out, forge, parsed.User ?? c.Ws.User, parsed.Host) is null)
            {
                error = SshClient.RemoteFatal;
                return null;
            }
            remote = forge.Open(parsed.Project);
        }
        catch (ForgeUnavailableException e)
        {
            error = Unreachable(e, parsed);
            return null;
        }
        if (remote is null)
        {
            error = $"fatal: '{parsed.Path}' does not appear to be a git repository\nfatal: Could not read from remote repository.\n\nPlease make sure you have the correct access rights\nand the repository exists.\n";
            return null;
        }
        return (parsed, forge, remote);
    }

    /// <summary>
    /// La forge injoignable, comme ssh l'écrit : serveur arrêté ou adresse fausse (« Connection timed out »), code refusé
    /// par le SMAG-X qui sert (« Permission denied (publickey) », comme une clé que la forge ne connaît pas).
    /// </summary>
    private static string Unreachable(ForgeUnavailableException e, RemoteUrl url) =>
        (e is ForgeRefusedException
            ? $"git@{url.Host}: Permission denied (publickey).\n"
            : $"ssh: connect to host {url.Host} port 22: {(e.ConnectionRefused ? "Connection refused" : "Connection timed out")}\n")
        + "fatal: Could not read from remote repository.\n\nPlease make sure you have the correct access rights\nand the repository exists.\n";

    // ── git remote ──────────────────────────────────────────────────────────

    private static int Remote(GitContext c)
    {
        var repo = c.Repo;
        var args = c.Args.ToList();
        var verbose = args.Remove("-v") | args.Remove("--verbose");
        var names = repo.Config.Keys.Where(k => k.StartsWith("remote.") && k.EndsWith(".url"))
            .Select(k => k["remote.".Length..^".url".Length]).Order(StringComparer.Ordinal).ToList();
        if (args.Count == 0)
        {
            var text = new StringBuilder();
            foreach (var name in names)
            {
                if (!verbose) { text.Append(name).Append('\n'); continue; }
                var url = repo.Config[$"remote.{name}.url"];
                text.Append($"{name}\t{url} (fetch)\n{name}\t{url} (push)\n");
            }
            c.Out.Write(text.ToString());
            return 0;
        }
        switch (args[0])
        {
            case "add" when args.Count >= 3:
                if (names.Contains(args[1])) { c.Out.Error($"error: remote {args[1]} already exists.\n"); return 3; }
                repo.Config[$"remote.{args[1]}.url"] = args[2];
                repo.Config[$"remote.{args[1]}.fetch"] = $"+refs/heads/*:refs/remotes/{args[1]}/*";
                return 0;
            case "remove" or "rm" when args.Count >= 2:
                if (!names.Contains(args[1])) { c.Out.Error($"error: No such remote: '{args[1]}'\n"); return 2; }
                foreach (var key in repo.Config.Keys.Where(k => k.StartsWith($"remote.{args[1]}.")).ToList()) repo.Config.Remove(key);
                foreach (var name in repo.Refs.Keys.Where(k => k.StartsWith($"refs/remotes/{args[1]}/")).ToList()) repo.Refs.Remove(name);
                foreach (var key in repo.Config.Keys.Where(k => k.EndsWith(".remote") && repo.Config[k] == args[1]).ToList())
                {
                    repo.Config.Remove(key);
                    repo.Config.Remove(key[..^".remote".Length] + ".merge");
                }
                return 0;
            case "set-url" when args.Count >= 3:
                if (!names.Contains(args[1])) { c.Out.Error($"error: No such remote '{args[1]}'\n"); return 2; }
                repo.Config[$"remote.{args[1]}.url"] = args[2];
                return 0;
            case "get-url" when args.Count >= 2:
                if (!names.Contains(args[1])) { c.Out.Error($"error: No such remote '{args[1]}'\n"); return 2; }
                c.Out.Write(repo.Config[$"remote.{args[1]}.url"] + "\n");
                return 0;
            default:
                c.Out.Error("usage: git remote [-v | --verbose]\n   or: git remote add <name> <url>\n   or: git remote remove <name>\n   or: git remote set-url <name> <newurl>\n");
                return 129;
        }
    }

    // ── git clone ───────────────────────────────────────────────────────────

    private static int Clone(GitContext c)
    {
        var args = c.Args.Where(a => !a.StartsWith('-')).ToList();
        if (args.Count == 0)
        {
            c.Out.Error("fatal: You must specify a repository to clone.\n\nusage: git clone [<options>] [--] <repo> [<dir>]\n");
            return 129;
        }
        var url = args[0];
        var parsed = RemoteUrl.Parse(url);
        var name = args.Count > 1 ? args[1] : (parsed?.Project ?? url).Split('/', ':').Last();
        var root = c.Ws.Resolve(name);
        if (c.Ws.FilesUnder(root).Any() || c.Ws.Files.Keys.Any(f => f == root) || c.Ws.Repositories.ContainsKey(root)
            || c.Ws.Directories.Any(d => d.StartsWith(root + "/", StringComparison.Ordinal)))
            return c.Out.Fatal($"destination path '{name}' already exists and is not an empty directory.");
        c.Out.Error($"Cloning into '{name}'...\n");
        if (Connect(c, url, out var error) is not { } remote)
        {
            c.Out.Error(error!);
            return 128;
        }
        c.Ws.EnsureDirectory(root);
        var repo = new Repository(root);
        c.Ws.Repositories[root] = repo;
        repo.Config["remote.origin.url"] = url;
        repo.Config["remote.origin.fetch"] = "+refs/heads/*:refs/remotes/origin/*";
        var who = c.Identity() ?? Fallback(c);
        repo.Objects.CopyFrom(remote.Remote.Objects);
        foreach (var (refName, id) in remote.Remote.Refs.Where(r => r.Key.StartsWith("refs/heads/")))
            repo.UpdateRef("refs/remotes/origin/" + refName["refs/heads/".Length..], id, who, $"clone: from {url}");
        var defaultBranch = remote.Remote.HeadRef is { } h && h.StartsWith("refs/heads/") ? h["refs/heads/".Length..] : "main";
        repo.HeadRef = "refs/heads/" + defaultBranch;
        repo.Config[$"branch.{defaultBranch}.remote"] = "origin";
        repo.Config[$"branch.{defaultBranch}.merge"] = "refs/heads/" + defaultBranch;
        if (remote.Remote.Refs.TryGetValue("refs/heads/" + defaultBranch, out var start))
        {
            repo.Refs["refs/remotes/origin/HEAD"] = start;
            repo.RemoteHeads["origin"] = defaultBranch;
            repo.UpdateRef(repo.HeadRef, start, who, $"clone: from {url}");
            foreach (var (path, entry) in repo.Files(start))
            {
                repo.Index.Set(new IndexEntry(path, entry.Mode, entry.Id));
                c.Ws.WriteFile(Workstation.Combine(root, path), repo.Objects.Get<Blob>(entry.Id).Data);
            }
        }
        else c.Out.Error("warning: You appear to have cloned an empty repository.\n");
        return 0;
    }

    // ── git fetch ───────────────────────────────────────────────────────────

    private static int Fetch(GitContext c)
    {
        var repo = c.Repo;
        var name = c.Args.FirstOrDefault(a => !a.StartsWith('-'))
                   ?? (repo.CurrentBranch is { } b && Upstream(repo, b) is { } up ? up.Remote : "origin");
        return FetchRemote(c, name, out _);
    }

    /// <summary>Récupère les objets et les branches d'un dépôt distant, met à jour les branches de suivi et écrit les lignes de Git.</summary>
    private static int FetchRemote(GitContext c, string name, out RemoteUrl? url)
    {
        url = null;
        var repo = c.Repo;
        if (!repo.Config.TryGetValue($"remote.{name}.url", out var address))
        {
            c.Out.Error($"fatal: '{name}' does not appear to be a git repository\nfatal: Could not read from remote repository.\n\nPlease make sure you have the correct access rights\nand the repository exists.\n");
            return 128;
        }
        if (Connect(c, address, out var error) is not { } remote)
        {
            c.Out.Error(error!);
            return 128;
        }
        url = remote.Url;
        repo.Objects.CopyFrom(remote.Remote.Objects);
        var who = c.Identity() ?? Fallback(c);
        var lines = new List<(char Code, string Summary, string Branch, string Local, string Suffix)>();
        foreach (var (refName, id) in remote.Remote.Refs.Where(r => r.Key.StartsWith("refs/heads/")))
        {
            var branch = refName["refs/heads/".Length..];
            var tracking = $"refs/remotes/{name}/{branch}";
            var old = repo.Refs.TryGetValue(tracking, out var o) ? o : (ObjectId?)null;
            if (old is { } same && same.Equals(id)) continue;
            if (old is null) lines.Add(('*', "[new branch]", branch, $"{name}/{branch}", ""));
            else if (repo.IsAncestor(old.Value, id)) lines.Add((' ', $"{old.Value.Short}..{id.Short}", branch, $"{name}/{branch}", ""));
            else lines.Add(('+', $"{old.Value.Short}...{id.Short}", branch, $"{name}/{branch}", "  (forced update)"));
            repo.UpdateRef(tracking, id, who, old is null ? "fetch: storing head" : "fetch: fast-forward");
        }
        // Git 2.48 et suivants (fetch.followRemoteHEAD=create) : refs/remotes/origin/HEAD est créée si elle manque
        if (!repo.RemoteHeads.ContainsKey(name) && remote.Remote.HeadRef is { } remoteHead && remote.Remote.Refs.ContainsKey(remoteHead))
            repo.RemoteHeads[name] = remoteHead["refs/heads/".Length..];
        if (repo.RemoteHeads.TryGetValue(name, out var headBranch) && repo.Refs.TryGetValue($"refs/remotes/{name}/{headBranch}", out var headId))
            repo.Refs[$"refs/remotes/{name}/HEAD"] = headId;
        if (lines.Count > 0)
        {
            var width = Math.Max(10, lines.Max(l => l.Branch.Length));
            var text = new StringBuilder($"From {remote.Url.FromDisplay}\n");
            foreach (var l in lines)
                text.Append($" {l.Code} {l.Summary.PadRight(17)} {l.Branch.PadRight(width)} -> {l.Local}{l.Suffix}\n");
            c.Out.Error(text.ToString());
        }
        return 0;
    }

    // ── git push ────────────────────────────────────────────────────────────

    private static int Push(GitContext c)
    {
        var repo = c.Repo;
        var args = c.Args.ToList();
        var setUpstream = args.Remove("-u") | args.Remove("--set-upstream");
        var force = args.Remove("-f") | args.Remove("--force") | args.Remove("--force-with-lease");
        var positional = args.Where(a => !a.StartsWith('-')).ToList();

        string remoteName;
        List<(string Local, string Remote)> updates = [];
        if (positional.Count == 0)
        {
            if (repo.CurrentBranch is not { } branch)
                return c.Out.Fatal("You are not currently on a branch.\nTo push the history leading to the current (detached HEAD)\nstate now, use\n\n    git push origin HEAD:<name-of-remote-branch>\n");
            if (Upstream(repo, branch) is not { } up)
            {
                var only = repo.Config.Keys.Count(k => k.StartsWith("remote.") && k.EndsWith(".url")) > 0 ? "origin" : null;
                if (only is null)
                    return c.Out.Fatal("No configured push destination.\nEither specify the URL from the command-line or configure a remote repository using\n\n    git remote add <name> <url>\n\nand then push using the remote name\n\n    git push <name>\n");
                return c.Out.Fatal($"The current branch {branch} has no upstream branch.\nTo push the current branch and set the remote as upstream, use\n\n    git push --set-upstream origin {branch}\n\n" +
                                   "To have this happen automatically for branches without a tracking\nupstream, see 'push.autoSetupRemote' in 'git help config'.\n");
            }
            remoteName = up.Remote;
            updates.Add((branch, Short(up.TrackingRef)[(up.Remote.Length + 1)..]));
        }
        else
        {
            remoteName = positional[0];
            var specs = positional.Skip(1).ToList();
            if (specs.Count == 0)
            {
                if (repo.CurrentBranch is not { } branch)
                    return c.Out.Fatal("You are not currently on a branch.\nTo push the history leading to the current (detached HEAD)\nstate now, use\n\n    git push origin HEAD:<name-of-remote-branch>\n");
                specs.Add(branch);
            }
            foreach (var spec in specs)
            {
                var parts = spec.Split(':');
                var local = parts[0] == "HEAD" ? repo.CurrentBranch ?? "HEAD" : parts[0];
                updates.Add((local, parts.Length > 1 ? parts[1] : local));
            }
        }

        if (!repo.Config.TryGetValue($"remote.{remoteName}.url", out var address))
        {
            c.Out.Error($"fatal: '{remoteName}' does not appear to be a git repository\nfatal: Could not read from remote repository.\n\nPlease make sure you have the correct access rights\nand the repository exists.\n");
            return 128;
        }
        if (Connect(c, address, out var error) is not { } remote)
        {
            c.Out.Error(error!);
            return 128;
        }
        var url = remote.Url;
        var who = c.Identity() ?? Fallback(c);
        var planned = new List<(string Local, string Target, ObjectId? Old, ObjectId New)>();
        var refused = new List<(string Local, string Target, bool FetchFirst)>();
        foreach (var (local, target) in updates)
        {
            var source = local == "HEAD" ? repo.Head : repo.Refs.TryGetValue("refs/heads/" + local, out var l) ? l : Resolve(repo, local);
            if (source is not { } id)
            {
                c.Out.Error($"error: src refspec {local} does not match any\nerror: failed to push some refs to '{url.Anonymized}'\n");
                return 1;
            }
            var old = remote.Remote.Refs.TryGetValue("refs/heads/" + target, out var o) ? o : (ObjectId?)null;
            if (old is { } same && same.Equals(id)) continue;
            if (old is { } previous && !force && (!repo.Objects.Contains(previous) || !repo.IsAncestor(previous, id)))
            {
                refused.Add((local, target, !repo.Objects.Contains(previous)));
                continue;
            }
            planned.Add((local, target, old, id));
        }
        if (planned.Count == 0 && refused.Count == 0)
        {
            if (setUpstream) SetUpstreams(c, remoteName, updates);
            c.Out.Error("Everything up-to-date\n");
            return 0;
        }

        // La forge n'applique une mise à jour que si la branche n'a pas bougé depuis la copie : sinon, « fetch first »
        IReadOnlyList<string> raced;
        try
        {
            raced = planned.Count == 0 ? [] : remote.Forge.Push(url.Project, repo.Objects,
                planned.Select(p => new RefUpdate("refs/heads/" + p.Target, p.Old, p.New)).ToList());
        }
        catch (ForgeUnavailableException e)
        {
            c.Out.Error(Unreachable(e, url));
            return 128;
        }
        var lines = new StringBuilder();
        var serverMessages = new StringBuilder();
        var fetchFirst = refused.Any(r => r.FetchFirst);
        foreach (var (local, target, old, id) in planned)
        {
            if (raced.Contains("refs/heads/" + target))
            {
                lines.Append($" ! {"[rejected]",-17} {local} -> {target} (fetch first)\n");
                fetchFirst = true;
                continue;
            }
            repo.UpdateRef($"refs/remotes/{remoteName}/{target}", id, who, "update by push");
            if (old is null)
            {
                lines.Append($" * {"[new branch]",-17} {local} -> {target}\n");
                if (target != (remote.Remote.HeadRef?["refs/heads/".Length..] ?? "main"))
                    serverMessages.Append(remote.Forge.NewBranchMessage(url, target));
            }
            else if (repo.IsAncestor(old.Value, id)) lines.Append($"   {$"{old.Value.Short}..{id.Short}",-17} {local} -> {target}\n");
            else lines.Append($" + {$"{old.Value.Short}...{id.Short}",-17} {local} -> {target} (forced update)\n");
        }
        foreach (var (local, target, first) in refused)
            lines.Append($" ! {"[rejected]",-17} {local} -> {target} ({(first ? "fetch first" : "non-fast-forward")})\n");

        c.Out.Error(serverMessages + $"To {url.Anonymized}\n" + lines);
        if (refused.Count > 0 || raced.Count > 0)
        {
            c.Out.Error($"error: failed to push some refs to '{url.Anonymized}'\n");
            c.Hint(fetchFirst
                ? "Updates were rejected because the remote contains work that you do not\nhave locally. This is usually caused by another repository pushing to\nthe same ref. If you want to integrate the remote changes, use\n'git pull' before pushing again.\nSee the 'Note about fast-forwards' in 'git push --help' for details."
                : "Updates were rejected because the tip of your current branch is behind\nits remote counterpart. If you want to integrate the remote changes,\nuse 'git pull' before pushing again.\nSee the 'Note about fast-forwards' in 'git push --help' for details.", color: false);
            return 1;
        }
        if (setUpstream) SetUpstreams(c, remoteName, updates);
        return 0;
    }

    private static void SetUpstreams(GitContext c, string remote, List<(string Local, string Remote)> updates)
    {
        foreach (var (local, target) in updates.Where(u => u.Local != "HEAD"))
        {
            c.Repo.Config[$"branch.{local}.remote"] = remote;
            c.Repo.Config[$"branch.{local}.merge"] = "refs/heads/" + target;
            c.Out.Write($"branch '{local}' set up to track '{remote}/{target}'.\n");
        }
    }

    // ── git pull ────────────────────────────────────────────────────────────

    private static int Pull(GitContext c)
    {
        var repo = c.Repo;
        var args = c.Args.ToList();
        var noRebase = args.Remove("--no-rebase");
        var ffOnly = args.Remove("--ff-only");
        if (args.Remove("--rebase") || args.Remove("-r"))
        {
            c.Out.Error("SMAG-X : « git pull --rebase » n'est pas encore reproduite par le simulateur.\n");
            return 1;
        }
        if (repo.CurrentBranch is not { } branch)
            return c.Out.Fatal("You are not currently on a branch.\nPlease specify which branch you want to merge with.\nSee git-pull(1) for details.\n\n    git pull <remote> <branch>\n");
        var positional = args.Where(a => !a.StartsWith('-')).ToList();
        string remoteName, trackingRef;
        if (positional.Count >= 2)
        {
            remoteName = positional[0];
            trackingRef = $"refs/remotes/{remoteName}/{positional[1]}";
        }
        else if (Upstream(repo, branch) is { } up)
        {
            remoteName = up.Remote;
            trackingRef = up.TrackingRef;
        }
        else
        {
            c.Out.Error("There is no tracking information for the current branch.\nPlease specify which branch you want to merge with.\nSee git-pull(1) for details.\n\n    git pull <remote> <branch>\n\n" +
                        $"If you wish to set tracking information for this branch you can do so with:\n\n    git branch --set-upstream-to=origin/<branch> {branch}\n\n");
            return 1;
        }

        var fetched = FetchRemote(c, remoteName, out var url);
        if (fetched != 0) return fetched;
        if (!repo.Refs.TryGetValue(trackingRef, out var theirs))
            return c.Out.Fatal($"couldn't find remote ref {trackingRef["refs/remotes/".Length..][(remoteName.Length + 1)..]}");
        var head = repo.Head;
        if (head is { } h && repo.IsAncestor(theirs, h))
        {
            c.Out.Write("Already up to date.\n");
            return 0;
        }
        var diverged = head is { } hd && !repo.IsAncestor(hd, theirs);
        var mode = c.Config("pull.rebase");
        var ffConfig = c.Config("pull.ff");
        if (diverged && (ffOnly || ffConfig == "only"))
            return c.Out.Fatal("Not possible to fast-forward, aborting.");
        if (diverged && !noRebase && mode is null && ffConfig is null)
        {
            c.Hint("You have divergent branches and need to specify how to reconcile them.\nYou can do so by running one of the following commands sometime before\nyour next pull:\n\n" +
                   "  git config pull.rebase false  # merge\n  git config pull.rebase true   # rebase\n  git config pull.ff only       # fast-forward only\n\n" +
                   "You can replace \"git config\" with \"git config --global\" to set a default\npreference for all repositories. You can also pass --rebase, --no-rebase,\n" +
                   "or --ff-only on the command line to override the configured default per\ninvocation.", color: false);
            return c.Out.Fatal("Need to specify how to reconcile divergent branches.");
        }
        if (diverged && mode is "true")
        {
            c.Out.Error("SMAG-X : « git pull » avec rebase n'est pas encore reproduite par le simulateur.\n");
            return 1;
        }
        // Comme « git merge FETCH_HEAD » : le marqueur de conflit porte l'identifiant du commit récupéré
        var message = $"Merge branch '{trackingRef[(trackingRef.LastIndexOf('/') + 1)..]}' of {url!.FromDisplay}\n";
        return Merge(new GitContext(c.Ws, c.Out, ["--pull-message=" + message, theirs.Hex]));
    }
}
