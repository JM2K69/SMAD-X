using System.Reflection;

namespace SMARX.Tests;

/// <summary>
/// Pas de tiret cadratin entre deux mots dans les textes de SMAR-X : « SMAR-X : qui peut faire quoi ? ».
/// Seul « — » tout seul reste, pour une valeur vide.
/// </summary>
public class DisplayedNamesTests
{
    [Fact]
    public void Texts_HaveNoDashBetweenWords()
    {
        var texts = (Dictionary<string, (string Fr, string En)>)typeof(SmarxText)
            .GetField("Texts", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;

        Assert.NotEmpty(texts);
        Assert.DoesNotContain(texts, t => t.Value.Fr.Contains(" — ") || t.Value.En.Contains(" — "));
    }
}
