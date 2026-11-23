using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using SMADX.Models;
using SMADX.Services;
using SMADX.Services.Labs;

namespace SMADX.Studio.ViewModels
{
    /// <summary>
    /// Un exercice fourni avec SMAD-X, dans le menu Fichier : « identifiant · titre ». L'identifiant est le début du nom
    /// des copies (« chemin-04-write-members_Prénom Nom.smadx-submission.json ») : le formateur retrouve l'exercice d'une copie.
    /// </summary>
    public sealed record BuiltInLabItem(string Id, string Title);

    // Exercices fournis avec SMAD-X : les ouvrir sans le dépôt, pour corriger les copies des apprenants
    public sealed partial class StudioViewModel
    {
        private const string AttackPathCategory = "attack-paths";

        /// <summary>Lus une fois, pour le menu seulement : un exercice ouvert est relu à neuf (voir <see cref="LabService.BuiltInLab"/>).</summary>
        private IReadOnlyList<LabDefinition>? _builtInLabs;

        /// <summary>Exercices généraux fournis avec SMAD-X (menu Exercices > Exercices fournis de SMAD-X), bilingues.</summary>
        public IReadOnlyList<BuiltInLabItem> BuiltInLabs => BuiltInMenu(category: null, language: null);

        /// <summary>Série « Chemins d'attaque » en français (chemin-01 à chemin-15).</summary>
        public IReadOnlyList<BuiltInLabItem> FrenchAttackPathLabs => BuiltInMenu(AttackPathCategory, "fr-FR");

        /// <summary>Série « Chemins d'attaque » en anglais (path-01 à path-15).</summary>
        public IReadOnlyList<BuiltInLabItem> EnglishAttackPathLabs => BuiltInMenu(AttackPathCategory, "en-US");

        /// <summary>
        /// Exercices d'une série, triés par identifiant comme les copies dans un dossier. Contrairement à SMAD-X, Studio propose
        /// les deux langues quelle que soit celle de l'interface : le formateur corrige les copies que ses apprenants ont faites.
        /// </summary>
        private IReadOnlyList<BuiltInLabItem> BuiltInMenu(string? category, string? language)
        {
            var culture = Text.Culture;
            return LoadBuiltInLabs()
                .Where(l => l.Category == category && string.Equals(l.Language, language, StringComparison.OrdinalIgnoreCase))
                .OrderBy(l => l.Id, StringComparer.OrdinalIgnoreCase)
                .Select(l => new BuiltInLabItem(l.Id, $"{l.Id} · {l.Title.Resolve(culture)}"))
                .ToList();
        }

        private IReadOnlyList<LabDefinition> LoadBuiltInLabs()
        {
            if (_builtInLabs != null) return _builtInLabs;
            try { _builtInLabs = _labs.BuiltInLabs(); }
            catch (LabFormatException ex)
            {
                ErrorReporter.Log("Studio", ex);
                _builtInLabs = [];
            }
            return _builtInLabs;
        }

        /// <summary>
        /// Paramètre object : le menu évalue CanExecute avec le DataContext de l'entrée, qui n'est pas toujours un
        /// BuiltInLabItem (un RelayCommand&lt;BuiltInLabItem&gt; lèverait alors une exception).
        /// </summary>
        [RelayCommand]
        private async Task OpenBuiltInLab(object? parameter)
        {
            if (parameter is BuiltInLabItem item)
                await OpenBuiltInLabAsync(item.Id);
        }

        /// <summary>
        /// Ouvre un exercice fourni avec SMAD-X, tel qu'il est embarqué (indices et corrigé compris) : ses copies se corrigent
        /// comme avec <c>SMAD-X.Studio grade</c> sur le fichier du dépôt. Il n'a pas de fichier : « Enregistrer » le range dans
        /// un dossier d'exercice. Faux si l'exercice n'existe pas ou si l'utilisateur garde l'exercice ouvert.
        /// </summary>
        public async Task<bool> OpenBuiltInLabAsync(string id)
        {
            if (!await ConfirmCloseAsync()) return false;
            var opened = false;
            await RunAsync(() =>
            {
                if (_labs.BuiltInLab(id) is not { } lab)
                {
                    Status = string.Format(Text["Submissions.UnknownLab"], id);
                    return;
                }
                Load(lab, null, "");
                MarkSaved(); // tel qu'il est fourni : rien à enregistrer
                if (lab.Language is { } lang) EditCulture = lang;
                Status = string.Format(Text["Status.OpenedBuiltIn"], BuiltInTitle(lab));
                // Un lot déposé a des copies de cet exercice : elles s'y ajoutent, corrigées
                if (_batch?.Find(lab.Id) != null)
                {
                    AddBatchCopies();
                    Status = BatchStatus();
                }
                opened = true;
            });
            return opened;
        }

        private string BuiltInTitle(LabDefinition lab) => lab.Title.Resolve(Text.Culture) is { Length: > 0 } title ? title : lab.Id;

        /// <summary>
        /// Copies déposées sans exercice ouvert : Studio lit l'exercice qu'elles rappellent et ouvre l'exercice fourni qui
        /// porte cet identifiant (le plus fréquent, si elles en rappellent plusieurs ; les autres copies le signalent à la
        /// correction). Sinon, la barre d'état dit quoi ouvrir. Vrai si un exercice est ouvert.
        /// </summary>
        private async Task<bool> OpenBuiltInLabForAsync(IReadOnlyCollection<string> submissions)
        {
            var ids = submissions.Select(LabSubmissionService.ReadLabId).OfType<string>()
                .GroupBy(id => id, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .ToList();
            if (ids.Count == 0)
            {
                Status = Text["Submissions.OpenLabFirst"];
                return false;
            }
            foreach (var id in ids)
                if (LoadBuiltInLabs().Any(l => string.Equals(l.Id, id, StringComparison.OrdinalIgnoreCase)))
                    return await OpenBuiltInLabAsync(id) && Lab != null;
            Status = string.Format(Text["Submissions.UnknownLab"], ids[0]);
            return false;
        }

        /// <summary>Les copies d'un dossier glissé sur la fenêtre ; aucune s'il ne peut pas être lu (la correction le dira).</summary>
        private static string[] SubmissionsIn(string folder)
        {
            try { return Directory.GetFiles(folder, "*" + LabSubmissionService.FileExtension, SearchOption.AllDirectories); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return []; }
        }
    }
}
