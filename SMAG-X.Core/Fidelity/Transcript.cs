using System.Text;

namespace SMAGX.Fidelity;

/// <summary>Une étape d'un scénario de fidélité : une commande tapée, ou un fichier écrit, complété ou supprimé.</summary>
public sealed record ScenarioStep(string Kind, string Argument, string? Content = null)
{
    public bool IsCommand => Kind == "$";
}

/// <summary>Ce qu'une commande a produit : sorties standard et d'erreur, code de retour.</summary>
public sealed record StepResult(string Command, string Out, string Err, int ExitCode);

/// <summary>
/// Les scénarios rejoués à l'identique sur le vrai Git (tools/SmagxGitCheck) et sur le moteur de SMAG-X (tests de fidélité).
/// Format d'un scénario : « $ git status » (commande), « write a.txt Bonjour\n », « append a.txt Salut\n », « delete a.txt »,
/// « # commentaire ». Chaque commande a sa date : 27 novembre 2026, 10 h, plus une minute par commande.
/// </summary>
public static class Transcript
{
    public static readonly DateTimeOffset Start = new(2026, 11, 27, 10, 0, 0, TimeSpan.FromHours(1));

    /// <summary>La date de la commande n° index (0, 1, 2…) du scénario.</summary>
    public static DateTimeOffset DateOf(int index) => Start.AddMinutes(index);

    public static List<ScenarioStep> Parse(string text)
    {
        var steps = new List<ScenarioStep>();
        foreach (var raw in text.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.TrimEnd();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            if (line.StartsWith("$ ")) { steps.Add(new ScenarioStep("$", line[2..])); continue; }
            // « @bob » : la suite se passe sur le poste de bob (son dossier ~/projet, sa configuration)
            if (line.StartsWith('@')) { steps.Add(new ScenarioStep("@", line[1..].Trim())); continue; }
            var space = line.IndexOf(' ');
            var kind = space < 0 ? line : line[..space];
            var rest = space < 0 ? "" : line[(space + 1)..];
            switch (kind)
            {
                case "write" or "append":
                    var cut = rest.IndexOf(' ');
                    steps.Add(new ScenarioStep(kind, cut < 0 ? rest : rest[..cut], cut < 0 ? "" : Unescape(rest[(cut + 1)..])));
                    break;
                // Départ des scénarios : « key » (la clé du poste enregistrée sur les forges), « trust hôte » (serveur accepté)
                case "delete" or "mkdir" or "key" or "trust":
                    steps.Add(new ScenarioStep(kind, rest));
                    break;
                // « forge classe/projet » : un projet vide sur la forge (branche par défaut main), joint par
                // git@gitlab.example:classe/projet.git
                case "forge":
                    steps.Add(new ScenarioStep(kind, rest));
                    break;
                default:
                    throw new FormatException($"Étape inconnue : {line}");
            }
        }
        return steps;
    }

    /// <summary>« \n », « \t », « \r », « \e » (couleurs) et « \\ » : contenu d'un fichier, ou ligne de résultat relue.</summary>
    public static string Unescape(string text)
    {
        var result = new StringBuilder();
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '\\' && i + 1 < text.Length)
            {
                var next = text[++i];
                result.Append(next switch { 'n' => '\n', 't' => '\t', 'r' => '\r', 'e' => '\u001b', _ => next });
            }
            else result.Append(text[i]);
        }
        return result.ToString();
    }

    /// <summary>Les résultats écrits pour le dépôt : « $ commande », « 1 ligne » (sortie), « 2 ligne » (erreur), « = code ».</summary>
    public static string Write(IEnumerable<StepResult> results)
    {
        var text = new StringBuilder();
        foreach (var r in results)
        {
            text.Append("$ ").Append(r.Command).Append('\n');
            Stream(text, '1', r.Out);
            Stream(text, '2', r.Err);
            text.Append("= ").Append(r.ExitCode).Append('\n');
        }
        return text.ToString();
    }

    private static void Stream(StringBuilder text, char tag, string output)
    {
        if (output.Length == 0) return;
        var lines = output.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var last = i == lines.Length - 1;
            if (last && lines[i].Length == 0) break;
            text.Append(tag).Append(last ? "% " : " ").Append(Escape(lines[i])).Append('\n');
        }
    }

    public static List<StepResult> Read(string text)
    {
        var results = new List<StepResult>();
        string? command = null;
        StringBuilder output = new(), error = new();
        foreach (var line in text.Replace("\r\n", "\n").Split('\n'))
        {
            if (line.StartsWith("$ ")) { command = line[2..]; output.Clear(); error.Clear(); }
            else if (line.StartsWith("= ") && command is not null)
                results.Add(new StepResult(command, output.ToString(), error.ToString(), int.Parse(line[2..])));
            else if (line.Length >= 2 && line[0] is '1' or '2')
            {
                var target = line[0] == '1' ? output : error;
                if (line[1] == '%') target.Append(Unescape(line.Length > 3 ? line[3..] : ""));
                else target.Append(Unescape(line.Length > 2 ? line[2..] : "")).Append('\n');
            }
        }
        return results;
    }

    private static string Escape(string line) =>
        line.Replace("\\", "\\\\").Replace("\u001b", "\\e").Replace("\t", "\\t").Replace("\r", "\\r");

}
