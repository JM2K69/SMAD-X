using SMADX.Services.Labs;
using SMARX.Labs;
using SMARX.Models;
using SMARX.Services.Labs;

namespace SMARX.Tests;

/// <summary>
/// Les recettes de SMAR-X : pour chaque recette, chaque plateforme qu'elle permet et trois entreprises (quatre services
/// dont une Direction ; trois services aux noms accentués ; deux services), l'exercice généré échoue au départ, ses gardes
/// tiennent, et sa solution atteint tous les objectifs. Les solutions de facilité sont rattrapées.
/// </summary>
public class SmarxRecipeTests
{
    private const string Fr = "fr-FR";

    private static SmarxOrganization Org(int index, SmarxPlatform platform) => index switch
    {
        0 => SmarxOrganization.Sample(platform),
        1 => new SmarxOrganization("Atelier Martin", ["Comptabilité", "Production", "Marketing"], platform),
        _ => new SmarxOrganization("Acme", ["Compta", "Direction"], platform)
    };

    public static TheoryData<string, SmarxPlatform, int> Cases()
    {
        var data = new TheoryData<string, SmarxPlatform, int>();
        foreach (var recipe in SmarxRecipes.All)
        foreach (var platform in recipe.Platforms)
        for (var org = 0; org < 3; org++)
            data.Add(recipe.Id, platform, org);
        return data;
    }

    private static SmarxRecipeResult Create(string id, SmarxPlatform platform, int org = 0) =>
        SmarxRecipes.Create(SmarxRecipes.Find(id)!, Org(org, platform), Fr);

    [Theory]
    [MemberData(nameof(Cases))]
    public void Recipe_FailsAtStart_KeepsItsGuards_AndItsSolutionMeetsEverything(string id, SmarxPlatform platform, int org)
    {
        var created = Create(id, platform, org);

        var issues = SmarxLabValidator.Check(created.Lab, created.Solution, Fr);
        Assert.True(issues.All(i => i.Level != LabIssueLevel.Error), string.Join("\n", issues.Select(i => $"{i.ObjectiveId} {i.Message}")));
        Assert.Contains(issues, i => i.Level == LabIssueLevel.Ok);

        // Le fichier d'exercice se relit à l'identique, et l'énoncé existe dans les deux langues
        var parsed = SmarxLabService.Parse(SmarxLabService.Serialize(created.Lab));
        Assert.Equal(created.Lab.MaxPoints, parsed.MaxPoints);
        Assert.Contains("## Le besoin", created.Lab.Statement["fr-FR"]);
        Assert.Contains("## The need", created.Lab.Statement["en-US"]);
        Assert.Contains("```", created.TeacherNotes["fr-FR"]);
    }

    [Fact]
    public void Organization_NamesItsDomainGroupsAndFolders_WithoutAccents()
    {
        var org = new SmarxOrganization("Atelier Martin", ["Comptabilité", "Production"], SmarxPlatform.Both);
        Assert.Equal("ATELIERMARTIN", org.Domain);
        var compta = org.Services[0];
        Assert.Equal(("Comptabilité", "G_Comptabilite", "DL_Comptabilite_LM", @"D:\Partages\Comptabilite", "/srv/comptabilite"),
            (compta.Display, compta.Global, compta.ModifyGroup, compta.WindowsFolder, compta.LinuxFolder));
        Assert.Equal("Il faut au moins 2 services.", SmarxOrganization.Problem(["Compta"], Fr));
        Assert.Equal("Le service Compta apparaît deux fois.", SmarxOrganization.Problem(["Compta", "compta"], Fr));
    }

    /// <summary>L'arborescence : mettre les comptes directement dans les groupes de domaine local, la garde des comptes l'annule.</summary>
    [Fact]
    public void Tree_AccountsInDomainLocalGroups_AreCancelledByTheGuard()
    {
        var created = Create("SMX-ARBO", SmarxPlatform.Windows);
        var work = SmarxLabGrader.Clone(created.Solution);
        var host = work.Windows[0];
        foreach (var g in host.Groups.Where(g => g.Scope == WindowsGroupScope.Global))
        {
            var dls = g.Groups.ToList();
            g.Groups.Clear();
            foreach (var u in host.Users.Where(u => u.Groups.Contains(g.Name))) u.Groups.AddRange(dls);
        }

        var result = SmarxLabGrader.Grade(created.Lab, work, null, Fr);

        Assert.False(result.Objectives.Single(o => o.Objective.Check.Type == SmarxCheckType.AccountUnchanged).Passed);
        Assert.True(result.Objectives.Single(o => o.Objective.Check.Type == SmarxCheckType.ExactAccess).Annulled);
    }

    /// <summary>L'arborescence : les droits NTFS donnés aux groupes globaux, la garde AGDLP l'annule.</summary>
    [Fact]
    public void Tree_GlobalGroupsInTheAcls_AreCancelledByTheAgdlpGuard()
    {
        var created = Create("SMX-ARBO", SmarxPlatform.Windows);
        var work = SmarxLabGrader.Clone(created.Solution);
        var host = work.Windows[0];
        var org = SmarxOrganization.Sample();
        foreach (var s in org.Services)
        {
            var folder = host.Find(s.WindowsFolder)!;
            folder.Aces.RemoveAll(a => a.Trustee.StartsWith("DL_"));
            folder.Aces.AddRange(org.On(s).Select(r => NtfsAce.Allow(r.Who.Global, r.Modify ? "M" : "RX")));
        }
        foreach (var g in host.Groups) g.Groups.Clear();

        var result = SmarxLabGrader.Grade(created.Lab, work, null, Fr);

        Assert.False(result.Objectives.Single(o => o.Objective.Check.Rule == SmarxHygieneRule.DomainLocalOnly).Passed);
        Assert.True(result.Objectives.Single(o => o.Objective.Check.Type == SmarxCheckType.ExactAccess).Annulled);
    }

    /// <summary>Le dossier ouvert à tous : le partage en Tout le monde Contrôle total, même avec NTFS refermé, reste refusé.</summary>
    [Fact]
    public void Open_ShareLeftToEveryone_FailsTheShareRule()
    {
        var created = Create("SMX-OUVERT", SmarxPlatform.Windows);
        var work = SmarxLabGrader.Clone(created.Solution);
        work.Windows[0].Share(SmarxOrganization.WindowsShare)!.Permissions = [ShareAce.Allow(WellKnown.Everyone, ShareRight.FullControl)];

        var result = SmarxLabGrader.Grade(created.Lab, work, null, Fr);

        Assert.False(result.Objectives.Single(o => o.Objective.Check.Rule == SmarxHygieneRule.ShareStandard).Passed);
        Assert.True(result.Objectives.Single(o => o.Objective.Check.Type == SmarxCheckType.ExactAccess).Passed);
    }

    /// <summary>Le mauvais groupe : mettre le compte dans les deux groupes ne suffit pas, il garde l'accès de l'autre service.</summary>
    [Fact]
    public void WrongGroup_KeepingBothGroups_IsNotExact()
    {
        var created = Create("SMX-GROUPE", SmarxPlatform.Windows);
        var work = SmarxLabGrader.Clone(created.Lab.Start);
        var org = SmarxOrganization.Sample();
        var who = work.Windows[0].User(org.Services[0].Users[1])!;
        who.Groups.Add(org.Services[0].Global);

        var result = SmarxLabGrader.Grade(created.Lab, work, null, Fr);

        Assert.True(result.Objectives.Single(o => o.Objective.Check.Type == SmarxCheckType.AccessAllowed).Passed);
        Assert.False(result.Objectives.Single(o => o.Objective.Check.Type == SmarxCheckType.ExactAccess).Passed);
    }

    /// <summary>L'audit : fermer le dépôt au lieu de lui mettre le sticky bit, la garde du dépôt l'annule.</summary>
    [Fact]
    public void Audit_ClosingTheDropFolder_IsCancelledByItsGuard()
    {
        var created = Create("SMX-AUDIT", SmarxPlatform.Linux);
        var work = SmarxLabGrader.Clone(created.Solution);
        work.Linux[0].Find("/srv/depot")!.Mode = "0770";

        var result = SmarxLabGrader.Grade(created.Lab, work, null, Fr);

        Assert.Contains(result.Objectives, o => o.Objective.Guard && !o.Passed && o.Objective.Check.Path == "/srv/depot");
        Assert.True(result.Objectives.Single(o => o.Objective.Check.Type == SmarxCheckType.AccessDenied).Annulled);
    }

    [Fact]
    public void UnsupportedPlatform_IsRefused()
    {
        Assert.Throws<ArgumentException>(() => Create("SMX-AUDIT", SmarxPlatform.Windows));
        Assert.Throws<ArgumentException>(() => Create("SMX-PARTAGE", SmarxPlatform.Linux));
    }
}
