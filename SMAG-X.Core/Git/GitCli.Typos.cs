namespace SMAGX.Git;

public static partial class GitCli
{
    /// <summary>Les commandes de Git 2.55 (git --list-cmds=main), parmi lesquelles il cherche la plus proche d'une faute de frappe.</summary>
    private static readonly string[] MainCommands =
    [
        "add", "am", "annotate", "apply", "archive", "backfill", "bisect", "blame", "branch", "bugreport", "bundle", "cat-file",
        "check-attr", "check-ignore", "check-mailmap", "check-ref-format", "checkout", "checkout--worker", "checkout-index", "cherry",
        "cherry-pick", "citool", "clean", "clone", "column", "commit", "commit-graph", "commit-tree", "config", "count-objects",
        "credential", "credential-cache", "credential-cache--daemon", "credential-store", "credential-wincred", "daemon", "describe",
        "diagnose", "diff", "diff-files", "diff-index", "diff-pairs", "diff-tree", "difftool", "difftool--helper", "fast-export",
        "fast-import", "fetch", "fetch-pack", "filter-branch", "fmt-merge-msg", "for-each-ref", "for-each-repo", "format-patch",
        "format-rev", "fsck", "fsck-objects", "fsmonitor--daemon", "gc", "get-tar-commit-id", "grep", "gui", "gui--askpass",
        "gui--askyesno", "gui.tcl", "hash-object", "help", "history", "hook", "http-backend", "http-fetch", "http-push", "imap-send",
        "index-pack", "init", "init-db", "interpret-trailers", "last-modified", "log", "ls-files", "ls-remote", "ls-tree", "mailinfo",
        "mailsplit", "maintenance", "merge", "merge-base", "merge-file", "merge-index", "merge-octopus", "merge-one-file", "merge-ours",
        "merge-recursive", "merge-recursive-ours", "merge-recursive-theirs", "merge-resolve", "merge-subtree", "merge-tree", "mergetool",
        "mktag", "mktree", "multi-pack-index", "mv", "name-rev", "notes", "pack-objects", "pack-redundant", "pack-refs", "patch-id",
        "pickaxe", "prune", "prune-packed", "pull", "push", "quiltimport", "range-diff", "read-tree", "rebase", "receive-pack", "reflog",
        "refs", "remote", "remote-ext", "remote-fd", "remote-ftp", "remote-ftps", "remote-http", "remote-https", "repack", "replace",
        "replay", "repo", "request-pull", "rerere", "reset", "restore", "rev-list", "rev-parse", "revert", "rm", "send-email",
        "send-pack", "sh-i18n--envsubst", "shortlog", "show", "show-branch", "show-index", "show-ref", "sparse-checkout", "stage",
        "stash", "status", "stripspace", "submodule", "submodule--helper", "subtree", "survey", "switch", "symbolic-ref", "tag",
        "unpack-file", "unpack-objects", "update", "update-index", "update-ref", "update-server-info", "upload-archive",
        "upload-archive--writer", "upload-pack", "url-parse", "var", "verify-commit", "verify-pack", "verify-tag", "version",
        "web--browse", "whatchanged", "worktree", "write-tree"
    ];

    /// <summary>Les commandes courantes (git help) : un début de leur nom leur donne le meilleur score.</summary>
    private static readonly HashSet<string> CommonCommands =
    [
        "clone", "init", "add", "mv", "restore", "rm", "bisect", "diff", "grep", "log", "show", "status", "backfill", "branch",
        "commit", "history", "merge", "rebase", "reset", "switch", "tag", "fetch", "pull", "push"
    ];

    /// <summary>« git: 'comit' is not a git command », puis les commandes les plus proches, comme help_unknown_cmd de Git.</summary>
    private static int NotACommand(string command, Machine.CommandResult r)
    {
        r.Error($"git: '{command}' is not a git command. See 'git --help'.\n");
        var scored = MainCommands
            .Select(name => (Name: name, Score: CommonCommands.Contains(name) && name.StartsWith(command, StringComparison.Ordinal)
                ? 0 : Levenshtein(command, name, swap: 0, substitution: 2, insertion: 1, deletion: 3) + 1))
            .OrderBy(c => c.Score).ThenBy(c => c.Name, StringComparer.Ordinal).ToList();
        var n = scored.TakeWhile(c => c.Score == 0).Count();
        int best;
        if (n >= scored.Count) best = 8;
        else
        {
            best = scored[n++].Score;
            while (n < scored.Count && scored[n].Score == best) n++;
        }
        if (best < 7)
        {
            r.Error(n == 1 ? "\nThe most similar command is\n" : "\nThe most similar commands are\n");
            foreach (var c in scored.Take(n)) r.Error("\t" + c.Name + "\n");
        }
        return 1;
    }

    /// <summary>La distance de Damerau-Levenshtein pondérée de Git (levenshtein.c).</summary>
    private static int Levenshtein(string a, string b, int swap, int substitution, int insertion, int deletion)
    {
        var row0 = new int[b.Length + 1];
        var row1 = new int[b.Length + 1];
        var row2 = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++) row1[j] = j * insertion;
        for (var i = 0; i < a.Length; i++)
        {
            row2[0] = (i + 1) * deletion;
            for (var j = 0; j < b.Length; j++)
            {
                row2[j + 1] = row1[j] + substitution * (a[i] != b[j] ? 1 : 0);
                if (i > 0 && j > 0 && a[i - 1] == b[j] && a[i] == b[j - 1] && row2[j + 1] > row0[j - 1] + swap)
                    row2[j + 1] = row0[j - 1] + swap;
                if (row2[j + 1] > row1[j + 1] + deletion) row2[j + 1] = row1[j + 1] + deletion;
                if (row2[j + 1] > row2[j] + insertion) row2[j + 1] = row2[j] + insertion;
            }
            (row0, row1, row2) = (row1, row2, row0);
        }
        return row1[b.Length];
    }

    /// <summary>« fatal: 'mauvais nom' is not a valid branch name », suivi de son aide (sans couleur, mesuré).</summary>
    internal static int InvalidBranchName(GitContext c, string name)
    {
        c.Out.Fatal($"'{name}' is not a valid branch name");
        c.Hint("See 'git help check-ref-format'\nDisable this message with \"git config set advice.refSyntax false\"", color: false);
        return 128;
    }
}
