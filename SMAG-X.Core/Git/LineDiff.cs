using System.Text;

namespace SMAGX.Git;

public enum EditKind { Same, Delete, Insert }

/// <summary>Une opération du script d'édition : ligne gardée, supprimée (de A) ou ajoutée (de B).</summary>
public readonly record struct Edit(EditKind Kind, int A, int B);

/// <summary>Le texte d'un fichier découpé en lignes, avec l'indication « pas de fin de ligne à la fin ».</summary>
public sealed record TextLines(IReadOnlyList<string> Lines, bool MissingFinalNewline)
{
    public static TextLines Of(byte[] data) => Of(Encoding.UTF8.GetString(data));

    public static TextLines Of(string text)
    {
        if (text.Length == 0) return new TextLines([], false);
        var lines = text.Split('\n').ToList();
        var missing = lines[^1].Length > 0;
        if (!missing) lines.RemoveAt(lines.Count - 1);
        return new TextLines(lines, missing);
    }

    /// <summary>Les lignes comparées par Git : la dernière ligne sans fin de ligne diffère de la même avec.</summary>
    public IReadOnlyList<string> Keys =>
        MissingFinalNewline ? [.. Lines.Take(Lines.Count - 1), Lines[^1] + "\\ No newline"] : Lines;
}

/// <summary>Différence ligne à ligne, algorithme de Myers (celui de Git par défaut) : le plus court script d'édition.</summary>
public static class LineDiff
{
    public static IReadOnlyList<Edit> Compute(IReadOnlyList<string> a, IReadOnlyList<string> b)
    {
        int n = a.Count, m = b.Count, max = n + m;
        var v = new int[2 * max + 2];
        var trace = new List<int[]>();
        for (var d = 0; d <= max; d++)
        {
            trace.Add((int[])v.Clone());
            for (var k = -d; k <= d; k += 2)
            {
                int x;
                if (k == -d || (k != d && v[max + k - 1] < v[max + k + 1])) x = v[max + k + 1];
                else x = v[max + k - 1] + 1;
                var y = x - k;
                while (x < n && y < m && a[x] == b[y]) { x++; y++; }
                v[max + k] = x;
                if (x >= n && y >= m) return Backtrack(trace, a, b, max, d);
            }
        }
        return [];
    }

    private static IReadOnlyList<Edit> Backtrack(List<int[]> trace, IReadOnlyList<string> a, IReadOnlyList<string> b, int max, int depth)
    {
        var edits = new List<Edit>();
        int x = a.Count, y = b.Count;
        for (var d = depth; d >= 0; d--)
        {
            var v = trace[d];
            var k = x - y;
            int prevK;
            if (d == 0) prevK = 0;
            else if (k == -d || (k != d && v[max + k - 1] < v[max + k + 1])) prevK = k + 1;
            else prevK = k - 1;
            var prevX = d == 0 ? 0 : v[max + prevK];
            var prevY = prevX - prevK;
            while (x > prevX && y > prevY)
            {
                x--; y--;
                edits.Add(new Edit(EditKind.Same, x, y));
            }
            if (d > 0)
            {
                if (x == prevX) edits.Add(new Edit(EditKind.Insert, x, y - 1));
                else edits.Add(new Edit(EditKind.Delete, x - 1, y));
            }
            x = prevX;
            y = prevY;
        }
        edits.Reverse();
        return edits;
    }

    /// <summary>Lignes ajoutées et supprimées entre deux contenus (pour « 1 insertion(+), 2 deletions(-) »).</summary>
    public static (int Insertions, int Deletions) Count(byte[]? before, byte[]? after)
    {
        var a = before is null ? [] : TextLines.Of(before).Keys;
        var b = after is null ? [] : TextLines.Of(after).Keys;
        var edits = Compute(a, b);
        return (edits.Count(e => e.Kind == EditKind.Insert), edits.Count(e => e.Kind == EditKind.Delete));
    }
}
