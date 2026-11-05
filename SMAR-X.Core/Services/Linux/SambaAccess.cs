using System;
using System.Collections.Generic;
using System.Linq;
using SMARX.Models;

namespace SMARX.Services.Linux
{
    /// <summary>Pourquoi Samba refuse avant même le noyau.</summary>
    public enum SambaRefusal
    {
        None,
        /// <summary>root n'a pas de mot de passe Samba : smbpasswd -a ne l'a pas ajouté, comme le veut la bonne pratique.</summary>
        NoSambaAccount,
        /// <summary>Le compte n'est pas dans valid users, ou il est dans invalid users.</summary>
        NotValidUser,
        /// <summary>smbd n'entre pas dans le dossier partagé sous l'identité Unix du compte (x manquant).</summary>
        CannotEnterShare,
        /// <summary>Partage en lecture seule pour ce compte (read only, read list), et l'opération écrit.</summary>
        ReadOnly
    }

    /// <summary>
    /// Un accès par un partage Samba. <see cref="Unix"/> : l'identité sous laquelle smbd agit (force user, force group) ;
    /// <see cref="Kernel"/> : la vérification du noyau, si Samba a laissé passer.
    /// </summary>
    public sealed record SambaAccessResult(string User, SambaShare Share, string Path, string Unc, LinuxOperation Operation, bool Allowed,
                                           SambaRefusal Refusal, LinuxUser Unix, bool Writable, AccessResult? Kernel);

    /// <summary>Une entrée d'un dossier listé par le partage, visible ou cachée par hide unreadable.</summary>
    public sealed record SambaListEntry(LinuxNode Node, string Path, bool Visible);

    /// <summary>
    /// L'accès distant à un serveur Linux par un partage Samba, relevé sur un vrai Samba 4.22 (Fidelity/linux-samba.txt) :
    /// <list type="number">
    /// <item>se connecter : être dans valid users (s'il y en a) et pas dans invalid users, puis smbd entre dans le
    /// dossier partagé sous l'identité Unix du compte : il lui faut x sur ce dossier et ceux qui y mènent ;</item>
    /// <item>écrire, créer ou supprimer : le partage doit être en écriture pour ce compte (read only = no, ou write list,
    /// mais pas read list) ;</item>
    /// <item>puis le noyau décide, sous l'identité Unix : le compte, ou force user, avec force group comme groupe
    /// principal. Le client ouvre un fichier en lecture-écriture pour y écrire : il faut r et w (avec une ACL, une même
    /// entrée doit les donner tous deux) ;</item>
    /// <item>hide unreadable cache d'une liste ce que le compte ne peut pas lire, en jugeant comme Windows : les classes
    /// s'additionnent (les autres peuvent lire, même si le groupe ne le peut pas) ;</item>
    /// <item>ce qui est créé reçoit (0666, plus x pour le propriétaire : l'attribut archive) &amp; create mask | force create
    /// mode, ou 0777 &amp; directory mask | force directory mode pour un dossier ; Samba n'applique pas d'umask, le noyau
    /// garde le bit setgid et l'ACL par défaut.</item>
    /// </list>
    /// </summary>
    public static class SambaAccess
    {
        /// <summary>Les opérations qu'on teste par un partage.</summary>
        public static readonly LinuxOperation[] FileOperations = [LinuxOperation.Read, LinuxOperation.Write, LinuxOperation.Delete];
        public static readonly LinuxOperation[] DirectoryOperations = [LinuxOperation.List, LinuxOperation.Create, LinuxOperation.Delete];

        /// <summary>
        /// L'identité Unix de smbd pour ce compte : force user (avec ses groupes), puis force group comme groupe
        /// principal.
        /// </summary>
        public static LinuxUser Unix(LinuxHost host, SambaShare share, LinuxUser user)
        {
            var basis = share.ForceUser is { } forced && host.User(forced) is { } other ? other : user;
            if (share.ForceGroup is not { } group) return basis;
            return new LinuxUser
            {
                Name = basis.Name, Uid = basis.Uid, PrimaryGroup = group, Umask = basis.Umask,
                Groups = basis.Groups.Append(basis.PrimaryGroup).Where(g => g != group).Distinct().ToList()
            };
        }

        public static bool IsValidUser(SambaShare share, LinuxUser user) =>
            (share.ValidUsers.Count == 0 || SambaShare.Matches(share.ValidUsers, user)) && !SambaShare.Matches(share.InvalidUsers, user);

        /// <summary>Le partage est en écriture pour ce compte : read list l'emporte, puis write list, puis read only.</summary>
        public static bool Writable(SambaShare share, LinuxUser user) =>
            !SambaShare.Matches(share.ReadList, user) && (SambaShare.Matches(share.WriteList, user) || !share.ReadOnly);

        /// <summary>Se connecter au partage : compte admis, et smbd entre dans le dossier partagé.</summary>
        public static SambaRefusal Connect(LinuxHost host, SambaShare share, LinuxUser user)
        {
            if (user.IsRoot) return SambaRefusal.NoSambaAccount;
            if (!IsValidUser(share, user)) return SambaRefusal.NotValidUser;
            return LinuxAccess.Check(host, Unix(host, share, user), share.Path, LinuxOperation.Enter).Allowed ? SambaRefusal.None : SambaRefusal.CannotEnterShare;
        }

        private static bool Writes(LinuxOperation operation) =>
            operation is LinuxOperation.Write or LinuxOperation.Create or LinuxOperation.Delete or LinuxOperation.ChangePermissions;

        public static SambaAccessResult Check(LinuxHost host, LinuxUser user, SambaShare share, string path, LinuxOperation operation)
        {
            if (!share.Contains(path)) throw new ArgumentException($"{path} n'est pas dans le partage {share.Name}.", nameof(path));
            var unix = Unix(host, share, user);
            var writable = Writable(share, user);
            var unc = share.Unc(host.Name, path);
            SambaAccessResult Refused(SambaRefusal refusal) => new(user.Name, share, path, unc, operation, false, refusal, unix, writable, null);

            if (Connect(host, share, user) is var refusal && refusal != SambaRefusal.None) return Refused(refusal);
            if (Writes(operation) && !writable) return Refused(SambaRefusal.ReadOnly);

            // Écrire un fichier : le client l'ouvre en lecture-écriture
            var kernel = operation == LinuxOperation.Write ? ReadWrite(host, unix, path) : LinuxAccess.Check(host, unix, path, operation);
            return new SambaAccessResult(user.Name, share, path, unc, operation, kernel.Allowed, SambaRefusal.None, unix, writable, kernel);
        }

        private static AccessResult ReadWrite(LinuxHost host, LinuxUser unix, string path)
        {
            var write = LinuxAccess.Check(host, unix, path, LinuxOperation.Write);
            if (!write.Allowed) return write;
            var node = host.Find(path)!;
            var both = LinuxPermissions.Check(unix, node, path, Perm.Read | Perm.Write);
            var steps = write.Steps.Take(write.Steps.Count - 1).Append(new AccessStep(AccessStepKind.Permission, path, both.Granted, both)).ToList();
            return write with { Allowed = both.Granted, Steps = steps };
        }

        /// <summary>
        /// Ce que hide unreadable juge lisible : les droits r de toutes les classes qui s'appliquent au compte, additionnés
        /// comme le ferait Windows (le propriétaire, les entrées nommées et les groupes limités par le masque, les autres).
        /// </summary>
        public static bool Readable(LinuxUser user, LinuxNode node)
        {
            if (user.IsRoot) return true;
            var classes = new List<Perm>();
            if (user.Name == node.Owner) classes.Add(node.OwnerPerm);
            if (node.Acl is { } acl)
            {
                var mask = node.GroupPerm;
                classes.AddRange(acl.Users.Where(e => e.Name == user.Name).Select(e => Perms.Parse(e.Perm) & mask));
                if (user.IsMemberOf(node.Group)) classes.Add(Perms.Parse(acl.GroupObj) & mask);
                classes.AddRange(acl.Groups.Where(e => user.IsMemberOf(e.Name)).Select(e => Perms.Parse(e.Perm) & mask));
            }
            else if (user.IsMemberOf(node.Group)) classes.Add(node.GroupPerm);
            classes.Add(node.OtherPerm);
            return classes.Any(p => p.HasFlag(Perm.Read));
        }

        /// <summary>Lister un dossier par le partage : chaque entrée, visible ou cachée par hide unreadable.</summary>
        public static IReadOnlyList<SambaListEntry> List(LinuxHost host, LinuxUser user, SambaShare share, string folder)
        {
            var node = host.Find(folder) ?? throw new ArgumentException($"{folder} n'existe pas.", nameof(folder));
            var unix = Unix(host, share, user);
            return node.Children.Select(c => new SambaListEntry(c, LinuxHost.Combine(folder, c.Name), !share.HideUnreadable || Readable(unix, c))).ToList();
        }

        /// <summary>Le mode que Samba demande pour un nouvel objet : ses masques, l'attribut archive en x du propriétaire.</summary>
        public static int RequestedMode(SambaShare share, LinuxNodeKind kind) => kind == LinuxNodeKind.Directory
            ? (LinuxCreation.DirectoryMode & Convert.ToInt32(share.DirectoryMask, 8)) | Convert.ToInt32(share.ForceDirectoryMode, 8)
            : ((LinuxCreation.FileMode | 0x40) & Convert.ToInt32(share.CreateMask, 8)) | Convert.ToInt32(share.ForceCreateMode, 8);

        /// <summary>Créer un fichier ou un dossier par le partage.</summary>
        public static (SambaAccessResult Access, CreationResult? Created) Create(LinuxHost host, LinuxUser user, SambaShare share, string folder, string name, LinuxNodeKind kind)
        {
            var access = Check(host, user, share, folder, LinuxOperation.Create);
            if (!access.Allowed) return (access, null);
            var created = LinuxCreation.Create(host, access.Unix, folder, name, kind, RequestedMode(share, kind), 0, access.Unix.PrimaryGroup, access.Kernel!);
            return (access, created);
        }

        /// <summary>Les partages qui donnent accès à un objet, du plus proche au plus large.</summary>
        public static IReadOnlyList<SambaShare> SharesOf(LinuxHost host, string path) =>
            host.Shares.Where(s => s.Contains(path)).OrderByDescending(s => s.Path.Length).ToList();
    }
}
