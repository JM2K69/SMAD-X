using System.Text;
using SMAGX.Git;

namespace SMAGX.Machine;

/// <summary>Ce qu'une commande a écrit, dans l'ordre, sur la sortie standard (1) et la sortie d'erreur (2), et son code de retour.</summary>
public sealed class CommandResult
{
    private readonly List<(bool Error, string Text)> _chunks = [];

    public int ExitCode { get; set; }

    public IReadOnlyList<(bool Error, string Text)> Chunks => _chunks;

    public string Out => string.Concat(_chunks.Where(c => !c.Error).Select(c => c.Text));

    public string Err => string.Concat(_chunks.Where(c => c.Error).Select(c => c.Text));

    /// <summary>Tout ce que voit l'élève dans son terminal, dans l'ordre d'écriture.</summary>
    public string Display => string.Concat(_chunks.Select(c => c.Text));

    public void Write(string text) { if (text.Length > 0) _chunks.Add((false, text)); }

    public void Error(string text) { if (text.Length > 0) _chunks.Add((true, text)); }

    /// <summary>« fatal: … » sur la sortie d'erreur, code 128, comme Git.</summary>
    public int Fatal(string message)
    {
        Error("fatal: " + message + "\n");
        return ExitCode = 128;
    }
}

/// <summary>Le terminal du poste : découpe la ligne tapée comme un shell (guillemets, échappements, jokers) et lance la commande.</summary>
public sealed class Shell(Workstation ws)
{
    public Workstation Workstation { get; } = ws;

    /// <summary>L'invite affichée avant chaque commande : « alice@poste:~/projet (main)$ ».</summary>
    public string Prompt()
    {
        var dir = Workstation.Cwd == Workstation.Home ? "~"
            : Workstation.Cwd.StartsWith(Workstation.Home + "/") ? "~" + Workstation.Cwd[Workstation.Home.Length..] : Workstation.Cwd;
        var repo = Workstation.FindRepository(Workstation.Cwd);
        var branch = repo is null ? "" : $" ({(repo.CurrentBranch ?? repo.DetachedHead?.Short + "...")}{(repo.MergeHead is null ? "" : "|MERGING")})";
        return $"{Workstation.User}@{Workstation.Host}:{dir}{branch}$ ";
    }

    public CommandResult Run(string line)
    {
        var result = new CommandResult();
        List<string> args;
        try
        {
            args = Expand(Tokenize(line));
        }
        catch (FormatException e)
        {
            result.Error($"bash: {e.Message}\n");
            result.ExitCode = 2;
            return result;
        }
        if (args.Count == 0) return result;
        result.ExitCode = args[0] switch
        {
            "git" => GitCli.Run(Workstation, args.Skip(1).ToList(), result),
            "ssh-keygen" => SMAGX.Ssh.SshCommands.Keygen(Workstation, args, result),
            "ssh" => SMAGX.Ssh.SshCommands.Ssh(Workstation, args, result),
            "cd" => Cd(args, result),
            "pwd" => Echo(Workstation.Cwd, result),
            "ls" => Ls(args, result),
            "cat" => Cat(args, result),
            "mkdir" => Mkdir(args, result),
            "touch" => Touch(args, result),
            "rm" => Rm(args, result),
            "echo" => EchoCommand(line, args, result),
            "clear" => 0,
            _ => Unknown(args[0], result)
        };
        return result;
    }

    // ── Découpage de la ligne ───────────────────────────────────────────────

    /// <summary>Les mots de la ligne, guillemets retirés, comme bash (« ' » littéral, « " » avec \" et \\, \ hors guillemets).</summary>
    public static List<string> Tokenize(string line)
    {
        var words = new List<string>();
        var word = new StringBuilder();
        var inWord = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (char.IsWhiteSpace(c))
            {
                if (inWord) { words.Add(word.ToString()); word.Clear(); inWord = false; }
                continue;
            }
            inWord = true;
            if (c == '\'')
            {
                var end = line.IndexOf('\'', i + 1);
                if (end < 0) throw new FormatException("unexpected EOF while looking for matching `''");
                word.Append(line, i + 1, end - i - 1);
                i = end;
            }
            else if (c == '"')
            {
                i++;
                for (; i < line.Length && line[i] != '"'; i++)
                {
                    if (line[i] == '\\' && i + 1 < line.Length && line[i + 1] is '"' or '\\' or '$' or '`') i++;
                    word.Append(line[i]);
                }
                if (i >= line.Length) throw new FormatException("unexpected EOF while looking for matching `\"'");
            }
            else if (c == '\\' && i + 1 < line.Length) word.Append(line[++i]);
            else word.Append(c);
        }
        if (inWord) words.Add(word.ToString());
        return words;
    }

    /// <summary>Les jokers (* et ?) remplacés par les fichiers du dossier courant qui correspondent, comme bash.</summary>
    private List<string> Expand(List<string> words) =>
        words.SelectMany(w => w.IndexOfAny(['*', '?']) < 0 ? [w] : Glob(w)).ToList();

    private IEnumerable<string> Glob(string pattern)
    {
        var regex = new System.Text.RegularExpressions.Regex("^" + System.Text.RegularExpressions.Regex.Escape(pattern)
            .Replace("\\*", "[^/]*").Replace("\\?", "[^/]") + "$");
        var names = Workstation.Files.Keys.Concat(Workstation.Directories)
            .Where(p => Workstation.Parent(p) == Workstation.Cwd && p != Workstation.Cwd)
            .Select(p => p[(Workstation.Cwd == "/" ? 1 : Workstation.Cwd.Length + 1)..])
            .Where(n => !n.StartsWith('.') && regex.IsMatch(n)).Distinct().Order(StringComparer.Ordinal).ToList();
        return names.Count > 0 ? names : [pattern];
    }

    // ── Commandes du shell ──────────────────────────────────────────────────

    private int Cd(List<string> args, CommandResult r)
    {
        var target = args.Count > 1 ? Workstation.Resolve(args[1]) : Workstation.Home;
        if (!Workstation.DirectoryExists(target))
        {
            r.Error($"bash: cd: {args[1]}: No such file or directory\n");
            return 1;
        }
        Workstation.Cwd = target;
        return 0;
    }

    private static int Echo(string text, CommandResult r)
    {
        r.Write(text + "\n");
        return 0;
    }

    private int Ls(List<string> args, CommandResult r)
    {
        var showAll = args.Contains("-a") || args.Contains("-la") || args.Contains("-al");
        var dir = args.Skip(1).FirstOrDefault(a => !a.StartsWith('-')) is { } d ? Workstation.Resolve(d) : Workstation.Cwd;
        var names = Workstation.Files.Keys.Concat(Workstation.Directories)
            .Where(p => p != dir && Workstation.Parent(p) == dir)
            .Select(p => p[(dir == "/" ? 1 : dir.Length + 1)..])
            .Concat(Workstation.Repositories.ContainsKey(dir) ? [".git"] : [])
            .Where(n => showAll || !n.StartsWith('.')).Distinct().Order(StringComparer.Ordinal);
        var line = string.Join("  ", names);
        if (line.Length > 0) r.Write(line + "\n");
        return 0;
    }

    private int Cat(List<string> args, CommandResult r)
    {
        var code = 0;
        foreach (var name in args.Skip(1))
        {
            var path = Workstation.Resolve(name);
            if (Workstation.FileExists(path)) r.Write(Workstation.ReadText(path));
            else { r.Error($"cat: {name}: No such file or directory\n"); code = 1; }
        }
        return code;
    }

    private int Mkdir(List<string> args, CommandResult r)
    {
        foreach (var name in args.Skip(1).Where(a => !a.StartsWith('-')))
            Workstation.EnsureDirectory(Workstation.Resolve(name));
        return 0;
    }

    private int Touch(List<string> args, CommandResult r)
    {
        foreach (var name in args.Skip(1))
        {
            var path = Workstation.Resolve(name);
            if (!Workstation.FileExists(path)) Workstation.WriteFile(path, []);
        }
        return 0;
    }

    private int Rm(List<string> args, CommandResult r)
    {
        var code = 0;
        var recursive = args.Any(a => a is "-r" or "-rf" or "-fr" or "-R");
        foreach (var name in args.Skip(1).Where(a => !a.StartsWith('-')))
        {
            var path = Workstation.Resolve(name);
            if (Workstation.FileExists(path)) Workstation.DeleteFile(path);
            else if (recursive && Workstation.DirectoryExists(path))
            {
                foreach (var file in Workstation.Files.Keys.Where(f => f.StartsWith(path + "/")).ToList()) Workstation.DeleteFile(file);
                Workstation.Directories.RemoveWhere(d => d == path || d.StartsWith(path + "/"));
                Workstation.Repositories.Remove(path);
            }
            else { r.Error($"rm: cannot remove '{name}': No such file or directory\n"); code = 1; }
        }
        return code;
    }

    /// <summary>« echo texte », « echo texte > fichier » (remplace) et « echo texte >> fichier » (ajoute).</summary>
    private int EchoCommand(string line, List<string> args, CommandResult r)
    {
        var append = line.Contains(">>");
        var redirect = line.IndexOf('>');
        if (redirect < 0) return Echo(string.Join(' ', args.Skip(1)), r);
        var words = Tokenize(line[..redirect]);
        var target = Tokenize(line[(redirect + (append ? 2 : 1))..]);
        if (target.Count == 0) { r.Error("bash: syntax error near unexpected token `newline'\n"); return 2; }
        var path = Workstation.Resolve(target[0]);
        var text = string.Join(' ', words.Skip(1)) + "\n";
        Workstation.WriteText(path, append && Workstation.FileExists(path) ? Workstation.ReadText(path) + text : text);
        return 0;
    }

    private static int Unknown(string name, CommandResult r)
    {
        r.Error($"{name}: command not found\n");
        return 127;
    }
}
