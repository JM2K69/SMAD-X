using System.Text.Json.Nodes;
using SMADX.Models;
using SMADX.Services;
using SMADX.Services.Labs;
using static SMADX.Tests.TestHelpers;

namespace SMADX.Tests;

/// <summary>Questions de compréhension, crédit partiel, sujet distribué, notes du formateur et exports (format v3).</summary>
public class LabQuestionTests
{
    private static readonly DateTime Reference = new(2026, 10, 22);
    private readonly LabService _labs = new();

    public LabQuestionTests()
    {
        TierConfigurationService.Instance.ResetToDefaults();
        LocalizationService.Instance.SetLanguage("fr-FR");
    }

    private static LocalizedText Of(string text) => LocalizedText.Of(text, text);

    private static LabChoice Choice(string id, bool correct = false) => new() { Id = id, Text = Of($"Choix {id}"), Correct = correct };

    private static LabQuestion Single() => new()
    {
        Id = "q1", Kind = LabQuestionKind.SingleChoice, Points = 2, Title = Of("Quel compte signe les tickets Kerberos ?"),
        Choices = [Choice("a", correct: true), Choice("b"), Choice("c")]
    };

    private static LabQuestion Multiple(bool partial) => new()
    {
        Id = "q2", Kind = LabQuestionKind.MultipleChoice, Points = 4, Partial = partial, Title = Of("Quelles options sont dangereuses ?"),
        Choices = [Choice("a", correct: true), Choice("b", correct: true), Choice("c"), Choice("d")]
    };

    private static LabQuestion YesNo() => new()
    {
        Id = "q3", Kind = LabQuestionKind.TrueFalse, Points = 1, Title = Of("Un gMSA change seul son mot de passe."),
        Choices = [new() { Id = "yes", Text = LocalizedText.Of("Oui", "Yes"), Correct = true }, new() { Id = "no", Text = LocalizedText.Of("Non", "No") }]
    };

    private static LabQuestion Matching(bool partial) => new()
    {
        Id = "q4", Kind = LabQuestionKind.Matching, Points = 3, Partial = partial, Title = Of("Associez chaque attaque à sa cause."),
        Prompts =
        [
            new() { Id = "p1", Text = Of("Kerberoasting"), Match = "a" },
            new() { Id = "p2", Text = Of("AS-REP roasting"), Match = "b" },
            new() { Id = "p3", Text = Of("DCSync"), Match = "c" }
        ],
        Choices = [Choice("a"), Choice("b"), Choice("c"), Choice("d")] // d : intrus
    };

    private static LabQuestion Written(params string[] accepted) => new()
    {
        Id = "q5", Kind = LabQuestionKind.Text, Points = 2, Title = Of("Quel est le nom de cette attaque ?"),
        AcceptedAnswers = accepted.Length > 0 ? [.. accepted] : null
    };

    private static LabAnswer Chose(params string[] ids) => new() { Choices = [.. ids] };

    private static double? Fraction(LabQuestion question, LabAnswer? answer) => LabQuestionGrader.Grade(question, answer).Fraction;

    // ─── Correction des questions ────────────────────────────────────────────

    [Fact]
    public void SingleChoice_AndYesNo_AreAllOrNothing()
    {
        Assert.Equal(1, Fraction(Single(), Chose("a")));
        Assert.Equal(0, Fraction(Single(), Chose("b")));
        Assert.Equal(0, Fraction(Single(), Chose("a", "b")));
        Assert.Equal(1, Fraction(YesNo(), Chose("yes")));
        Assert.Equal(0, Fraction(YesNo(), Chose("no")));
    }

    [Fact]
    public void MultipleChoice_PartialCredit_CountsRightChoicesMinusWrongOnes()
    {
        Assert.Equal(1, Fraction(Multiple(partial: true), Chose("b", "a")));
        Assert.Equal(0.5, Fraction(Multiple(partial: true), Chose("a")));
        Assert.Equal(0, Fraction(Multiple(partial: true), Chose("a", "c")));
        Assert.Equal(0.5, Fraction(Multiple(partial: true), Chose("a", "b", "c")));
        Assert.Equal(0, Fraction(Multiple(partial: false), Chose("a")));
        Assert.Equal(2, LabQuestionGrader.Grade(Multiple(partial: true), Chose("a")).EarnedPoints);
    }

    [Fact]
    public void Matching_PaysEachRightPair_WithPartialCredit()
    {
        var two = new LabAnswer { Matches = new() { ["p1"] = "a", ["p2"] = "b", ["p3"] = "d" } };
        Assert.Equal(2 / 3.0, Fraction(Matching(partial: true), two));
        Assert.Equal(0, Fraction(Matching(partial: false), two));
        Assert.Equal(1, Fraction(Matching(partial: false), new LabAnswer { Matches = new() { ["p1"] = "a", ["p2"] = "b", ["p3"] = "c" } }));
        Assert.Equal(2, LabQuestionGrader.Grade(Matching(partial: true), two).EarnedPoints);
    }

    [Fact]
    public void WrittenAnswer_IsRecognisedWithoutCaseNorAccents_ElseLeftToTheTrainer()
    {
        var question = Written("Kerberoasting", "kerberos roasting");
        Assert.Equal(1, Fraction(question, new LabAnswer { Text = "  KERBÉROASTING. " }));
        Assert.Equal(1, Fraction(question, new LabAnswer { Text = "Kerberos   roasting" }));

        var other = LabQuestionGrader.Grade(question, new LabAnswer { Text = "attaque du ticket" });
        Assert.Equal(0, other.Fraction);
        Assert.True(other.NeedsReview);

        var noKey = LabQuestionGrader.Grade(Written(), new LabAnswer { Text = "attaque du ticket" });
        Assert.Null(noKey.Fraction);
        Assert.True(noKey.NeedsReview);
    }

    [Fact]
    public void NoAnswer_IsWorthNothing_EvenWithoutTheKey()
    {
        var result = LabQuestionGrader.Grade(Written(), new LabAnswer { Text = "   " });
        Assert.Equal(0, result.Fraction);
        Assert.False(result.NeedsReview);
        Assert.Equal(0, Fraction(Single(), null));
    }

    // ─── Exemplaire du formateur et sujet ────────────────────────────────────

    /// <summary>Un examen tiré d'une recette, avec une question de chaque forme : l'exemplaire du formateur.</summary>
    private static LabDefinition ExamWithQuestions(string culture = "fr-FR")
    {
        var lab = LabRecipes.CreateLab(LabRecipes.All.Single(r => r.RuleId == "KRB-04"), culture, "questions.local", Reference).Lab;
        lab.Exam = true;
        lab.Questions = [Single(), Multiple(partial: true), YesNo(), Matching(partial: true), Written()];
        foreach (var q in lab.Questions) q.Hint = Of("Relisez la fiche.");
        return lab;
    }

    [Fact]
    public void Subject_HasNeitherAnswerKeyNorHints_AndShufflesTheMatchingProposals()
    {
        var master = ExamWithQuestions();
        var json = _labs.SerializeSubject(master);
        var subject = _labs.Parse(json);

        Assert.True(_labs.Parse(_labs.Serialize(master)).IsTrainerCopy);
        Assert.False(subject.IsTrainerCopy);
        Assert.All(subject.Questions, q => Assert.False(q.HasAnswerKey));
        Assert.All(subject.Questions, q => Assert.Empty(q.Hint));
        Assert.DoesNotContain("\"Correct\"", json);
        Assert.DoesNotContain("\"Match\"", json);
        // Les propositions ne sont plus en face de leur élément ; le mélange est le même à chaque export
        var matching = subject.Questions.Single(q => q.Kind == LabQuestionKind.Matching);
        Assert.NotEqual(["a", "b", "c", "d"], matching.Choices.Select(c => c.Id));
        Assert.Equal(json, _labs.SerializeSubject(master));
        // L'exemplaire en mémoire garde son corrigé
        Assert.All(master.Questions.Where(q => q.Kind != LabQuestionKind.Text), q => Assert.True(q.HasAnswerKey));
    }

    private string Submit(LabDefinition received, string student, Dictionary<string, LabAnswer> answers, Guid? session = null, string? path = null)
    {
        var lab = LabSession.Begin(received, Reference, session);
        var work = lab.NewWorkingCopy();
        path ??= TempFile(LabSubmissionService.FileExtension);
        new LabSubmissionService().SaveFile(lab.ToSubmission(student, work, [], Reference.AddMinutes(30), answers, "e123"), path);
        return path;
    }

    [Fact]
    public void ExamSubmission_IsGradedWithTheTrainerKey_ThenWithTheTrainerMarks()
    {
        var master = ExamWithQuestions();
        var subject = _labs.Parse(_labs.SerializeSubject(master));
        var written = new LabAnswer { Text = "Une attaque hors ligne sur le ticket." };
        var answers = new Dictionary<string, LabAnswer>
        {
            ["q1"] = Chose("a"), ["q2"] = Chose("a"), ["q3"] = Chose("no"),
            ["q4"] = new() { Matches = new() { ["p1"] = "a", ["p2"] = "b", ["p3"] = "c" } }, ["q5"] = written
        };
        var path = Submit(subject, "Lou", answers);

        var row = SubmissionGrader.Grade(master, path, "fr-FR");
        Assert.True(row.Passed(master.Questions[0]));
        Assert.Equal(2, row.Question(master.Questions[1])!.EarnedPoints); // moitié de 4
        Assert.False(row.Passed(master.Questions[2]));
        Assert.True(row.Passed(master.Questions[3]));
        Assert.Null(row.Question(master.Questions[4])!.Fraction);
        Assert.Equal(1, row.ToReview);
        Assert.Equal("e123", row.StudentId);
        Assert.DoesNotContain(row.Issues, i => i.Level == LabIssueLevel.Error);

        // Le formateur note la réponse écrite et laisse une appréciation, à côté de la copie
        var submission = new LabSubmissionService().LoadFile(path);
        SubmissionMarksService.Save(new SubmissionMarks
        {
            LabId = master.Id, SessionId = submission.SessionId, Comment = "Bonne analyse.",
            Questions = { ["q5"] = new QuestionMark { Points = 1.5, Answer = written.Signature() } }
        }, path);
        Assert.EndsWith(".smadx-marks.json", SubmissionMarksService.MarksPath(path));

        var marked = SubmissionGrader.Grade(master, path, "fr-FR");
        Assert.Equal(1.5, marked.Question(master.Questions[4])!.EarnedPoints);
        Assert.Equal(0, marked.ToReview);
        Assert.Equal(row.Points + 1.5, marked.Points);
        Assert.Equal("Bonne analyse.", marked.Marks!.Comment);

        // Une nouvelle copie, avec une autre réponse : la note ne s'applique plus
        answers["q5"] = new LabAnswer { Text = "Autre chose." };
        Submit(subject, "Lou", answers, submission.SessionId, path);
        var changed = SubmissionGrader.Grade(master, path, "fr-FR");
        Assert.Equal(1, changed.ToReview);
        Assert.Contains(changed.Issues, i => i.Message.Contains("a changé depuis votre note"));
    }

    [Fact]
    public void SubmissionOnTheTrainerCopy_IsReported()
    {
        var master = ExamWithQuestions();
        var row = SubmissionGrader.Grade(master, Submit(master, "Lou", []), "fr-FR");
        Assert.Contains(row.Issues, i => i.Level == LabIssueLevel.Error && i.Message.Contains("votre exemplaire au lieu du sujet"));
    }

    [Fact]
    public void GradingWithTheSubject_WarnsThatTheKeyIsMissing()
    {
        var subject = _labs.Parse(_labs.SerializeSubject(ExamWithQuestions()));
        var row = SubmissionGrader.Grade(subject, Submit(subject, "Lou", new() { ["q1"] = Chose("a") }), "fr-FR");
        Assert.Contains(row.Issues, i => i.Message.Contains("ouvrez votre exemplaire"));
        Assert.True(row.ToReview > 0);
    }

    // ─── Crédit partiel des objectifs ────────────────────────────────────────

    [Fact]
    public void PartialObjective_PaysThePartFixed_AndAGuardStillCancelsIt()
    {
        var lab = LabRecipes.CreateLab(LabRecipes.All.Single(r => r.RuleId == "KRB-04"), "fr-FR", "partiel.local", Reference).Lab;
        var scan = Flatten(lab.Start.Domain).Single(o => o.Name == "scan-reseau");
        var other = Flatten(lab.Start.Domain).First(o => o.Type == ADObjectType.User && o.Tier == "Tier 2" && o != scan && o.Parent?.Parent?.Type == ADObjectType.OrganizationalUnit);
        other.AccountFlags |= ADAccountFlags.DontRequirePreauth;
        var objective = lab.Objectives.Single(o => o.Check is { Type: LabCheckType.RuleClear, RuleId: "KRB-04" });
        objective.Partial = true;
        Assert.True(LabAuthoring.CanBePartial(objective.Check));

        var start = _labs.PrepareStart(lab, Reference).Domain;
        var work = _labs.PrepareStart(lab, Reference).Domain;
        Flatten(work).Single(o => o.Name == "scan-reseau").AccountFlags &= ~ADAccountFlags.DontRequirePreauth;
        var half = new LabGrader().Grade(lab, start, work, Reference).Objectives.Single(r => r.Objective == objective);
        Assert.False(half.Passed);
        Assert.Equal(0.5, half.Fraction);
        Assert.Equal(Math.Round(objective.Points * 0.5, 2), half.EarnedPoints);
        Assert.Contains("50 %", half.Feedback);

        // Supprimer le compte gardé au lieu de le corriger : la garde annule aussi la part acquise
        var shortcut = _labs.PrepareStart(lab, Reference).Domain;
        var removed = Flatten(shortcut).Single(o => o.Name == "scan-reseau");
        removed.Parent!.Children.Remove(removed);
        var cancelled = new LabGrader().Grade(lab, start, shortcut, Reference).Objectives.Single(r => r.Objective == objective);
        Assert.Equal(0, cancelled.EarnedPoints);
    }

    // ─── Exports ─────────────────────────────────────────────────────────────

    [Fact]
    public void MoodleCsv_HasTheIdentifierAndTheGradeOutOfTheScale_AndTheStudentReportHidesTheKey()
    {
        var master = ExamWithQuestions();
        var subject = _labs.Parse(_labs.SerializeSubject(master));
        var path = Submit(subject, "Lou Martin", new() { ["q1"] = Chose("b"), ["q3"] = Chose("yes") });
        var rows = SubmissionGrader.GradeAll(master, [path], "fr-FR");

        var moodle = SubmissionGrader.MoodleCsv(master, rows, "fr-FR", 20).Split('\n');
        Assert.Equal($"Identifiant,Apprenant,{master.Id}", moodle[0]);
        var expected = Math.Round(20 * rows[0].Points / master.MaxPoints, 2).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal($"e123,Lou Martin,{expected}", moodle[1]);

        var csv = SubmissionGrader.Csv(master, rows, "fr-FR", 10);
        Assert.Contains("Note sur 10", csv);
        Assert.Contains(";q5;", csv.Split('\n')[0] + ";");

        var report = SubmissionGrader.StudentReport(master, rows[0], "fr-FR");
        Assert.Contains("Lou Martin", report);
        Assert.Contains("| Votre réponse |", report);
        Assert.Contains("Choix b", report); // sa réponse
        Assert.DoesNotContain("✔ Choix a", report); // pas le corrigé
        Assert.Contains("sur 20", report.Replace("/ 20", "sur 20"));
        Assert.DoesNotContain("sceau", report);
    }

    [Fact]
    public void StudentReport_KeepsTheCommentInOneParagraph()
    {
        var master = ExamWithQuestions();
        var path = Submit(_labs.Parse(_labs.SerializeSubject(master)), "Lou", new() { ["q1"] = Chose("a") });
        var submission = new LabSubmissionService().LoadFile(path);
        SubmissionMarksService.Save(new SubmissionMarks { LabId = master.Id, SessionId = submission.SessionId, Comment = "Bien vu : corrigez sans supprimer." }, path);

        var report = SubmissionGrader.StudentReport(master, SubmissionGrader.Grade(master, path, "fr-FR"), "fr-FR");

        // « mot : mot » sur une ligne de texte deviendrait une ligne de tableau dans le PDF
        Assert.Contains("Bien vu\u00A0: corrigez sans supprimer.", report);
        Assert.DoesNotContain(report.Split('\n'), l => !l.StartsWith('|') && !l.StartsWith('-') && !l.StartsWith('#') && l.Contains(" : "));
    }

    [Fact]
    public void StudentReport_RendersAsPdf()
    {
        var master = ExamWithQuestions();
        var rows = SubmissionGrader.GradeAll(master, [Submit(_labs.Parse(_labs.SerializeSubject(master)), "Lou", new() { ["q1"] = Chose("a") })], "fr-FR");
        var pdf = TempFile(".pdf");
        ADDocumentReportService.MarkdownToPdf(SubmissionGrader.StudentReport(master, rows[0], "fr-FR"), pdf);
        Assert.StartsWith("%PDF", File.ReadAllText(pdf)[..4]);
    }

    // ─── Validation ──────────────────────────────────────────────────────────

    [Fact]
    public void Validator_ChecksEveryQuestionForm()
    {
        var lab = ExamWithQuestions();
        Assert.DoesNotContain(LabValidator.Validate(lab, "fr-FR", Reference).Issues, i => i.Level == LabIssueLevel.Error);

        lab.Questions[0].Choices.ForEach(c => c.Correct = false);
        lab.Questions[2].Choices.RemoveAt(1);
        lab.Questions[3].Prompts![0].Match = "z";
        var errors = LabValidator.Validate(lab, "fr-FR", Reference).Issues.Where(i => i.Level == LabIssueLevel.Error).Select(i => i.ObjectiveId).ToList();
        Assert.Contains("q1", errors);
        Assert.Contains("q3", errors);
        Assert.Contains("q4", errors);

        // Hors examen, le corrigé se lit dans le fichier
        var exercise = ExamWithQuestions();
        exercise.Exam = false;
        Assert.Contains(LabValidator.Validate(exercise, "fr-FR", Reference).Issues, i => i.Level == LabIssueLevel.Warning && i.Message.Contains("corrigé des questions"));
    }

    [Fact]
    public void OlderLabsAndSubmissions_StillLoad_WithoutQuestions()
    {
        var builtIn = _labs.BuiltInLabs().First();
        Assert.Empty(builtIn.Questions);
        var node = JsonNode.Parse(_labs.Serialize(builtIn))!.AsObject();
        node["FormatVersion"] = 2;
        node.Remove("Questions");
        Assert.Empty(_labs.Parse(node.ToJsonString()).Questions);
    }
}
