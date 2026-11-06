using System.Collections.Generic;
using System.Linq;
using SMARX.Models;

namespace SMARX.Services.Windows
{
    /// <summary>Ce qu'un compte tente de faire en local (session ouverte sur le serveur), et le droit NTFS que cela demande.</summary>
    public enum WindowsOperation
    {
        /// <summary>Lire un fichier : Lecture de données.</summary>
        Read,
        /// <summary>Modifier le contenu d'un fichier : Écriture de données.</summary>
        Write,
        /// <summary>Exécuter un programme : Exécuter le fichier.</summary>
        Execute,
        /// <summary>Lister un dossier : Liste du dossier.</summary>
        List,
        /// <summary>Créer un fichier dans un dossier : Création de fichiers.</summary>
        CreateFile,
        /// <summary>Créer un sous-dossier : Création de dossiers.</summary>
        CreateFolder,
        /// <summary>Supprimer : Suppression sur l'objet, ou Suppression de sous-dossier et fichier sur son dossier parent.</summary>
        Delete,
        /// <summary>Lire les autorisations (onglet Sécurité, icacls).</summary>
        ReadPermissions,
        /// <summary>Modifier les autorisations : le propriétaire le peut toujours.</summary>
        ChangePermissions,
        /// <summary>Prendre possession (devenir propriétaire).</summary>
        TakeOwnership
    }

    /// <summary>
    /// Résultat d'un accès. <see cref="Checks"/> : un AccessCheck, ou deux pour supprimer (l'objet, puis son dossier
    /// parent) ; <see cref="NotEmpty"/> : un dossier ne se supprime que vide.
    /// </summary>
    public sealed record WindowsAccessResult(string User, string Path, WindowsOperation Operation, bool Allowed, WindowsToken Token,
                                             IReadOnlyList<AccessCheckResult> Checks, bool NotFound = false, bool NotApplicable = false, bool NotEmpty = false);

    public sealed record WindowsAccessRow(WindowsUser User, IReadOnlyDictionary<WindowsOperation, WindowsAccessResult> Results);

    /// <summary>Un droit spécial à l'ouverture : refusé, accordé par l'objet, ou accordé par son dossier parent.</summary>
    public enum OpenGrant { Denied, Granted, ByParent }

    /// <summary>
    /// Accès local à un serveur Windows. Les dossiers du chemin ne sont pas vérifiés : tout le monde a le privilège
    /// « Contourner la vérification de parcours » (SeChangeNotifyPrivilege), activé par défaut. Seul l'objet visé
    /// compte, et son dossier parent pour supprimer.
    /// </summary>
    public static class WindowsAccess
    {
        public static readonly WindowsOperation[] FileOperations =
            [WindowsOperation.Read, WindowsOperation.Write, WindowsOperation.Execute, WindowsOperation.Delete, WindowsOperation.ChangePermissions];

        public static readonly WindowsOperation[] FolderOperations =
            [WindowsOperation.List, WindowsOperation.CreateFile, WindowsOperation.CreateFolder, WindowsOperation.Delete, WindowsOperation.ChangePermissions];

        public static NtfsRights Needed(WindowsOperation operation) => operation switch
        {
            WindowsOperation.Read or WindowsOperation.List => NtfsRights.ReadData,
            WindowsOperation.Write or WindowsOperation.CreateFile => NtfsRights.WriteData,
            WindowsOperation.Execute => NtfsRights.ExecuteFile,
            WindowsOperation.CreateFolder => NtfsRights.AppendData,
            WindowsOperation.Delete => NtfsRights.Delete,
            WindowsOperation.ReadPermissions => NtfsRights.ReadPermissions,
            WindowsOperation.ChangePermissions => NtfsRights.ChangePermissions,
            WindowsOperation.TakeOwnership => NtfsRights.TakeOwnership,
            _ => NtfsRights.None
        };

        public static bool Applies(WindowsOperation operation, NtfsNode node, string path) => operation switch
        {
            WindowsOperation.Read or WindowsOperation.Write or WindowsOperation.Execute => !node.IsDirectory,
            WindowsOperation.List or WindowsOperation.CreateFile or WindowsOperation.CreateFolder => node.IsDirectory,
            WindowsOperation.Delete => WindowsHost.Parent(path) != null,
            _ => true
        };

        public static WindowsAccessResult Check(WindowsHost host, WindowsUser user, string path, WindowsOperation operation, bool elevated = false) =>
            Check(host, WindowsToken.Local(host, user, elevated), path, operation);

        /// <summary>Un accès sous un jeton donné : celui d'une session locale, ou d'une session réseau (voir SmbAccess).</summary>
        public static WindowsAccessResult Check(WindowsHost host, WindowsToken token, string path, WindowsOperation operation)
        {
            var user = token.User;
            var node = host.Find(path);
            if (node == null)
                return new(user.Name, path, operation, false, token, [], NotFound: true);
            if (!Applies(operation, node, path))
                return new(user.Name, path, operation, false, token, [], NotApplicable: true);

            var check = WindowsPermissions.Check(host, token, path, Needed(operation));
            if (operation != WindowsOperation.Delete)
                return new(user.Name, path, operation, check.Granted, token, [check]);

            // Supprimer : Suppression sur l'objet, ou Suppression de sous-dossier et fichier sur le dossier parent
            var checks = new List<AccessCheckResult> { check };
            var allowed = check.Granted;
            if (!allowed)
            {
                var parent = WindowsPermissions.Check(host, token, WindowsHost.Parent(path)!, NtfsRights.DeleteSubdirectoriesAndFiles);
                checks.Add(parent);
                allowed = parent.Granted;
            }
            if (allowed && node.IsDirectory && node.Children.Count > 0)
                return new(user.Name, path, operation, false, token, checks, NotEmpty: true);
            return new(user.Name, path, operation, allowed, token, checks);
        }

        /// <summary>
        /// Un droit spécial demandé à l'ouverture de l'objet (CreateFile) : AccessCheck sur sa DACL, puis deux règles de
        /// NTFS qui regardent le dossier parent, vérifiées sur un vrai serveur : Suppression est accordée si le parent
        /// accorde Suppression de sous-dossier et fichier ; Attributs de lecture, s'il accorde Liste du dossier (lister un
        /// dossier montre déjà les attributs de ce qu'il contient).
        /// </summary>
        public static OpenGrant Open(WindowsHost host, WindowsToken token, string path, NtfsRights right)
        {
            if (WindowsPermissions.Check(host, token, path, right).Granted)
                return OpenGrant.Granted;
            var fromParent = right switch
            {
                NtfsRights.Delete => NtfsRights.DeleteSubdirectoriesAndFiles,
                NtfsRights.ReadAttributes => NtfsRights.ReadData,
                _ => NtfsRights.None
            };
            return fromParent != NtfsRights.None && WindowsHost.Parent(path) is { } parent
                   && WindowsPermissions.Check(host, token, parent, fromParent).Granted
                ? OpenGrant.ByParent
                : OpenGrant.Denied;
        }

        /// <summary>Matrice d'accès : chaque compte, les opérations courantes sur un fichier ou un dossier.</summary>
        public static IReadOnlyList<WindowsAccessRow> Matrix(WindowsHost host, string path, bool elevated = false)
        {
            var node = host.Find(path);
            if (node == null) return [];
            var operations = (node.IsDirectory ? FolderOperations : FileOperations).Where(o => Applies(o, node, path)).ToList();
            return host.Users
                .Select(u => new WindowsAccessRow(u, operations.ToDictionary(o => o, o => Check(host, u, path, o, elevated))))
                .ToList();
        }
    }
}
