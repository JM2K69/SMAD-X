using SMAGX.Fidelity;
using SMAGX.Forge;
using SMAGX.Machine;
using SMAGX.Ssh;

namespace SMAGX.Labs;

/// <summary>
/// Le départ d'un scénario, joué avant que l'élève ne prenne la main : projets créés sur la forge, commits poussés par
/// le formateur (un poste caché), fichiers et dépôts préparés sur les postes des rôles. Sur deux SMAG-X, celui qui sert
/// la forge prépare la forge et son rôle ; celui qui la rejoint ne prépare que son rôle.
/// </summary>
public static class SmagxLabSetup
{
    public const string Trainer = "formateur";

    /// <param name="stations">Les postes des rôles présents sur ce SMAG-X.</param>
    /// <param name="forges">Les forges de ce SMAG-X, à préparer ; null s'il rejoint celles d'un camarade.</param>
    /// <returns>Les commandes du départ qui ont échoué (pour les tests des scénarios), avec leur sortie.</returns>
    public static List<string> Apply(SmagxLab lab, IReadOnlyDictionary<string, Workstation> stations,
        IReadOnlyList<SMAGX.Forge.Forge>? forges, DateTimeOffset start)
    {
        var failures = new List<string>();
        var shells = new Dictionary<Workstation, Shell>();
        Workstation? trainer = null;
        Workstation? current = stations.GetValueOrDefault(lab.Roles.FirstOrDefault()?.Id ?? "");
        var minute = 0;

        Workstation Trainer()
        {
            if (trainer is not null) return trainer;
            trainer = new Workstation(SmagxLabSetup.Trainer, "poste") { Interactive = false, Ask = (_, _) => "yes", Random = new Random(1) };
            trainer.Network.AddRange(forges!);
            Run(trainer, "ssh-keygen -q -N \"\" -f /home/formateur/.ssh/id_ed25519");
            RegisterKey(trainer);
            return trainer;
        }

        void Run(Workstation ws, string command)
        {
            if (!shells.TryGetValue(ws, out var shell)) shells[ws] = shell = new Shell(ws);
            var date = start.AddMinutes(minute++);
            var (clock, ask, interactive) = (ws.Clock, ws.Ask, ws.Interactive);
            ws.Clock = () => date;
            ws.Ask = (_, _) => "yes";
            ws.Interactive = false;
            try
            {
                var result = shell.Run(SmagxLabGrader.Expand(command, ws)!);
                if (result.ExitCode != 0) failures.Add($"{ws.User}$ {command}\n{result.Display}");
            }
            finally
            {
                (ws.Clock, ws.Ask, ws.Interactive) = (clock, ask, interactive);
            }
        }

        foreach (var step in Transcript.Parse(lab.Setup))
        {
            switch (step.Kind)
            {
                case "@":
                    current = step.Argument == SmagxLabSetup.Trainer
                        ? forges is null ? null : Trainer()
                        : stations.GetValueOrDefault(step.Argument);
                    continue;
                case "forge":
                {
                    if (forges is null) continue;
                    var colon = step.Argument.IndexOf(':');
                    var host = colon > 0 ? step.Argument[..colon] : "gitlab.example";
                    var project = colon > 0 ? step.Argument[(colon + 1)..] : step.Argument;
                    forges.FirstOrDefault(f => f.Host == host)?.Create(project);
                    continue;
                }
            }
            if (current is not { } ws) continue;
            switch (step.Kind)
            {
                case "$":
                    Run(ws, step.Argument);
                    break;
                case "write":
                    ws.WriteText(ws.Resolve(step.Argument), SmagxLabGrader.Expand(step.Content, ws) ?? "");
                    break;
                case "append":
                    var path = ws.Resolve(step.Argument);
                    ws.WriteText(path, (ws.FileExists(path) ? ws.ReadText(path) : "") + SmagxLabGrader.Expand(step.Content, ws));
                    break;
                case "delete":
                    ws.DeleteFile(ws.Resolve(step.Argument));
                    break;
                case "mkdir":
                    ws.EnsureDirectory(ws.Resolve(step.Argument));
                    break;
                case "key":
                    if (!RegisterKey(ws)) failures.Add($"{ws.User}: key");
                    break;
                case "trust":
                    Trust(ws, step.Argument);
                    break;
            }
        }
        return failures;
    }

    // La clé publique du poste (~/.ssh/id_ed25519.pub) enregistrée sur les forges qu'il joint, à son nom
    private static bool RegisterKey(Workstation ws)
    {
        var pub = ws.Home + "/.ssh/id_ed25519.pub";
        if (!ws.FileExists(pub)) return false;
        foreach (var forge in ws.Network) forge.AddKey(ws.User, ws.User + "@poste", ws.ReadText(pub).Trim());
        return true;
    }

    // Le serveur déjà accepté : sa clé dans ~/.ssh/known_hosts, comme après un premier « yes »
    private static void Trust(Workstation ws, string host)
    {
        var path = ws.Home + "/.ssh/known_hosts";
        ws.EnsureDirectory(ws.Home + "/.ssh");
        var text = ws.FileExists(path) ? ws.ReadText(path) : "";
        if (KnownHosts.Find(text, host) is null) ws.WriteText(path, text + KnownHosts.Line(host, SshPublicKey.HostKey(host), ws.Random));
    }
}
