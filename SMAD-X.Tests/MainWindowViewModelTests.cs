using SMADX.Models;
using SMADX.Services;
using SMADX.ViewModels;
using static SMADX.Tests.TestHelpers;

namespace SMADX.Tests;

/// <summary>Service de dialogues de test : répond avec des valeurs préparées et note ce qui a été ouvert.</summary>
internal sealed class FakeDialogService : IDialogService
{
    public NewDomainRequest? NewDomain { get; set; }
    public string? SavePath { get; set; }
    public string? OpenPath { get; set; }
    public List<string> Opened { get; } = new();
    public RelationsViewModel? LastRelations { get; private set; }

    public Task<string?> PickSaveFileAsync(string title, string suggestedFileName, string defaultExtension, params FileFilter[] filters)
    {
        Opened.Add($"Save:{suggestedFileName}");
        return Task.FromResult(SavePath);
    }

    public Task<string?> PickOpenFileAsync(string title, params FileFilter[] filters)
    {
        Opened.Add("Open");
        return Task.FromResult(OpenPath);
    }

    public Task<NewDomainRequest?> ShowNewDomainAsync() => Task.FromResult(NewDomain);

    public Task ShowRelationsAsync(RelationsViewModel viewModel)
    {
        LastRelations = viewModel;
        Opened.Add("Relations");
        return Task.CompletedTask;
    }

    public GraphViewModel? LastGraph { get; private set; }
    public void ShowGraph(GraphViewModel viewModel) { LastGraph = viewModel; Opened.Add("Graph"); }
    public AttackPathsViewModel? LastAttackPaths { get; private set; }
    public void ShowAttackPaths(AttackPathsViewModel viewModel) { LastAttackPaths = viewModel; Opened.Add("AttackPaths"); }
    public AuditViewModel? LastAudit { get; private set; }
    public void ShowAudit(AuditViewModel viewModel) { LastAudit = viewModel; Opened.Add("Audit"); }
    public LabViewModel? LastLab { get; private set; }
    public void ShowLab(LabViewModel viewModel) { LastLab = viewModel; Opened.Add("Lab"); }
    public ComparisonViewModel? LastComparison { get; private set; }
    public void ShowComparison(ComparisonViewModel viewModel) { LastComparison = viewModel; Opened.Add("Comparison"); }
    public Task ShowTierConfigurationAsync() { Opened.Add("Tiers"); return Task.CompletedTask; }
    public Task ShowTimelineAsync() { Opened.Add("Timeline"); return Task.CompletedTask; }
    public Task ShowDelegationsAsync(ADObject? root) { Opened.Add($"Delegations:{root?.Name}"); return Task.CompletedTask; }
    public Task ShowSitesAsync(ADSitesTopology? topology) { Opened.Add($"Sites:{topology?.Sites.Count}"); return Task.CompletedTask; }
    public Task<string?> ShowExportReportAsync(ADRootDocument document) { Opened.Add($"Report:{document.Domain.Name}"); return Task.FromResult<string?>(null); }
    public Task ShowAboutAsync() { Opened.Add("About"); return Task.CompletedTask; }
    public string? StudioDomain { get; private set; }
    public bool StudioAvailable { get; set; } = true;
    public bool OpenInStudio(string domainFile, out string studioPath)
    {
        studioPath = "SMAD-X.Studio";
        StudioDomain = domainFile;
        Opened.Add("Studio");
        return StudioAvailable;
    }
    public Task ShowShortcutsAsync() { Opened.Add("Shortcuts"); return Task.CompletedTask; }
}

public class MainWindowViewModelTests
{
    private readonly FakeDialogService _dialogs = new();
    private readonly MainWindowViewModel _vm;

    public MainWindowViewModelTests() => _vm = new MainWindowViewModel(_dialogs);

    private ADTreeNode Node(string name) => FindNode(_vm.RootNodes[0], name)!;

    private static ADTreeNode? FindNode(ADTreeNode node, string name) =>
        node.Data.Name == name ? node : node.Children.Select(c => FindNode(c, name)).FirstOrDefault(n => n != null);

    [Fact]
    public void Startup_LoadsSampleDomainWithSites()
    {
        Assert.Equal("contoso.com", _vm.RootObject!.Name);
        Assert.NotEmpty(_vm.SiteRootNodes);
        Assert.Contains("Sites", _vm.ObjectCountsSummary);
    }

    [Fact]
    public async Task CreateNewDomain_ReplacesTreeAndSites()
    {
        _vm.SelectedNode = _vm.RootNodes[0];
        _dialogs.NewDomain = new NewDomainRequest("corp.local", EnableTiering: true);

        await _vm.CreateNewDomainCommand.ExecuteAsync(null);

        Assert.Equal("corp.local", _vm.RootObject!.Name);
        Assert.Null(_vm.SelectedNode); // l'ancienne sélection appartenait au domaine précédent
        var site = Assert.Single(_vm.SitesTopology!.Sites);
        Assert.Equal(["DC01.corp.local"], site.DomainControllers);
    }

    [Fact]
    public async Task CreateNewDomain_Cancelled_KeepsCurrentDomain()
    {
        _dialogs.NewDomain = null;

        await _vm.CreateNewDomainCommand.ExecuteAsync(null);

        Assert.Equal("contoso.com", _vm.RootObject!.Name);
    }

    [Fact]
    public async Task SaveThenLoad_RoundTripsThroughDialogs()
    {
        var path = TempFile(".smad-x.json");
        _dialogs.SavePath = path;
        await _vm.SaveToFileCommand.ExecuteAsync(null);
        Assert.True(File.Exists(path));
        Assert.Contains(_dialogs.Opened, o => o.StartsWith("Save:contoso.com_") && o.EndsWith(".smad-x.json"));

        _dialogs.NewDomain = new NewDomainRequest("corp.local", true);
        await _vm.CreateNewDomainCommand.ExecuteAsync(null);
        _dialogs.OpenPath = path;
        await _vm.LoadFromFileCommand.ExecuteAsync(null);

        Assert.Equal("contoso.com", _vm.RootObject!.Name);
        Assert.Equal(3, _vm.SitesTopology!.Sites.Count);
    }

    [Fact]
    public async Task Save_Cancelled_WritesNothing()
    {
        _dialogs.SavePath = null;
        var before = _vm.StatusMessage;

        await _vm.SaveToFileCommand.ExecuteAsync(null);

        Assert.Equal(before, _vm.StatusMessage);
    }

    [Fact]
    public async Task Load_InvalidFile_ShowsErrorAndKeepsDomain()
    {
        var path = TempFile(".json");
        await File.WriteAllTextAsync(path, "pas du json");
        _dialogs.OpenPath = path;

        await _vm.LoadFromFileCommand.ExecuteAsync(null);

        Assert.Equal("contoso.com", _vm.RootObject!.Name);
        var expectedPrefix = LocalizationService.Instance["Status.ErrorLoadInvalid"].Split("{0}")[0];
        Assert.StartsWith(expectedPrefix, _vm.StatusMessage);
        Assert.True(_vm.StatusMessage.Length > expectedPrefix.Length, "le détail de l'erreur doit suivre");
    }

    [Fact]
    public async Task Load_NewerFormat_ExplainsAndKeepsDomain()
    {
        var path = TempFile(".smad-x.json");
        await File.WriteAllTextAsync(path, $$"""{ "Version": {{ADRootDocument.CurrentVersion + 1}}, "Domain": { "Name": "futur.local", "Type": "Domain" } }""");
        _dialogs.OpenPath = path;

        await _vm.LoadFromFileCommand.ExecuteAsync(null);

        Assert.Equal("contoso.com", _vm.RootObject!.Name);
        Assert.Equal(
            string.Format(LocalizationService.Instance["Status.ErrorLoadTooNew"], ADRootDocument.CurrentVersion + 1, ADRootDocument.CurrentVersion),
            _vm.StatusMessage);
    }

    [Fact]
    public async Task Load_OldFormat_SaysItWasConverted()
    {
        var path = TempFile(".json");
        await new ADDataService().SaveToFileAsync(BuildSmallDomain(), path); // export JSON simple = format v1
        _dialogs.OpenPath = path;

        await _vm.LoadFromFileCommand.ExecuteAsync(null);

        Assert.Equal("contoso.com", _vm.RootObject!.Name);
        Assert.EndsWith(string.Format(LocalizationService.Instance["Status.Loaded.Migrated"], 1, ADRootDocument.CurrentVersion), _vm.StatusMessage);
    }

    [Fact]
    public async Task ExportToPowerShell_WritesScript()
    {
        var path = TempFile(".ps1");
        _dialogs.SavePath = path;

        await _vm.ExportToPowerShellCommand.ExecuteAsync(null);

        Assert.Contains("New-ADOrganizationalUnitIfNotExists", await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task ImportDescription_ReplacesSelectedDescription()
    {
        var path = TempFile(".md");
        await File.WriteAllTextAsync(path, "# Note de cours\n\nCompte de test.");
        _vm.SelectedNode = Node("Domain Admins");
        _dialogs.OpenPath = path;

        await _vm.ImportDescriptionCommand.ExecuteAsync(null);

        Assert.StartsWith("# Note de cours", _vm.SelectedObjectDescription);
        Assert.StartsWith("# Note de cours", Node("Domain Admins").Data.Description);
    }

    [Fact]
    public async Task ImportDescription_WithoutSelection_DoesNotOpenPicker()
    {
        _vm.SelectedNode = null;

        await _vm.ImportDescriptionCommand.ExecuteAsync(null);

        Assert.DoesNotContain("Open", _dialogs.Opened);
    }

    [Fact]
    public void CopyPaste_ClonesIntoSelectedContainer()
    {
        var ou = FindNode(_vm.RootNodes[0], "Users")!;
        _vm.SelectedNode = ou.Children.First(c => c.Data.Type == ADObjectType.User);
        var copied = _vm.SelectedNode.Data.Name;
        _vm.CopyObjectCommand.Execute(null);

        _vm.SelectedNode = ou;
        _vm.PasteObjectCommand.Execute(null);

        Assert.Equal($"{copied} (Copy)", _vm.SelectedNode!.Data.Name);
        Assert.Same(ou, _vm.SelectedNode.Parent);
        Assert.EndsWith(ou.Data.DistinguishedName, _vm.SelectedNode.Data.DistinguishedName);
    }

    [Fact]
    public void Delete_RemovesFromTreeAndModel()
    {
        var node = Node("Domain Admins");
        var parent = node.Parent!;
        _vm.SelectedNode = node;

        _vm.DeleteObjectCommand.Execute(null);

        Assert.DoesNotContain(node, parent.Children);
        Assert.DoesNotContain(node.Data, parent.Data.Children);
    }

    [Fact]
    public void ConfirmRename_UpdatesDistinguishedNamesOfChildren()
    {
        var ou = _vm.RootNodes[0].Children.First(c => c.Data.Type == ADObjectType.OrganizationalUnit && c.Children.Count > 0);
        _vm.SelectedNode = ou;

        _vm.ConfirmRenameCommand.Execute("Renamed");

        Assert.All(ou.Children, c => Assert.Contains("OU=Renamed,", c.Data.DistinguishedName));
    }

    [Fact]
    public void ConfirmRename_InvalidName_IsRefused()
    {
        var node = Node("Domain Admins");
        _vm.SelectedNode = node;

        _vm.ConfirmRenameCommand.Execute("a,b");

        Assert.Equal("Domain Admins", node.Data.Name);
    }

    [Fact]
    public void Search_HidesNonMatchingNodes()
    {
        _vm.SearchText = "krbtgt";

        Assert.True(Node("krbtgt").IsVisible);
        Assert.False(Node("Domain Admins").IsVisible);

        _vm.ClearSearchCommand.Execute(null);
        Assert.True(Node("Domain Admins").IsVisible);
    }

    [Fact]
    public async Task AddToGroup_OpensRelationsForSelectedObject()
    {
        _vm.SelectedNode = Node("krbtgt");

        await _vm.AddToGroupCommand.ExecuteAsync(null);

        Assert.NotNull(_dialogs.LastRelations);
    }

    [Fact]
    public async Task AddToGroup_WithoutSelection_OpensNothing()
    {
        _vm.SelectedNode = null;

        await _vm.AddToGroupCommand.ExecuteAsync(null);

        Assert.Null(_dialogs.LastRelations);
    }

    [Fact]
    public async Task WindowCommands_GoThroughDialogService()
    {
        await _vm.ShowDelegationsCommand.ExecuteAsync(null);
        await _vm.ShowSitesCommand.ExecuteAsync(null);
        await _vm.ShowTimelineCommand.ExecuteAsync(null);
        await _vm.ShowTierConfigurationCommand.ExecuteAsync(null);
        await _vm.ShowAboutCommand.ExecuteAsync(null);
        await _vm.ShowShortcutsCommand.ExecuteAsync(null);
        await _vm.ExportReportCommand.ExecuteAsync(null);
        _vm.ShowGraphViewCommand.Execute(null);

        Assert.Equal(
            ["Delegations:contoso.com", "Sites:3", "Timeline", "Tiers", "About", "Shortcuts", "Report:contoso.com", "Graph"],
            _dialogs.Opened);
    }
}
