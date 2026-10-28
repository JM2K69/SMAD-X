using System;
using System.IO;
using System.Text.Json;
using SMADX.Models;

namespace SMADX.Services.Labs
{
    /// <summary>Lecture et écriture des notes du formateur (.smadx-marks.json), rangées à côté de chaque copie.</summary>
    public static class SubmissionMarksService
    {
        public const string FileExtension = ".smadx-marks.json";

        /// <summary>« X.smadx-submission.json » → « X.smadx-marks.json ».</summary>
        public static string MarksPath(string submissionPath) =>
            (submissionPath.EndsWith(LabSubmissionService.FileExtension, StringComparison.OrdinalIgnoreCase)
                ? submissionPath[..^LabSubmissionService.FileExtension.Length]
                : submissionPath) + FileExtension;

        /// <summary>Notes de la copie, ou null s'il n'y en a pas, si le fichier est illisible ou s'il concerne une autre copie.</summary>
        public static SubmissionMarks? Load(string submissionPath, LabSubmission submission)
        {
            var path = MarksPath(submissionPath);
            if (!File.Exists(path)) return null;
            try
            {
                var marks = JsonSerializer.Deserialize<SubmissionMarks>(File.ReadAllText(path), ADDataService.SerializerOptions);
                return marks is { Format: "smadx-marks" } && marks.FormatVersion <= SubmissionMarks.CurrentFormatVersion
                       && string.Equals(marks.LabId, submission.LabId, StringComparison.OrdinalIgnoreCase) && marks.SessionId == submission.SessionId
                    ? marks : null;
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
            {
                ErrorReporter.Log("Notes du formateur", ex);
                return null;
            }
        }

        public static void Save(SubmissionMarks marks, string submissionPath) =>
            File.WriteAllText(MarksPath(submissionPath), JsonSerializer.Serialize(marks, ADDataService.SerializerOptions));
    }
}
