using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace SMARX.Models
{
    /// <summary>Serveur Linux simulé : ses comptes, ses groupes et son arborescence de fichiers.</summary>
    public sealed class LinuxHost
    {
        public string Name { get; set; } = "srv-linux";
        public List<LinuxUser> Users { get; set; } = new();
        public List<LinuxGroup> Groups { get; set; } = new();
        public LinuxNode Root { get; set; } = LinuxNode.Directory("", "root", "root", "0755");

        /// <summary>Les partages Samba du serveur : l'accès distant passe par eux.</summary>
        public List<SambaShare> Shares { get; set; } = new();

        public SambaShare? Share(string name) => Shares.FirstOrDefault(s => s.Name == name);

        public LinuxUser? User(string name) => Users.FirstOrDefault(u => u.Name == name);

        public LinuxGroup? Group(string name) => Groups.FirstOrDefault(g => g.Name == name);

        /// <summary>Nœud d'un chemin absolu (« /srv/compta/budget.ods »), ou null. Les noms respectent la casse, comme sous Linux.</summary>
        public LinuxNode? Find(string path)
        {
            var node = Root;
            foreach (var name in Split(path))
            {
                node = node.Children.FirstOrDefault(c => c.Name == name);
                if (node == null) return null;
            }
            return node;
        }

        /// <summary>Dossiers traversés pour atteindre le chemin, de « / » à son parent, avec leur chemin.</summary>
        public IEnumerable<(LinuxNode Node, string Path)> Ancestors(string path)
        {
            var names = Split(path);
            var node = Root;
            var current = "/";
            for (var i = 0; i < names.Length; i++)
            {
                yield return (node, current);
                node = node.Children.FirstOrDefault(c => c.Name == names[i]);
                if (node == null) yield break;
                current = Combine(current, names[i]);
            }
        }

        public static string[] Split(string path) => path.Split('/', StringSplitOptions.RemoveEmptyEntries);

        public static string Combine(string directory, string name) => directory.EndsWith('/') ? directory + name : directory + "/" + name;

        /// <summary>Dossier parent d'un chemin absolu (« / » pour « /srv »).</summary>
        public static string Parent(string path)
        {
            var names = Split(path);
            return names.Length <= 1 ? "/" : "/" + string.Join('/', names[..^1]);
        }
    }

    /// <summary>Compte Linux ; root est le compte d'UID 0.</summary>
    public sealed class LinuxUser
    {
        public string Name { get; set; } = string.Empty;
        public int Uid { get; set; }

        /// <summary>Groupe principal : celui des fichiers qu'il crée, hors dossier setgid.</summary>
        public string PrimaryGroup { get; set; } = string.Empty;

        /// <summary>Groupes secondaires.</summary>
        public List<string> Groups { get; set; } = new();

        /// <summary>Masque de création de la session (« 022 », « 027 », « 077 »).</summary>
        public string Umask { get; set; } = "022";

        [JsonIgnore]
        public bool IsRoot => Uid == 0;

        public bool IsMemberOf(string group) => PrimaryGroup == group || Groups.Contains(group);
    }

    public sealed class LinuxGroup
    {
        public string Name { get; set; } = string.Empty;
        public int Gid { get; set; }
    }

    public enum LinuxNodeKind
    {
        File,
        Directory
    }

    /// <summary>
    /// Fichier ou dossier. <see cref="Mode"/> est le mode octal (« 0640 », « 2770 », « 1777 ») : droits spéciaux,
    /// propriétaire, groupe, autres. Avec une ACL étendue (<see cref="Acl"/>), les droits du groupe du mode sont
    /// ceux du masque, comme le montre ls -l : les droits du groupe propriétaire sont alors dans l'ACL.
    /// </summary>
    public sealed class LinuxNode
    {
        public string Name { get; set; } = string.Empty;
        public LinuxNodeKind Kind { get; set; }
        public string Owner { get; set; } = "root";
        public string Group { get; set; } = "root";
        public string Mode { get; set; } = "0644";

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public PosixAcl? Acl { get; set; }

        /// <summary>ACL par défaut d'un dossier : celle que reçoit ce qui y est créé.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public PosixDefaultAcl? DefaultAcl { get; set; }

        public List<LinuxNode> Children { get; set; } = new();

        public static LinuxNode File(string name, string owner, string group, string mode) =>
            new() { Name = name, Kind = LinuxNodeKind.File, Owner = owner, Group = group, Mode = mode };

        public static LinuxNode Directory(string name, string owner, string group, string mode, params LinuxNode[] children) =>
            new() { Name = name, Kind = LinuxNodeKind.Directory, Owner = owner, Group = group, Mode = mode, Children = children.ToList() };

        [JsonIgnore]
        public bool IsDirectory => Kind == LinuxNodeKind.Directory;

        /// <summary>Mode en bits (0o7777).</summary>
        [JsonIgnore]
        public int ModeBits
        {
            get => Convert.ToInt32(Mode, 8) & 0xFFF;
            set => Mode = Convert.ToString(value & 0xFFF, 8).PadLeft(4, '0');
        }

        [JsonIgnore] public Perm OwnerPerm => (Perm)((ModeBits >> 6) & 7);

        /// <summary>Droits du groupe du mode : ceux du groupe propriétaire, ou le masque s'il y a une ACL étendue.</summary>
        [JsonIgnore] public Perm GroupPerm => (Perm)((ModeBits >> 3) & 7);

        [JsonIgnore] public Perm OtherPerm => (Perm)(ModeBits & 7);

        [JsonIgnore] public bool SetUid => (ModeBits & 0x800) != 0;
        [JsonIgnore] public bool SetGid => (ModeBits & 0x400) != 0;
        [JsonIgnore] public bool Sticky => (ModeBits & 0x200) != 0;

        /// <summary>Au moins un bit x (propriétaire, groupe ou autres) : root ne peut exécuter un fichier que dans ce cas.</summary>
        [JsonIgnore] public bool AnyExecute => (ModeBits & 0x49) != 0;
    }

    /// <summary>
    /// Entrées d'une ACL d'accès POSIX au-delà du mode : utilisateurs et groupes nommés, et les droits du groupe
    /// propriétaire. Le propriétaire, le masque et les autres restent dans le mode.
    /// </summary>
    public sealed class PosixAcl
    {
        public List<AclEntry> Users { get; set; } = new();

        /// <summary>Droits du groupe propriétaire (« r-x »), limités par le masque.</summary>
        public string GroupObj { get; set; } = "---";

        public List<AclEntry> Groups { get; set; } = new();
    }

    /// <summary>ACL par défaut d'un dossier, complète : elle devient l'ACL de ce qui y est créé.</summary>
    public sealed class PosixDefaultAcl
    {
        public string UserObj { get; set; } = "rwx";
        public List<AclEntry> Users { get; set; } = new();
        public string GroupObj { get; set; } = "r-x";
        public List<AclEntry> Groups { get; set; } = new();

        /// <summary>Masque ; sans lui, setfacl prend l'union des entrées nommées et du groupe propriétaire.</summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Mask { get; set; }

        public string Other { get; set; } = "r-x";
    }

    public sealed class AclEntry
    {
        public string Name { get; set; } = string.Empty;
        public string Perm { get; set; } = "---";

        public static AclEntry Of(string name, string perm) => new() { Name = name, Perm = perm };
    }
}
