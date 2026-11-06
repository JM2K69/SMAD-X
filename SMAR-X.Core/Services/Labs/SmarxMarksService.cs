using System;
using System.IO;
using System.Text.Json;
using SMADX.Models;
using SMADX.Services;
using SMARX.Models;

namespace SMARX.Services.Labs
{
    /// <summary>
    /// Les notes du formateur sur une copie SMAR-X (.smarx-marks.json, à côté d'elle) : les points donnés à la main aux
    /// questions et l'appréciation reprise dans le compte rendu. Le même modèle que SMAD-X ; la copie, scellée, n'est
    /// jamais modifiée.
    /// </summary>
    public static class SmarxMarksService
    {
        public const string FileExtension = ".smarx-marks.json";
        public const string Format = "smarx-marks";

        /// <summary>« X.smarx-submission.json » → « X.smarx-marks.json ».</summary>
        public static string MarksPath(string submissionPath) =>
            (submissionPath.EndsWith(SmarxSubmission.FileExtension, StringComparison.OrdinalIgnoreCase)
                ? submissionPath[..^SmarxSubmission.FileExtension.Length]
                : submissionPath) + FileExtension;

        /// <summary>Notes de la copie, ou null s'il n'y en a pas, si le fichier est illisible ou s'il concerne une autre copie.</summary>
        public static SubmissionMarks? Load(string submissionPath, SmarxSubmission submission)
        {
            var path = MarksPath(submissionPath);
            if (!File.Exists(path)) return null;
            try
            {
                var marks = JsonSerializer.Deserialize<SubmissionMarks>(File.ReadAllText(path), SmarxDocument.SerializerOptions);
                return marks is { Format: Format } && marks.FormatVersion <= SubmissionMarks.CurrentFormatVersion
                       && string.Equals(marks.LabId, submission.LabId, StringComparison.OrdinalIgnoreCase) && marks.SessionId == submission.SessionId
                    ? marks : null;
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
            {
                ErrorReporter.Log("Notes du formateur (SMAR-X)", ex);
                return null;
            }
        }

        public static void Save(SubmissionMarks marks, string submissionPath)
        {
            marks.Format = Format;
            File.WriteAllText(MarksPath(submissionPath), JsonSerializer.Serialize(marks, SmarxDocument.SerializerOptions));
        }
    }
}
