using System.Collections.Generic;
using System.Linq;
using SMARX.Models;

namespace SMARX.Services.Linux
{
    /// <summary>Ce qu'un compte tente de faire en local (session sur le serveur : console, SSH).</summary>
    public enum LinuxOperation
    {
        /// <summary>Lire un fichier : r.</summary>
        Read,
        /// <summary>Modifier le contenu d'un fichier : w.</summary>
        Write,
        /// <summary>Exécuter un programme compilé : x.</summary>
        Execute,
        /// <summary>Exécuter un script : l'interpréteur doit aussi le lire, donc r et x.</summary>
        ExecuteScript,
        /// <summary>Lister les noms d'un dossier : r.</summary>
        List,
        /// <summary>Entrer dans un dossier ou le traverser : x.</summary>
        Enter,
        /// <summary>Créer, renommer ou supprimer dans un dossier : w et x.</summary>
        Create,
        /// <summary>Supprimer un fichier ou un dossier vide : w et x sur le dossier parent, pas sur lui ; sticky bit.</summary>
        Delete,
        /// <summary>chmod, setfacl : le propriétaire ou root, quels que soient les droits.</summary>
        ChangePermissions,
        /// <summary>chown : root seulement.</summary>
        ChangeOwner,
        /// <summary>chgrp : le propriétaire, vers un de ses groupes, ou root.</summary>
        ChangeGroup
    }

    public enum AccessStepKind
    {
        /// <summary>x sur un dossier du chemin.</summary>
        Traverse,
        /// <summary>Droits demandés sur le fichier ou le dossier visé (ou son parent pour supprimer).</summary>
        Permission,
        /// <summary>Dossier avec sticky bit : seuls le propriétaire du fichier, celui du dossier ou root suppriment.</summary>
        StickyBit,
        /// <summary>Réservé au propriétaire et à root.</summary>
        OwnerOnly,
        /// <summary>Réservé à root.</summary>
        RootOnly,
        /// <summary>Un dossier ne se supprime que vide.</summary>
        NotEmpty
    }

    /// <summary>
    /// Une étape de la vérification. <see cref="Owner"/> : propriétaire du fichier ; <see cref="DirectoryOwner"/> :
    /// propriétaire du dossier qui a le sticky bit.
    /// </summary>
    public sealed record AccessStep(AccessStepKind Kind, string Path, bool Passed, PermissionCheck? Check = null, string? Owner = null, string? DirectoryOwner = null);

    public enum AccessProblem
    {
        None,
        /// <summary>Le chemin n'existe pas.</summary>
        NotFound,
        /// <summary>L'opération ne s'applique pas à ce type d'objet (lire un dossier, lister un fichier, supprimer « / »).</summary>
        NotApplicable
    }

    /// <summary>Résultat d'un accès : autorisé ou non, et toutes les étapes vérifiées, dans l'ordre, jusqu'à la première qui échoue.</summary>
    public sealed record AccessResult(string User, string Path, LinuxOperation Operation, bool Allowed, IReadOnlyList<AccessStep> Steps, AccessProblem Problem = AccessProblem.None);

    /// <summary>Une ligne de la matrice d'accès : ce qu'un compte peut faire sur un fichier ou un dossier.</summary>
    public sealed record AccessRow(LinuxUser User, IReadOnlyDictionary<LinuxOperation, AccessResult> Results);

    /// <summary>
    /// Accès local à un serveur Linux : chaque dossier du chemin doit être traversable (x), puis l'opération demande
    /// ses propres droits, sur le fichier visé ou, pour supprimer, sur le dossier qui le contient.
    /// </summary>
    public static class LinuxAccess
    {
        public static readonly LinuxOperation[] FileOperations =
            [LinuxOperation.Read, LinuxOperation.Write, LinuxOperation.Execute, LinuxOperation.Delete, LinuxOperation.ChangePermissions];

        public static readonly LinuxOperation[] DirectoryOperations =
            [LinuxOperation.List, LinuxOperation.Enter, LinuxOperation.Create, LinuxOperation.Delete, LinuxOperation.ChangePermissions];

        public static AccessResult Check(LinuxHost host, LinuxUser user, string path, LinuxOperation operation)
        {
            var steps = new List<AccessStep>();
            AccessResult Done(bool allowed, AccessProblem problem = AccessProblem.None) => new(user.Name, path, operation, allowed, steps, problem);

            var node = host.Find(path);
            if (node == null)
                return Done(false, AccessProblem.NotFound);
            if (!Applies(operation, node, path))
                return Done(false, AccessProblem.NotApplicable);

            foreach (var (directory, directoryPath) in host.Ancestors(path))
            {
                var traverse = LinuxPermissions.Check(user, directory, directoryPath, Perm.Execute);
                steps.Add(new AccessStep(AccessStepKind.Traverse, directoryPath, traverse.Granted, traverse));
                if (!traverse.Granted)
                    return Done(false);
            }

            switch (operation)
            {
                case LinuxOperation.Delete:
                    var parentPath = LinuxHost.Parent(path);
                    var parent = host.Find(parentPath)!;
                    // Le dossier parent a déjà été traversé : il faut en plus y écrire
                    var write = LinuxPermissions.Check(user, parent, parentPath, Perm.Write | Perm.Execute);
                    steps.Add(new AccessStep(AccessStepKind.Permission, parentPath, write.Granted, write));
                    if (!write.Granted)
                        return Done(false);
                    if (parent.Sticky)
                    {
                        var allowed = user.IsRoot || user.Name == node.Owner || user.Name == parent.Owner;
                        steps.Add(new AccessStep(AccessStepKind.StickyBit, parentPath, allowed, Owner: node.Owner, DirectoryOwner: parent.Owner));
                        if (!allowed)
                            return Done(false);
                    }
                    if (node.IsDirectory && node.Children.Count > 0)
                    {
                        steps.Add(new AccessStep(AccessStepKind.NotEmpty, path, false));
                        return Done(false);
                    }
                    return Done(true);

                case LinuxOperation.ChangePermissions:
                case LinuxOperation.ChangeGroup:
                    var owner = user.IsRoot || user.Name == node.Owner;
                    steps.Add(new AccessStep(AccessStepKind.OwnerOnly, path, owner, Owner: node.Owner));
                    return Done(owner);

                case LinuxOperation.ChangeOwner:
                    steps.Add(new AccessStep(AccessStepKind.RootOnly, path, user.IsRoot, Owner: node.Owner));
                    return Done(user.IsRoot);

                default:
                    var check = LinuxPermissions.Check(user, node, path, Needed(operation));
                    steps.Add(new AccessStep(AccessStepKind.Permission, path, check.Granted, check));
                    return Done(check.Granted);
            }
        }

        /// <summary>Droits demandés par une opération sur le fichier ou le dossier visé.</summary>
        public static Perm Needed(LinuxOperation operation) => operation switch
        {
            LinuxOperation.Read => Perm.Read,
            LinuxOperation.Write => Perm.Write,
            LinuxOperation.Execute => Perm.Execute,
            LinuxOperation.ExecuteScript => Perm.Read | Perm.Execute,
            LinuxOperation.List => Perm.Read,
            LinuxOperation.Enter => Perm.Execute,
            LinuxOperation.Create => Perm.Write | Perm.Execute,
            _ => Perm.None
        };

        /// <summary>L'opération a un sens sur ce type d'objet.</summary>
        public static bool Applies(LinuxOperation operation, LinuxNode node, string path) => operation switch
        {
            LinuxOperation.Read or LinuxOperation.Write or LinuxOperation.Execute or LinuxOperation.ExecuteScript => !node.IsDirectory,
            LinuxOperation.List or LinuxOperation.Enter or LinuxOperation.Create => node.IsDirectory,
            LinuxOperation.Delete => LinuxHost.Split(path).Length > 0,
            _ => true
        };

        /// <summary>Matrice d'accès : pour chaque compte du serveur, les opérations courantes sur un fichier ou un dossier.</summary>
        public static IReadOnlyList<AccessRow> Matrix(LinuxHost host, string path)
        {
            var node = host.Find(path);
            if (node == null) return [];
            var operations = (node.IsDirectory ? DirectoryOperations : FileOperations).Where(o => Applies(o, node, path)).ToList();
            return host.Users
                .Select(u => new AccessRow(u, operations.ToDictionary(o => o, o => Check(host, u, path, o))))
                .ToList();
        }
    }
}
