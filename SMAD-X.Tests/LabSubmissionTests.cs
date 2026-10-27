using System.Text.Json;
using System.Text.Json.Nodes;
using SMADX.Models;
using SMADX.Services;
using SMADX.Services.Labs;
using static SMADX.Tests.TestHelpers;

namespace SMADX.Tests;

/// <summary>Copies d'apprenants (.smadx-submission.json) et correction d'une classe.</summary>
public class LabSubmissionTests
{
    private static readonly DateTime Started = new(2026, 10, 27, 9, 0, 0);
    private readonly LabService _labs = new();
    private readonly LabSubmissionService _submissions = new();

    public LabSubmissionTests() => TierConfigurationService.Instance.ResetToDefaults();

    private LabDefinition Hygiene() => _labs.BuiltInLabs().Single(l => l.Id == "05-hygiene-comptes");

    /// <summary>Une copie : l'apprenant commence à <see cref="Started"/>, fait <paramref name="work"/> puis enregistre.</summary>
    private string Submit(LabDefinition lab, string student, Action<ADObject>? work = null, string[]? hints = null, DateTime? savedAt = null)
    {
        var session = LabSession.Begin(lab, Started);
        var document = session.NewWorkingCopy();
        work?.Invoke(document.Domain);
        var path = TempFile(LabSubmissionService.FileExtension);
        _submissions.SaveFile(session.ToSubmission(student, document, hints ?? [], savedAt ?? Started.AddMinutes(40)), path);
        return path;
    }

    private static void DisableGuest(ADObject domain) =>
        Flatten(domain).Single(o => o.Name == "Guest").AccountFlags |= ADAccountFlags.AccountDisabled;

    [Fact]
    public void Submission_RoundTrips_WithTheLabItWasMadeOn()
    {
        var lab = Hygiene();
        var path = Submit(lab, "  Camille Dupont ", DisableGuest, hints: ["o1", "o1", "o2"]);

        var submission = _submissions.LoadFile(path);

        Assert.Equal("Camille Dupont", submission.Student);
        Assert.Equal(lab.Id, submission.LabId);
        Assert.Equal(Started.Date, submission.StartedOn);
        Assert.Equal(["o1", "o2"], submission.HintsUsed);
        Assert.Equal(lab.Objectives.Count, submission.Lab.Objectives.Count);
        Assert.True(Flatten(submission.Work.Domain).Single(o => o.Name == "Guest").AccountFlags.HasFlag(ADAccountFlags.AccountDisabled));
        Assert.NotNull(Flatten(submission.Work.Domain).Single(o => o.Name == "Guest").Parent);
        // Le formateur reconnaît son exercice : même domaine de départ
        Assert.Equal(LabSubmissionService.StartFingerprint(lab), LabSubmissionService.StartFingerprint(submission.Lab));
    }

    [Theory]
    [InlineData("""{ "Format": "smadx-lab" }""", "smadx-submission")]
    [InlineData("""{ "Format": "smadx-submission", "FormatVersion": 99 }""", "v99")]
    [InlineData("""{ "Format": "smadx-submission", "FormatVersion": 1, "Work": {} }""", "Lab")]
    [InlineData("pas du json", "illisible")]
    public void InvalidSubmissions_AreRefusedWithAReason(string json, string expectedInMessage)
    {
        LocalizationService.Instance.SetLanguage("fr-FR");
        var ex = Assert.Throws<LabFormatException>(() => _submissions.Parse(json));
        Assert.Contains(expectedInMessage, ex.Message);
    }

    [Fact]
    public void Grade_GivesTheSamePointsAsSmadX_EvenGradedMuchLater()
    {
        var lab = Hygiene();
        var session = LabSession.Begin(lab, Started);
        var work = session.NewWorkingCopy();
        DisableGuest(work.Domain);
        var expected = new LabGrader().Grade(lab, session.Start.Domain, work.Domain, Started.AddMinutes(40));

        // Correction des mois plus tard : le domaine de départ est repréparé à la date où l'apprenant a commencé
        var graded = SubmissionGrader.Grade(lab, Submit(lab, "Camille", DisableGuest), "fr-FR");

        Assert.True(graded.Graded);
        Assert.Empty(graded.Issues);
        Assert.Equal(expected.Points, graded.Points);
        Assert.Equal(2, graded.Points);
        Assert.True(graded.Passed(lab.Objectives.Single(o => o.Id == "o1")));
    }

    [Fact]
    public void Grade_RefusesASubmissionForAnotherLab()
    {
        var other = _labs.BuiltInLabs().Single(l => l.Id == "01-premier-audit");
        var graded = SubmissionGrader.Grade(Hygiene(), Submit(other, "Camille"), "fr-FR");

        Assert.False(graded.Graded);
        Assert.Contains(graded.Issues, i => i.Level == LabIssueLevel.Error && i.Message.Contains("01-premier-audit"));
    }

    [Fact]
    public void Grade_WarnsWhenTheStartingDomainDiffers()
    {
        var trainer = Hygiene();
        var learner = Hygiene();
        learner.Start.Domain.Description = "autre version";
        var graded = SubmissionGrader.Grade(trainer, Submit(learner, "Camille"), "en-US");

        Assert.True(graded.Graded);
        Assert.Contains(graded.Issues, i => i.Level == LabIssueLevel.Warning && i.Message.Contains("starting domain"));
    }

    [Fact]
    public void GradeAll_SortsByName_AndFlagsUnreadableNamelessAndDuplicateSubmissions()
    {
        var lab = Hygiene();
        var broken = TempFile(LabSubmissionService.FileExtension);
        File.WriteAllText(broken, "{");
        var paths = new[] { Submit(lab, "Zoé"), Submit(lab, "Camille", DisableGuest), Submit(lab, "camille"), Submit(lab, ""), broken };

        var rows = SubmissionGrader.GradeAll(lab, paths, "fr-FR");

        Assert.Equal(5, rows.Count);
        Assert.Equal(4, rows.Count(r => r.Graded));
        Assert.Contains(rows.Single(r => !r.Graded).Issues, i => i.Level == LabIssueLevel.Error);
        Assert.All(rows.Where(r => r.Submission?.Student.Equals("camille", StringComparison.OrdinalIgnoreCase) == true),
            r => Assert.Contains(r.Issues, i => i.Message.Contains("plusieurs copies")));
        Assert.Contains(rows, r => r.Graded && r.Issues.Any(i => i.Message.Contains("nom")));
        var named = rows.Where(r => r.Submission is { Student.Length: > 0 }).Select(r => r.Student).ToList();
        Assert.Equal(named.Order(StringComparer.CurrentCultureIgnoreCase), named);
    }

    [Fact]
    public void Statistics_AverageMedianAndSuccessRate()
    {
        var lab = Hygiene();
        var rows = SubmissionGrader.GradeAll(lab, [Submit(lab, "A", DisableGuest), Submit(lab, "B"), Submit(lab, "C")], "fr-FR");
        var o1 = lab.Objectives.Single(o => o.Id == "o1");

        Assert.Equal(1.0 / 3, SubmissionGrader.SuccessRate(o1, rows)!.Value, 3);
        Assert.Equal(rows.Average(r => r.Points), SubmissionGrader.Average(rows));
        Assert.Equal(rows.Select(r => r.Points).Order().ElementAt(1), SubmissionGrader.Median(rows));
        Assert.Null(SubmissionGrader.Median([]));
    }

    [Fact]
    public void Csv_UsesTheSeparatorOfTheLanguage()
    {
        var lab = Hygiene();
        var rows = SubmissionGrader.GradeAll(lab, [Submit(lab, "Dupont; Camille", DisableGuest)], "fr-FR");

        var fr = SubmissionGrader.Csv(lab, rows, "fr-FR").Split('\n');
        Assert.StartsWith("Apprenant;Fichier;", fr[0]);
        Assert.EndsWith(";Remarques", fr[0]);
        Assert.StartsWith("\"Dupont; Camille\";", fr[1]); // le séparateur dans un nom est protégé
        Assert.Contains(";2;", fr[1]);

        var en = SubmissionGrader.Csv(lab, rows, "en-US").Split('\n');
        Assert.StartsWith("Learner,File,", en[0]);
    }

    [Fact]
    public void ClassReport_SummarisesTheClass()
    {
        var lab = Hygiene();
        var rows = SubmissionGrader.GradeAll(lab, [Submit(lab, "Camille", DisableGuest), Submit(lab, "Lucas")], "fr-FR");

        var report = SubmissionGrader.ClassReport(lab, rows, "fr-FR");

        Assert.Contains("bilan de la classe", report);
        Assert.Contains("**Copies corrigées** : 2 / 2", report);
        Assert.Contains("| Camille |", report);
        Assert.Contains("## Réussite par objectif", report);
        Assert.Contains("| o1 · ", report);
        Assert.Contains("1 / 2 (50 %)", report);
    }

    [Fact]
    public void Exam_IsWrittenOnlyWhenSet()
    {
        var lab = Hygiene();
        Assert.DoesNotContain("\"Exam\"", _labs.Serialize(lab));

        lab.Exam = true;
        var json = _labs.Serialize(lab);
        Assert.Contains("\"Exam\": true", json);
        Assert.True(_labs.Parse(json).Exam);
    }

    [Fact]
    public void Session_NewWorkingCopy_MatchesTheReferenceStart()
    {
        var lab = _labs.BuiltInLabs().Single(l => l.Id == "04-cloisonnement");
        var session = LabSession.Begin(lab, Started);
        var result = new LabGrader().Grade(lab, session.Start.Domain, session.NewWorkingCopy().Domain, Started.AddDays(3));

        // Recommencer le surlendemain ne fait pas apparaître de modification
        var unchanged = result.Objectives.Where(o => o.Objective.Check.Type == LabCheckType.NotModified).ToList();
        Assert.NotEmpty(unchanged);
        Assert.All(unchanged, o => Assert.True(o.Passed, o.Feedback));
    }

    // ─── Sceau et session ────────────────────────────────────────────────────

    private static void Rewrite(string path, Action<JsonObject> change)
    {
        var node = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        change(node);
        File.WriteAllText(path, node.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    [Fact]
    public void Seal_IsValidAfterSaving_WhateverTheLayout()
    {
        var path = Submit(Hygiene(), "Inès Moreau", DisableGuest);
        Assert.Equal(SubmissionSeal.Valid, _submissions.LoadFile(path).SealCheck);

        // Une autre indentation et d'autres fins de ligne ne changent pas le contenu
        var node = JsonNode.Parse(File.ReadAllText(path))!;
        File.WriteAllText(path, node.ToJsonString(new JsonSerializerOptions { WriteIndented = true, IndentSize = 4 }).ReplaceLineEndings("\r\n"));
        Assert.Equal(SubmissionSeal.Valid, _submissions.LoadFile(path).SealCheck);
    }

    [Theory]
    [InlineData("name")]
    [InlineData("hints")]
    [InlineData("date")]
    [InlineData("domain")]
    public void Seal_IsBroken_ByAnyChangeMadeByHand(string what)
    {
        var lab = Hygiene();
        var path = Submit(lab, "Camille", hints: ["o1"]);
        Rewrite(path, node =>
        {
            switch (what)
            {
                case "name": node["Student"] = "Lucas"; break;
                case "hints": node["HintsUsed"] = new JsonArray(); break;
                case "date": node["SavedAt"] = "2026-10-27T08:00:00"; break;
                case "domain": node["Work"]!["Domain"]!["Description"] = "retouché"; break;
            }
        });

        Assert.Equal(SubmissionSeal.Broken, _submissions.LoadFile(path).SealCheck);
        var graded = SubmissionGrader.Grade(lab, path, "fr-FR");
        Assert.True(graded.Graded); // la note reste visible, la remarque prévient le formateur
        Assert.Contains(graded.Issues, i => i.Level == LabIssueLevel.Error && i.Message.Contains("sceau"));
        Assert.True(SubmissionGrader.NeedsAttention(graded));
    }

    [Fact]
    public void Seal_Missing_IsReported()
    {
        var lab = Hygiene();
        var path = Submit(lab, "Camille");
        Rewrite(path, node => node.Remove("Seal"));

        Assert.Equal(SubmissionSeal.Missing, _submissions.LoadFile(path).SealCheck);
        Assert.Contains(SubmissionGrader.Grade(lab, path, "fr-FR").Issues, i => i.Level == LabIssueLevel.Warning && i.Message.Contains("sceau"));
    }

    [Fact]
    public void Session_GivesEachLearnerTheirOwnIdentifiers_StableOnRestart()
    {
        var lab = Hygiene();
        var a = LabSession.Begin(lab, Started);
        var b = LabSession.Begin(lab, Started);
        Assert.NotEqual(a.SessionId, b.SessionId);

        var ids = Flatten(a.NewWorkingCopy().Domain).Select(o => o.Id).ToList();
        Assert.Equal(ids, Flatten(a.NewWorkingCopy().Domain).Select(o => o.Id)); // « Recommencer »
        Assert.Empty(ids.Intersect(Flatten(b.NewWorkingCopy().Domain).Select(o => o.Id)));
        Assert.Empty(ids.Intersect(Flatten(lab.Start.Domain).Select(o => o.Id)));
    }

    [Fact]
    public void Resume_KeepsTheSession()
    {
        var submission = _submissions.LoadFile(Submit(Hygiene(), "Camille"));
        var resumed = LabSession.Resume(submission);

        Assert.Equal(submission.SessionId, resumed.SessionId);
        Assert.Equal(Flatten(submission.Work.Domain).Select(o => o.Id), Flatten(resumed.NewWorkingCopy().Domain).Select(o => o.Id));
    }

    [Fact]
    public void GradeAll_IdenticalHonestSolutions_AreNotFlagged()
    {
        var lab = Hygiene();
        var rows = SubmissionGrader.GradeAll(lab, [Submit(lab, "Camille", DisableGuest), Submit(lab, "Lucas", DisableGuest)], "fr-FR");

        Assert.All(rows, r => Assert.Empty(r.Issues));
        Assert.Equal(rows[0].Points, rows[1].Points);
    }

    [Fact]
    public void GradeAll_FlagsACopyResavedUnderAnotherName()
    {
        var lab = Hygiene();
        var camille = Submit(lab, "Camille", DisableGuest);
        // Lucas rouvre la copie de Camille dans SMAD-X et l'enregistre sous son nom : le sceau est valide, la session est celle de Camille
        var original = _submissions.LoadFile(camille);
        var lucas = TempFile(LabSubmissionService.FileExtension);
        _submissions.SaveFile(LabSession.Resume(original).ToSubmission("Lucas", original.Work, [], Started.AddMinutes(50)), lucas);

        var rows = SubmissionGrader.GradeAll(lab, [camille, lucas], "fr-FR");

        Assert.All(rows, r => Assert.Contains(r.Issues, i => i.Level == LabIssueLevel.Error && i.Message.Contains("Même session")));
        Assert.Contains(rows.Single(r => r.Student == "Lucas").Issues, i => i.Message.Contains("Camille"));
    }

    [Fact]
    public void GradeAll_FlagsADomainTakenFromAnotherLearner_AndNamesTheOwner()
    {
        var lab = Hygiene();
        var camille = LabSession.Begin(lab, Started);
        var work = camille.NewWorkingCopy();
        DisableGuest(work.Domain);
        var camillePath = TempFile(LabSubmissionService.FileExtension);
        _submissions.SaveFile(camille.ToSubmission("Camille", work, [], Started.AddMinutes(30)), camillePath);
        // Lucas a sa propre session, mais rend le domaine de Camille
        var lucasPath = TempFile(LabSubmissionService.FileExtension);
        _submissions.SaveFile(LabSession.Begin(lab, Started).ToSubmission("Lucas", work, [], Started.AddMinutes(45)), lucasPath);

        var rows = SubmissionGrader.GradeAll(lab, [camillePath, lucasPath], "en-US");

        Assert.Empty(rows.Single(r => r.Student == "Camille").Issues);
        var lucas = rows.Single(r => r.Student == "Lucas");
        Assert.Contains(lucas.Issues, i => i.Level == LabIssueLevel.Error && i.Message.Contains("does not come from this submission's session"));
        Assert.Contains(lucas.Issues, i => i.Message.Contains("Camille's session"));
    }

    [Fact]
    public void SuggestedFileName_RemovesForbiddenCharacters()
    {
        Assert.Equal("lab_Camille Dupont.smadx-submission.json", LabSubmissionService.SuggestedFileName("lab", " Camille Dupont "));
        Assert.DoesNotContain("/", LabSubmissionService.SuggestedFileName("lab", "a/b"));
        Assert.Equal("lab_copie.smadx-submission.json", LabSubmissionService.SuggestedFileName("lab", " "));
    }
}
