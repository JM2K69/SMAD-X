using System.Runtime.CompilerServices;
using System.Text.Json;
using SMADX.Services.Audit;
using SMADX.Services.Sources;

namespace SMADX.Tests;

/// <summary>
/// Les sources de SMAD-X, relevées sur le web par tools/SmadxSources (sources/sources.json et les fiches
/// sources/{référentiel}/{fr,en}) et embarquées dans SMAD-X.Core pour Studio : chaque référence citée par une règle
/// d'audit a sa fiche dans les deux langues, chaque référentiel dit d'où il vient et sous quelle licence, et les niveaux
/// ANSSI de SMAD-X sont ceux de la checklist officielle. Une règle qui cite une nouvelle source fait échouer ces tests
/// tant que l'outil n'a pas été relancé (update) ; une fiche qui n'est plus celle que le code écrit, tant que write n'a
/// pas été relancé.
/// </summary>
public class SourcesTests
{
    private static readonly string Root = RepoRoot();
    private static readonly SourcesSnapshot Snapshot = SourcesSnapshot.Load(Path.Combine(Root, "sources", "sources.json"))!;

    [Fact]
    public void TheEmbeddedSnapshot_IsTheRepositoryOne() =>
        Assert.Equal(JsonSerializer.Serialize(Snapshot, SourcesSnapshot.Json), JsonSerializer.Serialize(SourcesSnapshot.Embedded, SourcesSnapshot.Json));

    /// <summary>
    /// Chaque référence citée a sa fiche dans sources/{référentiel}/fr et sources/{référentiel}/en, qui nomme la règle ;
    /// une fiche ANSSI ou MITRE porte l'identifiant que SMAD-X affiche (vuln_spn_priv.md, T1558.003.md).
    /// </summary>
    [Fact]
    public void EveryCitedReference_HasASheet_InFrenchAndInEnglish()
    {
        foreach (var rule in AuditRules.All)
            foreach (var reference in rule.References)
            {
                var item = SourceCitations.Find(Snapshot, reference);
                Assert.True(item != null, $"{rule.Id} cite {reference.Id} ({reference.Url}) : pas de fiche, relancer tools/SmadxSources update");
                Assert.Contains(rule.Id, item!.UsedBy);
                foreach (var lang in SourceSheets.Languages)
                {
                    var folder = SourceSheets.Folder(Root, item.Framework, lang);
                    var file = Path.Combine(folder, SourceSheets.FileName(item) + ".md");
                    Assert.True(File.Exists(file), $"{rule.Id} cite {reference.Id} : pas de fiche {file}");
                    Assert.Contains($"**{rule.Id}**", File.ReadAllText(file));
                    if (item.Framework is "anssi" or "mitre-attack")
                        Assert.Equal(reference.Id + ".md", Path.GetFileName(file));
                    Assert.Contains($"]({Uri.EscapeDataString(Path.GetFileName(file))})", File.ReadAllText(Path.Combine(folder, "README.md")));
                }
            }
    }

    /// <summary>Les fiches du dépôt sont celles que le code écrit : après un changement de mise en page ou de titre de règle, relancer write.</summary>
    [Fact]
    public void TheSheets_AreThoseTheCodeWrites()
    {
        var rules = SourceCitations.RuleTitles();
        foreach (var framework in Snapshot.Frameworks)
            foreach (var item in Snapshot.Items.Where(i => i.Framework == framework.Id))
                foreach (var lang in SourceSheets.Languages)
                {
                    var file = Path.Combine(SourceSheets.Folder(Root, framework.Id, lang), SourceSheets.FileName(item) + ".md");
                    var expected = SourceSheets.Sheet(lang, framework, item, rules, links: true).Replace("\r\n", "\n").TrimEnd() + "\n";
                    Assert.True(expected == File.ReadAllText(file).Replace("\r\n", "\n"), $"{file} n'est plus à jour : relancer tools/SmadxSources write");
                }
    }

    [Fact]
    public void EveryFramework_SaysWhereItComesFrom_AndUnderWhichLicence()
    {
        foreach (var f in Snapshot.Frameworks)
        {
            Assert.StartsWith("https://", f.Url);
            Assert.Matches(@"^\d{4}-\d{2}-\d{2}$", f.Retrieved);
            Assert.False(string.IsNullOrWhiteSpace(f.LicenseFr));
            Assert.False(string.IsNullOrWhiteSpace(f.LicenseEn));
        }
        Assert.All(Snapshot.Items, i => Assert.StartsWith("https://", i.Url));
        // CIS : la licence ne permet que l'identifiant et le titre ; Microsoft : la description de la page, courte
        Assert.All(Snapshot.Items.Where(i => i.Framework == "cis"), i => Assert.Null(i.TextEn));
        Assert.All(Snapshot.Items.Where(i => i.Framework == "microsoft"), i => Assert.True((i.TextEn ?? "").Length < 400));
        Assert.Contains("The MITRE Corporation", Snapshot.Frameworks.Single(f => f.Id == "mitre-attack").Notice);
    }

    /// <summary>
    /// Le niveau ANSSI d'une règle est l'un des niveaux des points de la checklist qu'elle cite, tels que le CERT-FR les
    /// publie. Un point peut en avoir plusieurs, chacun avec son seuil : vuln_privileged_members_perm est de niveau 1
    /// pour les permissions exploitables sans prérequis, de niveau 2 pour toutes les permissions dangereuses, que
    /// retient ACL-02.
    /// </summary>
    [Fact]
    public void AnssiLevels_AreThoseOfTheOfficialChecklist()
    {
        var wrong = new List<string>();
        foreach (var rule in AuditRules.All.Where(r => r.AnssiLevel != null))
        {
            var levels = rule.References.Where(r => r.Framework == AuditFramework.Anssi && r.Id != "ANSSI-PA-099")
                .Select(r => SourceCitations.Find(Snapshot, r)).OfType<SourceItem>()
                .SelectMany(i => i.Levels ?? []).Distinct().Order().ToList();
            if (levels.Count > 0 && !levels.Contains(rule.AnssiLevel!.Value))
                wrong.Add($"{rule.Id} : SMAD-X {rule.AnssiLevel}, checklist {string.Join("/", levels)}");
        }
        Assert.True(wrong.Count == 0, string.Join("\n", wrong));
    }

    /// <summary>Un relevé de l'ANSSI fait plus tard (dans Studio) avec un titre changé, et la date donnée.</summary>
    private static SourcesSnapshot AnssiRetrievedOn(string date, string title)
    {
        var anssi = SourcesSnapshot.Embedded.Only(["anssi"]);
        return anssi with
        {
            Retrieved = date,
            Frameworks = anssi.Frameworks.Select(f => f with { Retrieved = date }).ToList(),
            Items = anssi.Items.Select(i => i.Id == "vuln_spn_priv" ? i with { TitleFr = title, UsedBy = ["XXX-99"] } : i).ToList()
        };
    }

    [Fact]
    public void ANewerRetrieval_ReplacesTheContent_ButKeepsTheItemsAndRulesOfThisVersion()
    {
        var shipped = SourcesSnapshot.Embedded;
        var merged = shipped.WithNewer(AnssiRetrievedOn("2099-01-01", "Nouveau titre"));
        var item = merged.Items.Single(i => i.Key == "anssi|vuln_spn_priv");
        Assert.Equal("Nouveau titre", item.TitleFr);
        Assert.Equal(shipped.Items.Single(i => i.Key == item.Key).UsedBy, item.UsedBy);
        Assert.Equal("2099-01-01", merged.Frameworks.Single(f => f.Id == "anssi").Retrieved);
        Assert.Equal(shipped.Items.Select(i => i.Key), merged.Items.Select(i => i.Key));
        Assert.Same(shipped.Frameworks.Single(f => f.Id == "nist"), merged.Frameworks.Single(f => f.Id == "nist"));
    }

    [Fact]
    public void AnOlderRetrieval_GivesWayToTheShippedOne() =>
        Assert.Same(SourcesSnapshot.Embedded, SourcesSnapshot.Embedded.WithNewer(AnssiRetrievedOn("2000-01-01", "Ancien titre")));

    [Fact]
    public void Merge_ReplacesTheFrameworksReadAgain_WithAllTheirItems()
    {
        var merged = SourcesSnapshot.Embedded.Merge(AnssiRetrievedOn("2099-01-01", "Nouveau titre"));
        Assert.Equal("2099-01-01", merged.Retrieved);
        Assert.Equal("Nouveau titre", merged.Items.Single(i => i.Key == "anssi|vuln_spn_priv").TitleFr);
        Assert.Equal(SourcesSnapshot.Embedded.Items.Count, merged.Items.Count);
        Assert.Equal(SourcesSnapshot.Embedded.Frameworks.Count, merged.Frameworks.Count);
    }

    [Fact]
    public void Diff_SaysWhatChanged_InTheLanguageAsked()
    {
        var before = SourcesSnapshot.Embedded.Only(["anssi"]);
        var after = AnssiRetrievedOn("2099-01-01", "Nouveau titre");
        Assert.Contains(SourceUpdate.Diff(before, after, "fr"), c => c.StartsWith("~ anssi vuln_spn_priv : titre (fr) « ") && c.EndsWith("« Nouveau titre »"));
        Assert.Contains(SourceUpdate.Diff(before, after, "en"), c => c.StartsWith("~ anssi vuln_spn_priv: title (fr) “"));
    }

    [Fact]
    public void ASheet_IsWrittenAsPdf()
    {
        var file = Path.Combine(Path.GetTempPath(), $"smadx-source-{Guid.NewGuid():N}.pdf");
        try
        {
            var item = Snapshot.Items.Single(i => i.Key == "anssi|vuln_spn_priv");
            SourcePdf.Save(file, "test", Snapshot.Frameworks.Single(f => f.Id == "anssi"), item, "fr", SourceCitations.RuleTitles());
            Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(File.ReadAllBytes(file), 0, 4));
        }
        finally
        {
            File.Delete(file);
        }
    }

    private static string RepoRoot([CallerFilePath] string path = "")
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Path.GetDirectoryName(path)! })
            for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
                if (File.Exists(Path.Combine(dir.FullName, "SMAD-X.slnx"))) return dir.FullName;
        throw new DirectoryNotFoundException("SMAD-X.slnx introuvable");
    }
}
