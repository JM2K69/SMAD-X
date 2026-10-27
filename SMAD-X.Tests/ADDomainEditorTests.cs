using SMADX.Models;
using SMADX.Services;
using static SMADX.Tests.TestHelpers;

namespace SMADX.Tests;

/// <summary>Renommer et supprimer sans casser les références par nom (appartenances, PSO, GPO, délégations…).</summary>
public class ADDomainEditorTests
{
    public ADDomainEditorTests() => LocalizationService.Instance.SetLanguage("fr-FR");

    /// <summary>contoso.com / Corp (jdoe, Helpdesk, Servers / SRV01), une GPO, un PSO et un site.</summary>
    private static (ADObject Domain, ADSitesTopology Sites) BuildLinkedDomain()
    {
        var domain = BuildSmallDomain();
        var corp = Find(domain, "Corp");
        var servers = Add(corp, new ADObject("Servers", ADObjectType.OrganizationalUnit));
        Add(servers, new ADObject("SRV01", ADObjectType.Computer));
        var gpo = Add(domain, new ADObject("GPO-Securite", ADObjectType.Policy));
        var pso = Add(domain, new ADObject("PSO-Admins", ADObjectType.PasswordSettingsObject));

        Find(domain, "jdoe").MemberOf.Add("Helpdesk");
        Find(domain, "SRV01").Owner = "Helpdesk";
        corp.LinkedGPOs.Add(gpo.Name);
        pso.PSOAppliesTo.Add("Helpdesk");
        servers.Delegations.Add(new ADDelegation { TrusteeName = "Helpdesk", Right = "GenericAll", TargetDN = servers.DistinguishedName });

        var sites = new ADSitesTopology();
        sites.Sites.Add(new ADSite { Name = "Paris", DomainControllers = { "SRV01.contoso.com" }, LinkedGPOs = { gpo.Name } });
        return (domain, sites);
    }

    [Fact]
    public void RenamingAGroup_RewritesEveryReferenceToIt()
    {
        var (domain, sites) = BuildLinkedDomain();

        var error = ADDomainEditor.Rename(domain, Find(domain, "Helpdesk"), "GG-HelpDesk", sites, out var references);

        Assert.Null(error);
        Assert.Equal(4, references);
        Assert.Equal(["GG-HelpDesk"], Find(domain, "jdoe").MemberOf);
        Assert.Equal("GG-HelpDesk", Find(domain, "SRV01").Owner);
        Assert.Equal(["GG-HelpDesk"], Find(domain, "PSO-Admins").PSOAppliesTo);
        Assert.Equal("GG-HelpDesk", Assert.Single(Find(domain, "Servers").Delegations).TrusteeName);
    }

    [Fact]
    public void RenamingAnOu_UpdatesTheDnsOfItsSubtreeAndItsDelegations()
    {
        var (domain, sites) = BuildLinkedDomain();

        ADDomainEditor.Rename(domain, Find(domain, "Corp"), "Siege", sites, out _);

        Assert.Equal("CN=SRV01,OU=Servers,OU=Siege,DC=contoso,DC=com", Find(domain, "SRV01").DistinguishedName);
        Assert.Equal("OU=Servers,OU=Siege,DC=contoso,DC=com", Assert.Single(Find(domain, "Servers").Delegations).TargetDN);
    }

    [Fact]
    public void RenamingAGpoOrADomainController_UpdatesOusAndSites()
    {
        var (domain, sites) = BuildLinkedDomain();

        ADDomainEditor.Rename(domain, Find(domain, "GPO-Securite"), "GPO-Durcissement", sites, out _);
        ADDomainEditor.Rename(domain, Find(domain, "SRV01"), "DC02", sites, out _);

        Assert.Equal(["GPO-Durcissement"], Find(domain, "Corp").LinkedGPOs);
        Assert.Equal(["GPO-Durcissement"], sites.Sites[0].LinkedGPOs);
        Assert.Equal(["DC02.contoso.com"], sites.Sites[0].DomainControllers);
    }

    [Fact]
    public void ReferencesOfANamesake_AreLeftAlone()
    {
        var (domain, sites) = BuildLinkedDomain();
        // Un second « Helpdesk », ailleurs : on ne sait pas lequel la référence désigne
        Add(Find(domain, "Servers"), new ADObject("Helpdesk", ADObjectType.Group));

        ADDomainEditor.Rename(domain, Find(domain, "Corp").Children.First(c => c.Name == "Helpdesk"), "GG-HelpDesk", sites, out var references);

        Assert.Equal(0, references);
        Assert.Equal(["Helpdesk"], Find(domain, "jdoe").MemberOf);
    }

    [Fact]
    public void ChangingOnlyTheCase_KeepsTheReference()
    {
        var (domain, sites) = BuildLinkedDomain();

        ADDomainEditor.Rename(domain, Find(domain, "Helpdesk"), "HELPDESK", sites, out _);

        Assert.Equal(["HELPDESK"], Find(domain, "jdoe").MemberOf);
    }

    [Theory]
    [InlineData("jdoe")]                    // un frère porte déjà ce nom
    [InlineData("JDOE")]
    [InlineData("Compta\rRemove-ADUser")]   // caractère de contrôle
    [InlineData("a,b")]
    [InlineData("   ")]
    public void InvalidOrTakenNames_AreRefused(string name)
    {
        var (domain, sites) = BuildLinkedDomain();

        var error = ADDomainEditor.Rename(domain, Find(domain, "Helpdesk"), name, sites, out _);

        Assert.NotNull(error);
        Assert.Equal("Helpdesk", Find(domain, "Corp").Children.Single(c => c.Type == ADObjectType.Group).Name);
    }

    [Fact]
    public void AnOuAndAContainer_MayShareANameSinceTheirDnsDiffer()
    {
        var domain = BuildSmallDomain();
        Add(domain, new ADObject("Users", ADObjectType.Container));

        Assert.False(ADDomainEditor.HasSiblingConflict(domain, ADObjectType.OrganizationalUnit, "Users"));
        Assert.True(ADDomainEditor.HasSiblingConflict(domain, ADObjectType.Group, "users"));
    }

    [Fact]
    public void UniqueChildName_AddsANumber()
    {
        var domain = BuildSmallDomain();
        Add(domain, new ADObject("NewOU", ADObjectType.OrganizationalUnit));
        Add(domain, new ADObject("NewOU 2", ADObjectType.OrganizationalUnit));

        Assert.Equal("NewOU 3", ADDomainEditor.UniqueChildName(domain, ADObjectType.OrganizationalUnit, "NewOU"));
        Assert.Equal("Other", ADDomainEditor.UniqueChildName(domain, ADObjectType.OrganizationalUnit, "Other"));
    }

    [Fact]
    public void DeletingAGroupOrAGpo_RemovesTheReferencesToIt()
    {
        var (domain, sites) = BuildLinkedDomain();
        var corp = Find(domain, "Corp");
        var helpdesk = Find(domain, "Helpdesk");
        var gpo = Find(domain, "GPO-Securite");
        corp.Children.Remove(helpdesk);
        domain.Children.Remove(gpo);

        var removed = ADDomainEditor.RemoveReferences(domain, helpdesk, sites) + ADDomainEditor.RemoveReferences(domain, gpo, sites);

        Assert.Equal(6, removed);
        Assert.Empty(Find(domain, "jdoe").MemberOf);
        Assert.Null(Find(domain, "SRV01").Owner);
        Assert.Empty(Find(domain, "PSO-Admins").PSOAppliesTo);
        Assert.Empty(Find(domain, "Servers").Delegations);
        Assert.Empty(corp.LinkedGPOs);
        Assert.Empty(sites.Sites[0].LinkedGPOs);
    }

    [Fact]
    public void DeletingAnOu_RemovesTheDomainControllersItHeldFromTheSites()
    {
        var (domain, sites) = BuildLinkedDomain();
        var servers = Find(domain, "Servers");
        servers.Parent!.Children.Remove(servers);

        ADDomainEditor.RemoveReferences(domain, servers, sites);

        Assert.Empty(sites.Sites[0].DomainControllers);
    }
}
