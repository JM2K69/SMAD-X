using SMAGX.Forge;
using SMAGX.Machine;

namespace SMAGX.Ssh;

/// <summary>
/// La partie SSH du poste : la connexion à une forge (clé du serveur vérifiée dans known_hosts, puis clés de l'élève
/// proposées une à une), et les commandes ssh-keygen et ssh. Les messages sont ceux d'OpenSSH 10.3.
/// </summary>
public static class SshClient
{
    public const string RemoteFatal = "fatal: Could not read from remote repository.\n\nPlease make sure you have the correct access rights\nand the repository exists.\n";

    /// <summary>L'adresse IP fictive d'une forge, dans une plage réservée à la documentation.</summary>
    public static string Address(string host) => host.StartsWith("github", StringComparison.Ordinal) ? "203.0.113.20" : "203.0.113.10";

    /// <summary>Les clés que ssh essaie, dans son ordre : ~/.ssh/id_rsa, id_ecdsa, id_ecdsa_sk, id_ed25519, id_ed25519_sk.</summary>
    public static IEnumerable<string> IdentityFiles(Workstation ws) =>
        new[] { "id_rsa", "id_ecdsa", "id_ecdsa_sk", "id_ed25519", "id_ed25519_sk" }.Select(n => $"{ws.Home}/.ssh/{n}").Where(ws.FileExists);

    /// <summary>
    /// Se connecte à la forge comme <paramref name="user"/> : vérifie la clé du serveur (et la demande la première fois),
    /// puis propose les clés du poste. Rend le compte de la forge à qui appartient la clé acceptée, ou null après avoir
    /// écrit le refus de ssh. Lève ForgeUnavailableException si la forge ne répond pas.
    /// </summary>
    public static string? Login(Workstation ws, CommandResult r, IForge forge, string user, string host, string? identity = null)
    {
        forge.Ping();
        if (!CheckHost(ws, r, host)) return null;
        var files = IdentityFiles(ws).ToList();
        if (identity is not null) files.Insert(0, identity);
        foreach (var path in files.Distinct())
        {
            if (!ws.FileExists(path)) continue;
            if (SshKeyFile.ReadPrivate(ws.ReadText(path)) is not { } key)
            {
                r.Error($"Load key \"{path}\": invalid format\n");
                continue;
            }
            // Les forges n'ouvrent qu'au compte « git » : la clé désigne ensuite l'élève
            if (user != "git" || forge.Authenticate(key.Line) is not { } account) continue;
            if (ws.KeyPassphrases.TryGetValue(path, out var passphrase) && !Unlock(ws, r, path, passphrase)) continue;
            return account;
        }
        r.Error($"{user}@{host}: Permission denied (publickey).\n");
        return null;
    }

    /// <summary>Une question dans le terminal ; la réponse tapée s'affiche à sa suite (sauf une saisie cachée).</summary>
    public static string? Prompt(Workstation ws, CommandResult r, string question, bool secret = false)
    {
        r.Write(question);
        var answer = ws.Ask?.Invoke(r.Display, secret);
        r.Write((secret ? "" : answer ?? "") + "\n");
        return answer;
    }

    // La clé du serveur : connue (et identique), ou acceptée par l'élève
    private static bool CheckHost(Workstation ws, CommandResult r, string host)
    {
        var path = ws.Home + "/.ssh/known_hosts";
        var hostKey = SshPublicKey.HostKey(host);
        var text = ws.FileExists(path) ? ws.ReadText(path) : "";
        if (KnownHosts.Find(text, host) is { } known)
        {
            if (known.SameKey(hostKey)) return true;
            var line = text.Split('\n').TakeWhile(l => KnownHosts.Find(l, host) is null).Count() + 1;
            r.Error("@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@\n"
                    + "@    WARNING: REMOTE HOST IDENTIFICATION HAS CHANGED!     @\n"
                    + "@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@@\n"
                    + "IT IS POSSIBLE THAT SOMEONE IS DOING SOMETHING NASTY!\n"
                    + "Someone could be eavesdropping on you right now (man-in-the-middle attack)!\n"
                    + "It is also possible that a host key has just been changed.\n"
                    + $"The fingerprint for the {hostKey.Label} key sent by the remote host is\n{hostKey.Fingerprint}.\n"
                    + "Please contact your system administrator.\n"
                    + $"Add correct host key in {path} to get rid of this message.\n"
                    + $"Offending {known.Label} key in {path}:{line}\n"
                    + $"Host key for {host} has changed and you have requested strict checking.\n"
                    + "Host key verification failed.\n");
            return false;
        }
        if (ws.Ask is null)
        {
            r.Error("Host key verification failed.\n");
            return false;
        }
        var question = $"The authenticity of host '{host} ({Address(host)})' can't be established.\n"
                       + $"{hostKey.Label} key fingerprint is: {hostKey.Fingerprint}\n"
                       + "This key is not known by any other names.\n"
                       + "Are you sure you want to continue connecting (yes/no/[fingerprint])? ";
        while (true)
        {
            var answer = Prompt(ws, r, question)?.Trim();
            if (answer is null || answer.Equals("no", StringComparison.OrdinalIgnoreCase))
            {
                r.Error("Host key verification failed.\n");
                return false;
            }
            if (answer.Equals("yes", StringComparison.OrdinalIgnoreCase) || answer == hostKey.Fingerprint) break;
            question = "Please type 'yes', 'no' or the fingerprint: ";
        }
        ws.EnsureDirectory(ws.Home + "/.ssh");
        ws.WriteText(path, text + KnownHosts.Line(host, hostKey, ws.Random));
        r.Error($"Warning: Permanently added '{host}' ({hostKey.Label}) to the list of known hosts.\n");
        return true;
    }

    // Une clé protégée : trois essais de phrase de passe, une réponse vide passe à la clé suivante
    private static bool Unlock(Workstation ws, CommandResult r, string path, string passphrase)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var answer = Prompt(ws, r, $"Enter passphrase for key '{path}': ", secret: true);
            if (string.IsNullOrEmpty(answer)) return false;
            if (answer == passphrase) return true;
        }
        return false;
    }
}

/// <summary>Les commandes ssh-keygen et ssh du terminal.</summary>
public static class SshCommands
{
    // ── ssh-keygen ──────────────────────────────────────────────────────────

    public static int Keygen(Workstation ws, List<string> args, CommandResult r)
    {
        if (!Options(args, "tbCfNR", "lvyq", out var options, out var bad, out _))
        {
            r.Error($"ssh-keygen: unknown option -- {bad}\nusage: ssh-keygen [-q] [-b bits] [-C comment] [-f output_keyfile] [-N new_passphrase] [-t ecdsa | ed25519 | rsa]\n       ssh-keygen -l [-v] [-f input_keyfile]\n       ssh-keygen -y [-f input_keyfile]\n       ssh-keygen -R hostname [-f known_hosts_file]\n");
            return 1;
        }
        if (options.TryGetValue('R', out var removed)) return RemoveHost(ws, removed, options.GetValueOrDefault('f'), r);
        if (options.ContainsKey('l') || options.ContainsKey('y')) return Show(ws, options, r);
        return Generate(ws, options, r);
    }

    private static int Generate(Workstation ws, Dictionary<char, string> options, CommandResult r)
    {
        var name = options.GetValueOrDefault('t') ?? "ed25519";
        if (SshKeyFile.TypeOf(name) is not { } type)
        {
            r.Error($"unknown key type {name}\n");
            return 255;
        }
        var bits = type == "ssh-rsa" ? 3072 : 256;
        if (type == "ssh-rsa" && options.TryGetValue('b', out var b))
        {
            if (!int.TryParse(b, out bits)) { r.Error($"Bits has bad value {b} (invalid)\n"); return 255; }
            if (bits < 1024) { r.Error("Invalid RSA key length: minimum is 1024 bits\n"); return 255; }
            if (bits > 16384) { r.Error("Invalid RSA key length: maximum is 16384 bits\n"); return 255; }
            bits -= bits % 8;
        }
        var quiet = options.ContainsKey('q');
        if (!quiet) r.Write($"Generating public/private {name} key pair.\n");

        var suffix = type switch { "ssh-rsa" => "rsa", "ssh-ed25519" => "ed25519", _ => "ecdsa" };
        var shown = options.GetValueOrDefault('f');
        if (shown is null)
        {
            var standard = $"{ws.Home}/.ssh/id_{suffix}";
            var answer = SshClient.Prompt(ws, r, $"Enter file in which to save the key ({standard}): ")?.Trim();
            shown = string.IsNullOrEmpty(answer) ? standard : answer.StartsWith("~/") ? ws.Home + answer[1..] : answer;
        }
        var path = ws.Resolve(shown);
        var sshDir = ws.Home + "/.ssh";
        if (Workstation.Parent(path) == sshDir && !ws.DirectoryExists(sshDir))
        {
            ws.EnsureDirectory(sshDir);
            if (!quiet) r.Error($"Created directory '{sshDir}'.\n");
        }
        if (!ws.DirectoryExists(Workstation.Parent(path)))
        {
            r.Error($"Saving key \"{shown}\" failed: No such file or directory\n");
            return 255;
        }
        if (ws.FileExists(path))
        {
            r.Write($"{shown} already exists.\n");
            if (SshClient.Prompt(ws, r, "Overwrite (y/n)? ")?.Trim() is not { Length: > 0 } yes || yes[0] is not ('y' or 'Y')) return 1;
        }

        var passphrase = options.GetValueOrDefault('N');
        while (passphrase is null)
        {
            var first = SshClient.Prompt(ws, r, $"Enter passphrase for \"{shown}\" (empty for no passphrase): ", secret: true) ?? "";
            var second = SshClient.Prompt(ws, r, "Enter same passphrase again: ", secret: true) ?? "";
            if (first == second) passphrase = first;
            else r.Write("Passphrases do not match.  Try again.\n");
        }

        var comment = options.GetValueOrDefault('C') ?? $"{ws.User}@{ws.Host}";
        var (key, privateFile) = SshKeyFile.Generate(type, bits, comment, ws.Random);
        ws.WriteText(path, privateFile);
        ws.WriteText(path + ".pub", key.Line + "\n");
        if (passphrase.Length > 0) ws.KeyPassphrases[path] = passphrase;
        else ws.KeyPassphrases.Remove(path);
        if (quiet) return 0;
        r.Write($"Your identification has been saved in {shown}\n"
                + $"Your public key has been saved in {shown}.pub\n"
                + "The key fingerprint is:\n"
                + $"{key.Fingerprint} {comment}\n"
                + "The key's randomart image is:\n"
                + key.Randomart + "\n");
        return 0;
    }

    // -l : empreinte (et dessin avec -v) ; -y : la clé publique d'une clé privée
    private static int Show(Workstation ws, Dictionary<char, string> options, CommandResult r)
    {
        var shown = options.GetValueOrDefault('f');
        if (shown is null)
        {
            var answer = SshClient.Prompt(ws, r, $"Enter file in which the key is ({ws.Home}/.ssh/id_ed25519): ")?.Trim();
            shown = string.IsNullOrEmpty(answer) ? $"{ws.Home}/.ssh/id_ed25519" : answer;
        }
        var path = ws.Resolve(shown);
        if (!ws.FileExists(path))
        {
            r.Error($"ssh-keygen: {shown}: No such file or directory\n");
            return 255;
        }
        var text = ws.ReadText(path);
        var key = SshKeyFile.ReadPrivate(text) ?? (options.ContainsKey('y') ? null : SshPublicKey.Parse(text.Split('\n')[0]));
        if (key is null)
        {
            r.Error(options.ContainsKey('y') ? $"Load key \"{shown}\": invalid format\n" : $"{shown} is not a public key file.\n");
            return 255;
        }
        if (options.ContainsKey('y'))
        {
            r.Write(key.Line + "\n");
            return 0;
        }
        r.Write(key.FingerprintLine + "\n" + (options.ContainsKey('v') ? key.Randomart + "\n" : ""));
        return 0;
    }

    // -R : oublie la clé d'un serveur dans known_hosts (et garde l'ancien fichier en .old)
    private static int RemoveHost(Workstation ws, string host, string? file, CommandResult r)
    {
        var shown = file ?? ws.Home + "/.ssh/known_hosts";
        var path = ws.Resolve(shown);
        if (!ws.FileExists(path))
        {
            r.Error($"Cannot stat {shown}: No such file or directory\n");
            return 255;
        }
        var text = ws.ReadText(path);
        var (kept, lines) = KnownHosts.Remove(text, host);
        if (lines.Count == 0)
        {
            r.Write($"Host {host} not found in {shown}\n");
            return 0;
        }
        foreach (var line in lines) r.Write($"# Host {host} found: line {line}\n");
        ws.WriteText(path + ".old", text);
        ws.WriteText(path, kept);
        r.Error($"{shown} updated.\nOriginal contents retained as {shown}.old\n");
        return 0;
    }

    // ── ssh ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// « ssh -T git@gitlab.example » : le test de connexion à la forge. Elle n'ouvre pas de shell : GitLab souhaite la
    /// bienvenue, GitHub salue l'élève et ferme (code 1).
    /// </summary>
    public static int Ssh(Workstation ws, List<string> args, CommandResult r)
    {
        if (!Options(args, "ipl", "TtvqN", out var options, out var bad, out var words, stopAtWord: true))
        {
            r.Error($"unknown option -- {bad}\nusage: ssh [-46AaCfGgKkMNnqsTtVvXxYy] [-i identity_file] [-l login_name] [-p port] destination [command [argument ...]]\n");
            return 255;
        }
        if (words.Count == 0)
        {
            r.Error("usage: ssh [-46AaCfGgKkMNnqsTtVvXxYy] [-i identity_file] [-l login_name] [-p port] destination [command [argument ...]]\n");
            return 255;
        }
        var destination = words[0];
        var at = destination.LastIndexOf('@');
        var user = at > 0 ? destination[..at] : options.GetValueOrDefault('l') ?? ws.User;
        var host = at > 0 ? destination[(at + 1)..] : destination;
        if (ws.FindForge(host) is not { } forge)
        {
            r.Error($"ssh: Could not resolve hostname {host}: Name or service not known\n");
            return 255;
        }
        string? account;
        try
        {
            account = SshClient.Login(ws, r, forge, user, host, options.TryGetValue('i', out var i) ? ws.Resolve(i) : null);
        }
        catch (ForgeRefusedException)
        {
            r.Error($"{user}@{host}: Permission denied (publickey).\n");
            return 255;
        }
        catch (ForgeUnavailableException e)
        {
            r.Error($"ssh: connect to host {host} port 22: {(e.ConnectionRefused ? "Connection refused" : "Connection timed out")}\n");
            return 255;
        }
        if (account is null) return 255;
        var pty = !options.ContainsKey('T') && words.Count == 1;
        if (pty) r.Error("PTY allocation request failed on channel 0\n");
        int code;
        if (forge.Kind == ForgeKind.GitLab)
        {
            r.Write($"Welcome to GitLab, @{account}!\n");
            code = 0;
        }
        else
        {
            r.Error($"Hi {account}! You've successfully authenticated, but GitHub does not provide shell access.\n");
            code = 1;
        }
        if (pty) r.Error($"Connection to {host} closed.\n");
        return code;
    }

    /// <summary>
    /// Les options façon getopt (« -t ed25519 », « -tED25519 », « -lvf fichier ») et les autres mots ; avec
    /// <paramref name="stopAtWord"/>, tout ce qui suit le premier mot est un mot (la commande de ssh).
    /// </summary>
    private static bool Options(List<string> args, string withValue, string flags, out Dictionary<char, string> options, out char bad, out List<string> words, bool stopAtWord = false)
    {
        options = [];
        words = [];
        bad = ' ';
        for (var i = 1; i < args.Count; i++)
        {
            var arg = args[i];
            if (arg.Length < 2 || arg[0] != '-' || (stopAtWord && words.Count > 0))
            {
                words.Add(arg);
                continue;
            }
            for (var j = 1; j < arg.Length; j++)
            {
                var c = arg[j];
                if (withValue.Contains(c))
                {
                    var value = j + 1 < arg.Length ? arg[(j + 1)..] : i + 1 < args.Count ? args[++i] : null;
                    if (value is null) { bad = c; return false; }
                    options[c] = value;
                    break;
                }
                if (!flags.Contains(c)) { bad = c; return false; }
                options[c] = "";
            }
        }
        return true;
    }
}
