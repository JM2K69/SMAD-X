using SMADX.Models;
using SMADX.Services.Labs;
using SMARX.Labs;
using SMARX.Models;
using SMARX.Services.Labs;

namespace SMARX.Tests;

/// <summary>
/// Les notes du formateur sur une copie SMAR-X (.smarx-marks.json, à côté de la copie scellée) : les points donnés à la
/// main remplacent la correction automatique tant que la réponse notée est celle de la copie ; l'appréciation passe dans
/// le compte rendu de l'apprenant, qui ne montre ni le corrigé ni les remarques sur la copie.
/// </summary>
public class SmarxMarksTests
{
    private const string Fr = "fr-FR";

    private static (SmarxLab Lab, string Path, SmarxSubmission Submission) Copy(string answer)
    {
        var lab = SmarxBuiltInLabs.SambaDiagnosis();
        var folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "smarx-marks-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var submission = new SmarxSubmission
        {
            Student = "Camille Martin", LabId = lab.Id, SessionId = Guid.NewGuid(), StartedOn = new DateTime(2026, 11, 6, 9, 0, 0),
            SavedAt = new DateTime(2026, 11, 6, 9, 30, 0), Lab = lab, Work = SmarxLabGrader.Clone(lab.Start),
            Answers = { ["q1"] = new LabAnswer { Choices = [answer] } }
        };
        var path = System.IO.Path.Combine(folder, SmarxLabService.SubmissionFileName(lab.Id, submission.Student));
        File.WriteAllText(path, SmarxLabService.SerializeSubmission(submission));
        return (lab, path, SmarxLabService.ParseSubmission(File.ReadAllText(path)));
    }

    [Fact]
    public void HandGivenPoints_ReplaceTheAutomaticGrade_AndTheCommentGoesToTheReport()
    {
        var (lab, path, submission) = Copy("b");
        SmarxMarksService.Save(new SubmissionMarks
        {
            LabId = submission.LabId, SessionId = submission.SessionId, Comment = "Bonne piste : relisez la page Samba.",
            Questions = { ["q1"] = new QuestionMark { Points = 0.5, Answer = "b" } }
        }, path);

        var row = SmarxClassGrader.GradeAll(lab, [path], Fr).Single();

        var q1 = row.Result!.Questions.Single(q => q.Question.Id == "q1");
        Assert.True(q1.Manual);
        Assert.Equal(0.5, q1.EarnedPoints);
        Assert.True(File.Exists(SmarxMarksService.MarksPath(path)));
        Assert.EndsWith(".smarx-marks.json", SmarxMarksService.MarksPath(path));

        var report = SmarxClassGrader.StudentReport(lab, row, Fr);
        Assert.Contains("## Appréciation du formateur", report);
        Assert.Contains("Bonne piste", report);
        Assert.Contains("| Le noyau Linux |", report);
        Assert.DoesNotContain("Correct", report);
        Assert.Equal($"{lab.Id}_Camille Martin.pdf", SmarxClassGrader.StudentReportFileName(lab, row, ".pdf"));
    }

    [Fact]
    public void Marks_OnAnotherAnswer_AreIgnored_AndFlagged()
    {
        var (lab, path, submission) = Copy("a");
        SmarxMarksService.Save(new SubmissionMarks
        {
            LabId = submission.LabId, SessionId = submission.SessionId,
            Questions = { ["q1"] = new QuestionMark { Points = 0, Answer = "b" } }
        }, path);

        var row = SmarxClassGrader.GradeAll(lab, [path], Fr).Single();

        Assert.False(row.Result!.Questions.Single(q => q.Question.Id == "q1").Manual);
        Assert.Contains(row.Issues, i => i.Message == "La réponse à q1 a changé depuis votre note : à noter de nouveau.");
    }

    [Fact]
    public void Marks_OfAnotherSession_DoNotApply()
    {
        var (lab, path, submission) = Copy("b");
        SmarxMarksService.Save(new SubmissionMarks
        {
            LabId = submission.LabId, SessionId = Guid.NewGuid(),
            Questions = { ["q1"] = new QuestionMark { Points = 1, Answer = "b" } }
        }, path);

        var row = SmarxClassGrader.GradeAll(lab, [path], Fr).Single();

        Assert.Null(row.Marks);
        Assert.Equal(0, row.Result!.Questions.Single(q => q.Question.Id == "q1").EarnedPoints);
    }

    [Fact]
    public void Success_CountsTheGradedCopiesThatMeetEachObjective()
    {
        var (lab, path, _) = Copy("a");
        var rows = SmarxClassGrader.GradeAll(lab, [path], Fr);

        Assert.Equal((1, 1), SmarxClassGrader.Success(rows, "q1"));
        Assert.Equal((0, 1), SmarxClassGrader.Success(rows, "q2"));
    }
}
