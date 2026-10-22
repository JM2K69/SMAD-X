using SMADX.Models;
using SMADX.Services;
using SMADX.Services.Labs;
using SMADX.ViewModels;
using static SMADX.Tests.TestHelpers;

namespace SMADX.Tests;

public class LabViewModelTests
{
    private readonly FakeDialogService _dialogs = new();
    private readonly MainWindowViewModel _main;

    public LabViewModelTests()
    {
        TierConfigurationService.Instance.ResetToDefaults();
        _main = new MainWindowViewModel(_dialogs);
    }

    private LabViewModel Open(string id)
    {
        _main.OpenBuiltInLabCommand.Execute(_main.BuiltInLabs.Single(l => l.Lab.Id == id));
        return _dialogs.LastLab!;
    }

    [Fact]
    public void BuiltInLabs_AreListedForTheMenu()
    {
        Assert.Equal(5, _main.BuiltInLabs.Count);
        Assert.All(_main.BuiltInLabs, l => Assert.Contains(" — ", l.Title));
    }

    [Fact]
    public void OpeningALab_LoadsItsStartDomain()
    {
        var lab = Open("02-kerberoasting");

        Assert.Equal("contoso.local", _main.RootObject!.Name);
        Assert.Contains(Flatten(_main.RootObject), o => o.Name == "svc-sql");
        Assert.Equal(5, lab.Objectives.Count);
        Assert.Equal(10, lab.MaxPoints);
    }

    [Fact]
    public void Check_GradesTheCurrentDomain_AndRestartResetsIt()
    {
        var lab = Open("05-hygiene-comptes");
        lab.CheckCommand.Execute(null);
        Assert.Equal(0, lab.Points);
        Assert.All(lab.Objectives, o => Assert.False(o.Passed));

        // L'apprenant désactive le compte Invité dans la fenêtre principale
        Flatten(_main.RootObject!).Single(o => o.Name == "Guest").AccountFlags |= ADAccountFlags.AccountDisabled;
        lab.CheckCommand.Execute(null);
        Assert.Equal(2, lab.Points);
        Assert.True(lab.Objectives.Single(o => o.Objective.Id == "o1").Passed);

        lab.RestartCommand.Execute(null);
        Assert.False(Flatten(_main.RootObject!).Single(o => o.Name == "Guest").AccountFlags.HasFlag(ADAccountFlags.AccountDisabled));
        Assert.Equal(0, lab.Points);
        Assert.All(lab.Objectives, o => Assert.Null(o.Passed));
    }

    [Fact]
    public async Task ExportResult_WritesAReportForTheTeacher()
    {
        var lab = Open("01-premier-audit");
        lab.StudentName = "Camille Dupont";
        lab.Objectives[0].ToggleHintCommand.Execute(null);
        var path = TempFile(".md");
        _dialogs.SavePath = path;

        await lab.ExportResultCommand.ExecuteAsync(null);

        var report = await File.ReadAllTextAsync(path);
        Assert.Contains("Camille Dupont", report);
        Assert.Contains("`01-premier-audit`", report);
        Assert.Contains("1 / 10", report);            // seul l'objectif « garde-fou » est acquis au départ
        Assert.Contains("✘", report);
        Assert.Contains(_dialogs.Opened, o => o.StartsWith("Save:01-premier-audit_Camille Dupont_"));
    }

    [Fact]
    public async Task OpeningAnInvalidLabFile_ExplainsWhy()
    {
        var path = TempFile(LabService.FileExtension);
        await File.WriteAllTextAsync(path, """{ "Version": 2, "Domain": {} }""");
        _dialogs.OpenPath = path;

        await _main.OpenLabFileCommand.ExecuteAsync(null);

        Assert.Contains("smadx-lab", _main.StatusMessage);
        Assert.Null(_dialogs.LastLab);
    }

    [Fact]
    public async Task CreateLabFromDomain_WritesALoadableLab()
    {
        var path = TempFile(LabService.FileExtension);
        _dialogs.SavePath = path;

        await _main.CreateLabFromDomainCommand.ExecuteAsync(null);

        var lab = new LabService().LoadFile(path);
        Assert.Equal("contoso.com", lab.Start.Domain.Name);
        Assert.NotEmpty(lab.Objectives);
        Assert.NotNull(lab.Start.TierConfigurations);
    }
}

public class DelegationEditingTests
{
    [Fact]
    public void AddAndRemoveDelegation_OnTheSelectedOu()
    {
        var domain = BuildSmallDomain();
        var vm = new DelegationsViewModel();
        vm.LoadFromTree(domain);
        var corp = Find(domain, "Corp");

        Assert.False(vm.AddDelegationCommand.CanExecute(null)); // ni OU ni mandataire
        vm.SelectedOUNode = corp;
        vm.NewTrustee = "Helpdesk";
        vm.NewCategory = RightCategory.PasswordReset;
        vm.AddDelegationCommand.Execute(null);

        var delegation = Assert.Single(corp.Delegations);
        Assert.Equal("ResetPassword", delegation.Right);
        Assert.Equal(TrusteeType.Group, delegation.TrusteeType);
        Assert.Equal(corp.DistinguishedName, delegation.TargetDN);
        Assert.Contains(delegation, vm.FilteredDelegations);
        Assert.Contains("Helpdesk", vm.Principals);

        vm.SelectedDelegation = delegation;
        vm.RemoveDelegationCommand.Execute(null);
        Assert.Empty(corp.Delegations);
        Assert.DoesNotContain(delegation, vm.AllDelegations);
    }

    [Fact]
    public void InheritedDelegation_IsNotRemovedHere()
    {
        var domain = BuildSmallDomain();
        var corp = Find(domain, "Corp");
        var inherited = new ADDelegation { TrusteeName = "Helpdesk", TargetDN = corp.DistinguishedName, Right = "GenericAll", RightCategory = RightCategory.FullControl, IsInherited = true };
        corp.Delegations.Add(inherited);
        var vm = new DelegationsViewModel();
        vm.LoadFromTree(domain);

        vm.SelectedDelegation = inherited;
        vm.RemoveDelegationCommand.Execute(null);

        Assert.Single(corp.Delegations);
    }
}
