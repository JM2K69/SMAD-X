using System.Text;

namespace SMAGX.Git;

/// <summary>Le résultat de la fusion d'un fichier : son contenu (avec marqueurs en cas de conflit) et s'il reste des conflits.</summary>
public sealed record MergedText(byte[] Content, bool Conflict);

/// <summary>
/// La fusion à trois voies d'un fichier, comme Git (diff3, niveau « zealous ») : les morceaux changés d'un seul côté sont
/// repris, ceux changés pareil des deux côtés aussi ; ailleurs, un conflit entre marqueurs, réduit aux lignes qui diffèrent.
/// </summary>
public static class ThreeWayMerge
{
    public static MergedText Merge(byte[]? @base, byte[] ours, byte[] theirs, string oursLabel, string theirsLabel)
    {
        var b = TextLines.Of(@base ?? []);
        var o = TextLines.Of(ours);
        var t = TextLines.Of(theirs);
        if (ours.AsSpan().SequenceEqual(theirs)) return new MergedText(ours, false);
        if (@base is not null && @base.AsSpan().SequenceEqual(ours)) return new MergedText(theirs, false);
        if (@base is not null && @base.AsSpan().SequenceEqual(theirs)) return new MergedText(ours, false);

        var bk = b.Keys;
        var ok = o.Keys;
        var tk = t.Keys;
        var toOurs = Matches(bk, ok);
        var toTheirs = Matches(bk, tk);

        var output = new List<string>();
        var conflict = false;
        int i = 0, j = 0, k = 0;
        while (true)
        {
            var stable = 0;
            while (i + stable < bk.Count && toOurs[i + stable] == j + stable && toTheirs[i + stable] == k + stable) stable++;
            if (stable > 0)
            {
                output.AddRange(bk.Skip(i).Take(stable));
                i += stable; j += stable; k += stable;
                continue;
            }
            var next = i;
            while (next < bk.Count && (toOurs[next] < 0 || toTheirs[next] < 0)) next++;
            int nj, nk;
            if (next >= bk.Count) { next = bk.Count; nj = ok.Count; nk = tk.Count; }
            else { nj = toOurs[next]; nk = toTheirs[next]; }
            var baseChunk = bk.Skip(i).Take(next - i).ToList();
            var oursChunk = ok.Skip(j).Take(nj - j).ToList();
            var theirsChunk = tk.Skip(k).Take(nk - k).ToList();
            if (baseChunk.Count == 0 && oursChunk.Count == 0 && theirsChunk.Count == 0) break;
            if (oursChunk.SequenceEqual(baseChunk)) output.AddRange(theirsChunk);
            else if (theirsChunk.SequenceEqual(baseChunk) || oursChunk.SequenceEqual(theirsChunk)) output.AddRange(oursChunk);
            else
            {
                conflict = true;
                var prefix = 0;
                while (prefix < oursChunk.Count && prefix < theirsChunk.Count && oursChunk[prefix] == theirsChunk[prefix]) prefix++;
                var suffix = 0;
                while (suffix < oursChunk.Count - prefix && suffix < theirsChunk.Count - prefix
                       && oursChunk[^(suffix + 1)] == theirsChunk[^(suffix + 1)]) suffix++;
                output.AddRange(oursChunk.Take(prefix));
                output.Add("<<<<<<< " + oursLabel);
                output.AddRange(oursChunk.Skip(prefix).Take(oursChunk.Count - prefix - suffix));
                output.Add("=======");
                output.AddRange(theirsChunk.Skip(prefix).Take(theirsChunk.Count - prefix - suffix));
                output.Add(">>>>>>> " + theirsLabel);
                output.AddRange(oursChunk.Skip(oursChunk.Count - suffix));
            }
            i = next; j = nj; k = nk;
            if (i >= bk.Count && j >= ok.Count && k >= tk.Count) break;
        }

        var text = new StringBuilder();
        foreach (var line in output)
            text.Append(line.EndsWith("\\ No newline") ? line[..^"\\ No newline".Length] + "\n" : line + "\n");
        return new MergedText(Encoding.UTF8.GetBytes(text.ToString()), conflict);
    }

    /// <summary>Pour chaque ligne de la base, la ligne qui lui correspond dans l'autre version (plus longue sous-suite commune), ou -1.</summary>
    private static int[] Matches(IReadOnlyList<string> @base, IReadOnlyList<string> other)
    {
        var map = Enumerable.Repeat(-1, @base.Count).ToArray();
        foreach (var edit in LineDiff.Compute(@base, other))
            if (edit.Kind == EditKind.Same) map[edit.A] = edit.B;
        return map;
    }
}
