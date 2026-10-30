using SMADX.Services;
using SMADX.Services.Audit;
using static SMADX.Tests.TestHelpers;

namespace SMADX.Tests;

public class LocalizationContentTests
{
    private static readonly string[] Cultures = ["fr-FR", "en-US"];

    private static IEnumerable<string> EmbeddedKeys(string culture) =>
        typeof(LocalizationService).Assembly.GetManifestResourceNames()
            .Select(r => r.Replace('\\', '/')) // %(RecursiveDir) donne des antislashs sous Windows
            .Where(r => r.StartsWith($"Content/{culture}/") && r.EndsWith(".md"))
            .Select(r => Path.GetFileNameWithoutExtension(r[$"Content/{culture}/".Length..]))
            .Order();

    [Fact]
    public void MarkdownContent_IsEmbeddedForEachCulture()
    {
        foreach (var culture in Cultures)
        {
            Assert.Equal(112, EmbeddedKeys(culture).Count(k => k.StartsWith("Desc.")));
            Assert.Equal(AuditRules.All.Count, EmbeddedKeys(culture).Count(k => k.StartsWith("Audit.")));
        }
    }

    [Fact]
    public void MarkdownContent_HasSameKeysInEveryCulture()
    {
        Assert.Equal(EmbeddedKeys("fr-FR"), EmbeddedKeys("en-US"));
    }

    [Fact]
    public void MarkdownContent_IsLoadedWithLfLineEndings()
    {
        var loc = LocalizationService.Instance;
        var previous = loc.CurrentCulture.Name;
        try
        {
            foreach (var culture in Cultures)
            {
                loc.SetLanguage(culture);
                foreach (var key in EmbeddedKeys(culture))
                {
                    var value = loc[key];
                    Assert.False(string.IsNullOrWhiteSpace(value), $"{culture}/{key} est vide");
                    Assert.DoesNotContain("\r", value);
                }
            }
        }
        finally
        {
            loc.SetLanguage(previous);
        }
    }

    [Fact]
    public void FreshDomain_HasNoMissingDescription()
    {
        // Une clé absente est rendue "[Desc.xxx]" par LocalizationService
        var root = new ADDataService().CreateFreshDomainStructure("corp.local");

        Assert.DoesNotContain(Flatten(root), o => o.Description.StartsWith("[Desc."));
    }
}
