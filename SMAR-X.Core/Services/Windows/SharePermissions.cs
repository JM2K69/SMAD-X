using System;
using System.Collections.Generic;
using System.Linq;
using SMARX.Models;

namespace SMARX.Services.Windows
{
    /// <summary>
    /// La fenêtre « Autorisations » du Partage avancé : pour chaque compte, Contrôle total, Modifier et Lecture, à autoriser
    /// ou à refuser. Chaque ligne contient la suivante (Contrôle total ⊃ Modifier ⊃ Lecture) : un compte a un niveau par
    /// colonne. Comme dans la fenêtre Modifier de l'onglet Sécurité, cocher une case d'une colonne décoche dans l'autre
    /// celles qui partagent des droits avec elle (ici toutes, puisque chacune contient Lecture), et décocher une case
    /// décoche celles qui la contiennent.
    /// </summary>
    public static class SharePermissions
    {
        /// <summary>Les lignes, dans l'ordre de Windows.</summary>
        public static readonly ShareRight[] Rows = [ShareRight.FullControl, ShareRight.Change, ShareRight.Read];

        /// <summary>Le niveau autorisé et le niveau refusé d'un compte (null : rien).</summary>
        public static (ShareRight? Allow, ShareRight? Deny) Level(SmbShare share, string trustee)
        {
            ShareRight? Of(AceType type) => share.Permissions.Where(a => a.Type == type && Same(a.Trustee, trustee))
                .Select(a => (ShareRight?)a.Right).DefaultIfEmpty(null).Max();
            return (Of(AceType.Allow), Of(AceType.Deny));
        }

        /// <summary>La case est cochée : le niveau de la colonne contient cette ligne.</summary>
        public static bool IsChecked(SmbShare share, string trustee, ShareRight right, AceType type)
        {
            var (allow, deny) = Level(share, trustee);
            var level = type == AceType.Allow ? allow : deny;
            return level != null && level >= right;
        }

        public static void Set(SmbShare share, string trustee, ShareRight right, AceType type, bool on)
        {
            var (allow, deny) = Level(share, trustee);
            var level = type == AceType.Allow ? allow : deny;
            ShareRight? next = on
                ? (level == null || right > level ? right : level)
                : level == null || level < right ? level : right == ShareRight.Read ? null : right - 1;
            share.Permissions.RemoveAll(a => a.Type == type && Same(a.Trustee, trustee));
            if (on) share.Permissions.RemoveAll(a => a.Type != type && Same(a.Trustee, trustee));
            if (next is { } kept)
                share.Permissions.Add(new ShareAce { Type = type, Trustee = trustee, Right = kept });
        }

        /// <summary>Ajouter un compte : Lecture, comme Windows.</summary>
        public static void Add(SmbShare share, string trustee)
        {
            if (!share.Permissions.Any(a => Same(a.Trustee, trustee)))
                share.Permissions.Add(ShareAce.Allow(trustee, ShareRight.Read));
        }

        public static void Remove(SmbShare share, string trustee) => share.Permissions.RemoveAll(a => Same(a.Trustee, trustee));

        /// <summary>Les comptes cités, dans l'ordre de leur SID, comme l'onglet Sécurité.</summary>
        public static IReadOnlyList<string> Principals(WindowsHost host, SmbShare share) =>
            share.Permissions.Select(a => a.Trustee).Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(t => NtfsSecurityTab.Sid(host, t), StringComparer.Ordinal).ToList();

        /// <summary>
        /// Le partage en PowerShell : New-SmbShare avec ses autorisations exactes (sans paramètre d'accès, Windows donne
        /// Lecture à Tout le monde), et l'énumération basée sur l'accès ; Block-SmbShareAccess pour un refus.
        /// </summary>
        public static IReadOnlyList<string> Commands(WindowsHost host, SmbShare share)
        {
            string Accounts(ShareRight right) => string.Join(",", share.Permissions.Where(a => a.Type == AceType.Allow && a.Right == right)
                .Select(a => $"\"{WindowsListing.Account(host, a.Trustee)}\""));
            var line = $"New-SmbShare -Name \"{share.Name}\" -Path \"{share.Path}\"";
            if (share.AccessBasedEnumeration) line += " -FolderEnumerationMode AccessBased";
            foreach (var (right, parameter) in new[] { (ShareRight.FullControl, "-FullAccess"), (ShareRight.Change, "-ChangeAccess"), (ShareRight.Read, "-ReadAccess") })
                if (share.Permissions.Any(a => a.Type == AceType.Allow && a.Right == right))
                    line += $" {parameter} {Accounts(right)}";
            var lines = new List<string> { line };
            // Block-SmbShareAccess refuse le Contrôle total ; un refus plus fin ne s'écrit que par la fenêtre Autorisations
            foreach (var deny in share.Permissions.Where(a => a.Type == AceType.Deny))
                lines.Add(deny.Right == ShareRight.FullControl
                    ? $"Block-SmbShareAccess -Name \"{share.Name}\" -AccountName \"{WindowsListing.Account(host, deny.Trustee)}\" -Force"
                    : $"# Refuser {deny.Right} à {WindowsListing.Account(host, deny.Trustee)} : par la fenêtre Autorisations du Partage avancé");
            return lines;
        }

        private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }
}
