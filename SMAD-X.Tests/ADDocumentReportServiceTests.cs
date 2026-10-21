using SMADX.Models;
using SMADX.Services;
using static SMADX.Tests.TestHelpers;

namespace SMADX.Tests;

public class ADDocumentReportServiceTests
{
    private readonly ADDocumentReportService _service = new();

    /// <summary>Toutes les descriptions embarquées, dans les deux langues.</summary>
    public static TheoryData<string> ContentFiles()
    {
        var data = new TheoryData<string>();
        foreach (var r in typeof(LocalizationService).Assembly.GetManifestResourceNames()
                     .Select(r => r.Replace('\\', '/'))
                     .Where(r => r.StartsWith("Content/") && r.EndsWith(".md"))
                     .Order())
            data.Add(r["Content/".Length..]);
        return data;
    }

    private static string ReadContent(string relativePath)
    {
        var assembly = typeof(LocalizationService).Assembly;
        var resource = assembly.GetManifestResourceNames().Single(r => r.Replace('\\', '/') == "Content/" + relativePath);
        using var reader = new StreamReader(assembly.GetManifestResourceStream(resource)!);
        return reader.ReadToEnd();
    }

    private static void AssertNonEmptyFile(string path)
    {
        Assert.True(File.Exists(path), $"{path} n'a pas été créé");
        Assert.True(new FileInfo(path).Length > 1000, $"{path} est vide ou presque");
    }

    [Theory]
    [MemberData(nameof(ContentFiles))]
    public async Task SingleElementPdf_ExportsEveryDescription(string contentFile)
    {
        var path = TempFile(".pdf");

        Assert.True(await _service.ExportSingleElementPdfAsync("Objet", "Type", ReadContent(contentFile), path));
        AssertNonEmptyFile(path);
    }

    [Theory]
    [InlineData("# 👤 Titre\n\nUtiliser `Administrator` ici\n")]              // code en ligne + emoji
    [InlineData("## ⚠️ Sécurité\n")]                                            // sélecteur de variante U+FE0F
    [InlineData("> 🔴 **Tier 0**\n")]
    [InlineData("| A | B |\n|---|---|\n| x | ✓ |\n")]                          // symbole dans un tableau
    [InlineData("| A | B |\n|---|---|\n| 🔗 GPO | ✔ ✗ ★ |\n")]                 // emoji dans un tableau
    public async Task SingleElementPdf_HandlesEmojiEdgeCases(string markdown)
    {
        var path = TempFile(".pdf");

        Assert.True(await _service.ExportSingleElementPdfAsync("Objet", "Type", markdown, path));
        AssertNonEmptyFile(path);
    }

    /// <summary>Polices déclarées dans le PDF (/BaseFont), sans le préfixe de sous-ensemble ABCDEF+.</summary>
    private static HashSet<string> PdfFonts(string path) =>
        System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(path, System.Text.Encoding.Latin1), @"/BaseFont\s*/(?:[A-Z]{6}\+)?([A-Za-z0-9_-]+)")
            .Select(m => m.Groups[1].Value)
            .ToHashSet();

    [Fact]
    public async Task Pdf_UsesEmbeddedNotoFontsAndNoSystemFont()
    {
        var path = TempFile(".pdf");
        var markdown = ReadContent("fr-FR/Desc.Users.Administrator.md") + "\n\n| A | B |\n|---|---|\n| 🔗 GPO | ✓ → |\n";

        Assert.True(await _service.ExportSingleElementPdfAsync("Objet", "Type", markdown, path));

        var fonts = PdfFonts(path);
        Assert.Contains(fonts, f => f.StartsWith("NotoEmoji"));
        Assert.Contains(fonts, f => f.StartsWith("NotoSansSymbols2"));
        Assert.DoesNotContain(fonts, f => f.StartsWith("Segoe") || f.StartsWith("Arial"));
    }

    private static ADRootDocument BuildSampleDocument()
    {
        var data = new ADDataService();
        var root = data.CreateSampleStructure();
        var ou = Flatten(root).First(o => o.Type == ADObjectType.OrganizationalUnit);
        // Une délégation héritée produit une ligne de tableau « Hérité »
        ou.Delegations.Add(new ADDelegation
        {
            TrusteeName = "Helpdesk", TargetDN = ou.DistinguishedName, Right = "ResetPassword",
            RightCategory = RightCategory.PasswordReset, IsInherited = true
        });
        return new ADRootDocument { Domain = root, SitesTopology = data.CreateSampleTopology() };
    }

    [Theory]
    [InlineData("WordLike")]
    [InlineData("Plain")]
    [InlineData("TechnicalDocument")]
    [InlineData("GitHubLike")]
    [InlineData("Compact")]
    [InlineData("Report")]
    public async Task FullReportPdf_ExportsWithEveryTheme(string theme)
    {
        var path = TempFile(".pdf");

        Assert.True(await _service.ExportPdfAsync(BuildSampleDocument(), path, theme));
        AssertNonEmptyFile(path);
    }

    [Fact]
    public async Task FullReportDocx_Exports()
    {
        var path = TempFile(".docx");

        Assert.True(await _service.ExportDocxAsync(BuildSampleDocument(), path));
        AssertNonEmptyFile(path);
    }

    [Fact]
    public async Task SingleElementDocx_Exports()
    {
        var path = TempFile(".docx");

        Assert.True(await _service.ExportSingleElementDocxAsync("Objet", "Type", ReadContent("fr-FR/Desc.Users.Administrator.md"), path));
        AssertNonEmptyFile(path);
    }
}
