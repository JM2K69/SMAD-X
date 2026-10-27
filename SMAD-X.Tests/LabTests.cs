using SMADX.Models;
using SMADX.Services;
using SMADX.Services.Audit;
using SMADX.Services.Labs;
using static SMADX.Tests.TestHelpers;

namespace SMADX.Tests;

/// <summary>Les exercices fournis : chacun se charge, n'est pas résolu au départ, et sa solution obtient tous les points.</summary>
public class BuiltInLabTests
{
    private static readonly DateTime Now = new(2027, 3, 15); // longtemps après la date de référence des exercices
    private readonly LabService _service = new();

    public BuiltInLabTests() => TierConfigurationService.Instance.ResetToDefaults();

    private LabDefinition Lab(string id) => _service.BuiltInLabs().Single(l => l.Id == id);

    private (LabResult Start, LabResult Solved) Play(string id, Action<ADObject> solution)
    {
        var lab = Lab(id);
        var start = _service.PrepareStart(lab, Now).Domain;
        var working = _service.PrepareStart(lab, Now).Domain;
        var grader = new LabGrader();
        var before = grader.Grade(lab, start, working, Now);
        solution(working);
        return (before, grader.Grade(lab, start, working, Now));
    }

    private static void AssertSolved(LabResult result) =>
        Assert.True(result.Completed, string.Join("\n", result.Objectives.Where(o => !o.Passed).Select(o => $"{o.Objective.Id} : {o.Feedback}")));

    [Fact]
    public void BuiltInLabs_AreFiveAndHaveTextsInBothLanguages()
    {
        // Exercices généraux, bilingues ; la série « Chemins d'attaque » est testée dans AttackPathLabTests
        var labs = _service.BuiltInLabs().Where(l => l.Category == null).ToList();

        Assert.Equal(5, labs.Count);
        Assert.All(labs, lab =>
        {
            foreach (var culture in new[] { "fr-FR", "en-US" })
            {
                Assert.False(string.IsNullOrWhiteSpace(lab.Title[culture]));
                Assert.StartsWith("# ", lab.Statement[culture]);
                Assert.All(lab.Objectives, o =>
                {
                    Assert.False(string.IsNullOrWhiteSpace(o.Title[culture]));
                    Assert.False(string.IsNullOrWhiteSpace(o.Hint[culture]));
                });
            }
            Assert.Equal(lab.Objectives.Count, lab.Objectives.Select(o => o.Id).Distinct().Count());
        });
    }

    [Fact]
    public void BuiltInLabs_OnlyCiteExistingAuditRules()
    {
        var ruleIds = AuditRules.All.Select(r => r.Id).ToHashSet();
        var cited = _service.BuiltInLabs().SelectMany(l => l.Objectives).Select(o => o.Check.RuleId).OfType<string>();

        Assert.All(cited, id => Assert.Contains(id, ruleIds));
    }

    [Fact]
    public void PrepareStart_ShiftsDatesSoActiveAccountsStayActive()
    {
        var lab = Lab("02-kerberoasting");
        var start = _service.PrepareStart(lab, Now).Domain;

        var svc = Flatten(start).Single(o => o.Name == "svc-sql");
        Assert.Equal(1, (int)(Now - svc.LastLogonTimestamp!.Value).TotalDays);
        // Le domaine de l'exercice n'est pas modifié par la préparation
        Assert.Equal(lab.ReferenceDate.AddDays(-1), Flatten(lab.Start.Domain).Single(o => o.Name == "svc-sql").LastLogonTimestamp);
    }

    [Fact]
    public void Lab01_FirstAudit()
    {
        var (start, solved) = Play("01-premier-audit", d =>
        {
            var admin = Flatten(d).Single(o => o is { Name: "Administrator", Type: ADObjectType.User });
            admin.AccountFlags = ADAccountFlags.NotDelegated;
            admin.MemberOf.Remove("Enterprise Admins");
            admin.MemberOf.Remove("Schema Admins");
        });

        Assert.Equal(0, start.Points); // la garde « Administrator reste dans Domain Admins », atteinte au départ, ne rapporte rien
        AssertSolved(solved);
    }

    [Fact]
    public void Lab01_RemovingAdministratorFromDomainAdmins_IsCaught()
    {
        var (_, result) = Play("01-premier-audit", d =>
        {
            var admin = Flatten(d).Single(o => o is { Name: "Administrator", Type: ADObjectType.User });
            admin.MemberOf.Clear();
        });

        var guard = result.Objectives.Single(o => o.Objective.Id == "o4");
        Assert.False(guard.Passed);
        Assert.Contains("Domain Admins", guard.Feedback);
    }

    [Fact]
    public void Lab02_Kerberoasting()
    {
        var (start, solved) = Play("02-kerberoasting", d =>
        {
            Flatten(d).Single(o => o.Name == "GG-SQL-Admins").MemberOf.Remove("Domain Admins");
            Flatten(d).Single(o => o.Name == "legacy-scan").AccountFlags = ADAccountFlags.None;
        });

        Assert.Equal(0, start.Points); // la garde « svc-sql existe » ne rapporte rien
        AssertSolved(solved);
    }

    [Fact]
    public void Lab02_RemovingOnlyTheUser_LeavesTheNestedGroupViolation()
    {
        var (_, result) = Play("02-kerberoasting", d =>
        {
            Flatten(d).Single(o => o.Name == "svc-sql").MemberOf.Clear();
            Flatten(d).Single(o => o.Name == "legacy-scan").AccountFlags = ADAccountFlags.None;
        });

        Assert.True(result.Objectives.Single(o => o.Objective.Id == "o2").Passed);
        var noHigh = result.Objectives.Single(o => o.Objective.Id == "o5");
        Assert.False(noHigh.Passed);
        Assert.Contains("TIER-01", noHigh.Feedback); // GG-SQL-Admins (Tier 1) toujours dans Domain Admins
    }

    [Fact]
    public void Lab03_DelegationToHelpdesk()
    {
        var (start, solved) = Play("03-delegation-support", d =>
        {
            var users = Flatten(d).Single(o => o.Name == "Utilisateurs");
            users.Delegations.Clear();
            d.Delegations.Clear();
            foreach (var category in new[] { RightCategory.PasswordReset, RightCategory.AccountUnlock })
                users.Delegations.Add(new ADDelegation { TrusteeName = "GG-Helpdesk", TargetDN = users.DistinguishedName, Right = category.ToString(), RightCategory = category });
        });

        Assert.Equal(0, start.Points);
        AssertSolved(solved);
    }

    [Fact]
    public void Lab03_KeepingFullControl_ReportsTheExtraRight()
    {
        var (_, result) = Play("03-delegation-support", d =>
        {
            var users = Flatten(d).Single(o => o.Name == "Utilisateurs");
            users.Delegations.Add(new ADDelegation { TrusteeName = "GG-Helpdesk", TargetDN = users.DistinguishedName, Right = "ResetPassword", RightCategory = RightCategory.PasswordReset });
        });

        var only = result.Objectives.Single(o => o.Objective.Id == "o3");
        Assert.False(only.Passed);
        Assert.Contains("FullControl", only.Feedback);
    }

    [Fact]
    public void Lab04_Tiering()
    {
        var (start, solved) = Play("04-cloisonnement", d =>
        {
            var jdupont = Flatten(d).Single(o => o.Name == "jdupont");
            jdupont.MemberOf.Clear();
            Flatten(d).Single(o => o is { Name: "Domain Controllers", Type: ADObjectType.OrganizationalUnit }).LinkedGPOs.Remove("GPO-Postes");
            Flatten(d).Single(o => o.Name == "Read-only Domain Controllers").Tier = "Tier 0";
        });

        Assert.Equal(0, start.Points); // gardes : jdupont existe et la GPO par défaut est intacte
        AssertSolved(solved);
    }

    [Fact]
    public void Lab04_EditingTheDefaultDcPolicy_IsCaught()
    {
        var (_, result) = Play("04-cloisonnement", d =>
            Flatten(d).Single(o => o.Name == "Default Domain Controllers Policy").Tier = "Tier 1");

        var guard = result.Objectives.Single(o => o.Objective.Id == "o6");
        Assert.False(guard.Passed);
        Assert.Contains("Tier", guard.Feedback);
    }

    [Fact]
    public void Lab05_AccountHygiene()
    {
        var (start, solved) = Play("05-hygiene-comptes", d =>
        {
            Flatten(d).Single(o => o.Name == "Guest").AccountFlags |= ADAccountFlags.AccountDisabled;
            Flatten(d).Single(o => o.Name == "krbtgt").PwdLastSet = Now;
            Flatten(d).Single(o => o.Name == "stagiaire-2025").AccountFlags |= ADAccountFlags.AccountDisabled;
            Flatten(d).Single(o => o.Name == "PC-COMPTA-07").AccountFlags |= ADAccountFlags.AccountDisabled;
            Flatten(d).Single(o => o.Name == "jmartin").AccountFlags = ADAccountFlags.None;
        });

        Assert.Equal(0, start.Points);
        AssertSolved(solved);
    }
}

public class LabFormatTests
{
    private readonly LabService _service = new();

    public LabFormatTests() => TierConfigurationService.Instance.ResetToDefaults();

    [Fact]
    public void SaveAndLoad_RoundTripsALab()
    {
        var lab = _service.BuiltInLabs().First();
        var path = TempFile(LabService.FileExtension);

        _service.SaveFile(lab, path);
        var loaded = _service.LoadFile(path);

        Assert.Equal(lab.Id, loaded.Id);
        Assert.Equal(lab.Objectives.Select(o => o.Check.Type), loaded.Objectives.Select(o => o.Check.Type));
        Assert.All(loaded.Start.Domain.Children, c => Assert.Same(loaded.Start.Domain, c.Parent));
    }

    [Fact]
    public void StartDomainInV1Format_IsMigrated()
    {
        var json = """
        { "Format": "smadx-lab", "FormatVersion": 1, "Id": "v1", "ReferenceDate": "2026-01-01T00:00:00",
          "Start": { "Name": "old.local", "Type": "Domain", "Children": [ { "Name": "jdoe", "Type": "User" } ] },
          "Objectives": [] }
        """;

        var lab = _service.Parse(json);

        Assert.Equal(ADRootDocument.CurrentVersion, lab.Start.Version);
        Assert.Equal("jdoe", lab.Start.Domain.Children[0].Name);
    }

    [Theory]
    [InlineData("""{ "Version": 2, "Domain": {} }""", "smadx-lab")]
    [InlineData("""{ "Format": "smadx-lab", "FormatVersion": 99, "Start": {} }""", "v99")]
    [InlineData("""{ "Format": "smadx-lab", "FormatVersion": 1 }""", "Start")]
    [InlineData("pas du json", "illisible")]
    public void InvalidLabs_AreRefusedWithAReason(string json, string expectedInMessage)
    {
        var ex = Assert.Throws<LabFormatException>(() => _service.Parse(json));

        Assert.Contains(expectedInMessage, ex.Message);
    }
}

/// <summary>Chaque type de vérification, sur un petit domaine.</summary>
public class LabGraderTests
{
    private static readonly DateTime Now = new(2026, 10, 22);

    public LabGraderTests() => TierConfigurationService.Instance.ResetToDefaults();

    private static LabObjectiveResult Grade(LabCheck check, ADObject current, ADObject? start = null)
    {
        var lab = new LabDefinition { Objectives = [new LabObjective { Id = "o", Points = 2, Check = check }] };
        return new LabGrader().Grade(lab, start ?? BuildSmallDomain(), current, Now).Objectives[0];
    }

    [Fact]
    public void MemberOf_FollowsNestingUnlessDirectIsRequired()
    {
        var domain = BuildSmallDomain();
        Add(Find(domain, "Corp"), new ADObject("Domain Admins", ADObjectType.Group));
        Find(domain, "Helpdesk").MemberOf.Add("Domain Admins");
        Find(domain, "jdoe").MemberOf.Add("Helpdesk");

        Assert.True(Grade(new LabCheck { Type = LabCheckType.MemberOf, Object = "jdoe", Group = "Domain Admins" }, domain).Passed);
        Assert.False(Grade(new LabCheck { Type = LabCheckType.MemberOf, Object = "jdoe", Group = "Domain Admins", Effective = false }, domain).Passed);
        Assert.False(Grade(new LabCheck { Type = LabCheckType.NotMemberOf, Object = "jdoe", Group = "Domain Admins" }, domain).Passed);
    }

    [Fact]
    public void Flags_Tier_AndExistence()
    {
        var domain = BuildSmallDomain();
        Find(domain, "jdoe").AccountFlags = ADAccountFlags.DontRequirePreauth;

        Assert.True(Grade(new LabCheck { Type = LabCheckType.HasFlag, Object = "jdoe", Flag = ADAccountFlags.DontRequirePreauth }, domain).Passed);
        Assert.False(Grade(new LabCheck { Type = LabCheckType.NotHasFlag, Object = "jdoe", Flag = ADAccountFlags.DontRequirePreauth }, domain).Passed);
        Assert.True(Grade(new LabCheck { Type = LabCheckType.TierIs, Object = "Corp", Tier = "Tier 2" }, domain).Passed);
        Assert.True(Grade(new LabCheck { Type = LabCheckType.ObjectAbsent, Object = "ghost" }, domain).Passed);
        Assert.Contains("ghost", Grade(new LabCheck { Type = LabCheckType.ObjectExists, Object = "ghost" }, domain).Feedback);
    }

    [Fact]
    public void ScoreAndSeverityChecks_UseTheAudit()
    {
        var domain = BuildSmallDomain();
        Find(domain, "jdoe").AccountFlags = ADAccountFlags.DontRequirePreauth; // KRB-04, élevée

        Assert.False(Grade(new LabCheck { Type = LabCheckType.NoFindingAtOrAbove, Severity = AuditSeverity.High }, domain).Passed);
        Assert.True(Grade(new LabCheck { Type = LabCheckType.NoFindingAtOrAbove, Severity = AuditSeverity.Critical }, domain).Passed);
        Assert.False(Grade(new LabCheck { Type = LabCheckType.ScoreAtLeast, Score = 100 }, domain).Passed);
        Assert.Contains("jdoe", Grade(new LabCheck { Type = LabCheckType.RuleClear, RuleId = "KRB-04" }, domain).Feedback);
    }

    [Fact]
    public void NotModified_ComparesWithTheStartDomain()
    {
        var start = BuildSmallDomain();
        var current = BuildSmallDomain();
        Find(current, "jdoe").Description = "modifié";

        Assert.False(Grade(new LabCheck { Type = LabCheckType.NotModified, Object = "jdoe" }, current, start).Passed);
        Assert.True(Grade(new LabCheck { Type = LabCheckType.NotModified, Object = "Helpdesk" }, current, start).Passed);

        Find(current, "Corp").Children.Remove(Find(current, "Helpdesk"));
        Assert.False(Grade(new LabCheck { Type = LabCheckType.NotModified, Object = "Helpdesk" }, current, start).Passed);
    }

    [Fact]
    public void Points_AreEarnedOnlyWhenPassed()
    {
        var domain = BuildSmallDomain();
        var lab = new LabDefinition
        {
            Objectives =
            [
                new LabObjective { Id = "a", Points = 3, Check = new LabCheck { Type = LabCheckType.ObjectExists, Object = "jdoe" } },
                new LabObjective { Id = "b", Points = 2, Check = new LabCheck { Type = LabCheckType.ObjectAbsent, Object = "jdoe" } }
            ]
        };

        var result = new LabGrader().Grade(lab, domain, domain, Now);

        Assert.Equal(3, result.Points);
        Assert.Equal(5, result.MaxPoints);
        Assert.False(result.Completed);
    }
}
