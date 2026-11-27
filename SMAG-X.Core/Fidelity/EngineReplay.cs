using SMAGX.Machine;

namespace SMAGX.Fidelity;

/// <summary>
/// Rejoue un scénario sur le moteur de SMAG-X, aux mêmes dates que sur le vrai Git : sur le poste d'Alice, dans ~/projet,
/// ou sur celui d'un autre élève après « @nom » ; tous joignent la même forge (gitlab.example).
/// </summary>
public static class EngineReplay
{
    public const string Project = "/home/alice/projet";

    public static List<StepResult> Run(IEnumerable<ScenarioStep> steps)
    {
        var forge = new SMAGX.Forge.Forge("gitlab.example") { ServerMessages = false, Ssh = false };
        var stations = new Dictionary<string, (Workstation Ws, Shell Shell)>();
        (Workstation Ws, Shell Shell) Station(string user)
        {
            if (stations.TryGetValue(user, out var station)) return station;
            var ws = new Workstation(user, "poste") { Interactive = false };
            ws.Network.Add(forge);
            ws.EnsureDirectory($"/home/{user}/projet");
            ws.Cwd = $"/home/{user}/projet";
            return stations[user] = (ws, new Shell(ws));
        }
        var current = Station("alice");
        var results = new List<StepResult>();
        var index = 0;
        foreach (var step in steps)
        {
            var ws = current.Ws;
            switch (step.Kind)
            {
                case "@":
                    current = Station(step.Argument);
                    break;
                case "forge":
                    forge.Create(step.Argument);
                    break;
                case "$":
                    var date = Transcript.DateOf(index++);
                    ws.Clock = () => date;
                    var r = current.Shell.Run(step.Argument);
                    results.Add(new StepResult(step.Argument, r.Out, r.Err, r.ExitCode));
                    break;
                case "write":
                    ws.WriteText(ws.Resolve(step.Argument), step.Content ?? "");
                    break;
                case "append":
                    var path = ws.Resolve(step.Argument);
                    ws.WriteText(path, (ws.FileExists(path) ? ws.ReadText(path) : "") + step.Content);
                    break;
                case "delete":
                    ws.DeleteFile(ws.Resolve(step.Argument));
                    break;
                case "mkdir":
                    ws.EnsureDirectory(ws.Resolve(step.Argument));
                    break;
            }
        }
        return results;
    }
}
