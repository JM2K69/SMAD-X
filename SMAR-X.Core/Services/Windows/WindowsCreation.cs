using SMARX.Models;

namespace SMARX.Services.Windows
{
    /// <summary>Ce que reçoit un fichier ou un dossier créé (<see cref="Node"/>, null si la création est refusée), et son chemin.</summary>
    public sealed record WindowsCreationResult(WindowsAccessResult Access, NtfsNode? Node, string Path);

    /// <summary>
    /// Création d'un fichier ou d'un dossier : il faut Création de fichiers ou Création de dossiers sur le dossier. Le
    /// créateur en devient propriétaire (le réglage par défaut depuis Windows Vista, même pour un administrateur) ; il
    /// n'a aucune ACE explicite et hérite de celles du dossier, CREATOR OWNER devenant le créateur.
    /// </summary>
    public static class WindowsCreation
    {
        public static WindowsCreationResult Create(WindowsHost host, WindowsUser user, string folder, string name, NtfsNodeKind kind, bool elevated = false)
        {
            var operation = kind == NtfsNodeKind.Directory ? WindowsOperation.CreateFolder : WindowsOperation.CreateFile;
            var access = WindowsAccess.Check(host, user, folder, operation, elevated);
            var path = WindowsHost.Combine(folder, name);
            if (!access.Allowed)
                return new WindowsCreationResult(access, null, path);

            var node = new NtfsNode { Name = name, Kind = kind, Owner = user.Name };
            host.Find(folder)!.Children.Add(node);
            return new WindowsCreationResult(access, node, path);
        }

        /// <summary>Retire un nœud ajouté par <see cref="Create"/> (aperçu sans modifier le serveur).</summary>
        public static void Undo(WindowsHost host, WindowsCreationResult result)
        {
            if (result.Node != null)
                host.Find(WindowsHost.Parent(result.Path)!)?.Children.Remove(result.Node);
        }
    }
}
