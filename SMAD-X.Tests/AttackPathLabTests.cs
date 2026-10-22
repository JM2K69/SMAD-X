using SMADX.Models;
using SMADX.Services;
using SMADX.Services.Labs;
using SMADX.ViewModels;
using static SMADX.Tests.TestHelpers;

namespace SMADX.Tests;

/// <summary>
/// Les exercices « Chemins d'attaque » : en français comme en anglais, chacun n'est pas résolu au départ,
/// sa solution obtient tous les points, et chaque fausse solution fait échouer l'objectif qui l'interdit.
/// </summary>
public class AttackPathLabTests
{
    private static readonly DateTime Now = new(2027, 3, 15);
    private readonly LabService _service = new();

    public AttackPathLabTests() => TierConfigurationService.Instance.ResetToDefaults();

    /// <summary>Solution de chaque scénario, écrite sans les noms des objets (ils dépendent de la langue).</summary>
    private static readonly Dictionary<string, Action<ADObject>> Solutions = new()
    {
        ["01-kerberoasting"] = d =>
        {
            var svc = Flatten(d).Single(o => o.Type == ADObjectType.User && o.Name != "krbtgt" && o.ServicePrincipalNames.Count > 0);
            foreach (var group in Flatten(d).Where(o => svc.MemberOf.Contains(o.Name) && o.Type == ADObjectType.Group))
                group.MemberOf.Remove("Domain Admins");
        },
        ["02-asrep-roasting"] = d =>
        {
            var scan = Flatten(d).Single(o => o.AccountFlags.HasFlag(ADAccountFlags.DontRequirePreauth));
            scan.AccountFlags &= ~ADAccountFlags.DontRequirePreauth;
            scan.MemberOf.Remove("Backup Operators");
        },
        ["03-reset-password"] = RemoveRootFullControl,
        ["04-write-members"] = RemoveRootFullControl,
        ["05-gpo"] = d =>
        {
            foreach (var ou in Flatten(d).Where(o => o.Type == ADObjectType.OrganizationalUnit && o.Tier == "Tier 0"))
                ou.LinkedGPOs.Clear();
        },
        ["06-unconstrained-delegation"] = d =>
        {
            foreach (var c in Flatten(d).Where(o => o.Type == ADObjectType.Computer && o.Parent?.Name != "Domain Controllers"))
                c.AccountFlags &= ~ADAccountFlags.TrustedForDelegation;
        },
        ["07-constrained-delegation"] = d =>
        {
            foreach (var c in Flatten(d).Where(o => o.AllowedToDelegateTo.Count > 0))
            {
                c.AllowedToDelegateTo.Clear();
                c.AccountFlags &= ~ADAccountFlags.TrustedToAuthForDelegation;
            }
        },
        ["08-owner"] = d => Flatten(d).Single(o => o.Name == "DC02").Owner = "Domain Admins",
        ["09-full-control"] = d =>
        {
            foreach (var o in Flatten(d))
                RemoveWhere(o.Delegations, x => x.TrusteeName == "Authenticated Users");
            RemoveRootFullControl(d);
        },
        ["10-attack-chain"] = d =>
        {
            foreach (var u in Flatten(d).Where(o => o.Type == ADObjectType.User))
                u.MemberOf.Remove("DnsAdmins");
        },
        ["11-empty-password"] = d =>
        {
            // Guest a aussi PASSWD_NOTREQD, comme dans un vrai AD, mais il est désactivé
            var temp = Flatten(d).Single(o => o.AccountFlags.HasFlag(ADAccountFlags.PasswordNotRequired) && !o.AccountFlags.HasFlag(ADAccountFlags.AccountDisabled));
            temp.AccountFlags &= ~ADAccountFlags.PasswordNotRequired;
            temp.MemberOf.Remove("Account Operators");
        },
        ["12-nested-groups"] = d =>
        {
            foreach (var g in Flatten(d).Where(o => o.Type == ADObjectType.Group && o.Name.StartsWith("GG-")))
                g.MemberOf.Remove("DnsAdmins");
        }
    };

    private static void RemoveWhere(System.Collections.ObjectModel.ObservableCollection<ADDelegation> delegations, Func<ADDelegation, bool> predicate)
    {
        foreach (var d in delegations.Where(predicate).ToList())
            delegations.Remove(d);
    }

    /// <summary>Contrôle total délégué sur la racine du domaine à un groupe de l'organisation.</summary>
    private static void RemoveRootFullControl(ADObject d) =>
        RemoveWhere(d.Delegations, x => !x.IsInherited && x.RightCategory == RightCategory.FullControl && x.TrusteeName.StartsWith("GG-"));

    public static TheoryData<string, string> Labs()
    {
        var data = new TheoryData<string, string>();
        foreach (var id in Solutions.Keys)
        {
            data.Add("fr-FR", id);
            data.Add("en-US", id);
        }
        return data;
    }

    private LabDefinition Lab(string culture, string id) =>
        _service.BuiltInLabs().Single(l => l.Id == (culture == "fr-FR" ? "chemin-" : "path-") + id);

    private (LabDefinition Lab, ADObject Start, ADObject Working) Prepare(string culture, string id)
    {
        var lab = Lab(culture, id);
        return (lab, _service.PrepareStart(lab, Now).Domain, _service.PrepareStart(lab, Now).Domain);
    }

    private static LabResult Grade(LabDefinition lab, ADObject start, ADObject working) => new LabGrader().Grade(lab, start, working, Now);

    private static string Failures(LabResult result) =>
        string.Join("\n", result.Objectives.Where(o => !o.Passed).Select(o => $"{o.Objective.Id} : {o.Feedback}"));

    [Fact]
    public void AttackPathLabs_AreTwelvePerLanguage_WithDistinctDomains()
    {
        var labs = _service.BuiltInLabs().Where(l => l.Category == MainWindowViewModel.AttackPathCategory).ToList();

        Assert.Equal(12, labs.Count(l => l.Language == "fr-FR"));
        Assert.Equal(12, labs.Count(l => l.Language == "en-US"));
        Assert.Equal(24, labs.Select(l => l.Start.Domain.Name).Distinct().Count());
    }

    [Theory]
    [MemberData(nameof(Labs))]
    public void Lab_StatementGivesContextButNotTheAnswer(string culture, string id)
    {
        var lab = Lab(culture, id);
        var statement = lab.Statement.Resolve(culture);

        Assert.Contains(culture == "fr-FR" ? "## Contexte" : "## Context", statement);
        Assert.Contains(culture == "fr-FR" ? "## Votre mission" : "## Your mission", statement);
        Assert.DoesNotContain(culture == "fr-FR" ? "## Le corriger" : "## Fix it", statement);
        Assert.DoesNotContain(culture == "fr-FR" ? "## Le chemin" : "## The path", statement);
        Assert.All(lab.Objectives, o => Assert.False(string.IsNullOrWhiteSpace(o.Hint.Resolve(culture))));
    }

    [Theory]
    [MemberData(nameof(Labs))]
    public void Lab_IsNotSolvedAtStart_ButGuardsAlreadyHold(string culture, string id)
    {
        var (lab, start, working) = Prepare(culture, id);

        var result = Grade(lab, start, working);

        Assert.False(result.Completed);
        foreach (var o in result.Objectives)
        {
            var isGuard = o.Objective.Check.Type is LabCheckType.ObjectExists or LabCheckType.MemberOf or LabCheckType.DelegationExists;
            Assert.True(o.Passed == isGuard, $"{o.Objective.Id} ({o.Objective.Check.Type}) : {o.Feedback}");
        }
    }

    [Theory]
    [MemberData(nameof(Labs))]
    public void Lab_SolutionGetsAllPoints(string culture, string id)
    {
        var (lab, start, working) = Prepare(culture, id);

        Solutions[id](working);
        var result = Grade(lab, start, working);

        Assert.True(result.Completed, Failures(result));
        Assert.Equal(lab.MaxPoints, result.Points);
    }

    /// <summary>Supprimer le compte, vider le groupe, retirer la délégation légitime : l'objectif qui le protège échoue.</summary>
    [Theory]
    [MemberData(nameof(Labs))]
    public void Lab_BruteForceShortcuts_AreRefused(string culture, string id)
    {
        foreach (var guard in Lab(culture, id).Objectives.Where(o => o.Check.Type is LabCheckType.ObjectExists or LabCheckType.MemberOf or LabCheckType.DelegationExists))
        {
            var (lab, start, working) = Prepare(culture, id);
            Solutions[id](working);
            var check = guard.Check;
            var target = Flatten(working).First(o => o.Name == (check.Type == LabCheckType.DelegationExists ? check.Target : check.Object));
            switch (check.Type)
            {
                case LabCheckType.ObjectExists:
                    target.Parent!.Children.Remove(target);
                    break;
                case LabCheckType.MemberOf:
                    target.MemberOf.Remove(check.Group!);
                    break;
                case LabCheckType.DelegationExists:
                    RemoveWhere(target.Delegations, x => x.TrusteeName == check.Trustee);
                    break;
            }

            var result = Grade(lab, start, working);

            Assert.False(result.Objectives.Single(o => o.Objective.Id == guard.Id).Passed, $"{guard.Id} aurait dû échouer");
        }
    }

    [Theory]
    [InlineData("02-asrep-roasting")]
    [InlineData("10-attack-chain")]
    public void CuttingOnlyTheVisiblePath_LeavesOtherSourcesDangerous(string id)
    {
        foreach (var culture in new[] { "fr-FR", "en-US" })
        {
            var (lab, start, working) = Prepare(culture, id);
            if (id == "02-asrep-roasting")
                Flatten(working).Single(o => o.AccountFlags.HasFlag(ADAccountFlags.DontRequirePreauth)).AccountFlags &= ~ADAccountFlags.DontRequirePreauth;
            else
                Flatten(working).Single(o => o.Type == ADObjectType.User && o.Name != "krbtgt" && o.ServicePrincipalNames.Count > 0).ServicePrincipalNames.Clear();

            var result = Grade(lab, start, working);

            Assert.True(result.Objectives.Single(o => o.Objective.Id == "o1").Passed, Failures(result));
            var o2 = result.Objectives.Single(o => o.Objective.Id == "o2");
            Assert.False(o2.Passed);
            Assert.Contains("→", o2.Feedback); // le chemin restant est montré à l'apprenant
        }
    }

    [Fact]
    public void Menu_ShowsAttackPathLabsInTheInterfaceLanguage()
    {
        var loc = LocalizationService.Instance;
        var previous = loc.CurrentCulture.Name;
        try
        {
            var vm = new MainWindowViewModel(new FakeDialogService());
            loc.SetLanguage("fr-FR");
            Assert.All(vm.AttackPathLabs, l => Assert.StartsWith("chemin-", l.Lab.Id));
            Assert.Equal(12, vm.AttackPathLabs.Count);
            Assert.DoesNotContain(vm.BuiltInLabs, l => l.Lab.Category != null);

            loc.SetLanguage("en-US");
            Assert.All(vm.AttackPathLabs, l => Assert.StartsWith("path-", l.Lab.Id));
            Assert.Equal(12, vm.AttackPathLabs.Count);
        }
        finally
        {
            loc.SetLanguage(previous);
        }
    }
}
