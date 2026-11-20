using System.Reflection;
using SMADX.Services;
using SMADX.Studio;

namespace SMADX.Tests;

/// <summary>
/// Les noms affichés n'ont pas de tiret cadratin : « Débutant · titre » dans les menus, « nom : rôle » en tête
/// d'une fiche, « SMAD-X · exercice : … » dans le titre d'une fenêtre.
/// </summary>
public class DisplayedNamesTests
{
    private const string EmDash = "—";

    [Fact]
    public void DescriptionHeadings_HaveNoEmDash()
    {
        var assembly = typeof(LocalizationService).Assembly;
        var headings = assembly.GetManifestResourceNames()
            .Where(r => r.Replace('\\', '/').StartsWith("Content/") && r.EndsWith(".md"))
            .SelectMany(r =>
            {
                using var reader = new StreamReader(assembly.GetManifestResourceStream(r)!);
                return reader.ReadToEnd().Split('\n').Where(l => l.StartsWith('#')).Select(l => $"{r} : {l.Trim()}").ToList();
            })
            .ToList();

        Assert.NotEmpty(headings);
        Assert.DoesNotContain(headings, h => h.Contains(EmDash));
    }

    [Theory]
    [InlineData("fr-FR")]
    [InlineData("en-US")]
    public void WindowTitles_HaveNoEmDash(string culture)
    {
        foreach (var key in new[] { "Lab.Mode.Title", "Lab.Mode.ExamTitle", "Delegations.Window.Title", "Timeline.Window.Title", "About.Copyright" })
            Assert.DoesNotContain(EmDash, LocalizationService.Instance.Translate(key, culture));
    }

    [Fact]
    public void StudioTitles_HaveNoEmDash()
    {
        var texts = (Dictionary<string, (string Fr, string En)>)typeof(StudioText)
            .GetField("Texts", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        var titles = texts.Where(t => t.Key.EndsWith("Title")).ToList();

        Assert.NotEmpty(titles);
        Assert.DoesNotContain(titles, t => t.Value.Fr.Contains(EmDash) || t.Value.En.Contains(EmDash));
    }
}
