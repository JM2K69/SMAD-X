using System.Text.Json;
using SMADX.Models;
using SMADX.Services;
using SMADX.ViewModels;
using static SMADX.Tests.TestHelpers;

namespace SMADX.Tests;

public class UndoHistoryTests
{
    [Fact]
    public void UndoRedo_WalkThroughStates()
    {
        var history = new UndoHistory();
        history.Reset("a");
        history.Record("b");
        history.Record("c");

        Assert.Equal("b", history.Undo());
        Assert.Equal("a", history.Undo());
        Assert.Null(history.Undo());
        Assert.False(history.CanUndo);
        Assert.Equal("b", history.Redo());
        Assert.Equal("c", history.Redo());
        Assert.Null(history.Redo());
    }

    [Fact]
    public void Record_SameState_IsIgnored()
    {
        var history = new UndoHistory();
        history.Reset("a");

        Assert.False(history.Record("a"));
        Assert.False(history.CanUndo);
    }

    [Fact]
    public void Record_AfterUndo_DropsRedoStates()
    {
        var history = new UndoHistory();
        history.Reset("a");
        history.Record("b");
        history.Undo();

        history.Record("x");

        Assert.False(history.CanRedo);
        Assert.Equal("a", history.Undo());
    }

    [Fact]
    public void Capacity_ForgetsOldestStates()
    {
        var history = new UndoHistory(capacity: 3);
        history.Reset("1");
        foreach (var s in new[] { "2", "3", "4", "5" })
            history.Record(s);

        Assert.Equal("4", history.Undo());
        Assert.Equal("3", history.Undo());
        Assert.False(history.CanUndo);
    }

    [Fact]
    public void Reset_ClearsEverything()
    {
        var history = new UndoHistory();
        history.Reset("a");
        history.Record("b");

        history.Reset("z");

        Assert.Equal("z", history.Current);
        Assert.False(history.CanUndo);
        Assert.False(history.CanRedo);
    }
}

public class MainWindowHistoryTests
{
    private readonly FakeDialogService _dialogs = new();
    private readonly MainWindowViewModel _vm;

    public MainWindowHistoryTests() => _vm = new MainWindowViewModel(_dialogs);

    private ADTreeNode? Node(string name) => Find(_vm.RootNodes[0], name);

    private static IEnumerable<ADTreeNode> AllNodes(ADTreeNode node) => new[] { node }.Concat(node.Children.SelectMany(AllNodes));

    private static ADTreeNode? Find(ADTreeNode node, string name) =>
        node.Data.Name == name ? node : node.Children.Select(c => Find(c, name)).FirstOrDefault(n => n != null);

    [Fact]
    public void Startup_HasNothingToUndo()
    {
        Assert.False(_vm.UndoCommand.CanExecute(null));
        Assert.False(_vm.RedoCommand.CanExecute(null));
    }

    [Fact]
    public void Delete_ThenUndo_RestoresObject_ThenRedoDeletesAgain()
    {
        // Le domaine d'exemple a plusieurs « Domain Admins » : on suit celui-ci par son identifiant
        var id = Node("Domain Admins")!.Data.Id;
        bool InTree() => _vm.RootNodes.SelectMany(AllNodes).Any(n => n.Data.Id == id);
        bool InModel() => Flatten(_vm.RootObject!).Any(o => o.Id == id);

        _vm.SelectedNode = Node("Domain Admins");
        _vm.DeleteObjectCommand.Execute(null);
        Assert.False(InTree());
        Assert.True(_vm.UndoCommand.CanExecute(null));

        _vm.UndoCommand.Execute(null);

        Assert.True(InTree());
        Assert.True(InModel());
        Assert.True(_vm.RedoCommand.CanExecute(null));

        _vm.RedoCommand.Execute(null);

        Assert.False(InTree());
        Assert.False(InModel());
    }

    [Fact]
    public void Undo_RestoresParentReferencesAndKeepsSelection()
    {
        var users = Node("Users")!;
        _vm.SelectedNode = users;
        _vm.ConfirmRenameCommand.Execute("Comptes");

        _vm.UndoCommand.Execute(null);

        Assert.Equal("Users", _vm.SelectedNode?.Data.Name); // le DN a changé avec le renommage, la sélection suit l'objet
        Assert.All(Flatten(_vm.RootObject!).Skip(1), o => Assert.NotNull(o.Parent));
        Assert.Same(_vm.RootObject, _vm.RootNodes[0].Data);
    }

    [Fact]
    public void PendingDetailEdit_IsUndoneToo()
    {
        var node = Node("Domain Admins")!;
        _vm.SelectedNode = node;
        var before = node.Data.Description;
        node.Data.Description = "modifié dans le panneau";

        Assert.True(_vm.UndoCommand.CanExecute(null));
        _vm.UndoCommand.Execute(null);

        Assert.Equal(before, Node("Domain Admins")!.Data.Description);
    }

    [Fact]
    public async Task NewDomain_ResetsHistory()
    {
        _vm.SelectedNode = Node("Domain Admins");
        _vm.DeleteObjectCommand.Execute(null);
        _dialogs.NewDomain = new NewDomainRequest("corp.local", true);

        await _vm.CreateNewDomainCommand.ExecuteAsync(null);

        Assert.False(_vm.UndoCommand.CanExecute(null));
    }

    [Fact]
    public void ShowComparison_ComparesWithStateAtOpening()
    {
        _vm.SelectedNode = Node("Domain Admins");
        _vm.DeleteObjectCommand.Execute(null);

        _vm.ShowComparisonCommand.Execute(null);

        Assert.Contains(_dialogs.LastComparison!.Changes, c => c.ObjectName == "Domain Admins" && c.Icon == "➖");
    }

    [Fact]
    public void MarkBaseline_MovesTheReferencePoint()
    {
        _vm.SelectedNode = Node("Domain Admins");
        _vm.DeleteObjectCommand.Execute(null);

        _vm.MarkBaselineCommand.Execute(null);
        _vm.ShowComparisonCommand.Execute(null);

        Assert.Empty(_dialogs.LastComparison!.Changes);
    }
}

public class ComparisonViewModelTests
{
    private static readonly DateTime Now = DateTime.Now;

    public ComparisonViewModelTests() => TierConfigurationService.Instance.ResetToDefaults();

    private static string Serialize(ADObject domain) =>
        JsonSerializer.Serialize(new ADRootDocument { Version = ADRootDocument.CurrentVersion, Domain = domain }, ADDataService.SerializerOptions);

    private static ADObject WithAsRepUser()
    {
        var domain = BuildSmallDomain();
        var jdoe = Flatten(domain).Single(o => o.Name == "jdoe");
        jdoe.AccountFlags = ADAccountFlags.DontRequirePreauth;
        jdoe.PwdLastSet = Now.AddDays(-10);
        jdoe.LastLogonTimestamp = Now.AddDays(-1);
        return domain;
    }

    [Fact]
    public void FixingAFinding_ShowsItAsFixedAndRaisesScore()
    {
        var before = WithAsRepUser();
        var after = WithAsRepUser();
        Flatten(after).Single(o => o.Name == "jdoe").AccountFlags = ADAccountFlags.None;

        var vm = new ComparisonViewModel(() => Serialize(before), () => after);

        Assert.Contains(vm.FixedFindings, f => f.RuleId == "KRB-04" && f.ObjectName == "jdoe");
        Assert.DoesNotContain(vm.NewFindings, f => f.RuleId == "KRB-04");
        Assert.True(vm.ScoreAfter > vm.ScoreBefore);
        Assert.Equal("#27AE60", vm.ScoreTrendColor);
        Assert.Contains(vm.Changes, c => c.ObjectName == "jdoe" && c.Icon == "✏");
    }

    [Fact]
    public void IntroducingAWeakness_ShowsItAsNew()
    {
        var before = BuildSmallDomain();
        var after = WithAsRepUser();

        var vm = new ComparisonViewModel(() => Serialize(before), () => after);

        Assert.Contains(vm.NewFindings, f => f.RuleId == "KRB-04");
        Assert.Empty(vm.FixedFindings);
    }

    [Fact]
    public void SameState_HasNoDifference()
    {
        var domain = WithAsRepUser();

        var vm = new ComparisonViewModel(() => Serialize(domain), () => domain);

        Assert.Empty(vm.Changes);
        Assert.Empty(vm.FixedFindings);
        Assert.Empty(vm.NewFindings);
        Assert.Equal(vm.ScoreBefore, vm.ScoreAfter);
    }

    [Fact]
    public void Refresh_PicksUpLaterChanges()
    {
        var before = WithAsRepUser();
        var after = WithAsRepUser();
        var vm = new ComparisonViewModel(() => Serialize(before), () => after);
        Assert.Empty(vm.FixedFindings);

        Flatten(after).Single(o => o.Name == "jdoe").AccountFlags = ADAccountFlags.None;
        vm.RefreshCommand.Execute(null);

        Assert.Contains(vm.FixedFindings, f => f.RuleId == "KRB-04");
    }
}
