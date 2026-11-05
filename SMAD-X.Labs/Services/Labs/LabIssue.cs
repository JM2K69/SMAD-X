using System;

namespace SMADX.Services.Labs
{
    /// <summary>Fichier d'exercice illisible, d'un autre format ou d'une version plus récente.</summary>
    public sealed class LabFormatException : Exception
    {
        public LabFormatException(string message, Exception? inner = null) : base(message, inner)
        {
        }
    }

    public enum LabIssueLevel
    {
        Ok,
        Warning,
        Error
    }

    public sealed record LabIssue(LabIssueLevel Level, string Message, string? ObjectiveId = null);
}
