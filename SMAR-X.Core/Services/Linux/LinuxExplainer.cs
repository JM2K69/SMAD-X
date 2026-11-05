using System;
using System.Collections.Generic;
using System.Linq;
using SMARX.Models;

namespace SMARX.Services.Linux
{
    /// <summary>Une étape expliquée : ce qui est vérifié (« Traverser /srv ») et pourquoi c'est accordé ou refusé.</summary>
    public sealed record ExplainedStep(AccessStep Step, string Title, string Detail);

    /// <summary>
    /// Explique un accès en français ou en anglais, une ligne par étape vérifiée, puis le résultat :
    /// « Traverser /srv : bob relève des autres (r-x) → x accordé. »
    /// </summary>
    public static class LinuxExplainer
    {
        public static IReadOnlyList<string> Explain(AccessResult result, string culture)
        {
            if (Problem(result, culture) is { } problem)
                return [problem];
            return Steps(result, culture).Select(s => $"{s.Title}{Text(culture, " : ", ": ")}{s.Detail}")
                .Append(Verdict(result, culture))
                .ToList();
        }

        /// <summary>Le chemin n'existe pas, ou l'opération ne s'y applique pas ; null sinon.</summary>
        public static string? Problem(AccessResult result, string culture) => result.Problem switch
        {
            AccessProblem.NotFound => Text(culture, $"{result.Path} n'existe pas.", $"{result.Path} does not exist."),
            AccessProblem.NotApplicable => Text(culture, $"« {Operation(result.Operation, culture)} » ne s'applique pas à {result.Path}.",
                                                         $"\"{Operation(result.Operation, culture)}\" does not apply to {result.Path}."),
            _ => null
        };

        /// <summary>« Résultat : bob peut lire /srv/compta/budget.ods. »</summary>
        public static string Verdict(AccessResult result, string culture)
        {
            var action = $"{Lower(Operation(result.Operation, culture))} {result.Path}";
            return result.Allowed
                ? Text(culture, $"Résultat : {result.User} peut {action}.", $"Result: {result.User} can {action}.")
                : Text(culture, $"Résultat : {result.User} ne peut pas {action}.", $"Result: {result.User} cannot {action}.");
        }

        public static IReadOnlyList<ExplainedStep> Steps(AccessResult result, string culture) =>
            result.Steps.Select(s => Step(result, s, culture)).ToList();

        /// <summary>
        /// Un accès par un partage Samba : le compte Samba, l'identité Unix de smbd, l'entrée dans le dossier partagé,
        /// l'écriture permise ou non par le partage, puis les étapes du noyau.
        /// </summary>
        public static IReadOnlyList<(bool? Passed, string Title, string Detail)> SambaSteps(LinuxHost host, SambaAccessResult result, string culture)
        {
            var share = result.Share;
            var steps = new List<(bool?, string, string)>();
            if (result.Refusal == SambaRefusal.NoSambaAccount)
            {
                steps.Add((false, Text(culture, "Compte Samba", "Samba account"),
                    Text(culture, $"{result.User} n'a pas de mot de passe Samba (smbpasswd -a) : la connexion est refusée.",
                                  $"{result.User} has no Samba password (smbpasswd -a): the connection is denied.")));
                return steps;
            }
            var lists = share.ValidUsers.Count == 0 && share.InvalidUsers.Count == 0
                ? Text(culture, "le partage n'a ni valid users ni invalid users : tout compte Samba est admis.", "the share has neither valid users nor invalid users: every Samba account is admitted.")
                : Text(culture, $"valid users = {Or(share.ValidUsers, culture)} ; invalid users = {Or(share.InvalidUsers, culture)}.",
                                $"valid users = {Or(share.ValidUsers, culture)}; invalid users = {Or(share.InvalidUsers, culture)}.");
            var admitted = result.Refusal != SambaRefusal.NotValidUser;
            steps.Add((admitted, Text(culture, $"Compte {result.User} admis ?", $"Is {result.User} admitted?"),
                lists + (admitted ? "" : Text(culture, " La connexion est refusée.", " The connection is denied."))));
            if (!admitted) return steps;

            var unix = result.Unix;
            var forced = share.ForceUser != null || share.ForceGroup != null;
            steps.Add((null, Text(culture, "Identité Unix", "Unix identity"),
                Text(culture, $"smbd agit sous {unix.Name}, groupe principal {unix.PrimaryGroup}", $"smbd acts as {unix.Name}, primary group {unix.PrimaryGroup}")
                + (forced ? Text(culture, $" (force user = {share.ForceUser ?? "—"}, force group = {share.ForceGroup ?? "—"}).", $" (force user = {share.ForceUser ?? "—"}, force group = {share.ForceGroup ?? "—"}).") : ".")));

            var entered = result.Refusal != SambaRefusal.CannotEnterShare;
            steps.Add((entered, Text(culture, $"Entrer dans {share.Path}", $"Enter {share.Path}"),
                entered ? Text(culture, $"smbd entre dans le dossier partagé sous {unix.Name}.", $"smbd enters the shared directory as {unix.Name}.")
                        : Text(culture, $"{unix.Name} n'a pas x sur {share.Path} ou un dossier qui y mène : smbd n'y entre pas, la connexion est refusée.",
                                        $"{unix.Name} lacks x on {share.Path} or a directory leading to it: smbd cannot enter, the connection is denied.")));
            if (!entered) return steps;

            if (result.Operation is LinuxOperation.Write or LinuxOperation.Create or LinuxOperation.Delete or LinuxOperation.ChangePermissions)
            {
                var account = host.User(result.User) ?? result.Unix;
                var why = SambaShare.Matches(share.ReadList, account)
                    ? Text(culture, "le compte est dans read list", "the account is in the read list")
                    : result.Writable && share.ReadOnly ? Text(culture, "read only = yes, mais le compte est dans write list", "read only = yes, but the account is in the write list")
                    : share.ReadOnly ? "read only = yes" : "read only = no";
                steps.Add((result.Writable, Text(culture, "Le partage permet-il d'écrire ?", "Does the share allow writing?"),
                    why + (result.Writable ? Text(culture, " : Samba laisse passer, le noyau décide.", ": Samba lets it through, the kernel decides.")
                                           : Text(culture, " : Samba refuse avant le noyau.", ": Samba denies before the kernel."))));
                if (!result.Writable) return steps;
            }
            if (result.Operation == LinuxOperation.Write)
                steps.Add((null, Text(culture, "Ouvrir en lecture-écriture", "Open read-write"),
                    Text(culture, "le client ouvre le fichier en lecture et en écriture : il faut r et w, donnés par une même entrée.",
                                  "the client opens the file for reading and writing: r and w are needed, from one entry.")));
            if (result.Kernel is { } kernel)
                steps.AddRange(Steps(kernel, culture).Select(s => ((bool?)s.Step.Passed, s.Title, s.Detail)));
            return steps;
        }

        /// <summary>Le résultat d'un accès par un partage, sur son chemin réseau, et qui refuse : Samba ou le noyau.</summary>
        public static string SambaVerdict(SambaAccessResult result, string culture)
        {
            var action = $"{Lower(Operation(result.Operation, culture))} {result.Unc}";
            var verdict = result.Allowed
                ? Text(culture, $"Résultat : {result.User} peut {action}.", $"Result: {result.User} can {action}.")
                : Text(culture, $"Résultat : {result.User} ne peut pas {action}.", $"Result: {result.User} cannot {action}.");
            if (result.Allowed) return verdict;
            return verdict + (result.Refusal == SambaRefusal.None
                ? Text(culture, " Samba laisse passer, le noyau refuse.", " Samba lets it through, the kernel denies.")
                : Text(culture, " Samba refuse avant le noyau.", " Samba denies before the kernel."));
        }

        private static string Or(List<string> list, string culture) => list.Count == 0 ? Text(culture, "(vide)", "(empty)") : string.Join(' ', list);

        public static string Operation(LinuxOperation operation, string culture) => operation switch
        {
            LinuxOperation.Read => Text(culture, "Lire", "Read"),
            LinuxOperation.Write => Text(culture, "Modifier le contenu de", "Write to"),
            LinuxOperation.Execute => Text(culture, "Exécuter", "Execute"),
            LinuxOperation.ExecuteScript => Text(culture, "Exécuter le script", "Run the script"),
            LinuxOperation.List => Text(culture, "Lister", "List"),
            LinuxOperation.Enter => Text(culture, "Entrer dans", "Enter"),
            LinuxOperation.Create => Text(culture, "Créer dans", "Create in"),
            LinuxOperation.Delete => Text(culture, "Supprimer", "Delete"),
            LinuxOperation.ChangePermissions => Text(culture, "Modifier les droits de", "Change the permissions of"),
            LinuxOperation.ChangeOwner => Text(culture, "Changer le propriétaire de", "Change the owner of"),
            LinuxOperation.ChangeGroup => Text(culture, "Changer le groupe de", "Change the group of"),
            _ => operation.ToString()
        };

        private static ExplainedStep Step(AccessResult result, AccessStep step, string culture)
        {
            string T(string fr, string en) => Text(culture, fr, en);
            var verdict = step.Passed ? T("autorisé", "allowed") : T("refusé", "denied");
            return step.Kind switch
            {
                AccessStepKind.Traverse => new(step, T($"Traverser {step.Path}", $"Traverse {step.Path}"), Check(result.User, step.Check!, culture)),
                AccessStepKind.Permission => new(step,
                    result.Operation == LinuxOperation.Delete ? T($"Écrire dans {step.Path}", $"Write to {step.Path}") : $"{Operation(result.Operation, culture)} {step.Path}",
                    Check(result.User, step.Check!, culture)),
                AccessStepKind.StickyBit => new(step, T($"Sticky bit sur {step.Path}", $"Sticky bit on {step.Path}"),
                    T($"seuls le propriétaire du fichier ({step.Owner}), celui du dossier ({step.DirectoryOwner}) et root peuvent supprimer → {verdict}.",
                      $"only the file's owner ({step.Owner}), the directory's owner ({step.DirectoryOwner}) and root can delete → {verdict}.")),
                AccessStepKind.OwnerOnly => new(step, T("Propriétaire seulement", "Owner only"),
                    T($"réservé au propriétaire ({step.Owner}){(result.Operation == LinuxOperation.ChangeGroup ? ", vers un de ses groupes," : "")} et à root, quels que soient les droits → {verdict}.",
                      $"only the owner ({step.Owner}){(result.Operation == LinuxOperation.ChangeGroup ? ", to one of their groups," : "")} and root, whatever the permissions → {verdict}.")),
                AccessStepKind.RootOnly => new(step, T("root seulement", "root only"),
                    T($"réservé à root, même pour le propriétaire ({step.Owner}) → {verdict}.", $"only root, even for the owner ({step.Owner}) → {verdict}.")),
                AccessStepKind.NotEmpty => new(step, T("Dossier non vide", "Directory not empty"),
                    T($"{step.Path} n'est pas vide : il faut d'abord supprimer son contenu.", $"{step.Path} is not empty: its content must be deleted first.")),
                _ => new(step, step.Kind.ToString(), "")
            };
        }

        /// <summary>Qui accorde ou refuse, et pourquoi : « bob est membre du groupe propriétaire « compta » (r-x) → x accordé. »</summary>
        private static string Check(string user, PermissionCheck check, string culture)
        {
            string T(string fr, string en) => Text(culture, fr, en);
            var needed = Perms.Format(check.Needed).Replace("-", "");
            var perm = Perms.Format(check.Entry.Perm);
            var masked = check.Mask is { } mask && check.Effective != check.Entry.Perm
                ? T($", limité par le masque ({Perms.Format(mask)}) à {Perms.Format(check.Effective)}",
                    $", limited by the mask ({Perms.Format(mask)}) to {Perms.Format(check.Effective)}")
                : "";

            var who = check.Entry.Class switch
            {
                AccessClass.Owner => T($"{user} en est le propriétaire ({perm})", $"{user} is the owner ({perm})"),
                AccessClass.NamedUser => T($"l'ACL nomme {user} ({perm}{masked})", $"the ACL names {user} ({perm}{masked})"),
                AccessClass.OwningGroup => T($"{user} est membre du groupe propriétaire « {check.Entry.Name} » ({perm}{masked})",
                                             $"{user} is a member of the owning group \"{check.Entry.Name}\" ({perm}{masked})"),
                AccessClass.NamedGroup => T($"l'ACL donne {perm} au groupe « {check.Entry.Name} », dont {user} est membre{masked}",
                                            $"the ACL gives {perm} to the group \"{check.Entry.Name}\", which {user} belongs to{masked}"),
                AccessClass.Other => T($"{user} relève des autres ({perm})", $"{user} falls under others ({perm})"),
                AccessClass.Root => check.Granted
                    ? T("root passe outre les droits", "root overrides the permissions")
                    : T("même root ne peut pas exécuter un fichier qui n'a aucun bit x", "even root cannot execute a file that has no x bit"),
                _ => check.Entry.Class.ToString()
            };

            var why = "";
            if (!check.Granted && check.Entry.Class == AccessClass.Owner)
                why = T(" ; les droits du groupe et des autres ne s'appliquent pas au propriétaire", "; the group's and others' permissions do not apply to the owner");
            else if (!check.Granted && check.Groups.Count > 1)
                why = T($" ; aucune entrée de ses groupes ne suffit seule : {string.Join(", ", check.Groups.Select(g => $"« {g.Name} » {Perms.Format(g.Perm)}"))}",
                        $"; none of their groups' entries is enough on its own: {string.Join(", ", check.Groups.Select(g => $"\"{g.Name}\" {Perms.Format(g.Perm)}"))}");
            else if (!check.Granted && check.Entry.Class is AccessClass.OwningGroup or AccessClass.NamedGroup)
                why = T(" ; les droits des autres ne s'appliquent pas aux membres d'un groupe qui a une entrée", "; others' permissions do not apply to members of a group that has an entry");

            var verdict = check.Entry.Class == AccessClass.Root
                ? (check.Granted ? T("accordé", "granted") : T("refusé", "denied"))
                : check.Granted ? T($"{needed} accordé", $"{needed} granted") : T($"il faut {needed} : refusé", $"{needed} needed: denied");
            return $"{who}{why} → {verdict}.";
        }

        private static string Lower(string text) => text.Length == 0 ? text : char.ToLowerInvariant(text[0]) + text[1..];

        private static string Text(string culture, string fr, string en) => culture.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? en : fr;
    }
}
