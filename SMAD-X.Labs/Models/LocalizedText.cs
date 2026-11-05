using System;
using System.Collections.Generic;
using System.Linq;

namespace SMADX.Models
{
    /// <summary>Texte par culture (« fr-FR », « en-US »), avec repli sur le français puis sur le premier texte.</summary>
    public sealed class LocalizedText : Dictionary<string, string>
    {
        public LocalizedText() : base(StringComparer.OrdinalIgnoreCase)
        {
        }

        public static LocalizedText Of(string fr, string en) => new() { ["fr-FR"] = fr, ["en-US"] = en };

        public string Resolve(string culture) =>
            TryGetValue(culture, out var text) ? text
            : TryGetValue("fr-FR", out var fr) ? fr
            : Values.FirstOrDefault() ?? string.Empty;
    }

    public enum LabLevel
    {
        Beginner,
        Intermediate,
        Advanced
    }
}
