using System.Reflection;
using SMADX.Services;
using SMADX.Studio;

namespace SMADX.Tests;

/// <summary>
/// Pas de tiret cadratin entre deux mots dans les noms et les messages affichés : « Débutant · titre » dans les
/// menus, « nom : rôle » en tête d'une fiche, « SMAD-X · exercice : … » dans le titre d'une fenêtre,
/// « Aucun site : importez… » dans la barre d'état. Seul « — » tout seul reste, pour une case vide.
/// </summary>
public class DisplayedNamesTests
{
    private const string DashBetweenWords = " — ";

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
        Assert.DoesNotContain(headings, h => h.Contains('—'));
    }

    /// <summary>Les textes de l'interface de SMAD-X : tout sauf les fiches Markdown embarquées (descriptions, règles).</summary>
    [Theory]
    [InlineData("fr-FR")]
    [InlineData("en-US")]
    public void InterfaceTexts_HaveNoDashBetweenWords(string culture)
    {
        var markdown = typeof(LocalizationService).Assembly.GetManifestResourceNames()
            .Select(r => r.Replace('\\', '/'))
            .Where(r => r.StartsWith($"Content/{culture}/") && r.EndsWith(".md"))
            .Select(r => Path.GetFileNameWithoutExtension(r[$"Content/{culture}/".Length..]))
            .ToHashSet();
        var texts = LocalizationService.Instance.KeysOf(culture)
            .Where(k => !markdown.Contains(k))
            .Select(k => (Key: k, Text: LocalizationService.Instance.Translate(k, culture)))
            .ToList();

        Assert.NotEmpty(texts);
        Assert.DoesNotContain(texts, t => t.Text.Contains(DashBetweenWords));
    }

    [Fact]
    public void StudioTexts_HaveNoDashBetweenWords()
    {
        var texts = (Dictionary<string, (string Fr, string En)>)typeof(StudioText)
            .GetField("Texts", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;

        Assert.NotEmpty(texts);
        Assert.DoesNotContain(texts, t => t.Value.Fr.Contains(DashBetweenWords) || t.Value.En.Contains(DashBetweenWords));
    }
}
