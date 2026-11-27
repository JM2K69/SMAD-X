using System.Text;
using SMAGX.Fidelity;

namespace SMAGX.Tests;

/// <summary>
/// Chaque scénario de Fidelity/scenarios, rejoué sur le moteur de SMAG-X, donne exactement ce qu'a donné le vrai Git
/// (Fidelity/&lt;scénario&gt;.git.txt, produit par tools/SmagxGitCheck) : sorties, couleurs, codes de retour, identifiants.
/// </summary>
public class GitFidelityTests
{
    private static string Folder => Path.Combine(AppContext.BaseDirectory, "Fidelity");

    public static TheoryData<string> Scenarios()
    {
        var data = new TheoryData<string>();
        foreach (var file in Directory.GetFiles(Path.Combine(Folder, "scenarios"), "*.txt").Order())
            data.Add(Path.GetFileNameWithoutExtension(file));
        return data;
    }

    [Theory]
    [MemberData(nameof(Scenarios))]
    public void Engine_GivesWhatRealGitGave(string scenario)
    {
        var steps = Transcript.Parse(File.ReadAllText(Path.Combine(Folder, "scenarios", scenario + ".txt")));
        var expected = Transcript.Read(File.ReadAllText(Path.Combine(Folder, scenario + ".git.txt")));
        var actual = EngineReplay.Run(steps);

        Assert.Equal(expected.Count, actual.Count);
        var differences = new StringBuilder();
        for (var i = 0; i < expected.Count; i++)
        {
            var (e, a) = (expected[i], actual[i]);
            if (e == a) continue;
            differences.Append($"\n── {i + 1}. $ {e.Command}\n");
            if (e.ExitCode != a.ExitCode) differences.Append($"   code : attendu {e.ExitCode}, obtenu {a.ExitCode}\n");
            if (e.Out != a.Out) differences.Append($"   sortie attendue :\n{Show(e.Out)}   sortie obtenue :\n{Show(a.Out)}");
            if (e.Err != a.Err) differences.Append($"   erreur attendue :\n{Show(e.Err)}   erreur obtenue :\n{Show(a.Err)}");
        }
        Assert.True(differences.Length == 0, $"{scenario} diffère du vrai Git :{differences}");
    }

    private static string Show(string text) =>
        string.Concat(text.Split('\n').Select(l => "      |" + l.Replace("\u001b", "\\e").Replace("\t", "\\t") + "\n"));
}
