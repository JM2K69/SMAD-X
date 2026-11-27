// Rejoue les scénarios de fidélité de SMAG-X sur le vrai Git et fige ses sorties dans SMAG-X.Tests/Fidelity/<scénario>.git.txt.
// Environnement neutre : HOME et configuration à part (ni système ni utilisateur), langue C, pas de pager, dates fixées
// (une minute par commande), et les réglages d'un terminal (couleurs, décorations) puisque l'élève tape dans un terminal.
// Plusieurs postes : « @bob » passe sur le poste de bob (son HOME, son ~/projet). La forge : « forge classe/projet » crée
// un dépôt nu (branche main) ; l'adresse git@gitlab.example:classe/projet.git devient son file://, et les sorties
// reprennent l'adresse SSH (« To gitlab.example:classe/projet.git », comme Git l'écrit pour une adresse SSH).
// Usage : dotnet run --project tools/SmagxGitCheck -- <racine du dépôt> [scénario...]
using System.Diagnostics;
using System.Text;
using SMAGX.Fidelity;
using SMAGX.Machine;

const string ForgeUrl = "git@gitlab.example:";
var repoRoot = Path.GetFullPath(args.Length > 0 ? args[0] : ".");
var only = args.Skip(1).ToHashSet();
var scenarios = Path.Combine(repoRoot, "SMAG-X.Tests", "Fidelity", "scenarios");
var output = Path.Combine(repoRoot, "SMAG-X.Tests", "Fidelity");
Console.WriteLine(Run("git", ["--version"], Environment.CurrentDirectory, new()).Out.Trim());

foreach (var file in Directory.GetFiles(scenarios, "*.txt").Order())
{
    var name = Path.GetFileNameWithoutExtension(file);
    if (only.Count > 0 && !only.Contains(name)) continue;
    var steps = Transcript.Parse(File.ReadAllText(file));
    var temp = Path.Combine(Path.GetTempPath(), "smagx-" + Guid.NewGuid().ToString("N")[..8]);
    var forge = Path.Combine(temp, "forge");
    var forgeUrl = "file://" + forge.Replace('\\', '/');
    Directory.CreateDirectory(Path.Combine(temp, "xdg"));
    var cwds = new Dictionary<string, string>();
    string Home(string user) => Path.Combine(temp, "home", user);
    string Cwd(string user)
    {
        if (cwds.TryGetValue(user, out var dir)) return dir;
        Directory.CreateDirectory(Path.Combine(Home(user), "projet"));
        return cwds[user] = Path.Combine(Home(user), "projet");
    }
    var user = "alice";
    var results = new List<StepResult>();
    var index = 0;
    try
    {
        foreach (var step in steps)
        {
            var cwd = Cwd(user);
            switch (step.Kind)
            {
                case "@":
                    user = step.Argument;
                    Cwd(user);
                    break;
                case "forge":
                    var bare = Path.Combine(forge, step.Argument + ".git");
                    Directory.CreateDirectory(bare);
                    Run("git", ["init", "--bare", "-q", "-b", "main", bare], temp, new() { ["GIT_CONFIG_NOSYSTEM"] = "1", ["HOME"] = temp });
                    break;
                case "$":
                    var words = Shell.Tokenize(step.Argument);
                    var date = Transcript.DateOf(index++).ToString("yyyy-MM-ddTHH:mm:sszzz");
                    if (words[0] == "cd")
                    {
                        cwds[user] = Path.GetFullPath(Path.Combine(cwd, words.Count > 1 ? words[1] : Home(user)));
                        results.Add(new StepResult(step.Argument, "", "", 0));
                        continue;
                    }
                    if (words[0] != "git") throw new InvalidOperationException($"{name} : seules les commandes git et cd se rejouent ({step.Argument})");
                    var env = new Dictionary<string, string>
                    {
                        ["HOME"] = Home(user), ["XDG_CONFIG_HOME"] = Path.Combine(temp, "xdg"), ["GIT_CONFIG_NOSYSTEM"] = "1",
                        ["LANG"] = "C", ["LC_ALL"] = "C", ["GIT_PAGER"] = "cat", ["PAGER"] = "cat", ["GIT_EDITOR"] = "true",
                        ["GIT_TERMINAL_PROMPT"] = "0", ["GIT_AUTHOR_DATE"] = date, ["GIT_COMMITTER_DATE"] = date
                    };
                    var gitArgs = new List<string> { "-c", "color.ui=always", "-c", "log.decorate=short" };
                    gitArgs.AddRange(words.Skip(1).Select(w => w.StartsWith(ForgeUrl) ? forgeUrl + "/" + w[ForgeUrl.Length..] : w));
                    var r = Run("git", gitArgs, cwd, env);
                    results.Add(new StepResult(step.Argument, Normalize(r.Out, temp, forgeUrl), Normalize(r.Err, temp, forgeUrl), r.Code));
                    break;
                case "write":
                    Write(Path.Combine(cwd, step.Argument), step.Content ?? "");
                    break;
                case "append":
                    var path = Path.Combine(cwd, step.Argument);
                    Write(path, (File.Exists(path) ? File.ReadAllText(path) : "") + step.Content);
                    break;
                case "delete":
                    File.Delete(Path.Combine(cwd, step.Argument));
                    break;
                case "mkdir":
                    Directory.CreateDirectory(Path.Combine(cwd, step.Argument));
                    break;
            }
        }
    }
    finally
    {
        try { Directory.Delete(temp, true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
    File.WriteAllText(Path.Combine(output, name + ".git.txt"), Transcript.Write(results), new UTF8Encoding(false));
    Console.WriteLine($"  {name} : {results.Count} commandes");
}

static void Write(string path, string content)
{
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    File.WriteAllText(path, content, new UTF8Encoding(false));
}

// Les dossiers temporaires deviennent les /home/<élève> du poste simulé, le compte et la machine réels alice@poste,
// et l'adresse file:// de la forge redevient l'adresse SSH (anonymisée après « To », « From » et dans « refs to »)
static string Normalize(string text, string temp, string forgeUrl)
{
    var home = Path.Combine(temp, "home");
    text = text.Replace("To " + forgeUrl + "/", "To gitlab.example:")
        .Replace("From " + forgeUrl + "/", "From gitlab.example:")
        .Replace("refs to '" + forgeUrl + "/", "refs to 'gitlab.example:")
        // Le message d'une fusion par git pull reprend l'adresse anonymisée (« of github.com:groupe/projet »)
        .Replace("' of " + forgeUrl + "/", "' of gitlab.example:")
        .Replace(forgeUrl + "/", "git@gitlab.example:");
    return text.Replace(home.Replace('\\', '/'), "/home").Replace(home, "/home")
        .Replace($"{Environment.UserName}@{Environment.MachineName}", "alice@poste")
        .Replace($"{Environment.UserName}@{Environment.MachineName.ToLowerInvariant()}", "alice@poste");
}

static (string Out, string Err, int Code) Run(string file, List<string> arguments, string cwd, Dictionary<string, string> env)
{
    var info = new ProcessStartInfo(file)
    {
        WorkingDirectory = cwd,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        StandardOutputEncoding = Encoding.UTF8,
        StandardErrorEncoding = Encoding.UTF8
    };
    foreach (var a in arguments) info.ArgumentList.Add(a);
    foreach (var key in info.Environment.Keys.Where(k => k.StartsWith("GIT_", StringComparison.OrdinalIgnoreCase)).ToList())
        info.Environment.Remove(key);
    foreach (var (key, value) in env) info.Environment[key] = value;
    using var process = Process.Start(info)!;
    var stdout = process.StandardOutput.ReadToEndAsync();
    var stderr = process.StandardError.ReadToEndAsync();
    process.WaitForExit();
    return (stdout.Result.Replace("\r\n", "\n"), stderr.Result.Replace("\r\n", "\n"), process.ExitCode);
}
