using System;
using System.Collections.Generic;
using System.Linq;
using SMARX.Models;

namespace SMARX.Services.Windows
{
    /// <summary>Une étape expliquée d'un accès Windows : son titre (« ACE 2, explicite ») et ce qu'elle décide.</summary>
    public sealed record WindowsExplainedStep(bool? Passed, string Title, string Detail);

    /// <summary>
    /// Explique un accès Windows en français ou en anglais : le jeton de la session, la traversée non vérifiée, puis
    /// chaque ACE qui s'applique au jeton, dans l'ordre de la DACL, jusqu'à la décision.
    /// </summary>
    public static class WindowsExplainer
    {
        public static string Operation(WindowsOperation operation, string culture) => operation switch
        {
            WindowsOperation.Read => T(culture, "Lire", "Read"),
            WindowsOperation.Write => T(culture, "Modifier le contenu de", "Write to"),
            WindowsOperation.Execute => T(culture, "Exécuter", "Execute"),
            WindowsOperation.List => T(culture, "Lister", "List"),
            WindowsOperation.CreateFile => T(culture, "Créer un fichier dans", "Create a file in"),
            WindowsOperation.CreateFolder => T(culture, "Créer un dossier dans", "Create a folder in"),
            WindowsOperation.Delete => T(culture, "Supprimer", "Delete"),
            WindowsOperation.ReadPermissions => T(culture, "Lire les autorisations de", "Read the permissions of"),
            WindowsOperation.ChangePermissions => T(culture, "Modifier les autorisations de", "Change the permissions of"),
            WindowsOperation.TakeOwnership => T(culture, "Prendre possession de", "Take ownership of"),
            _ => operation.ToString()
        };

        /// <summary>Nom d'un droit spécial ou d'une autorisation de base, comme dans l'onglet Sécurité de Windows.</summary>
        public static string Rights(NtfsRights rights, string culture)
        {
            var french = !culture.StartsWith("en", StringComparison.OrdinalIgnoreCase);
            string Name(NtfsRights r) => r switch
            {
                NtfsRights.FullControl => french ? "Contrôle total" : "Full control",
                NtfsRights.Modify => french ? "Modification" : "Modify",
                NtfsRights.ReadAndExecute => french ? "Lecture et exécution" : "Read & execute",
                NtfsRights.Read => french ? "Lecture" : "Read",
                NtfsRights.Write => french ? "Écriture" : "Write",
                NtfsRights.ExecuteFile => french ? "Parcours du dossier/exécuter le fichier" : "Traverse folder / execute file",
                NtfsRights.ReadData => french ? "Liste du dossier/lecture de données" : "List folder / read data",
                NtfsRights.ReadAttributes => french ? "Attributs de lecture" : "Read attributes",
                NtfsRights.ReadExtendedAttributes => french ? "Lecture des attributs étendus" : "Read extended attributes",
                NtfsRights.WriteData => french ? "Création de fichiers/écriture de données" : "Create files / write data",
                NtfsRights.AppendData => french ? "Création de dossiers/ajout de données" : "Create folders / append data",
                NtfsRights.WriteAttributes => french ? "Attributs d'écriture" : "Write attributes",
                NtfsRights.WriteExtendedAttributes => french ? "Écriture d'attributs étendus" : "Write extended attributes",
                NtfsRights.DeleteSubdirectoriesAndFiles => french ? "Suppression de sous-dossier et fichier" : "Delete subfolders and files",
                NtfsRights.Delete => french ? "Suppression" : "Delete",
                NtfsRights.ReadPermissions => french ? "Autorisations de lecture" : "Read permissions",
                NtfsRights.ChangePermissions => french ? "Modifier les autorisations" : "Change permissions",
                NtfsRights.TakeOwnership => french ? "Appropriation" : "Take ownership",
                NtfsRights.Synchronize => french ? "Synchroniser" : "Synchronize",
                _ => Ntfs.Format(r)
            };
            if (Ntfs.SimpleOf(rights) is { } simple)
                return Name(simple);
            if (Ntfs.Special.Any(s => s.Right == rights))
                return Name(rights);
            return string.Join(", ", Ntfs.Split(rights).Select(Name));
        }

        /// <summary>À quoi s'applique une ACE posée sur un dossier, comme la colonne « S'applique à » de Windows.</summary>
        public static string AppliesTo(AceInheritance inheritance, bool folder, string culture)
        {
            if (!folder) return T(culture, "Ce fichier", "This file");
            var flags = inheritance & ~AceInheritance.NoPropagate;
            var text = flags switch
            {
                AceInheritance.None => T(culture, "Ce dossier seulement", "This folder only"),
                AceInheritance.ThisFolderSubfoldersAndFiles => T(culture, "Ce dossier, les sous-dossiers et les fichiers", "This folder, subfolders and files"),
                AceInheritance.ContainerInherit => T(culture, "Ce dossier et les sous-dossiers", "This folder and subfolders"),
                AceInheritance.ObjectInherit => T(culture, "Ce dossier et les fichiers", "This folder and files"),
                AceInheritance.ThisFolderSubfoldersAndFiles | AceInheritance.InheritOnly => T(culture, "Les sous-dossiers et les fichiers seulement", "Subfolders and files only"),
                AceInheritance.ContainerInherit | AceInheritance.InheritOnly => T(culture, "Les sous-dossiers seulement", "Subfolders only"),
                AceInheritance.ObjectInherit | AceInheritance.InheritOnly => T(culture, "Les fichiers seulement", "Files only"),
                _ => inheritance.ToString()
            };
            return inheritance.HasFlag(AceInheritance.NoPropagate) ? text + T(culture, " (enfants directs)", " (direct children)") : text;
        }

        /// <summary>Le jeton, la traversée, puis les étapes de chaque AccessCheck.</summary>
        public static IReadOnlyList<WindowsExplainedStep> Steps(WindowsHost host, WindowsAccessResult result, string culture)
        {
            var french = !culture.StartsWith("en", StringComparison.OrdinalIgnoreCase);
            var steps = new List<WindowsExplainedStep>();
            if (result.NotFound || result.NotApplicable) return steps;

            var sids = string.Join(", ", result.Token.Sids.Select(s => WellKnown.Display(s.Name, french)
                + (s.DenyOnly ? T(culture, " (refus seulement : session non élevée)", " (deny only: session not elevated)") : "")));
            steps.Add(new(null, T(culture, $"Jeton de {result.User}", $"{result.User}'s token"),
                T(culture, $"session locale : {sids}.", $"local session: {sids}.")));
            steps.Add(new(null, T(culture, "Dossiers du chemin", "Folders of the path"),
                T(culture, "non vérifiés : tout le monde a le privilège « Contourner la vérification de parcours ».",
                           "not checked: everyone has the \"Bypass traverse checking\" privilege.")));

            for (var c = 0; c < result.Checks.Count; c++)
            {
                var check = result.Checks[c];
                if (result.Operation == WindowsOperation.Delete)
                    steps.Add(new(null, c == 0 ? T(culture, $"Supprimer par {check.Path}", $"Delete through {check.Path}") : T(culture, $"Ou par son dossier {check.Path}", $"Or through its folder {check.Path}"),
                        T(culture, $"il faut « {Rights(check.Desired, culture)} ».", $"\"{Rights(check.Desired, culture)}\" is needed.")));
                else
                    steps.Add(new(null, $"{Operation(result.Operation, culture)} {check.Path}",
                        T(culture, $"il faut « {Rights(check.Desired, culture)} ».", $"\"{Rights(check.Desired, culture)}\" is needed.")));
                foreach (var step in check.Steps)
                    steps.Add(Step(host, check, step, culture, french));
            }
            if (result.NotEmpty)
                steps.Add(new(false, T(culture, "Dossier non vide", "Folder not empty"),
                    T(culture, $"{result.Path} n'est pas vide : il faut d'abord supprimer son contenu.", $"{result.Path} is not empty: its content must be deleted first.")));
            return steps;
        }

        private static WindowsExplainedStep Step(WindowsHost host, AccessCheckResult check, CheckStep step, string culture, bool french)
        {
            var rights = Rights(step.Rights, culture);
            switch (step.Kind)
            {
                case CheckStepKind.Owner:
                    return new(true, T(culture, "Propriétaire", "Owner"),
                        T(culture, $"{WellKnown.Display(check.Owner, true)} est propriétaire : « {rights} » accordé d'office.",
                                   $"{check.Owner} is the owner: \"{rights}\" granted implicitly."));
                case CheckStepKind.Privilege:
                    return new(true, T(culture, "Privilège", "Privilege"),
                        T(culture, "administrateur en session élevée : le privilège « Prendre possession » accorde l'appropriation.",
                                   "administrator in an elevated session: the \"Take ownership\" privilege grants it."));
                case CheckStepKind.Missing:
                    return new(false, T(culture, "Fin de la liste", "End of the list"),
                        T(culture, $"aucune ACE n'accorde « {rights} » : sans autorisation, c'est refusé.",
                                   $"no ACE grants \"{rights}\": without a permission, access is denied."));
            }

            var ace = step.Ace!;
            var origin = ace.Inherited ? T(culture, $"héritée de {ace.InheritedFrom}", $"inherited from {ace.InheritedFrom}") : T(culture, "explicite", "explicit");
            var trustee = WellKnown.Display(ace.Trustee, french);
            // Des droits spéciaux nombreux se lisent mieux en abréviations d'icacls : « (X,RD,WD,…) »
            var named = Ntfs.SimpleOf(ace.Mask) != null || Ntfs.Special.Any(s => s.Right == ace.Mask);
            var granted = named ? T(culture, $"« {Rights(ace.Mask, culture)} »", $"\"{Rights(ace.Mask, culture)}\"") : Ntfs.Format(ace.Mask);
            var what = ace.Type == AceType.Allow
                ? T(culture, $"Autoriser {granted} à {trustee}", $"Allow {granted} to {trustee}")
                : T(culture, $"Refuser {granted} à {trustee}", $"Deny {granted} to {trustee}");
            var title = T(culture, $"ACE {step.Index}, {origin}", $"ACE {step.Index}, {origin}");
            return step.Kind switch
            {
                CheckStepKind.Allow => new(true, title, T(culture, $"{what} → accorde « {rights} ».", $"{what} → grants \"{rights}\".")),
                CheckStepKind.DenyBlocks => new(false, title, T(culture, $"{what} → refuse « {rights} », pas encore accordé.", $"{what} → denies \"{rights}\", not granted yet.")),
                _ => new(null, title, T(culture, $"{what} → sans effet : ce qu'il refuse est déjà accordé par une ACE lue avant, ou n'est pas demandé.",
                                                 $"{what} → no effect: what it denies was already granted by an earlier ACE, or is not requested."))
            };
        }

        public static string Verdict(WindowsAccessResult result, string culture)
        {
            if (result.NotFound) return T(culture, $"{result.Path} n'existe pas.", $"{result.Path} does not exist.");
            if (result.NotApplicable) return T(culture, $"« {Operation(result.Operation, culture)} » ne s'applique pas à {result.Path}.", $"\"{Operation(result.Operation, culture)}\" does not apply to {result.Path}.");
            var action = $"{Lower(Operation(result.Operation, culture))} {result.Path}";
            return result.Allowed
                ? T(culture, $"Résultat : {result.User} peut {action}.", $"Result: {result.User} can {action}.")
                : T(culture, $"Résultat : {result.User} ne peut pas {action}.", $"Result: {result.User} cannot {action}.");
        }

        private static string Lower(string text) => text.Length == 0 ? text : char.ToLowerInvariant(text[0]) + text[1..];

        private static string T(string culture, string fr, string en) => culture.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? en : fr;
    }
}
