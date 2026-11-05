using System;
using System.Linq;
using SMARX.Models;

namespace SMARX.Services.Linux
{
    /// <summary>
    /// Ce que reçoit un fichier ou un dossier créé : <see cref="Node"/> (null si la création est refusée), et
    /// pourquoi : l'umask appliquée, le groupe imposé par un dossier setgid, l'ACL par défaut héritée.
    /// </summary>
    public sealed record CreationResult(AccessResult Access, LinuxNode? Node, bool UmaskApplied, bool GroupFromSetGid, bool FromDefaultAcl);

    /// <summary>
    /// Création d'un fichier (touch, mode demandé 0666) ou d'un dossier (mkdir, 0777), comme sous Linux :
    /// <list type="bullet">
    /// <item>le propriétaire est le compte qui crée ;</item>
    /// <item>le groupe est son groupe principal, ou celui du dossier si le dossier a le bit setgid ; un dossier créé
    /// dans un dossier setgid reçoit aussi le bit setgid ;</item>
    /// <item>sans ACL par défaut dans le dossier, les droits sont le mode demandé moins l'umask ;</item>
    /// <item>avec une ACL par défaut, l'umask ne compte pas : l'ACL par défaut, limitée par le mode demandé, devient
    /// l'ACL du nouvel objet, et un dossier hérite aussi de l'ACL par défaut (posix_acl_create).</item>
    /// </list>
    /// </summary>
    public static class LinuxCreation
    {
        public const int FileMode = 0x1B6;      // 0666
        public const int DirectoryMode = 0x1FF; // 0777

        public static CreationResult Create(LinuxHost host, LinuxUser user, string directoryPath, string name, LinuxNodeKind kind)
        {
            var access = LinuxAccess.Check(host, user, directoryPath, LinuxOperation.Create);
            if (!access.Allowed)
                return new CreationResult(access, null, false, false, false);

            var directory = host.Find(directoryPath)!;
            var requested = kind == LinuxNodeKind.Directory ? DirectoryMode : FileMode;
            var node = new LinuxNode
            {
                Name = name,
                Kind = kind,
                Owner = user.Name,
                Group = directory.SetGid ? directory.Group : user.PrimaryGroup
            };

            int mode;
            if (directory.DefaultAcl is { } defaults)
            {
                mode = FromDefaultAcl(defaults, requested, node);
                if (kind == LinuxNodeKind.Directory)
                    node.DefaultAcl = Copy(defaults);
            }
            else
            {
                mode = requested & ~Convert.ToInt32(user.Umask, 8);
            }
            if (directory.SetGid && kind == LinuxNodeKind.Directory)
                mode |= 0x400;
            node.ModeBits = mode;

            return new CreationResult(access, node, directory.DefaultAcl == null, directory.SetGid, directory.DefaultAcl != null);
        }

        /// <summary>posix_acl_create_masq : l'ACL par défaut limitée par le mode demandé ; le masque (ou, sans masque, le groupe) prend la place du groupe dans le mode.</summary>
        private static int FromDefaultAcl(PosixDefaultAcl defaults, int requested, LinuxNode node)
        {
            var owner = Perms.Parse(defaults.UserObj) & (Perm)((requested >> 6) & 7);
            var other = Perms.Parse(defaults.Other) & (Perm)(requested & 7);
            var groupObj = Perms.Parse(defaults.GroupObj);
            var extended = defaults.Users.Count > 0 || defaults.Groups.Count > 0 || defaults.Mask != null;
            var mask = (defaults.Mask is { } m ? Perms.Parse(m) : UnionMask(defaults)) & (Perm)((requested >> 3) & 7);

            if (extended)
            {
                node.Acl = new PosixAcl
                {
                    Users = defaults.Users.Select(e => AclEntry.Of(e.Name, e.Perm)).ToList(),
                    GroupObj = Perms.Format(groupObj),
                    Groups = defaults.Groups.Select(e => AclEntry.Of(e.Name, e.Perm)).ToList()
                };
                return ((int)owner << 6) | ((int)mask << 3) | (int)other;
            }
            return ((int)owner << 6) | ((int)(groupObj & (Perm)((requested >> 3) & 7)) << 3) | (int)other;
        }

        /// <summary>Masque calculé par setfacl quand il n'est pas donné : l'union du groupe propriétaire et des entrées nommées.</summary>
        public static Perm UnionMask(PosixDefaultAcl acl) =>
            acl.Users.Concat(acl.Groups).Aggregate(Perms.Parse(acl.GroupObj), (mask, e) => mask | Perms.Parse(e.Perm));

        private static PosixDefaultAcl Copy(PosixDefaultAcl acl) => new()
        {
            UserObj = acl.UserObj,
            Users = acl.Users.Select(e => AclEntry.Of(e.Name, e.Perm)).ToList(),
            GroupObj = acl.GroupObj,
            Groups = acl.Groups.Select(e => AclEntry.Of(e.Name, e.Perm)).ToList(),
            Mask = acl.Mask,
            Other = acl.Other
        };
    }
}
