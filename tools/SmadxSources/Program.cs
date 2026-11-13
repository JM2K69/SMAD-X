// Les sources de SMAD-X : relevées sur le web, rédigées en Markdown en français et en anglais, réunies en PDF.
//
//   dotnet run --project tools/SmadxSources -- update [<racine du dépôt>]
//       relit chaque source citée par SMAD-X (ANSSI, NIST, CIS, MITRE ATT&CK, Microsoft, modèles de cloisonnement),
//       compare au relevé précédent (sources/sources.json), réécrit sources/sources.json et les fiches
//       sources/{référentiel}/{fr,en}/{identifiant}.md ; s'arrête sans rien écrire si un élément cité a disparu de sa source
//   dotnet run --project tools/SmadxSources -- write [<racine du dépôt>]
//       réécrit les fiches depuis sources/sources.json, sans rien relire sur le web (après un changement de règle)
//   dotnet run --project tools/SmadxSources -- pdf [<racine du dépôt>] [--only <identifiant ou référentiel>] [--out <dossier>]
//       un PDF par fiche et par langue aux couleurs de SMAD-X, à côté de la fiche (ou sous <dossier>/{référentiel}/{langue})
using SMADX.Services.Sources;

var command = args.FirstOrDefault() ?? "";
var rest = args.Skip(1).ToList();
var output = Option("--out");
var only = Option("--only");
var repo = Path.GetFullPath(rest.FirstOrDefault() ?? FindRepo());
var snapshotPath = Path.Combine(repo, "sources", "sources.json");

switch (command)
{
    case "update":
    {
        var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var (snapshot, errors) = await SourceUpdate.RunAsync(today, progress: id => Console.WriteLine(id + "…"));
        if (snapshot == null)
        {
            Console.Error.WriteLine("Rien n'est écrit : des éléments cités par SMAD-X manquent dans leur source.");
            foreach (var e in errors) Console.Error.WriteLine("  " + e);
            return 1;
        }
        var changes = SourceUpdate.Diff(SourcesSnapshot.Load(snapshotPath), snapshot);
        Directory.CreateDirectory(Path.GetDirectoryName(snapshotPath)!);
        snapshot.Save(snapshotPath);
        SourceSheets.WriteAll(repo, snapshot, SourceCitations.RuleTitles());
        Console.WriteLine($"{snapshot.Items.Count} éléments relevés dans {snapshot.Frameworks.Count} référentiels.");
        Console.WriteLine(changes.Count == 0 ? "Aucun changement depuis le relevé précédent." : "Changements :");
        foreach (var c in changes) Console.WriteLine("  " + c);
        return 0;
    }
    case "write":
    {
        var snapshot = SourcesSnapshot.Load(snapshotPath) ?? throw new FileNotFoundException("Pas de relevé : lancer update d'abord.", snapshotPath);
        SourceSheets.WriteAll(repo, snapshot, SourceCitations.RuleTitles());
        Console.WriteLine("Fiches réécrites depuis " + snapshotPath);
        return 0;
    }
    case "pdf":
    {
        var snapshot = SourcesSnapshot.Load(snapshotPath) ?? throw new FileNotFoundException("Pas de relevé : lancer update d'abord.", snapshotPath);
        var rules = SourceCitations.RuleTitles();
        var version = SourcePdf.Version(repo);
        var root = output == null ? null : Path.GetFullPath(output);
        var count = 0;
        var busy = new List<string>();
        foreach (var f in snapshot.Frameworks)
            foreach (var item in snapshot.Items.Where(i => i.Framework == f.Id))
            {
                var name = SourceSheets.FileName(item);
                if (only != null && !only.Equals(f.Id, StringComparison.OrdinalIgnoreCase) && !only.Equals(name, StringComparison.OrdinalIgnoreCase)
                    && !only.Equals(item.Id, StringComparison.OrdinalIgnoreCase)) continue;
                foreach (var lang in SourceSheets.Languages)
                {
                    var dir = root == null ? SourceSheets.Folder(repo, f.Id, lang) : Path.Combine(root, f.Id, lang);
                    var file = Path.Combine(dir, name + ".pdf");
                    try
                    {
                        SourcePdf.Save(file, version, f, item, lang, rules);
                        count++;
                    }
                    catch (IOException)
                    {
                        // Un PDF ouvert dans une visionneuse ne peut pas être remplacé : on le garde et on continue
                        busy.Add(file);
                    }
                }
            }
        if (busy.Count > 0)
        {
            Console.Error.WriteLine($"{busy.Count} PDF ouverts ailleurs, laissés tels quels : fermez-les et relancez.");
            foreach (var file in busy) Console.Error.WriteLine("  " + file);
        }
        if (count == 0 && busy.Count == 0)
        {
            Console.Error.WriteLine($"Aucune fiche ne correspond à « {only} » : un identifiant (vuln_spn_priv) ou un référentiel (anssi).");
            return 1;
        }
        Console.WriteLine($"{count} PDF écrits, à côté des fiches" + (root == null ? "." : $", sous {root}."));
        return busy.Count == 0 ? 0 : 1;
    }
    default:
        Console.Error.WriteLine("usage : update | write | pdf [<racine du dépôt>] [--only <identifiant ou référentiel>] [--out <dossier>]");
        return 2;
}

// La valeur d'une option (--out <dossier>), retirée des arguments
string? Option(string name)
{
    var index = rest.IndexOf(name);
    if (index < 0) return null;
    var value = rest.ElementAtOrDefault(index + 1);
    rest.RemoveRange(index, Math.Min(2, rest.Count - index));
    return value;
}

// La racine du dépôt (dossier de SMAD-X.slnx), depuis le dossier courant
static string FindRepo()
{
    for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir != null; dir = dir.Parent)
        if (File.Exists(Path.Combine(dir.FullName, "SMAD-X.slnx"))) return dir.FullName;
    throw new DirectoryNotFoundException("SMAD-X.slnx introuvable : donner la racine du dépôt.");
}
