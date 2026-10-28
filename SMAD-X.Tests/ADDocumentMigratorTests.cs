using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using SMADX.Models;
using SMADX.Services;
using static SMADX.Tests.TestHelpers;

namespace SMADX.Tests;

public class ADDocumentMigratorTests
{
    private static JsonObject Json(string json) => (JsonObject)JsonNode.Parse(json)!;

    [Fact]
    public void Steps_CoverEveryVersionUpToCurrent()
    {
        // Incrémenter CurrentVersion sans ajouter l'étape de migration doit faire échouer ce test
        for (var version = 1; version < ADRootDocument.CurrentVersion; version++)
            Assert.True(ADDocumentMigrator.Steps.ContainsKey(version), $"Migration v{version} → v{version + 1} manquante");
    }

    [Theory]
    [InlineData("""{ "Name": "contoso.com", "Type": "Domain", "Children": [] }""", 1)]
    [InlineData("""{ "Children": [] }""", 1)]
    [InlineData("""{ "Version": 2, "Domain": {} }""", 2)]
    [InlineData("""{ "version": 2, "Domain": {} }""", 2)]
    [InlineData("""{ "Version": 7 }""", 7)]
    public void DetectVersion_ReadsVersionOrRecognisesV1(string json, int expected)
    {
        Assert.Equal(expected, ADDocumentMigrator.DetectVersion(Json(json)));
    }

    [Theory]
    [InlineData("""{ "Version": "deux" }""")]
    [InlineData("""{ "Version": 0 }""")]
    [InlineData("""{ "Version": -1 }""")]
    [InlineData("""{ "Version": 2.5 }""")]
    [InlineData("""{ "Nom": "pas un document SMAD-X" }""")]
    public void DetectVersion_RejectsInvalidDocuments(string json)
    {
        Assert.Throws<FormatException>(() => ADDocumentMigrator.DetectVersion(Json(json)));
    }

    [Fact]
    public void MigrateV1_WrapsTreeIntoDomain()
    {
        var migrated = ADDocumentMigrator.MigrateToCurrent(Json("""{ "Name": "contoso.com", "Type": "Domain", "Children": [] }"""), out var fileVersion);

        Assert.Equal(1, fileVersion);
        Assert.Equal(ADRootDocument.CurrentVersion, (int)migrated["Version"]!);
        Assert.Equal("contoso.com", (string)migrated["Domain"]!["Name"]!);
        Assert.True(migrated.ContainsKey("SitesTopology"));
    }

    [Fact]
    public void MigrateCurrent_IsUnchanged()
    {
        var root = Json($$"""{ "Version": {{ADRootDocument.CurrentVersion}}, "Domain": { "Name": "x" } }""");

        var migrated = ADDocumentMigrator.MigrateToCurrent(root, out var fileVersion);

        Assert.Equal(ADRootDocument.CurrentVersion, fileVersion);
        Assert.Same(root, migrated);
    }

    [Fact]
    public void MigrateV2_ToV3_KeepsTheDocument_AndAnOlderSmadXRefusesV3()
    {
        var root = Json("""{ "Version": 2, "Domain": { "Name": "x" } }""");

        var migrated = ADDocumentMigrator.MigrateToCurrent(root, out var fileVersion);

        Assert.Equal(2, fileVersion);
        Assert.Equal(3, (int)migrated["Version"]!);
        Assert.Equal("x", (string?)migrated["Domain"]!["Name"]);
    }

    [Fact]
    public void MigrateNewerVersion_IsRefused()
    {
        var ex = Assert.Throws<DocumentTooNewException>(() =>
            ADDocumentMigrator.MigrateToCurrent(Json($$"""{ "Version": {{ADRootDocument.CurrentVersion + 1}} }"""), out _));

        Assert.Equal(ADRootDocument.CurrentVersion + 1, ex.FileVersion);
    }
}

public class DocumentLoadingTests
{
    private readonly ADDataService _service = new();

    private static string RepoFile(string name, [CallerFilePath] string testFile = "") =>
        Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(testFile))!, name);

    private static async Task<string> WriteTemp(string content)
    {
        var path = TempFile(".smad-x.json");
        await File.WriteAllTextAsync(path, content);
        return path;
    }

    [Theory]
    [InlineData("example.mockad.json", 1)]
    [InlineData("ad_structure_20260504_144107.json", 1)]
    [InlineData("harden.ad_20260731_024900.smad-x.json", 2)]
    public async Task RepositorySamples_LoadAtCurrentVersion(string file, int expectedFileVersion)
    {
        var result = await _service.LoadDocumentAsync(RepoFile(file));

        Assert.Equal(DocumentLoadStatus.Loaded, result.Status);
        Assert.Equal(expectedFileVersion, result.FileVersion);
        Assert.Equal(ADRootDocument.CurrentVersion, result.Document!.Version);
        Assert.NotEmpty(result.Document.Domain.Children);
        Assert.All(result.Document.Domain.Children, c => Assert.Same(result.Document.Domain, c.Parent));
    }

    [Fact]
    public async Task NewerFile_IsRefusedWithItsVersion()
    {
        var path = await WriteTemp($$"""{ "Version": {{ADRootDocument.CurrentVersion + 1}}, "Domain": { "Name": "futur.local", "Type": "Domain" } }""");

        var result = await _service.LoadDocumentAsync(path);

        Assert.Equal(DocumentLoadStatus.TooNew, result.Status);
        Assert.Equal(ADRootDocument.CurrentVersion + 1, result.FileVersion);
        Assert.Null(result.Document);
    }

    [Theory]
    [InlineData("pas du json")]
    [InlineData("[1, 2, 3]")]
    [InlineData("""{ "Version": 2 }""")]
    [InlineData("""{ "Version": 2, "Domain": { "Type": "Licorne" } }""")]
    [InlineData("""{ "Nom": "autre chose" }""")]
    public async Task InvalidFile_IsReportedWithDetail(string content)
    {
        var result = await _service.LoadDocumentAsync(await WriteTemp(content));

        Assert.Equal(DocumentLoadStatus.InvalidFormat, result.Status);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
    }

    [Fact]
    public async Task Save_AlwaysWritesCurrentVersion()
    {
        var path = TempFile(".smad-x.json");

        await _service.SaveDocumentAsync(new ADRootDocument { Version = 1, Domain = BuildSmallDomain() }, path);

        Assert.Equal(ADRootDocument.CurrentVersion, (int)JsonNode.Parse(await File.ReadAllTextAsync(path))!["Version"]!);
    }
}
