using SMADX.Models;
using SMADX.Services;
using SMADX.Services.AttackPaths;
using SMADX.ViewModels;
using static SMADX.Services.AttackPaths.AttackEdgeKind;

namespace SMADX.Tests;

/// <summary>
/// Les domaines d'exemple de Samples/AttackPaths : chaque fichier, en français comme en anglais,
/// montre dans la fenêtre « Chemins d'attaque » le chemin que son scénario décrit.
/// </summary>
public class AttackPathSampleTests
{
    public AttackPathSampleTests() => TierConfigurationService.Instance.ResetToDefaults();

    /// <summary>Étapes attendues du chemin, et s'il part de « n'importe quel utilisateur du domaine ».</summary>
    private static readonly Dictionary<string, (bool FromAnyUser, AttackEdgeKind[] Steps)> Expected = new()
    {
        ["01-kerberoasting"] = (true, [Kerberoasting, MemberOf, MemberOf, PrivilegedGroup]),
        ["02-asrep-roasting"] = (true, [AsRepRoasting, MemberOf, PrivilegedGroup]),
        ["03-reset-password"] = (false, [ResetPassword, MemberOf, FullControl]),
        ["04-write-members"] = (false, [WriteMembers, FullControl]),
        ["05-gpo"] = (false, [Owner, GpoLink, MemberOf, PrivilegedGroup]),
        ["06-unconstrained-delegation"] = (false, [FullControl, UnconstrainedDelegation]),
        ["07-constrained-delegation"] = (false, [FullControl, ConstrainedDelegationToDc, DomainController]),
        ["08-owner"] = (false, [Owner, DomainController]),
        ["09-full-control"] = (true, [FullControl, MemberOf, FullControl]),
        ["10-attack-chain"] = (true, [Kerberoasting, ResetPassword, MemberOf, PrivilegedGroup]),
        ["11-empty-password"] = (true, [EmptyPassword, MemberOf, PrivilegedGroup]),
        ["12-nested-groups"] = (false, [MemberOf, MemberOf, MemberOf, PrivilegedGroup])
    };

    /// <summary>Copiés depuis Samples/AttackPaths par le projet de tests.</summary>
    private static string SamplesDir() => Path.Combine(AppContext.BaseDirectory, "Samples", "AttackPaths");

    public static TheoryData<string, string> Samples()
    {
        var data = new TheoryData<string, string>();
        foreach (var id in Expected.Keys)
            foreach (var lang in new[] { "fr", "en" })
                data.Add(lang, id);
        return data;
    }

    private static async Task<ADObject> Load(string lang, string id)
    {
        var result = await new ADDataService().LoadDocumentAsync(Path.Combine(SamplesDir(), lang, $"{id}.smad-x.json"));
        Assert.Equal(DocumentLoadStatus.Loaded, result.Status);
        return result.Document!.Domain;
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public async Task Sample_ShowsItsAttackPathOutsideTier0(string lang, string id)
    {
        var domain = await Load(lang, id);
        var (fromAnyUser, steps) = Expected[id];

        // Ce que voit l'utilisateur : la fenêtre, comptes de Tier 0 masqués
        var vm = new AttackPathsViewModel(() => domain, _ => { }, new FakeDialogService());

        Assert.Contains(vm.Paths, p => p.Path.Start.IsAnyUser == fromAnyUser && p.Path.Steps.Select(s => s.Kind).SequenceEqual(steps));
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public async Task Sample_DescribesItsScenarioInItsLanguage(string lang, string id)
    {
        var domain = await Load(lang, id);

        Assert.StartsWith("# ", domain.Description);
        Assert.Contains(lang == "fr" ? "## Le chemin" : "## The path", domain.Description);
        Assert.Contains(lang == "fr" ? "## Le corriger" : "## Fix it", domain.Description);
    }

    [Fact]
    public async Task Samples_AllUseDifferentDomainNames()
    {
        var names = new List<string>();
        foreach (var id in Expected.Keys)
            foreach (var lang in new[] { "fr", "en" })
                names.Add((await Load(lang, id)).Name);

        Assert.Equal(names.Count, names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Samples_FolderHasExactlyTheTestedFiles()
    {
        foreach (var lang in new[] { "fr", "en" })
            Assert.Equal(
                Expected.Keys.Order(),
                Directory.GetFiles(Path.Combine(SamplesDir(), lang), "*.smad-x.json").Select(f => Path.GetFileName(f)[..^".smad-x.json".Length]).Order());
    }
}
