using System;
using System.Collections.Generic;

namespace SMADX.Models
{
    /// <summary>
    /// Notes du formateur sur une copie, au format .smadx-marks.json, à côté d'elle : les points donnés à la main
    /// aux questions et l'appréciation reprise dans le compte rendu de l'apprenant. La copie, scellée, n'est
    /// jamais modifiée.
    /// </summary>
    public sealed class SubmissionMarks
    {
        public const int CurrentFormatVersion = 1;

        public string Format { get; set; } = "smadx-marks";
        public int FormatVersion { get; set; } = CurrentFormatVersion;

        /// <summary>Exercice et session de la copie notée : une note ne s'applique pas à une autre copie.</summary>
        public string LabId { get; set; } = string.Empty;
        public Guid SessionId { get; set; }

        /// <summary>Points donnés à la main, par identifiant de question.</summary>
        public Dictionary<string, QuestionMark> Questions { get; set; } = new();

        /// <summary>Appréciation du formateur, reprise dans le compte rendu individuel.</summary>
        public string? Comment { get; set; }
    }

    public sealed class QuestionMark
    {
        public double Points { get; set; }

        /// <summary>Réponse notée (<see cref="LabAnswer.Signature"/>) : si l'apprenant rend une autre réponse, la note ne s'applique plus.</summary>
        public string Answer { get; set; } = string.Empty;
    }
}
