using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using SMADX.Models;

namespace SMADX.Services.Labs
{
    /// <summary>
    /// Sceau d'une copie : le SHA-256 de son contenu sans le sceau, sous forme compacte. Il ne dépend pas de la
    /// mise en page (indentation, fins de ligne) mais du moindre changement de contenu : nom, dates, indices
    /// ou travail modifiés à la main se voient à la correction.
    /// </summary>
    public static class SubmissionSealer
    {
        /// <summary>SHA-256 de la copie sans sa clé « Seal », sous forme compacte.</summary>
        public static string Compute(JsonObject submission)
        {
            var content = (JsonObject)submission.DeepClone();
            content.Remove("Seal");
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content.ToJsonString())));
        }

        /// <summary>État du sceau de la copie telle qu'elle a été lue, avant toute migration.</summary>
        public static SubmissionSeal Check(JsonObject submission) =>
            submission["Seal"]?.GetValue<string>() is not { } seal ? SubmissionSeal.Missing
            : string.Equals(seal, Compute(submission), StringComparison.OrdinalIgnoreCase) ? SubmissionSeal.Valid
            : SubmissionSeal.Broken;
    }
}
