using System.Net.Http;
using SMADX.Services.Sources;
using SMADX.Studio;
using SMADX.Studio.ViewModels;

namespace SMADX.Tests;

/// <summary>
/// SMAD-X Studio, fenêtre « Fiches des sources » : les règles, les sources de la règle choisie et leur fiche, l'export
/// PDF, la source officielle, et la relecture d'une source sur le web (rien n'est gardé si elle échoue).
/// </summary>
[Collection(StudioTextCollection.Name)]
public sealed class StudioSourcesTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "smadx-studio-sources-" + Guid.NewGuid().ToString("N"));
    private string Local => Path.Combine(_dir, "sources.json");

    public StudioSourcesTests()
    {
        StudioText.Instance.Culture = "fr-FR";
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); }
        catch (IOException) { }
    }

    private sealed class Dialogs : ISourceDialogs
    {
        public string? SavePath { get; set; }
        public string? Folder { get; set; }
        public List<string> Urls { get; } = new();
        public List<string> ShownFolders { get; } = new();
        public Task<string?> SaveFileAsync(string title, string suggestedName, string filterName, string pattern) => Task.FromResult(SavePath);
        public Task<string?> PickFolderAsync(string title) => Task.FromResult(Folder);
        public Task OpenUrlAsync(string url) { Urls.Add(url); return Task.CompletedTask; }
        public Task ShowFolderAsync(string folder) { ShownFolders.Add(folder); return Task.CompletedTask; }
    }

    private static Func<IReadOnlyCollection<string>, string, Task<(SourcesSnapshot?, List<string>)>> Returns(SourcesSnapshot? snapshot, params string[] errors) =>
        (_, _) => Task.FromResult<(SourcesSnapshot?, List<string>)>((snapshot, errors.ToList()));

    /// <summary>Un relevé de l'ANSSI fait à la date donnée, où le point vuln_spn_priv a changé de titre.</summary>
    private static SourcesSnapshot AnssiRetrievedOn(string date, string title)
    {
        var anssi = SourcesSnapshot.Embedded.Only(["anssi"]);
        return anssi with
        {
            Retrieved = date,
            Frameworks = anssi.Frameworks.Select(f => f with { Retrieved = date }).ToList(),
            Items = anssi.Items.Select(i => i.Id == "vuln_spn_priv" ? i with { TitleFr = title } : i).ToList()
        };
    }

    [Fact]
    public void OpensOnTheRuleOfTheRecipe_WithItsAnssiPointFirst()
    {
        var vm = new SourcesViewModel(new Dialogs(), "KRB-01", Local);
        Assert.Equal("KRB-01", vm.Selected!.RuleId);
        Assert.Equal("ANSSI · vuln_spn_priv", vm.SelectedReference!.Label);
        Assert.Contains(vm.Selected.References, r => r.Label == "MITRE ATT&CK · T1558.003");
        Assert.StartsWith("# vuln_spn_priv — ", vm.Sheet);
        Assert.Contains("**KRB-01**", vm.Sheet);
        Assert.Contains("livré avec SMAD-X", vm.Origin);
        Assert.False(vm.HasStudioRetrieval);

        vm.Language = vm.Languages.Single(l => l.Value == "en");
        Assert.Contains("## Official text", vm.Sheet);
    }

    [Fact]
    public void TheFilter_FindsTheRulesThatCiteATechnique()
    {
        var vm = new SourcesViewModel(new Dialogs(), null, Local) { Filter = "T1558.003" };
        Assert.Equal(["KRB-01", "KRB-02"], vm.Rules.Select(r => r.RuleId).Order());
        vm.Select("ACL-03");
        Assert.Equal("", vm.Filter);
        Assert.Equal("ACL-03", vm.Selected!.RuleId);
    }

    [Fact]
    public async Task CheckingTheSource_SaysWhatChanged_AndKeepsTheNewRetrieval()
    {
        var asked = new List<string>();
        var newer = AnssiRetrievedOn("2099-01-01", "Nouveau titre");
        var vm = new SourcesViewModel(new Dialogs(), "KRB-01", Local, fetch: (only, _) =>
        {
            asked.AddRange(only);
            return Task.FromResult<(SourcesSnapshot?, List<string>)>((newer, []));
        });
        await vm.CheckSourceCommand.ExecuteAsync(null);

        Assert.Equal(["anssi"], asked);
        Assert.Contains("1 changement", vm.Message);
        Assert.Contains("« Nouveau titre »", vm.Message);
        Assert.StartsWith("# vuln_spn_priv — Nouveau titre", vm.Sheet);
        Assert.Contains("Relu par Studio le 2099-01-01", vm.Origin);
        Assert.True(File.Exists(Local));
        Assert.True(vm.HasStudioRetrieval);

        // Une autre fenêtre relit le relevé gardé ; revenir aux fiches livrées l'oublie
        var again = new SourcesViewModel(new Dialogs(), "KRB-01", Local);
        Assert.StartsWith("# vuln_spn_priv — Nouveau titre", again.Sheet);
        again.ResetToShippedCommand.Execute(null);
        Assert.False(File.Exists(Local));
        Assert.DoesNotContain("Nouveau titre", again.Sheet);
        Assert.False(again.HasStudioRetrieval);
    }

    [Fact]
    public void ARetrievalOlderThanTheShippedOne_IsNotShown()
    {
        AnssiRetrievedOn("2000-01-01", "Ancien titre").Save(Local);
        var vm = new SourcesViewModel(new Dialogs(), "KRB-01", Local);
        Assert.DoesNotContain("Ancien titre", vm.Sheet);
        Assert.Contains("livré avec SMAD-X", vm.Origin);
    }

    [Fact]
    public async Task CheckingTheSource_KeepsNothing_WhenItIsUnreachable()
    {
        var vm = new SourcesViewModel(new Dialogs(), "KRB-01", Local, fetch: (_, _) => throw new HttpRequestException("pas de réseau"));
        await vm.CheckSourceCommand.ExecuteAsync(null);
        Assert.Contains("injoignable", vm.Message);
        Assert.Contains("pas de réseau", vm.Message);
        Assert.False(File.Exists(Local));
        Assert.False(vm.IsBusy);
    }

    [Fact]
    public async Task CheckingTheSource_KeepsNothing_WhenACitedItemDisappeared()
    {
        var vm = new SourcesViewModel(new Dialogs(), "KRB-01", Local,
            fetch: Returns(null, "ANSSI : le point vuln_spn_priv n'est plus dans la checklist."));
        await vm.CheckSourceCommand.ExecuteAsync(null);
        Assert.Contains("Rien n'est gardé", vm.Message);
        Assert.Contains("vuln_spn_priv n'est plus dans la checklist", vm.Message);
        Assert.False(File.Exists(Local));
    }

    [Fact]
    public async Task TheSheet_IsExportedAsPdf_UnderTheNameShownInStudio()
    {
        var dialogs = new Dialogs { SavePath = Path.Combine(_dir, "fiche.pdf") };
        var vm = new SourcesViewModel(dialogs, "ACL-03", Local);
        Assert.Equal("ACL-03 - ANSSI vuln_permissions_naming_context", SourcesViewModel.ExportName(vm.Selected!, vm.SelectedReference!));
        Assert.Equal("ACL-03 - MITRE ATT&CK T1003.006", SourcesViewModel.ExportName(vm.Selected!, vm.Selected!.References.Single(r => r.ShortId == "T1003.006")));
        await vm.ExportPdfCommand.ExecuteAsync(null);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(File.ReadAllBytes(dialogs.SavePath), 0, 4));
        Assert.Contains(dialogs.SavePath, vm.Message);
    }

    /// <summary>KRB-01 et KRB-02 citent tous deux T1558.003 : chaque règle a sa fiche, à son nom.</summary>
    [Fact]
    public async Task TheTickedRules_AreExported_OnePdfPerSourceOfEachRule_NamedAsInStudio()
    {
        var dialogs = new Dialogs { Folder = Path.Combine(_dir, "export") };
        var vm = new SourcesViewModel(dialogs, "KRB-01", Local);
        var ticked = vm.Rules.Where(r => r.RuleId is "KRB-01" or "KRB-02").ToList();
        ticked.ForEach(r => r.IsChecked = true);
        await vm.ExportCheckedCommand.ExecuteAsync(null);

        var expected = ticked.Sum(r => r.References.Count);
        var files = Directory.GetFiles(dialogs.Folder, "*.pdf", SearchOption.AllDirectories);
        Assert.Equal(expected, files.Length);
        foreach (var name in new[] { "KRB-01 - ANSSI vuln_spn_priv", "KRB-01 - MITRE ATT&CK T1558.003", "KRB-02 - ANSSI info_spn_present", "KRB-02 - MITRE ATT&CK T1558.003" })
            Assert.True(File.Exists(Path.Combine(dialogs.Folder, name + ".pdf")), name);
        Assert.Equal([dialogs.Folder], dialogs.ShownFolders);
        Assert.Contains($"{expected} fiche(s) exportée(s)", vm.Message);
    }

    [Fact]
    public async Task TheOfficialSource_OpensInFrench_WhenThePageExistsInFrench()
    {
        var dialogs = new Dialogs();
        var vm = new SourcesViewModel(dialogs, "DLG-04", Local);
        vm.SelectedReference = vm.Selected!.References.First(r => r.Reference.Framework == SMADX.Services.Audit.AuditFramework.Microsoft
                                                                  && r.Reference.Url.Contains("learn.microsoft.com"));
        await vm.OpenSourceCommand.ExecuteAsync(null);
        Assert.Contains("/fr-fr/", Assert.Single(dialogs.Urls));
    }
}

/// <summary>Les tests qui changent la langue de Studio (globale) passent l'un après l'autre.</summary>
[CollectionDefinition(Name)]
public sealed class StudioTextCollection
{
    public const string Name = "StudioText";
}
