using System.Collections.Generic;
using System.Linq;
using SMARX.Models;

namespace SMARX.Services.Linux
{
    /// <summary>Ce qui donne ses droits à un compte sur un fichier : la classe Unix ou l'entrée d'ACL qui s'applique.</summary>
    public enum AccessClass
    {
        Owner,
        /// <summary>Utilisateur nommé dans l'ACL.</summary>
        NamedUser,
        /// <summary>Groupe propriétaire du fichier.</summary>
        OwningGroup,
        /// <summary>Groupe nommé dans l'ACL.</summary>
        NamedGroup,
        Other,
        /// <summary>root, qui passe outre les droits.</summary>
        Root
    }

    /// <summary>Une entrée qui s'applique au compte : un de ses groupes, avec les droits que l'ACL ou le mode lui donne.</summary>
    public sealed record AccessEntry(AccessClass Class, string? Name, Perm Perm);

    /// <summary>
    /// Vérification d'un droit sur un fichier ou un dossier. <see cref="Entry"/> est l'entrée retenue ;
    /// <see cref="Mask"/> le masque de l'ACL quand il la limite ; <see cref="Groups"/>, avec une ACL, toutes les
    /// entrées de groupe du compte (chacune doit suffire seule).
    /// </summary>
    public sealed record PermissionCheck(string Path, Perm Needed, bool Granted, AccessEntry Entry, Perm? Mask, IReadOnlyList<AccessEntry> Groups)
    {
        /// <summary>Droits que l'entrée donne vraiment : limités par le masque pour les entrées nommées et le groupe propriétaire.</summary>
        public Perm Effective => Mask is { } mask ? Entry.Perm & mask : Entry.Perm;

        /// <summary>root a passé outre des droits qui ne suffisaient pas.</summary>
        public bool RootOverride => Granted && Entry.Class == AccessClass.Root;
    }

    /// <summary>
    /// Droits d'un compte sur un fichier, comme les calcule le noyau Linux (acl_permission_check, posix_acl_permission
    /// et generic_permission) :
    /// <list type="number">
    /// <item>le propriétaire n'a que les droits du propriétaire, même si le groupe ou les autres en ont plus ;</item>
    /// <item>sinon un utilisateur nommé dans l'ACL a les droits de son entrée, limités par le masque ;</item>
    /// <item>sinon, s'il est membre du groupe propriétaire ou d'un groupe nommé, il faut qu'une de ces entrées, limitée
    /// par le masque, suffise seule ; si aucune ne suffit, c'est refusé, sans passer aux autres ;</item>
    /// <item>sinon, les droits des autres ;</item>
    /// <item>root passe outre, sauf pour exécuter un fichier qui n'a aucun bit x.</item>
    /// </list>
    /// </summary>
    public static class LinuxPermissions
    {
        public static PermissionCheck Check(LinuxUser user, LinuxNode node, string path, Perm needed)
        {
            var check = Dac(user, node, path, needed);
            if (check.Granted || !user.IsRoot)
                return check;

            // CAP_DAC_OVERRIDE et CAP_DAC_READ_SEARCH : tout, sauf exécuter un fichier sans aucun bit x
            var granted = node.IsDirectory || !needed.HasFlag(Perm.Execute) || node.AnyExecute;
            return check with { Granted = granted, Entry = new AccessEntry(AccessClass.Root, user.Name, Perm.All), Mask = null };
        }

        private static PermissionCheck Dac(LinuxUser user, LinuxNode node, string path, Perm needed)
        {
            if (user.Name == node.Owner)
                return Single(path, needed, new AccessEntry(AccessClass.Owner, node.Owner, node.OwnerPerm), null);

            if (node.Acl is { } acl)
            {
                var mask = node.GroupPerm;
                if (acl.Users.FirstOrDefault(e => e.Name == user.Name) is { } named)
                    return Single(path, needed, new AccessEntry(AccessClass.NamedUser, named.Name, Perms.Parse(named.Perm)), mask);

                var groups = new List<AccessEntry>();
                if (user.IsMemberOf(node.Group))
                    groups.Add(new AccessEntry(AccessClass.OwningGroup, node.Group, Perms.Parse(acl.GroupObj)));
                groups.AddRange(acl.Groups.Where(e => user.IsMemberOf(e.Name)).Select(e => new AccessEntry(AccessClass.NamedGroup, e.Name, Perms.Parse(e.Perm))));
                if (groups.Count > 0)
                {
                    var granting = groups.FirstOrDefault(g => (g.Perm & mask).Covers(needed));
                    return new PermissionCheck(path, needed, granting != null, granting ?? groups[0], mask, groups);
                }
            }
            else if (user.IsMemberOf(node.Group))
            {
                return Single(path, needed, new AccessEntry(AccessClass.OwningGroup, node.Group, node.GroupPerm), null);
            }

            return Single(path, needed, new AccessEntry(AccessClass.Other, null, node.OtherPerm), null);
        }

        private static PermissionCheck Single(string path, Perm needed, AccessEntry entry, Perm? mask)
        {
            var effective = mask is { } m ? entry.Perm & m : entry.Perm;
            return new PermissionCheck(path, needed, effective.Covers(needed), entry, mask, []);
        }
    }
}
