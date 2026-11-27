namespace SMAGX.Git;

public enum LabelKind { Head, LocalBranch, RemoteBranch, Tag }

/// <summary>Une étiquette posée sur un commit : HEAD, une branche locale ou distante, une étiquette (tag).</summary>
public sealed record GraphLabel(string Text, LabelKind Kind, bool IsCurrent = false);

/// <summary>Un lien d'un commit vers un parent : de la colonne du commit, par la colonne réservée au parent, jusqu'à lui.</summary>
public sealed record GraphEdge(int FromRow, int FromLane, int ToRow, int ToLane, int ViaLane);

/// <summary>Une ligne du graphe : le commit, sa colonne et ses étiquettes.</summary>
public sealed record GraphRow(int Row, int Lane, ObjectId Id, Commit Commit, IReadOnlyList<GraphLabel> Labels, bool IsHead);

/// <summary>La disposition du graphe des commits : lignes du plus récent au plus ancien, une colonne par lignée.</summary>
public sealed class CommitGraph
{
    public required IReadOnlyList<GraphRow> Rows { get; init; }
    public required IReadOnlyList<GraphEdge> Edges { get; init; }
    public int LaneCount { get; init; }

    /// <summary>Le graphe de tous les commits accessibles depuis les références et HEAD (comme « git log --all »).</summary>
    public static CommitGraph Build(Repository repo)
    {
        var starts = repo.Refs.Where(r => !r.Key.StartsWith("refs/stash")).Select(r => r.Value).ToList();
        if (repo.Head is { } head) starts.Add(head);
        var order = Topological(repo, GitCli.Walk(repo, starts));

        var lanes = new List<ObjectId?>();
        var placed = new Dictionary<ObjectId, (int Row, int Lane)>();
        var pending = new List<(int Row, int Lane, ObjectId Parent, int Via)>();
        var rows = new List<GraphRow>();
        for (var row = 0; row < order.Count; row++)
        {
            var id = order[row];
            var lane = lanes.IndexOf(id);
            if (lane < 0)
            {
                lane = lanes.IndexOf(null);
                if (lane < 0) { lane = lanes.Count; lanes.Add(null); }
            }
            // Les autres colonnes qui attendaient ce commit (plusieurs enfants) se referment sur lui
            for (var l = 0; l < lanes.Count; l++)
                if (l != lane && lanes[l] is { } waiting && waiting.Equals(id)) lanes[l] = null;
            placed[id] = (row, lane);

            var commit = repo.GetCommit(id);
            lanes[lane] = null;
            for (var p = 0; p < commit.Parents.Count; p++)
            {
                var parent = commit.Parents[p];
                var via = lanes.IndexOf(parent);
                if (via < 0)
                {
                    if (p == 0) via = lane;
                    else
                    {
                        via = lanes.IndexOf(null);
                        if (via < 0) { via = lanes.Count; lanes.Add(null); }
                    }
                    lanes[via] = parent;
                }
                pending.Add((row, lane, parent, via));
            }
            rows.Add(new GraphRow(row, lane, id, commit, Labels(repo, id), repo.Head is { } h && h.Equals(id)));
        }
        var edges = pending.Where(p => placed.ContainsKey(p.Parent))
            .Select(p => new GraphEdge(p.Row, p.Lane, placed[p.Parent].Row, placed[p.Parent].Lane, p.Via)).ToList();
        var laneCount = Math.Max(1, rows.Select(r => r.Lane).Concat(edges.Select(e => e.ViaLane)).DefaultIfEmpty(0).Max() + 1);
        return new CommitGraph { Rows = rows, Edges = edges, LaneCount = laneCount };
    }

    /// <summary>L'ordre de « git log » (dates), corrigé pour qu'un parent vienne toujours après tous ses enfants.</summary>
    private static List<ObjectId> Topological(Repository repo, List<ObjectId> byDate)
    {
        var children = byDate.ToDictionary(id => id, _ => 0);
        foreach (var id in byDate)
            foreach (var parent in repo.GetCommit(id).Parents)
                if (children.ContainsKey(parent)) children[parent]++;
        var result = new List<ObjectId>();
        var ready = byDate.Where(id => children[id] == 0).ToList();
        while (ready.Count > 0)
        {
            // Le plus récent des commits prêts, comme le fait la date
            var next = ready.OrderBy(byDate.IndexOf).First();
            ready.Remove(next);
            result.Add(next);
            foreach (var parent in repo.GetCommit(next).Parents)
                if (children.ContainsKey(parent) && --children[parent] == 0) ready.Add(parent);
        }
        return result;
    }

    /// <summary>Les étiquettes d'un commit : HEAD (et sa branche), les branches locales, distantes, puis les étiquettes.</summary>
    public static IReadOnlyList<GraphLabel> Labels(Repository repo, ObjectId id)
    {
        var labels = new List<GraphLabel>();
        if (repo.HeadRef is null && repo.DetachedHead is { } detached && detached.Equals(id))
            labels.Add(new GraphLabel("HEAD", LabelKind.Head));
        foreach (var (name, target) in repo.Refs)
        {
            if (!target.Equals(id)) continue;
            if (name.StartsWith("refs/heads/"))
            {
                var current = name == repo.HeadRef;
                if (current) labels.Insert(0, new GraphLabel("HEAD", LabelKind.Head));
                labels.Add(new GraphLabel(GitCli.Short(name), LabelKind.LocalBranch, current));
            }
            else if (name.StartsWith("refs/remotes/")) labels.Add(new GraphLabel(GitCli.Short(name), LabelKind.RemoteBranch));
            else if (name.StartsWith("refs/tags/")) labels.Add(new GraphLabel(GitCli.Short(name), LabelKind.Tag));
        }
        return labels;
    }
}
