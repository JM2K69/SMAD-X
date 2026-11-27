using SMADX.Models;
using SMADX.Services.Labs;
using SMAGX.Forge;
using SMAGX.Git;
using SMAGX.Machine;
using SMAGX.Ssh;

namespace SMAGX.Labs;

/// <summary>Un objectif vérifié : atteint, manqué, ou null (son poste est sur l'autre SMAG-X, ou la forge ne répond pas).</summary>
public sealed record SmagxObjectiveResult(SmagxObjective Objective, bool? Passed, string Feedback);

public sealed record SmagxLabResult(IReadOnlyList<SmagxObjectiveResult> Objectives, IReadOnlyList<LabQuestionResult> Questions, double Score, int MaxPoints)
{
    /// <summary>Tous les objectifs notés sont atteints (ceux vérifiés sur l'autre SMAG-X ne comptent pas ici).</summary>
    public bool Completed => Objectives.Where(o => !o.Objective.Guard && o.Passed is not null).All(o => o.Passed == true);
}

/// <summary>
/// Le correcteur des scénarios : chaque objectif est vérifié sur l'état réel des postes (dépôts, configuration, clés,
/// known_hosts) et de la forge (projets, branches, auteurs des commits). Il ne regarde pas les commandes tapées :
/// toute façon d'arriver au résultat est bonne.
/// </summary>
public static class SmagxLabGrader
{
    /// <param name="stations">Les postes présents sur ce SMAG-X, par rôle.</param>
    /// <param name="forges">Les forges que joignent ces postes (celles de ce SMAG-X, ou celles du camarade).</param>
    public static SmagxLabResult Grade(SmagxLab lab, IReadOnlyDictionary<string, Workstation> stations, IReadOnlyList<IForge> forges,
        IReadOnlyDictionary<string, LabAnswer>? answers, string culture)
    {
        var context = new Context(lab, stations, forges, culture);
        var results = lab.Objectives.Select(o => Evaluate(context, o)).ToList();

        // Une garde violée annule les objectifs du même rôle (ou tous, pour une garde sur la forge)
        foreach (var guard in results.Where(r => r.Objective.Guard && r.Passed == false).ToList())
        {
            for (var i = 0; i < results.Count; i++)
            {
                var r = results[i];
                if (r.Objective.Guard || r.Passed != true) continue;
                if (guard.Objective.Check.Role is { } role && r.Objective.Check.Role is { } other && role != other) continue;
                results[i] = r with
                {
                    Passed = false,
                    Feedback = T(culture, $"Annulé : « {guard.Objective.Title.Resolve(culture)} » n'est plus respecté.",
                        $"Cancelled: \"{guard.Objective.Title.Resolve(culture)}\" is no longer respected.")
                };
            }
        }

        var questions = lab.Questions.Select(q => LabQuestionGrader.Grade(q, answers?.GetValueOrDefault(q.Id))).ToList();
        var score = results.Where(r => !r.Objective.Guard && r.Passed == true).Sum(r => r.Objective.Points)
                    + questions.Sum(q => (q.Fraction ?? 0) * q.Question.Points);
        return new SmagxLabResult(results, questions, score, lab.MaxPoints);
    }

    private static string T(string culture, string fr, string en) => culture.StartsWith("fr") ? fr : en;

    private sealed class Context(SmagxLab lab, IReadOnlyDictionary<string, Workstation> stations, IReadOnlyList<IForge> forges, string culture)
    {
        public SmagxLab Lab { get; } = lab;
        public IReadOnlyDictionary<string, Workstation> Stations { get; } = stations;
        public IReadOnlyList<IForge> Forges { get; } = forges;
        public string Culture { get; } = culture;

        // Une copie de chaque projet de la forge par correction : la forge peut être chez un camarade
        private readonly Dictionary<string, Repository?> _projects = [];

        public string T(string fr, string en) => SmagxLabGrader.T(Culture, fr, en);

        public IForge? Forge(string? host) => Forges.FirstOrDefault(f => f.Host == (host ?? "gitlab.example"));

        public Repository? Project(string? host, string project)
        {
            var key = (host ?? "gitlab.example") + ":" + project;
            if (!_projects.TryGetValue(key, out var repo)) _projects[key] = repo = Forge(host)?.Open(project);
            return repo;
        }
    }

    private static SmagxObjectiveResult Evaluate(Context c, SmagxObjective objective)
    {
        var check = objective.Check;
        if (check.Role is { } role && !c.Stations.ContainsKey(role))
            return new SmagxObjectiveResult(objective, null, c.T("Vérifié sur le SMAG-X de votre camarade.", "Checked on your classmate's SMAG-X."));
        try
        {
            var (passed, why) = Run(c, check);
            if (check.Not) passed = !passed;
            return new SmagxObjectiveResult(objective, passed, passed ? "" : why);
        }
        catch (ForgeUnavailableException)
        {
            return new SmagxObjectiveResult(objective, null, c.T("La forge ne répond pas : impossible de vérifier.", "The forge does not answer: cannot check."));
        }
    }

    // La vérification et, si elle échoue, ce qui manque (le sens de « Not » est appliqué ensuite)
    private static (bool Passed, string Why) Run(Context c, SmagxCheck check)
    {
        var ws = check.Role is { } r ? c.Stations[r] : null;
        var repoPath = check.Repo ?? "projet";
        var repo = ws?.Repositories.GetValueOrDefault(ws.Home + "/" + repoPath);
        var noRepo = c.T($"Pas de dépôt dans ~/{repoPath}.", $"No repository in ~/{repoPath}.");
        var branchName = check.Branch;
        ObjectId? Tip(Repository rep) => branchName is null ? rep.Head : rep.Refs.TryGetValue("refs/heads/" + branchName, out var id) ? id : null;
        string Where() => branchName is null ? "HEAD" : branchName;

        switch (check.Type)
        {
            case SmagxCheckType.Config:
            {
                var key = check.Key ?? "user.name";
                var value = ws!.Config(repo, key);
                var expected = Expand(check.Value, ws);
                if (string.IsNullOrEmpty(value)) return (false, c.T($"{key} n'est pas encore réglé.", $"{key} is not set yet."));
                if (!string.IsNullOrEmpty(expected) && value != expected)
                    return (false, c.T($"{key} n'a pas la valeur attendue.", $"{key} does not have the expected value."));
                return (true, "");
            }
            case SmagxCheckType.Repository:
                return (repo is not null, noRepo);
            case SmagxCheckType.CommitCount:
            {
                if (repo is null) return (false, noRepo);
                var count = Tip(repo) is { } tip ? repo.Ancestors([tip]).Count : 0;
                return (count >= (check.Count ?? 1), c.T($"{Where()} compte {count} commit(s).", $"{Where()} has {count} commit(s)."));
            }
            case SmagxCheckType.FileInCommit:
                return repo is null ? (false, noRepo) : FileIn(c, repo, Tip(repo), check, Where());
            case SmagxCheckType.WorkFile:
            {
                var root = repo?.Root ?? ws!.Home + "/" + repoPath;
                var path = Workstation.Combine(root, check.File ?? "");
                if (!ws!.FileExists(path)) return (false, c.T($"{check.File} n'existe pas.", $"{check.File} does not exist."));
                return check.Text is null || ws.ReadText(path).Contains(check.Text)
                    ? (true, c.T($"{check.File} contient encore « {check.Text} ».", $"{check.File} still contains \"{check.Text}\"."))
                    : (false, c.T($"{check.File} ne contient pas « {check.Text} ».", $"{check.File} does not contain \"{check.Text}\"."));
            }
            case SmagxCheckType.Clean:
            {
                if (repo is null) return (false, noRepo);
                if (repo.MergeHead is not null) return (false, c.T("Une fusion est encore en cours.", "A merge is still in progress."));
                var status = Status.Compute(ws!, repo);
                return (repo.Head is not null && status.WorkTreeClean,
                    c.T("Des modifications attendent encore.", "Changes are still waiting."));
            }
            case SmagxCheckType.Ignored:
            {
                if (repo is null) return (false, noRepo);
                var file = check.File ?? "";
                if (!ws!.FileExists(Workstation.Combine(repo.Root, file))) return (false, c.T($"{file} n'est plus dans le répertoire de travail.", $"{file} is no longer in the working directory."));
                if (repo.Index.Contains(file)) return (false, c.T($"{file} est encore suivi par Git.", $"{file} is still tracked by Git."));
                return (Ignore.Load(Status.WorkTree(ws, repo)).IsIgnored(file), c.T($"{file} n'est pas ignoré par Git.", $"{file} is not ignored by Git."));
            }
            case SmagxCheckType.BranchExists:
                if (repo is null) return (false, noRepo);
                return (repo.Refs.ContainsKey("refs/heads/" + branchName),
                    check.Not ? c.T($"La branche {branchName} existe encore.", $"Branch {branchName} still exists.") : c.T($"Pas de branche {branchName}.", $"No branch {branchName}."));
            case SmagxCheckType.OnBranch:
                if (repo is null) return (false, noRepo);
                return (repo.CurrentBranch == branchName, c.T($"HEAD n'est pas sur {branchName}.", $"HEAD is not on {branchName}."));
            case SmagxCheckType.MergeCommit:
                if (repo is null) return (false, noRepo);
                return (Tip(repo) is { } t && HasMerge(repo, t), c.T($"Pas de commit de fusion dans {Where()}.", $"No merge commit in {Where()}."));
            case SmagxCheckType.CommitMessage:
            {
                if (repo is null) return (false, noRepo);
                if (Tip(repo) is not { } tip) return (false, c.T($"{Where()} n'a pas de commit.", $"{Where()} has no commit."));
                var subject = repo.GetCommit(tip).Subject;
                return (subject == check.Text, c.T($"{Where()} n'est pas sur le commit attendu.", $"{Where()} is not on the expected commit."));
            }
            case SmagxCheckType.HistoryContains:
            {
                if (repo is null) return (false, noRepo);
                var found = Tip(repo) is { } tip && repo.Ancestors([tip]).Any(id => repo.GetCommit(id).Subject == check.Text);
                return (found, check.Not
                    ? c.T($"Le commit « {check.Text} » est encore dans l'historique de {Where()}.", $"Commit \"{check.Text}\" is still in the history of {Where()}.")
                    : c.T($"Le commit « {check.Text} » n'est pas dans l'historique de {Where()}.", $"Commit \"{check.Text}\" is not in the history of {Where()}."));
            }
            case SmagxCheckType.Remote:
            {
                if (repo is null) return (false, noRepo);
                var name = check.Remote ?? "origin";
                if (!repo.Config.TryGetValue($"remote.{name}.url", out var url)) return (false, c.T($"Pas de dépôt distant « {name} ».", $"No remote \"{name}\"."));
                var parsed = RemoteUrl.Parse(url);
                return (parsed is not null && parsed.Host == (check.Host ?? "gitlab.example") && parsed.Project == check.Project,
                    c.T($"« {name} » ne désigne pas le bon projet.", $"\"{name}\" does not point to the right project."));
            }
            case SmagxCheckType.Tracks:
                if (repo is null) return (false, noRepo);
                return (GitCli.Upstream(repo, branchName ?? repo.CurrentBranch ?? "main") is not null,
                    c.T($"{Where()} ne suit aucune branche distante.", $"{Where()} does not track any remote branch."));
            case SmagxCheckType.KeyOnForge:
                return (c.Forge(check.Host)?.Keys(ws!.User).Count > 0,
                    c.T($"Aucune clé SSH sur le compte {ws!.User} de {check.Host ?? "gitlab.example"}.", $"No SSH key on account {ws!.User} of {check.Host ?? "gitlab.example"}."));
            case SmagxCheckType.KnownHost:
            {
                var known = ws!.Home + "/.ssh/known_hosts";
                var host = check.Host ?? "gitlab.example";
                return (ws.FileExists(known) && KnownHosts.Find(ws.ReadText(known), host) is not null,
                    c.T($"La clé du serveur {host} n'a pas encore été acceptée.", $"The key of server {host} has not been accepted yet."));
            }
            case SmagxCheckType.ForgeBranch:
            {
                var project = c.Project(check.Host, check.Project ?? "");
                if (project is null) return (false, c.T($"Pas de projet {check.Project} sur la forge.", $"No project {check.Project} on the forge."));
                return (project.Refs.ContainsKey("refs/heads/" + branchName), c.T($"Pas de branche {branchName} sur la forge.", $"No branch {branchName} on the forge."));
            }
            case SmagxCheckType.ForgeFile:
            {
                var project = c.Project(check.Host, check.Project ?? "");
                if (project is null) return (false, c.T($"Pas de projet {check.Project} sur la forge.", $"No project {check.Project} on the forge."));
                return FileIn(c, project, Tip(project), check, c.T($"{Where()} (forge)", $"{Where()} (forge)"));
            }
            case SmagxCheckType.ForgeAuthor:
            {
                var project = c.Project(check.Host, check.Project ?? "");
                if (project is null || Tip(project) is not { } tip) return (false, c.T($"Pas de branche {Where()} sur la forge.", $"No branch {Where()} on the forge."));
                var names = AuthorNames(c, check.Role);
                var found = project.Ancestors([tip]).Any(id => project.GetCommit(id).Author is var a && (names.Contains(a.Name) || names.Contains(a.Email)));
                return (found, c.T($"Aucun commit de {check.Role} dans {Where()} sur la forge.", $"No commit by {check.Role} in {Where()} on the forge."));
            }
            case SmagxCheckType.ForgeMergeCommit:
            {
                var project = c.Project(check.Host, check.Project ?? "");
                return (project is not null && Tip(project) is { } tip && HasMerge(project, tip),
                    c.T($"Pas de commit de fusion dans {Where()} sur la forge.", $"No merge commit in {Where()} on the forge."));
            }
            case SmagxCheckType.Synced:
            {
                if (repo is null) return (false, noRepo);
                var project = c.Project(check.Host, check.Project ?? "");
                var local = Tip(repo);
                var remote = project is not null && project.Refs.TryGetValue("refs/heads/" + (branchName ?? repo.CurrentBranch ?? "main"), out var r2) ? r2 : (ObjectId?)null;
                return (local is not null && Equals(local, remote),
                    c.T($"{Where()} du poste et celle de la forge ne pointent pas sur le même commit.", $"{Where()} on the workstation and on the forge do not point to the same commit."));
            }
            default:
                return (false, "");
        }
    }

    private static (bool, string) FileIn(Context c, Repository repo, ObjectId? tip, SmagxCheck check, string where)
    {
        var file = check.File ?? "";
        if (tip is null) return (false, c.T($"{where} n'a pas de commit.", $"{where} has no commit."));
        if (!repo.Files(tip).TryGetValue(file, out var entry))
            return (false, c.T($"{file} n'est pas dans le dernier commit de {where}.", $"{file} is not in the last commit of {where}."));
        if (check.Text is null)
            return (true, c.T($"{file} est dans le dernier commit de {where}.", $"{file} is in the last commit of {where}."));
        var text = repo.Objects.Get<Blob>(entry.Id).Text;
        return text.Contains(check.Text)
            ? (true, c.T($"Le {file} validé dans {where} contient « {check.Text} ».", $"The {file} committed in {where} contains \"{check.Text}\"."))
            : (false, c.T($"Le {file} validé dans {where} n'a pas le contenu attendu.", $"The {file} committed in {where} does not have the expected content."));
    }

    private static bool HasMerge(Repository repo, ObjectId tip) => repo.Ancestors([tip]).Any(id => repo.GetCommit(id).Parents.Count > 1);

    // Les noms et adresses sous lesquels un rôle signe ses commits
    private static HashSet<string> AuthorNames(Context c, string? role)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        if (role is null) return names;
        if (c.Lab.Roles.FirstOrDefault(r => r.Id == role) is { } definition)
            foreach (var n in new[] { definition.Name, definition.Email }) if (!string.IsNullOrEmpty(n)) names.Add(n);
        if (c.Stations.TryGetValue(role, out var ws))
            foreach (var n in new[] { ws.Config(null, "user.name"), ws.Config(null, "user.email"), ws.FullName, ws.Email })
                if (!string.IsNullOrEmpty(n)) names.Add(n);
        return names;
    }

    /// <summary>« {name} », « {email} », « {login} » : l'identité du poste.</summary>
    public static string? Expand(string? text, Workstation ws) =>
        text?.Replace("{name}", ws.FullName ?? "").Replace("{email}", ws.Email ?? "").Replace("{login}", ws.User);
}
