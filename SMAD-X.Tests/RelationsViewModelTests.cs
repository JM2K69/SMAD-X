using SMADX.Models;
using SMADX.Services;
using SMADX.ViewModels;
using static SMADX.Tests.TestHelpers;

namespace SMADX.Tests;

/// <summary>Appartenances et imbrications de groupes dans la fenêtre Relations, selon les règles d'AD.</summary>
public class RelationsViewModelTests
{
    public RelationsViewModelTests()
    {
        TierConfigurationService.Instance.ResetToDefaults();
        LocalizationService.Instance.SetLanguage("fr-FR");
    }

    [Fact]
    public void AGmsa_CanJoinAGroup()
    {
        var domain = new ADDataService().CreateSampleStructure();
        var vm = new RelationsViewModel(domain) { MembershipSource = "svc-webapp", MembershipTarget = "GG-HelpDesk" };

        vm.AddMembershipCommand.Execute(null);

        Assert.Contains("GG-HelpDesk", Flatten(domain).Single(o => o.Name == "svc-webapp").MemberOf);
        Assert.Contains(vm.Memberships, m => m.Source == "svc-webapp" && m.SourceType == "GMSA");
    }

    [Fact]
    public void AUniversalGroup_CannotGoIntoAGlobalGroup()
    {
        var domain = new ADDataService().CreateSampleStructure();
        var vm = new RelationsViewModel(domain);
        vm.AvailableNestingSources.Single(g => g.Name == "Enterprise Admins").IsSelected = true; // universel
        vm.AvailableNestingTargets.Single(g => g.Name == "GG-HelpDesk").IsSelected = true;       // global

        vm.AddGroupNestingCommand.Execute(null);

        Assert.DoesNotContain("GG-HelpDesk", Flatten(domain).Single(o => o.Name == "Enterprise Admins").MemberOf);
        Assert.Contains("AGDLP", vm.StatusMessage);
    }

    [Fact]
    public void AGlobalGroup_CanGoIntoADomainLocalGroup()
    {
        var domain = new ADDataService().CreateSampleStructure();
        var vm = new RelationsViewModel(domain);
        vm.AvailableNestingSources.Single(g => g.Name == "GG-HelpDesk").IsSelected = true;   // global
        vm.AvailableNestingTargets.Single(g => g.Name == "DnsAdmins").IsSelected = true;     // domaine local

        vm.AddGroupNestingCommand.Execute(null);

        Assert.Contains("DnsAdmins", Flatten(domain).Single(o => o.Name == "GG-HelpDesk").MemberOf);
    }
}
