using SMADX.Models;
using SMARX.Labs;
using SMARX.Models;
using SMARX.Services.Labs;

namespace SMARX.Tests;

/// <summary>
/// Exercices SMAR-X : chaque exercice fourni échoue au départ (gardes respectées) et réussit entièrement avec sa
/// solution, qui n'est écrite qu'ici. Les solutions de facilité (ouvrir à tous, ajouter un compte à un groupe,
/// toucher aux droits NTFS) sont rattrapées par les gardes ou par la règle d'hygiène.
/// </summary>
public class SmarxLabTests
{
    private const string Fr = "fr-FR";

    public static TheoryData<string> LabIds => new(SmarxBuiltInLabs.All().Select(l => l.Id));

    private static SmarxLab Lab(string id) => SmarxBuiltInLabs.All().Single(l => l.Id == id);

    private static SmarxDocument Work(SmarxLab lab) => SmarxLabGrader.Clone(lab.Start);

    /// <summary>La solution de chaque exercice fourni, appliquée à une copie de travail.</summary>
    private static SmarxDocument Solved(SmarxLab lab)
    {
        var work = Work(lab);
        switch (lab.Id)
        {
            case "linux-dossier-equipe":
            {
                var host = work.Linux[0];
                var folder = host.Find(SmarxBuiltInLabs.TeamFolder)!;
                folder.Group = "projet";
                folder.ModeBits = Convert.ToInt32("2770", 8);
                folder.DefaultAcl = new PosixDefaultAcl { UserObj = "rwx", GroupObj = "rwx", Other = "---" };
                var plan = host.Find(SmarxBuiltInLabs.TeamFolder + "/plan.txt")!;
                plan.Group = "projet";
                plan.ModeBits = Convert.ToInt32("0660", 8);
                break;
            }
            case "windows-partage-rh":
                work.Windows[0].Shares.Add(new SmbShare
                {
                    Name = "RH", Path = SmarxBuiltInLabs.RhFolder,
                    Permissions = [ShareAce.Allow("GL_RH", ShareRight.Change)]
                });
                break;
            case "samba-carol-ecrit":
            {
                var host = work.Linux[0];
                host.Share("partage")!.WriteList.Add("carol");
                host.Find(SmarxBuiltInLabs.SharedDoc)!.Acl!.Groups.Single(g => g.Name == "rh").Perm = "rw-";
                break;
            }
            default:
                throw new InvalidOperationException(lab.Id);
        }
        return work;
    }

    private static Dictionary<string, LabAnswer> RightAnswers(SmarxLab lab) =>
        lab.Questions.ToDictionary(q => q.Id, q => new LabAnswer { Choices = q.Choices.Where(c => c.Correct).Select(c => c.Id).ToList() });

    [Theory]
    [MemberData(nameof(LabIds))]
    public void BuiltInLab_FailsAtStart_WithItsGuardsMet(string id)
    {
        var lab = Lab(id);
        var result = SmarxLabGrader.Grade(lab, Work(lab), null, Fr);

        Assert.All(result.Objectives.Where(o => !o.Objective.Guard), o => Assert.False(o.Passed, $"{o.Objective.Id} atteint au départ : {o.Feedback}"));
        Assert.All(result.Objectives.Where(o => o.Objective.Guard), o => Assert.True(o.Passed, $"garde {o.Objective.Id} violée au départ : {o.Feedback}"));
        Assert.True(result.Score < lab.MaxPoints);
    }

    [Theory]
    [MemberData(nameof(LabIds))]
    public void BuiltInLab_IsFullySolvedByItsSolution(string id)
    {
        var lab = Lab(id);
        var result = SmarxLabGrader.Grade(lab, Solved(lab), RightAnswers(lab), Fr);

        Assert.All(result.Objectives, o => Assert.True(o.Passed, $"{o.Objective.Id} : {o.Feedback}"));
        Assert.True(result.Completed);
        Assert.Equal(lab.MaxPoints, result.Score);
    }

    [Theory]
    [MemberData(nameof(LabIds))]
    public void BuiltInLab_HasTextsInBothLanguages(string id)
    {
        var lab = Lab(id);
        foreach (var text in new[] { lab.Title, lab.Statement }.Concat(lab.Objectives.Select(o => o.Title)).Concat(lab.Questions.Select(q => q.Title)))
        {
            Assert.False(string.IsNullOrWhiteSpace(text.GetValueOrDefault("fr-FR")));
            Assert.False(string.IsNullOrWhiteSpace(text.GetValueOrDefault("en-US")));
        }
    }

    /// <summary>Ouvrir le dossier à tous : tout est accessible, mais la garde d'hygiène annule tous les objectifs.</summary>
    [Fact]
    public void TeamFolder_OpenToEveryone_IsCancelledByTheGuard()
    {
        var lab = SmarxBuiltInLabs.LinuxTeamFolder();
        var work = Work(lab);
        var host = work.Linux[0];
        host.Find(SmarxBuiltInLabs.TeamFolder)!.ModeBits = Convert.ToInt32("0777", 8);
        host.Find(SmarxBuiltInLabs.TeamFolder + "/plan.txt")!.ModeBits = Convert.ToInt32("0666", 8);

        var result = SmarxLabGrader.Grade(lab, work, null, Fr);

        Assert.False(result.Objectives.Single(o => o.Objective.Id == "g1").Passed);
        Assert.Equal(0, result.Score);
        Assert.Contains(result.Objectives, o => o.Annulled);
    }

    /// <summary>Ajouter carol au groupe projet viole la garde des comptes : l'objectif qui la concerne est annulé.</summary>
    [Fact]
    public void TeamFolder_AddingCarolToTheGroup_CancelsTheObjectiveAboutHer()
    {
        var lab = SmarxBuiltInLabs.LinuxTeamFolder();
        var work = Solved(lab);
        work.Linux[0].Users.Single(u => u.Name == "carol").Groups.Add("projet");

        var result = SmarxLabGrader.Grade(lab, work, null, Fr);

        Assert.Equal("Comptes modifiés : carol.", result.Objectives.Single(o => o.Objective.Id == "g2").Feedback);
        Assert.True(result.Objectives.Single(o => o.Objective.Id == "o4").Annulled);
        Assert.True(result.Objectives.Single(o => o.Objective.Id == "o1").Passed);
    }

    /// <summary>Un partage RH ouvert à Tout le monde en Contrôle total : NTFS protège encore, mais la règle d'hygiène refuse.</summary>
    [Fact]
    public void RhShare_OpenToEveryone_FailsTheShareRule()
    {
        var lab = SmarxBuiltInLabs.WindowsDepartmentShare();
        var work = Work(lab);
        work.Windows[0].Shares.Add(new SmbShare
        {
            Name = "RH", Path = SmarxBuiltInLabs.RhFolder,
            Permissions = [ShareAce.Allow(WellKnown.Everyone, ShareRight.FullControl)]
        });

        var result = SmarxLabGrader.Grade(lab, work, null, Fr);

        Assert.True(result.Objectives.Single(o => o.Objective.Id == "o1").Passed);
        var rule = result.Objectives.Single(o => o.Objective.Id == "o3");
        Assert.False(rule.Passed);
        Assert.Equal("Règle non respectée : RH : Everyone FullControl.", rule.Feedback);
    }

    /// <summary>Le partage n'existe pas encore : l'explication le dit.</summary>
    [Fact]
    public void RhShare_Missing_IsExplained()
    {
        var lab = SmarxBuiltInLabs.WindowsDepartmentShare();
        var result = SmarxLabGrader.Grade(lab, Work(lab), null, Fr);

        Assert.Equal(@"bob → modifier le contenu de D:\Partages\RH\salaires.xlsx : refusé. (le partage RH n'existe pas)",
            result.Objectives.Single(o => o.Objective.Id == "o1").Feedback);
    }

    /// <summary>Ajouter carol à la write list ne suffit pas : le client ouvre en lecture-écriture, il faut r et w dans une même entrée.</summary>
    [Fact]
    public void SambaDiagnosis_WriteListAlone_IsNotEnough()
    {
        var lab = SmarxBuiltInLabs.SambaDiagnosis();
        var work = Work(lab);
        work.Linux[0].Share("partage")!.WriteList.Add("carol");

        var result = SmarxLabGrader.Grade(lab, work, null, Fr);

        Assert.False(result.Objectives.Single(o => o.Objective.Id == "o1").Passed);
    }

    /// <summary>
    /// « Exactement les accès nécessaires » : sans crédit partiel, il faut toutes les cases justes (les refus déjà justes
    /// au départ ne rapportent rien) ; avec, chaque case juste rapporte sa part.
    /// </summary>
    [Fact]
    public void ExactAccess_PartialCredit_IsChosenByTheTrainer()
    {
        var lab = SmarxBuiltInLabs.LinuxTeamFolder();
        var objective = lab.Objectives.Single(o => o.Id == "o4");

        Assert.Equal(0, SmarxLabGrader.Evaluate(lab, objective, Work(lab), Fr).Fraction);
        objective.Partial = true;
        Assert.InRange(SmarxLabGrader.Evaluate(lab, objective, Work(lab), Fr).Fraction, 0.01, 0.99);
    }

    [Fact]
    public void Lab_RoundTripsThroughItsFile_AndTheSubjectHasNoAnswerKey()
    {
        var lab = SmarxBuiltInLabs.SambaDiagnosis();

        var parsed = SmarxLabService.Parse(SmarxLabService.Serialize(lab));
        var subject = SmarxLabService.Parse(SmarxLabService.SerializeSubject(lab));

        Assert.Equal(SmarxLabService.Serialize(lab), SmarxLabService.Serialize(parsed));
        Assert.True(parsed.Questions.All(q => q.HasAnswerKey));
        Assert.True(subject.Questions.All(q => !q.HasAnswerKey));
        Assert.Equal(lab.Objectives.Count, subject.Objectives.Count);
    }

    [Fact]
    public void Submission_IsSealed_AndATamperedOneIsSpotted()
    {
        var lab = SmarxBuiltInLabs.LinuxTeamFolder();
        var submission = new SmarxSubmission
        {
            Student = "Camille Martin", LabId = lab.Id, SessionId = Guid.NewGuid(), StartedOn = new DateTime(2026, 11, 6, 9, 0, 0),
            SavedAt = new DateTime(2026, 11, 6, 9, 20, 0), ElapsedMinutes = 20, Lab = lab, Work = Solved(lab)
        };

        var json = SmarxLabService.SerializeSubmission(submission);
        var read = SmarxLabService.ParseSubmission(json);
        var tampered = SmarxLabService.ParseSubmission(json.Replace("Camille Martin", "Alex Morgan"));

        Assert.Equal(SubmissionSeal.Valid, read.SealCheck);
        Assert.Equal(SubmissionSeal.Broken, tampered.SealCheck);
        Assert.Equal(lab.MaxPoints, SmarxLabGrader.Grade(read.Lab, read.Work, read.Answers, Fr).Score);
        Assert.Equal("linux-dossier-equipe.camille-martin.smarx-submission.json", SmarxLabService.SubmissionFileName(lab.Id, "Camille Martin"));
    }
}
