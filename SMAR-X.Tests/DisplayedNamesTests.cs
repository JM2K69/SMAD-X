using System.Reflection;

namespace SMARX.Tests;

/// <summary>Les titres de SMAR-X n'ont pas de tiret cadratin : « SMAR-X : qui peut faire quoi ? ».</summary>
public class DisplayedNamesTests
{
    [Fact]
    public void Titles_HaveNoEmDash()
    {
        var texts = (Dictionary<string, (string Fr, string En)>)typeof(SmarxText)
            .GetField("Texts", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        var titles = texts.Where(t => t.Key.EndsWith("Title")).ToList();

        Assert.NotEmpty(titles);
        Assert.DoesNotContain(titles, t => t.Value.Fr.Contains('—') || t.Value.En.Contains('—'));
    }
}
