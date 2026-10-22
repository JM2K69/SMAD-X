using SMADX.Models;
using SMADX.Services;
using SMADX.Services.Audit;
using SMADX.Services.Labs;
using static SMADX.Tests.TestHelpers;

namespace SMADX.Tests;

/// <summary>Outils de l'auteur d'exercices (SMAD-X Studio) : recettes, conversion, validation, fiche formateur.</summary>
public class LabAuthoringTests
{
    private static readonly DateTime Reference = new(2026, 10, 22);
    private static readonly DateTime Now = new(2027, 3, 15);

    public LabAuthoringTests() => TierConfigurationService.Instance.ResetToDefaults();

    public static TheoryData<string, string> Recipes()
    {
        var data = new TheoryData<string, string>();
        foreach (var recipe in LabRecipes.All)
            foreach (var culture in LabAuthoring.Cultures)
                data.Add(recipe.RuleId, culture);
        return data;
    }

    [Fact]
    public void Recipes_CoverEveryAuditRule()
    {
        Assert.Equal(AuditRules.All.Select(r => r.Id).Order(), LabRecipes.All.Select(r => r.RuleId).Order());
    }

    [Theory]
    [MemberData(nameof(Recipes))]
    public void Recipe_InjectsExactlyTheTargetedMisconfiguration(string ruleId, string culture)
    {
        var recipe = LabRecipes.All.Single(r => r.RuleId == ruleId);
        var builder = new LabDomainBuilder(culture, Reference);
        var domain = builder.Healthy($"recette-{ruleId.ToLowerInvariant()}.local"); // lève une exception si le domaine sain n'est pas propre

        recipe.Inject(builder, domain);

        Assert.Contains(new ADSecurityAuditService().Run(domain, Reference).FailedRules, r => r.Id == ruleId);
    }

    [Theory]
    [MemberData(nameof(Recipes))]
    public void RecipeLab_IsValidAndHidesTheSolution(string ruleId, string culture)
    {
        var (lab, notes) = LabRecipes.CreateLab(LabRecipes.All.Single(r => r.RuleId == ruleId), culture, "contoso.local", Reference);

        var report = LabValidator.Validate(lab, culture, Now);

        Assert.False(report.HasErrors, string.Join("\n", report.Issues.Select(i => $"{i.Level} {i.ObjectiveId} {i.Message}")));
        Assert.Contains(lab.Objectives, o => o.Check.Type == LabCheckType.RuleClear && o.Check.RuleId == ruleId);
        Assert.DoesNotContain(ruleId, lab.Statement.Resolve(culture));
        Assert.Contains(ruleId, notes);
        Assert.Equal(culture, lab.Language);
    }

    [Fact]
    public void RecipeLab_IsReproducible()
    {
        var recipe = LabRecipes.All.Single(r => r.RuleId == "KRB-01");
        var service = new LabService();

        var first = service.Serialize(LabRecipes.CreateLab(recipe, "fr-FR", "contoso.local", Reference).Lab);
        var second = service.Serialize(LabRecipes.CreateLab(recipe, "fr-FR", "contoso.local", Reference).Lab);

        Assert.Equal(first, second);
    }

    public static TheoryData<string, string> Scenarios()
    {
        var data = new TheoryData<string, string>();
        foreach (var lang in new[] { "fr", "en" })
            foreach (var file in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Samples", "AttackPaths", lang), "*.smad-x.json"))
                data.Add(lang, Path.GetFileName(file));
        return data;
    }

    [Theory]
    [MemberData(nameof(Scenarios))]
    public async Task ConvertScenario_MovesTheSolutionToTheTeacherNotes(string lang, string file)
    {
        var culture = lang == "fr" ? "fr-FR" : "en-US";
        var document = (await new ADDataService().LoadDocumentAsync(Path.Combine(AppContext.BaseDirectory, "Samples", "AttackPaths", lang, file))).Document!;

        var (lab, notes) = LabAuthoring.ConvertScenario(document, culture, "converti", Reference);

        Assert.Contains(lang == "fr" ? "## Le corriger" : "## Fix it", notes);
        Assert.DoesNotContain(Flatten(lab.Start.Domain), o => (o.Description ?? "").Contains(lang == "fr" ? "## Le corriger" : "## Fix it"));
        Assert.Contains(lab.Objectives, o => o.Check.Type == LabCheckType.NoAttackPath);
        var report = LabValidator.Validate(lab, culture, Now);
        Assert.False(report.HasErrors, string.Join("\n", report.Issues.Select(i => $"{i.Level} {i.ObjectiveId} {i.Message}")));
        // Le document d'origine n'est pas modifié
        Assert.Contains(lang == "fr" ? "## Le corriger" : "## Fix it", document.Domain.Description);
    }

    [Fact]
    public void Validator_RefusesASolutionInTheStatement()
    {
        var (lab, _) = LabRecipes.CreateLab(LabRecipes.All.Single(r => r.RuleId == "KRB-05"), "fr-FR", "contoso.local", Reference);
        lab.Statement["fr-FR"] += "\n## Le corriger\nChangez deux fois le mot de passe de krbtgt.";

        var report = LabValidator.Validate(lab, "fr-FR", Now);

        Assert.Contains(report.Issues, i => i.Level == LabIssueLevel.Error && i.Message.Contains("Le corriger"));
    }

    [Fact]
    public void Validator_RefusesAnObjectiveAlreadyMetAndAMissingObject()
    {
        var (lab, _) = LabRecipes.CreateLab(LabRecipes.All.Single(r => r.RuleId == "KRB-05"), "fr-FR", "contoso.local", Reference);
        lab.Objectives.Add(new LabObjective { Id = "x1", Points = 1, Title = LocalizedText.Of("a", "a"), Hint = LocalizedText.Of("a", "a"),
            Check = new LabCheck { Type = LabCheckType.RuleClear, RuleId = "GRP-01" } });
        lab.Objectives.Add(new LabObjective { Id = "x2", Points = 1, Title = LocalizedText.Of("b", "b"), Hint = LocalizedText.Of("b", "b"),
            Check = new LabCheck { Type = LabCheckType.ObjectExists, Object = "inconnu" } });

        var report = LabValidator.Validate(lab, "fr-FR", Now);

        Assert.Contains(report.Issues, i => i.ObjectiveId == "x1" && i.Level == LabIssueLevel.Error);
        Assert.Contains(report.Issues, i => i.ObjectiveId == "x2" && i.Message.Contains("inconnu"));
    }

    [Fact]
    public void Validator_GradesTheTrainerSolution()
    {
        var (lab, _) = LabRecipes.CreateLab(LabRecipes.All.Single(r => r.RuleId == "KRB-05"), "en-US", "contoso.local", Reference);
        var solution = new LabService().PrepareStart(lab, Now).Domain;
        Flatten(solution).Single(o => o.Name == "krbtgt").PwdLastSet = Now.AddDays(-1);

        var report = LabValidator.Validate(lab, "en-US", Now, solution);

        Assert.True(report.Solution!.Completed);
        Assert.Contains(report.Issues, i => i.Level == LabIssueLevel.Ok && i.Message.Contains("all points"));
    }

    [Fact]
    public void TeacherSheet_ListsObjectivesPathsAndNotes()
    {
        var (lab, notes) = LabRecipes.CreateLab(LabRecipes.All.Single(r => r.RuleId == "KRB-01"), "fr-FR", "contoso.local", Reference);

        var sheet = LabValidator.TeacherSheet(lab, "fr-FR", notes, Now);

        Assert.Contains("Document réservé au formateur", sheet);
        Assert.Contains("svc-app", sheet);            // le chemin d'attaque au départ
        Assert.Contains("KRB-01", sheet);
        Assert.All(lab.Objectives, o => Assert.Contains($"| {o.Id} |", sheet));
    }

    [Fact]
    public void Suggest_ProposesPathsRulesAndUncheckedGuards()
    {
        var (lab, _) = LabRecipes.CreateLab(LabRecipes.All.Single(r => r.RuleId == "KRB-01"), "fr-FR", "contoso.local", Reference);

        var suggestions = LabAuthoring.Suggest(lab.Start.Domain, Reference);

        Assert.Contains(suggestions, s => s.Recommended && s.Objective.Check.Type == LabCheckType.NoAttackPath && s.Objective.Check.Object == SMADX.Services.AttackPaths.AttackPathService.AnyUserId);
        Assert.Contains(suggestions, s => s.Recommended && s.Objective.Check.RuleId == "KRB-01");
        Assert.All(suggestions.Where(s => LabAuthoring.IsGuard(s.Objective.Check)), s => Assert.False(s.Recommended));
        Assert.Equal(suggestions.Count, suggestions.Select(s => s.Objective.Id).Distinct().Count());
    }
}
