using System.Text;
using SMAGX.Machine;

namespace SMAGX.Git;

public enum ChangeKind { Added, Modified, Deleted }

/// <summary>Un changement entre deux zones (HEAD et index, ou index et répertoire de travail).</summary>
public sealed record FileChange(ChangeKind Kind, string Path);

/// <summary>Un chemin en conflit et la nature du conflit (« both modified », « deleted by us »…).</summary>
public sealed record Unmerged(string Path, string Label, string Short);

/// <summary>L'état des trois zones d'un dépôt, tel que le calcule « git status ».</summary>
public sealed class StatusReport
{
    public required Repository Repository { get; init; }
    public List<FileChange> Staged { get; } = [];
    public List<FileChange> Unstaged { get; } = [];
    public List<Unmerged> Conflicts { get; } = [];

    /// <summary>Les fichiers non suivis, regroupés comme Git (un dossier entièrement non suivi s'écrit « dossier/ »).</summary>
    public List<string> Untracked { get; } = [];

    public bool IsInitial => Repository.Head is null;

    public bool HasStaged => Staged.Count > 0;

    public bool WorkTreeClean => Unstaged.Count == 0 && Untracked.Count == 0 && Conflicts.Count == 0 && Staged.Count == 0;
}

/// <summary>Le calcul et l'affichage de « git status » (format long et format court).</summary>
public static class Status
{
    private static readonly UTF8Encoding Utf8 = new(false);

    /// <summary>Les fichiers du répertoire de travail : chemin relatif à la racine → contenu.</summary>
    public static SortedDictionary<string, byte[]> WorkTree(Workstation ws, Repository repo)
    {
        var files = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var path in ws.FilesUnder(repo.Root))
            files[path] = ws.ReadFile(Workstation.Combine(repo.Root, path));
        return files;
    }

    public static ObjectId BlobId(byte[] data) => ObjectStore.Hash(new Blob(data));

    public static StatusReport Compute(Workstation ws, Repository repo)
    {
        var report = new StatusReport { Repository = repo };
        var head = repo.Files(repo.Head);
        var index = repo.Index.Snapshot();
        var work = WorkTree(ws, repo);
        var ignore = Ignore.Load(work);

        foreach (var (path, entry) in index)
        {
            if (!head.TryGetValue(path, out var h)) report.Staged.Add(new FileChange(ChangeKind.Added, path));
            else if (!h.Id.Equals(entry.Id) || h.Mode != entry.Mode) report.Staged.Add(new FileChange(ChangeKind.Modified, path));
        }
        foreach (var path in head.Keys.Where(p => !index.ContainsKey(p) && !repo.Index.Conflicts.ContainsKey(p)))
            report.Staged.Add(new FileChange(ChangeKind.Deleted, path));
        report.Staged.Sort((a, b) => string.CompareOrdinal(a.Path, b.Path));

        foreach (var (path, stages) in repo.Index.Conflicts)
            report.Conflicts.Add(Describe(path, stages));

        foreach (var (path, entry) in index)
        {
            if (!work.TryGetValue(path, out var data)) report.Unstaged.Add(new FileChange(ChangeKind.Deleted, path));
            else if (!BlobId(data).Equals(entry.Id)) report.Unstaged.Add(new FileChange(ChangeKind.Modified, path));
        }

        var untracked = work.Keys.Where(p => !repo.Index.Contains(p) && !ignore.IsIgnored(p)).ToList();
        foreach (var shown in untracked.Select(p => Collapse(p, repo.Index, ignore, work)).Distinct().Order(StringComparer.Ordinal))
            report.Untracked.Add(shown);
        return report;
    }

    /// <summary>Un fichier non suivi dans un dossier qui ne contient rien de suivi s'affiche comme ce dossier (« build/ »).</summary>
    private static string Collapse(string path, GitIndex index, Ignore ignore, SortedDictionary<string, byte[]> work)
    {
        var parts = path.Split('/');
        for (var i = 1; i < parts.Length; i++)
        {
            var dir = string.Join('/', parts[..i]) + "/";
            var tracked = index.Entries.Any(e => e.Path.StartsWith(dir, StringComparison.Ordinal))
                          || index.Conflicts.Keys.Any(p => p.StartsWith(dir, StringComparison.Ordinal));
            var ignoredInside = work.Keys.Any(p => p.StartsWith(dir, StringComparison.Ordinal) && ignore.IsIgnored(p));
            if (!tracked && !ignoredInside) return dir;
        }
        return path;
    }

    private static Unmerged Describe(string path, IndexEntry?[] stages)
    {
        bool b = stages[0] is not null, o = stages[1] is not null, t = stages[2] is not null;
        return (b, o, t) switch
        {
            (true, true, true) => new Unmerged(path, "both modified", "UU"),
            (false, true, true) => new Unmerged(path, "both added", "AA"),
            (true, false, true) => new Unmerged(path, "deleted by us", "DU"),
            (true, true, false) => new Unmerged(path, "deleted by them", "UD"),
            (false, true, false) => new Unmerged(path, "added by us", "AU"),
            (false, false, true) => new Unmerged(path, "added by them", "UA"),
            _ => new Unmerged(path, "both deleted", "DD")
        };
    }

    private static string Label(ChangeKind kind) => kind switch
    {
        ChangeKind.Added => "new file:   ",
        ChangeKind.Modified => "modified:   ",
        _ => "deleted:    "
    };

    /// <summary>Le chemin tel que Git l'écrit : relatif au dossier courant.</summary>
    public static string Show(Workstation ws, Repository repo, string path) =>
        Workstation.Relative(ws.Cwd, Workstation.Combine(repo.Root, path)) + (path.EndsWith('/') ? "/" : "");

    /// <summary>« git status » au format long, avec les lignes d'aide (status.hints).</summary>
    public static string Long(Workstation ws, StatusReport s, string? tracking = null)
    {
        var repo = s.Repository;
        var text = new StringBuilder();
        if (repo.CurrentBranch is { } branch) text.Append("On branch ").Append(branch).Append('\n');
        else text.Append(Detached(repo)).Append('\n');
        if (tracking is not null) text.Append(tracking).Append("\n\n");
        if (repo.MergeHead is not null)
        {
            if (s.Conflicts.Count > 0)
                text.Append("You have unmerged paths.\n  (fix conflicts and run \"git commit\")\n  (use \"git merge --abort\" to abort the merge)\n\n");
            else
                text.Append("All conflicts fixed but you are still merging.\n  (use \"git commit\" to conclude merge)\n\n");
        }
        if (s.IsInitial) text.Append("\nNo commits yet\n\n");

        if (s.Staged.Count > 0)
        {
            text.Append("Changes to be committed:\n");
            if (repo.MergeHead is null)
                text.Append(s.IsInitial ? "  (use \"git rm --cached <file>...\" to unstage)\n" : "  (use \"git restore --staged <file>...\" to unstage)\n");
            foreach (var change in s.Staged)
                text.Append('\t').Append(Ansi.Paint(Ansi.Green, Label(change.Kind) + Show(ws, repo, change.Path))).Append('\n');
            text.Append('\n');
        }
        if (s.Conflicts.Count > 0)
        {
            text.Append("Unmerged paths:\n");
            text.Append(s.Conflicts.Any(c => c.Short is "DU" or "UD" or "DD" or "AU" or "UA")
                ? "  (use \"git add/rm <file>...\" as appropriate to mark resolution)\n"
                : "  (use \"git add <file>...\" to mark resolution)\n");
            foreach (var c in s.Conflicts)
                text.Append('\t').Append(Ansi.Paint(Ansi.Red, (c.Label + ":").PadRight(17) + Show(ws, repo, c.Path))).Append('\n');
            text.Append('\n');
        }
        if (s.Unstaged.Count > 0)
        {
            text.Append("Changes not staged for commit:\n");
            text.Append(s.Unstaged.Any(c => c.Kind == ChangeKind.Deleted)
                ? "  (use \"git add/rm <file>...\" to update what will be committed)\n"
                : "  (use \"git add <file>...\" to update what will be committed)\n");
            text.Append("  (use \"git restore <file>...\" to discard changes in working directory)\n");
            foreach (var change in s.Unstaged)
                text.Append('\t').Append(Ansi.Paint(Ansi.Red, Label(change.Kind) + Show(ws, repo, change.Path))).Append('\n');
            text.Append('\n');
        }
        if (s.Untracked.Count > 0)
        {
            text.Append("Untracked files:\n  (use \"git add <file>...\" to include in what will be committed)\n");
            foreach (var path in s.Untracked)
                text.Append('\t').Append(Ansi.Paint(Ansi.Red, Show(ws, repo, path))).Append('\n');
            text.Append('\n');
        }

        if (s.Staged.Count == 0)
        {
            if (s.Unstaged.Count > 0 || s.Conflicts.Count > 0) text.Append("no changes added to commit (use \"git add\" and/or \"git commit -a\")\n");
            else if (s.Untracked.Count > 0) text.Append("nothing added to commit but untracked files present (use \"git add\" to track)\n");
            else if (s.IsInitial) text.Append("nothing to commit (create/copy files and use \"git add\" to track)\n");
            else if (repo.MergeHead is null) text.Append("nothing to commit, working tree clean\n");
        }
        return text.ToString();
    }

    /// <summary>« HEAD detached at abc1234 » (en rouge, sauf l'identifiant), ou « detached from » après de nouveaux commits.</summary>
    public static string Detached(Repository repo)
    {
        var at = repo.DetachedAt ?? repo.DetachedHead!.Value;
        var where = repo.DetachedHead!.Value.Equals(at) ? "at" : "from";
        return Ansi.Paint(Ansi.Red, $"HEAD detached {where} ") + at.Short;
    }

    /// <summary>« git status -s » : deux colonnes (index en vert, répertoire de travail en rouge), puis le chemin.</summary>
    public static string Short(Workstation ws, StatusReport s)
    {
        var repo = s.Repository;
        var rows = new SortedDictionary<string, (char X, char Y)>(StringComparer.Ordinal);
        foreach (var change in s.Staged) rows[change.Path] = (Code(change.Kind), ' ');
        foreach (var change in s.Unstaged)
            rows[change.Path] = (rows.TryGetValue(change.Path, out var row) ? row.X : ' ', Code(change.Kind));
        var text = new StringBuilder();
        foreach (var c in s.Conflicts) rows[c.Path] = (c.Short[0], c.Short[1]);
        foreach (var (path, (x, y)) in rows)
        {
            var conflict = s.Conflicts.Any(c => c.Path == path);
            if (conflict) text.Append(Ansi.Paint(Ansi.Red, $"{x}{y}"));
            else
            {
                text.Append(x == ' ' ? " " : Ansi.Paint(Ansi.Green, x.ToString()));
                text.Append(y == ' ' ? " " : Ansi.Paint(Ansi.Red, y.ToString()));
            }
            text.Append(' ').Append(Show(ws, repo, path)).Append('\n');
        }
        foreach (var path in s.Untracked)
            text.Append(Ansi.Paint(Ansi.Red, "??")).Append(' ').Append(Show(ws, repo, path)).Append('\n');
        return text.ToString();
    }

    private static char Code(ChangeKind kind) => kind switch { ChangeKind.Added => 'A', ChangeKind.Modified => 'M', _ => 'D' };
}

/// <summary>Les motifs des fichiers .gitignore du dépôt (motifs simples : nom, *.ext, dossier/, /racine).</summary>
public sealed class Ignore
{
    private readonly List<(string Dir, string Pattern, bool Negate, bool DirOnly, bool Anchored)> _rules = [];

    public static Ignore Load(SortedDictionary<string, byte[]> work)
    {
        var ignore = new Ignore();
        foreach (var (path, data) in work.Where(f => f.Key == ".gitignore" || f.Key.EndsWith("/.gitignore")))
        {
            var dir = path.Length > ".gitignore".Length ? path[..^".gitignore".Length] : "";
            foreach (var raw in Encoding.UTF8.GetString(data).Split('\n'))
            {
                var line = raw.TrimEnd('\r').TrimEnd();
                if (line.Length == 0 || line.StartsWith('#')) continue;
                var negate = line.StartsWith('!');
                if (negate) line = line[1..];
                var dirOnly = line.EndsWith('/');
                if (dirOnly) line = line.TrimEnd('/');
                var anchored = line.Contains('/');
                ignore._rules.Add((dir, line.TrimStart('/'), negate, dirOnly, anchored));
            }
        }
        return ignore;
    }

    public bool IsIgnored(string path)
    {
        var ignored = false;
        foreach (var (dir, pattern, negate, dirOnly, anchored) in _rules)
        {
            if (!path.StartsWith(dir, StringComparison.Ordinal)) continue;
            var relative = path[dir.Length..];
            if (Matches(relative, pattern, dirOnly, anchored)) ignored = !negate;
        }
        return ignored;
    }

    private static bool Matches(string relative, string pattern, bool dirOnly, bool anchored)
    {
        var parts = relative.Split('/');
        if (anchored)
        {
            var depth = pattern.Split('/').Length;
            for (var i = depth; i <= parts.Length; i++)
            {
                if (dirOnly && i == parts.Length) break;
                if (Glob(string.Join('/', parts[..i]), pattern)) return true;
            }
            return false;
        }
        var last = dirOnly ? parts.Length - 1 : parts.Length;
        for (var i = 0; i < last; i++)
            if (Glob(parts[i], pattern)) return true;
        return false;
    }

    private static bool Glob(string text, string pattern)
    {
        var regex = "^" + System.Text.RegularExpressions.Regex.Escape(pattern).Replace("\\*", "[^/]*").Replace("\\?", "[^/]") + "$";
        return System.Text.RegularExpressions.Regex.IsMatch(text, regex);
    }
}
