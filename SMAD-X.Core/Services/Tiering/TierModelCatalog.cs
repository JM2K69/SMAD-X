using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using SMADX.Models;

namespace SMADX.Services.Tiering
{
    /// <summary>
    /// Modèles de cloisonnement fournis avec SMAD-X : SMAD-X.Core/TierModels/*.json, embarqués dans l'assembly.
    /// Ils sont produits par tools/TierModelGenerator à partir de la source officielle de chaque modèle.
    /// </summary>
    public static class TierModelCatalog
    {
        private const string Prefix = "TierModels/";

        /// <summary>Ordre d'affichage : Microsoft, puis HardenAD.</summary>
        private static readonly string[] Order = ["microsoft", "hardenad"];

        public static IReadOnlyList<TierModelDefinition> All { get; } = Load();

        public static TierModelDefinition? Find(string? id) =>
            All.FirstOrDefault(m => string.Equals(m.Id, id, StringComparison.OrdinalIgnoreCase));

        /// <summary>Nom et version, tels qu'affichés : « HardenAD (HardenAD-2.9.9v2) ».</summary>
        public static string DisplayName(TierModelDefinition model) => $"{model.Name} ({model.Source.Version})";

        /// <summary>Source à citer : dépôt, version, commit, copyright et licence.</summary>
        public static string SourceText(TierModelDefinition model) =>
            string.Format(LocalizationService.Instance["TierModel.Source"], model.Source.Repository.Replace("https://", string.Empty),
                model.Source.Version, model.Source.Commit.Length >= 7 ? model.Source.Commit[..7] : model.Source.Commit,
                model.Source.Copyright, model.Source.License);

        /// <summary>Ce que le modèle crée, en une ligne.</summary>
        public static string ContentText(TierModelDefinition model) =>
            string.Format(LocalizationService.Instance["Dialog.TierModel.Content"], model.OrganizationalUnits.Count, model.Groups.Count,
                model.Accounts.Count, model.GroupPolicies.Count, model.Delegations.Count);

        /// <summary>Notes du modèle dans la langue de l'interface.</summary>
        public static IReadOnlyList<string> Notes(TierModelDefinition model) =>
            model.Source.Notes.Select(n => n.Resolve(LocalizationService.Instance.CurrentCulture.Name)).ToList();

        private static List<TierModelDefinition> Load()
        {
            var assembly = typeof(TierModelCatalog).Assembly;
            return assembly.GetManifestResourceNames()
                .Where(r => r.Replace('\\', '/').StartsWith(Prefix, StringComparison.Ordinal) && r.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                .Select(r =>
                {
                    using var reader = new StreamReader(assembly.GetManifestResourceStream(r)!);
                    return JsonSerializer.Deserialize<TierModelDefinition>(reader.ReadToEnd(), ADDataService.SerializerOptions)!;
                })
                .OrderBy(m => Array.IndexOf(Order, m.Id) is var i and >= 0 ? i : Order.Length)
                .ToList();
        }
    }
}
