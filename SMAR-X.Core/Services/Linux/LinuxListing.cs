using System.Collections.Generic;
using SMARX.Models;

namespace SMARX.Services.Linux
{
    /// <summary>Les droits tels que les montrent ls -l et getfacl.</summary>
    public static class LinuxListing
    {
        /// <summary>« -rw-r----- », « drwxrws---+ », « drwxrwxrwt » : s/S et t/T pour les droits spéciaux, + pour une ACL étendue.</summary>
        public static string Mode(LinuxNode node)
        {
            var owner = Perms.Format(node.OwnerPerm).ToCharArray();
            var group = Perms.Format(node.GroupPerm).ToCharArray();
            var other = Perms.Format(node.OtherPerm).ToCharArray();
            if (node.SetUid) owner[2] = owner[2] == 'x' ? 's' : 'S';
            if (node.SetGid) group[2] = group[2] == 'x' ? 's' : 'S';
            if (node.Sticky) other[2] = other[2] == 'x' ? 't' : 'T';
            return (node.IsDirectory ? "d" : "-") + new string(owner) + new string(group) + new string(other) + (node.Acl != null ? "+" : "");
        }

        /// <summary>Sortie de getfacl : propriétaire, entrées nommées et groupe (avec leurs droits effectifs si le masque les limite), masque, autres, ACL par défaut.</summary>
        public static IReadOnlyList<string> Getfacl(LinuxNode node, string path)
        {
            var lines = new List<string> { $"# file: {path.TrimStart('/')}", $"# owner: {node.Owner}", $"# group: {node.Group}" };
            var flags = $"{(node.SetUid ? 's' : '-')}{(node.SetGid ? 's' : '-')}{(node.Sticky ? 't' : '-')}";
            if (flags != "---") lines.Add($"# flags: {flags}");

            lines.Add($"user::{Perms.Format(node.OwnerPerm)}");
            if (node.Acl is { } acl)
            {
                var mask = node.GroupPerm;
                string Masked(string tag, string perm) => (Perms.Parse(perm) & mask) == Perms.Parse(perm) ? tag + perm : $"{tag}{perm}\t#effective:{Perms.Format(Perms.Parse(perm) & mask)}";
                foreach (var e in acl.Users) lines.Add(Masked($"user:{e.Name}:", Perms.Format(Perms.Parse(e.Perm))));
                lines.Add(Masked("group::", Perms.Format(Perms.Parse(acl.GroupObj))));
                foreach (var e in acl.Groups) lines.Add(Masked($"group:{e.Name}:", Perms.Format(Perms.Parse(e.Perm))));
                lines.Add($"mask::{Perms.Format(mask)}");
            }
            else
            {
                lines.Add($"group::{Perms.Format(node.GroupPerm)}");
            }
            lines.Add($"other::{Perms.Format(node.OtherPerm)}");

            if (node.DefaultAcl is { } d)
            {
                lines.Add($"default:user::{Perms.Format(Perms.Parse(d.UserObj))}");
                foreach (var e in d.Users) lines.Add($"default:user:{e.Name}:{Perms.Format(Perms.Parse(e.Perm))}");
                lines.Add($"default:group::{Perms.Format(Perms.Parse(d.GroupObj))}");
                foreach (var e in d.Groups) lines.Add($"default:group:{e.Name}:{Perms.Format(Perms.Parse(e.Perm))}");
                if (d.Users.Count > 0 || d.Groups.Count > 0 || d.Mask != null)
                    lines.Add($"default:mask::{Perms.Format(d.Mask is { } m ? Perms.Parse(m) : LinuxCreation.UnionMask(d))}");
                lines.Add($"default:other::{Perms.Format(Perms.Parse(d.Other))}");
            }
            return lines;
        }
    }
}
