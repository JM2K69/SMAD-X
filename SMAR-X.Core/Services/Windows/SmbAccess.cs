using System;
using System.Collections.Generic;
using System.Linq;
using SMARX.Models;

namespace SMARX.Services.Windows
{
    /// <summary>
    /// Un accès par un partage : le jeton réseau, la connexion au partage, ses autorisations pour le droit demandé, puis
    /// NTFS comme en local. <see cref="Allowed"/> : le partage et NTFS accordent tous deux ; le plus restrictif l'emporte.
    /// </summary>
    /// <remarks>
    /// <see cref="Hidden"/> : l'énumération basée sur l'accès cache l'objet, introuvable par le partage ;
    /// <see cref="InUse"/> : supprimer un dossier partagé échoue, le serveur le garde ouvert.
    /// </remarks>
    public sealed record ShareAccessResult(string User, SmbShare Share, string Path, string Unc, WindowsOperation Operation, bool Allowed,
                                           WindowsToken Token, bool Connected, AccessCheckResult ShareCheck, WindowsAccessResult Ntfs,
                                           bool Hidden = false, bool InUse = false);

    /// <summary>Une entrée d'un dossier listé par un partage, visible ou cachée par l'énumération basée sur l'accès.</summary>
    public sealed record ShareListEntry(NtfsNode Node, string Path, bool Visible);

    /// <summary>
    /// L'accès distant à un serveur Windows, par un partage SMB :
    /// <list type="number">
    /// <item>le jeton est celui d'une session réseau (<see cref="WindowsToken.Remote"/>) : NETWORK au lieu d'INTERACTIVE,
    /// les restrictions distantes de l'UAC pour un compte local administrateur ;</item>
    /// <item>se connecter au partage demande qu'il accorde au moins un droit au compte ;</item>
    /// <item>chaque ouverture passe deux AccessCheck : la DACL du partage (Lecture, Modifier, Contrôle total), puis celle
    /// du fichier ou du dossier, avec les règles de NTFS sur le dossier parent ;</item>
    /// <item>avec l'énumération basée sur l'accès, un dossier listé ne montre que ce que le compte peut lire ; et un objet
    /// ainsi caché, dans un dossier que le compte peut lister, est introuvable même par son chemin (STATUS_OBJECT_NAME_NOT_FOUND),
    /// quel que soit le droit demandé ;</item>
    /// <item>un dossier partagé ne s'ouvre pas pour être supprimé tant qu'un client est connecté au partage : le serveur le
    /// garde ouvert (STATUS_SHARING_VIOLATION) ; par son propre partage, c'est toujours le cas.</item>
    /// </list>
    /// Relevé sur un Windows Server 2025, depuis un Windows 11 du domaine : Fidelity/windows-shares.txt.
    /// </summary>
    public static class SmbAccess
    {
        /// <summary>
        /// Le droit qui rend visible une entrée avec l'énumération basée sur l'accès : Liste du dossier/lecture de données
        /// sur l'entrée elle-même.
        /// </summary>
        public const NtfsRights AbeRight = NtfsRights.ReadData;

        /// <summary>La DACL du partage, les refus d'abord, comme la fenêtre Autorisations du Partage avancé l'écrit.</summary>
        public static IReadOnlyList<EffectiveAce> Dacl(SmbShare share) =>
            share.Permissions.OrderBy(a => a.Type == AceType.Deny ? 0 : 1)
                .Select(a => new EffectiveAce(a.Type, a.Trustee, a.Mask, AceInheritance.None, null)).ToList();

        /// <summary>Ce que le partage accorde au jeton, au plus : un refus retire ce qui n'est pas encore accordé.</summary>
        public static NtfsRights MaximumAllowed(SmbShare share, WindowsToken token)
        {
            NtfsRights granted = NtfsRights.None, denied = NtfsRights.None;
            foreach (var ace in Dacl(share))
            {
                if (!token.Has(ace.Trustee, forAllow: ace.Type == AceType.Allow)) continue;
                if (ace.Type == AceType.Deny) denied |= ace.Mask & ~granted;
                else granted |= ace.Mask & ~denied;
            }
            return granted;
        }

        /// <summary>Se connecter au partage (tree connect) : refusé si le partage n'accorde aucun droit au compte.</summary>
        public static bool CanConnect(SmbShare share, WindowsToken token) => MaximumAllowed(share, token) != NtfsRights.None;

        public static AccessCheckResult CheckShare(SmbShare share, WindowsToken token, NtfsRights desired) =>
            WindowsPermissions.CheckDacl(share.Name, Dacl(share), token, desired);

        /// <summary>Un compte tente une opération sur un objet du partage, depuis le réseau.</summary>
        public static ShareAccessResult Check(WindowsHost host, WindowsUser user, SmbShare share, string path, WindowsOperation operation)
        {
            if (!share.Contains(path)) throw new ArgumentException($"{path} n'est pas dans le partage {share.Name}.", nameof(path));
            var token = WindowsToken.Remote(host, user);
            var connected = CanConnect(share, token);
            var shareCheck = CheckShare(share, token, WindowsAccess.Needed(operation));
            var ntfs = WindowsAccess.Check(host, token, path, operation);
            var hidden = connected && Hidden(host, token, share, path);
            var allowed = connected && !hidden && shareCheck.Granted && ntfs.Allowed;
            // Le dossier du partage par lequel on passe est ouvert par le serveur tant qu'on y est connecté : l'ouverture pour
            // le supprimer échoue avant même de savoir s'il est vide
            var inUse = operation == WindowsOperation.Delete && IsRoot(share, path) && connected && !hidden && shareCheck.Granted
                        && (ntfs.Allowed || ntfs.NotEmpty);
            return new ShareAccessResult(user.Name, share, path, share.Unc(host.Name, path), operation, allowed && !inUse,
                token, connected, shareCheck, ntfs, hidden, inUse);
        }

        /// <summary>
        /// Un droit spécial à l'ouverture par le partage : le partage doit l'accorder, puis NTFS (parent compris), sauf si
        /// l'énumération basée sur l'accès rend l'objet introuvable. (Supprimer un dossier partagé échoue ensuite, voir
        /// <see cref="IsShared"/>.) Par SMB, Synchroniser seul se décide comme Attributs de lecture : mesuré sur 220
        /// ouvertures (Fidelity/windows-shares.txt), dont la racine de Compta$, que dave lit seulement par son dossier parent.
        /// </summary>
        public static bool Open(WindowsHost host, WindowsToken token, SmbShare share, string path, NtfsRights right)
        {
            if (right == NtfsRights.Synchronize) right = NtfsRights.ReadAttributes;
            return CanConnect(share, token) && !Hidden(host, token, share, path)
                   && CheckShare(share, token, right).Granted && WindowsAccess.Open(host, token, path, right) != OpenGrant.Denied;
        }

        /// <summary>
        /// L'énumération basée sur l'accès cache l'objet au compte, qui peut lister son dossier : par le partage, l'objet est
        /// introuvable. Si le compte ne peut pas lister le dossier, l'accès est simplement refusé.
        /// </summary>
        public static bool Hidden(WindowsHost host, WindowsToken token, SmbShare share, string path)
        {
            if (!share.AccessBasedEnumeration || IsRoot(share, path) || WindowsHost.Parent(path) is not { } parent) return false;
            return !WindowsPermissions.Check(host, token, path, AbeRight).Granted
                   && CheckShare(share, token, NtfsRights.ReadData).Granted && WindowsPermissions.Check(host, token, parent, NtfsRights.ReadData).Granted;
        }

        /// <summary>Le dossier est celui d'un partage du serveur : le supprimer échoue tant qu'un client y est connecté.</summary>
        public static bool IsShared(WindowsHost host, string path) => host.Shares.Any(s => IsRoot(s, path));

        private static bool IsRoot(SmbShare share, string path) =>
            string.Equals(share.Path.TrimEnd('\\'), path.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Lister un dossier par le partage : ses entrées, chacune visible ou non. Sans énumération basée sur l'accès, tout
        /// est visible ; avec, une entrée n'apparaît que si NTFS accorde <see cref="AbeRight"/> au compte sur elle.
        /// </summary>
        public static IReadOnlyList<ShareListEntry> List(WindowsHost host, WindowsToken token, SmbShare share, string folder)
        {
            var node = host.Find(folder) ?? throw new ArgumentException($"{folder} n'existe pas.", nameof(folder));
            return node.Children.Select(c =>
            {
                var path = WindowsHost.Combine(folder, c.Name);
                return new ShareListEntry(c, path, !share.AccessBasedEnumeration || WindowsPermissions.Check(host, token, path, AbeRight).Granted);
            }).ToList();
        }

        /// <summary>Les partages qui donnent accès à un objet, du plus proche au plus large.</summary>
        public static IReadOnlyList<SmbShare> SharesOf(WindowsHost host, string path) =>
            host.Shares.Where(s => s.Contains(path)).OrderByDescending(s => s.Path.Length).ToList();
    }
}
