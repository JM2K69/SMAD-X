using System;
using System.Collections.Generic;
using System.Linq;
using SMARX.Models;

namespace SMARX.Services.Linux
{
    /// <summary>
    /// Scripts shell qui recréent un serveur simulé sur une vraie machine Linux (Debian : useradd, setfacl du
    /// paquet acl) et qui y tentent une opération sous l'identité d'un compte. L'arborescence est placée sous
    /// <c>prefix</c> (« /w ») : « / » du serveur simulé devient ce dossier.
    /// </summary>
    public static class LinuxScripts
    {
        /// <summary>Groupes, comptes, puis dossiers et fichiers avec propriétaire, mode, ACL et ACL par défaut.</summary>
        public static IReadOnlyList<string> Setup(LinuxHost host, string prefix)
        {
            var lines = new List<string>();
            foreach (var g in host.Groups.Where(g => g.Name != "root"))
                lines.Add($"getent group {g.Name} >/dev/null || groupadd -g {g.Gid} {g.Name}");
            foreach (var u in host.Users.Where(u => !u.IsRoot))
            {
                var groups = u.Groups.Count > 0 ? $" -G {string.Join(',', u.Groups)}" : "";
                lines.Add($"id -u {u.Name} >/dev/null 2>&1 || useradd -M -u {u.Uid} -g {u.PrimaryGroup}{groups} {u.Name}");
            }
            lines.Add($"rm -rf {Quote(prefix)}/* && mkdir -p {Quote(prefix)}");
            Add(lines, host.Root, prefix, prefix);
            return lines;
        }

        private static void Add(List<string> lines, LinuxNode node, string path, string prefix)
        {
            if (path != prefix)
                lines.Add(node.IsDirectory ? $"mkdir {Quote(path)}" : $"printf '#!/bin/sh\\nexit 0\\n' > {Quote(path)}");
            lines.AddRange(Commands(node, path));
            foreach (var child in node.Children)
                Add(lines, child, path + "/" + child.Name, prefix);
        }

        /// <summary>
        /// Commandes qui donnent à un fichier existant son propriétaire, son mode et ses ACL. chown d'abord : il efface
        /// setuid et setgid ; setfacl ensuite : le masque devient les droits du groupe du mode.
        /// </summary>
        public static IReadOnlyList<string> Commands(LinuxNode node, string path)
        {
            var lines = new List<string>
            {
                $"chown {node.Owner}:{node.Group} {Quote(path)}",
                $"chmod {node.Mode} {Quote(path)}"
            };
            if (node.Acl is { } acl)
            {
                var entries = acl.Users.Select(e => $"u:{e.Name}:{Perms.Format(Perms.Parse(e.Perm))}")
                    .Append($"g::{Perms.Format(Perms.Parse(acl.GroupObj))}")
                    .Concat(acl.Groups.Select(e => $"g:{e.Name}:{Perms.Format(Perms.Parse(e.Perm))}"))
                    .Append($"m::{Perms.Format(node.GroupPerm)}");
                lines.Add($"setfacl -m {string.Join(',', entries)} {Quote(path)}");
            }
            if (node.DefaultAcl is { } d)
            {
                var entries = new[] { $"u::{Perms.Format(Perms.Parse(d.UserObj))}" }
                    .Concat(d.Users.Select(e => $"u:{e.Name}:{Perms.Format(Perms.Parse(e.Perm))}"))
                    .Append($"g::{Perms.Format(Perms.Parse(d.GroupObj))}")
                    .Concat(d.Groups.Select(e => $"g:{e.Name}:{Perms.Format(Perms.Parse(e.Perm))}"))
                    .Concat(d.Users.Count > 0 || d.Groups.Count > 0 || d.Mask != null ? [$"m::{Perms.Format(d.Mask is { } m ? Perms.Parse(m) : LinuxCreation.UnionMask(d))}"] : [])
                    .Append($"o::{Perms.Format(Perms.Parse(d.Other))}");
                lines.Add($"setfacl -d -m {string.Join(',', entries)} {Quote(path)}");
            }
            return lines;
        }

        /// <summary>Commande qui tente l'opération sous l'identité du compte ; elle réussit si et seulement si l'accès est autorisé.</summary>
        public static string Probe(LinuxHost host, LinuxUser user, string path, LinuxOperation operation, string prefix)
        {
            var node = host.Find(path) ?? throw new ArgumentException($"{path} n'existe pas.", nameof(path));
            var p = Quote(prefix + (path == "/" ? "" : path));
            var command = operation switch
            {
                LinuxOperation.Read => $"cat -- {p}",
                // Ouvrir en écriture sans O_CREAT : avec O_CREAT (« >> »), fs.protected_regular refuse aussi le fichier d'un
                // autre compte dans un dossier sticky ouvert à tous, même à root, ce qui n'est pas une question de droits
                LinuxOperation.Write => $"dd if=/dev/null of={p} conv=notrunc,nocreat status=none",
                // access(X_OK) : la même vérification que l'exécution, sans dépendre du contenu du fichier
                LinuxOperation.Execute => $"test -x {p}",
                LinuxOperation.ExecuteScript => p,
                LinuxOperation.List => $"ls -f -- {p}",
                LinuxOperation.Enter => $"cd -- {p}",
                LinuxOperation.Create => $"mkdir -- {p}/.probe && rmdir -- {p}/.probe",
                LinuxOperation.Delete => node.IsDirectory ? $"rmdir -- {p}" : $"rm -f -- {p}",
                LinuxOperation.ChangePermissions => $"chmod {node.Mode} -- {p}",
                LinuxOperation.ChangeOwner => $"chown {(node.Owner == "root" ? host.Users.First(u => !u.IsRoot).Name : "root")} -- {p}",
                LinuxOperation.ChangeGroup => $"chgrp {user.PrimaryGroup} -- {p}",
                _ => throw new ArgumentOutOfRangeException(nameof(operation))
            };
            return user.IsRoot ? $"sh -c {Quote(command)}" : $"runuser -u {user.Name} -- sh -c {Quote(command)}";
        }

        /// <summary>L'opération modifie le serveur : il faut le recréer avant la suivante.</summary>
        public static bool Changes(LinuxOperation operation) =>
            operation is LinuxOperation.Delete or LinuxOperation.ChangePermissions or LinuxOperation.ChangeOwner or LinuxOperation.ChangeGroup;

        /// <summary>Argument shell : tel quel s'il n'a que des caractères sans danger (« /srv/compta »), sinon entre apostrophes.</summary>
        public static string Quote(string text) =>
            text.Length > 0 && text.All(c => char.IsAsciiLetterOrDigit(c) || c is '/' or '.' or '_' or '-')
                ? text
                : "'" + text.Replace("'", "'\\''") + "'";
    }
}
