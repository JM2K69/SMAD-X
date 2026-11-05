using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace SMARX.Models
{
    /// <summary>
    /// Serveur Windows simulé : ses comptes et groupes locaux, et un volume NTFS (« D: »). Les identités prédéfinies
    /// (Everyone, Authenticated Users, INTERACTIVE, CREATOR OWNER…) ne sont pas des groupes à créer : voir <see cref="WellKnown"/>.
    /// </summary>
    public sealed class WindowsHost
    {
        public string Name { get; set; } = "SRV-WIN";
        public List<WindowsUser> Users { get; set; } = new();
        public List<WindowsGroup> Groups { get; set; } = new();
        public NtfsNode Root { get; set; } = NtfsNode.Directory("D:", "Administrators");

        /// <summary>Les partages SMB du serveur : l'accès distant passe par eux.</summary>
        public List<SmbShare> Shares { get; set; } = new();

        public SmbShare? Share(string name) => Shares.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));

        public WindowsUser? User(string name) => Users.FirstOrDefault(u => string.Equals(u.Name, name, StringComparison.OrdinalIgnoreCase));

        /// <summary>Nœud d'un chemin « D:\Partages\Compta » (sans distinction de casse, comme NTFS sous Windows), ou null.</summary>
        public NtfsNode? Find(string path)
        {
            var names = Split(path);
            if (names.Length == 0 || !string.Equals(names[0], Root.Name, StringComparison.OrdinalIgnoreCase)) return null;
            var node = Root;
            foreach (var name in names.Skip(1))
            {
                node = node.Children.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
                if (node == null) return null;
            }
            return node;
        }

        /// <summary>Le nœud et ses dossiers parents, de la racine du volume au nœud.</summary>
        public IReadOnlyList<(NtfsNode Node, string Path)> Chain(string path)
        {
            var chain = new List<(NtfsNode, string)>();
            var names = Split(path);
            if (names.Length == 0 || !string.Equals(names[0], Root.Name, StringComparison.OrdinalIgnoreCase)) return chain;
            var node = Root;
            var current = Root.Name + "\\";
            chain.Add((node, current));
            foreach (var name in names.Skip(1))
            {
                node = node.Children.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
                if (node == null) return [];
                current = Combine(current, node.Name);
                chain.Add((node, current));
            }
            return chain;
        }

        public static string[] Split(string path) => path.Split('\\', StringSplitOptions.RemoveEmptyEntries);

        public static string Combine(string folder, string name) => folder.EndsWith('\\') ? folder + name : folder + "\\" + name;

        /// <summary>Dossier parent (« D:\ » pour « D:\Partages »), null pour la racine.</summary>
        public static string? Parent(string path)
        {
            var names = Split(path);
            return names.Length <= 1 ? null : names.Length == 2 ? names[0] + "\\" : string.Join('\\', names[..^1]);
        }
    }

    /// <summary>Compte local. Tous les comptes locaux sont membres de Users ; « Administrator » est le compte intégré.</summary>
    public sealed class WindowsUser
    {
        public string Name { get; set; } = string.Empty;

        /// <summary>Groupes locaux, Administrators compris ; Users est implicite.</summary>
        public List<string> Groups { get; set; } = new();

        [JsonIgnore]
        public bool IsAdministrator => Groups.Contains(WellKnown.Administrators, StringComparer.OrdinalIgnoreCase);

        /// <summary>Le compte Administrator intégré : le contrôle de compte d'utilisateur ne filtre pas son jeton.</summary>
        [JsonIgnore]
        public bool IsBuiltInAdministrator => string.Equals(Name, "Administrator", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Compte du domaine, et non compte local du serveur : à distance, le contrôle de compte d'utilisateur ne filtre que
        /// les comptes locaux (restrictions distantes de l'UAC).
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool Domain { get; set; }
    }

    public sealed class WindowsGroup
    {
        public string Name { get; set; } = string.Empty;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Description { get; set; }
    }

    /// <summary>Identités prédéfinies de Windows, sous leur nom anglais (celui d'icacls sur un Windows anglais).</summary>
    public static class WellKnown
    {
        public const string Everyone = "Everyone";
        public const string AuthenticatedUsers = "Authenticated Users";
        public const string Interactive = "INTERACTIVE";
        public const string Network = "NETWORK";
        public const string Users = "Users";
        public const string Administrators = "Administrators";
        public const string System = "SYSTEM";
        public const string CreatorOwner = "CREATOR OWNER";
        public const string OwnerRights = "OWNER RIGHTS";

        /// <summary>Nom affiché : celui d'un Windows français ou anglais.</summary>
        public static string Display(string name, bool french) => !french ? name : name switch
        {
            Everyone => "Tout le monde",
            AuthenticatedUsers => "Utilisateurs authentifiés",
            Interactive => "INTERACTIF",
            Network => "RÉSEAU",
            Users => "Utilisateurs",
            Administrators => "Administrateurs",
            System => "Système",
            CreatorOwner => "CRÉATEUR PROPRIÉTAIRE",
            OwnerRights => "DROITS DU PROPRIÉTAIRE",
            _ => name
        };
    }

    public enum NtfsNodeKind
    {
        File,
        Directory
    }

    /// <summary>
    /// Fichier ou dossier NTFS : son propriétaire, ses ACE explicites et l'héritage. Les ACE héritées ne sont pas
    /// stockées : elles se calculent depuis les dossiers parents (voir Services.Windows.NtfsInheritance), comme
    /// Windows les propage.
    /// </summary>
    public sealed class NtfsNode
    {
        public string Name { get; set; } = string.Empty;
        public NtfsNodeKind Kind { get; set; }
        public string Owner { get; set; } = WellKnown.Administrators;

        /// <summary>Faux : héritage désactivé (DACL protégée), seules les ACE explicites comptent.</summary>
        public bool InheritanceEnabled { get; set; } = true;

        public List<NtfsAce> Aces { get; set; } = new();
        public List<NtfsNode> Children { get; set; } = new();

        [JsonIgnore]
        public bool IsDirectory => Kind == NtfsNodeKind.Directory;

        public static NtfsNode File(string name, string owner, params NtfsAce[] aces) =>
            new() { Name = name, Kind = NtfsNodeKind.File, Owner = owner, Aces = aces.ToList() };

        public static NtfsNode Directory(string name, string owner, params NtfsAce[] aces) =>
            new() { Name = name, Kind = NtfsNodeKind.Directory, Owner = owner, Aces = aces.ToList() };

        public NtfsNode With(params NtfsNode[] children)
        {
            Children.AddRange(children);
            return this;
        }

        public NtfsNode Protected()
        {
            InheritanceEnabled = false;
            return this;
        }
    }

    /// <summary>Une entrée de contrôle d'accès explicite : autoriser ou refuser des droits à une identité, et à quoi elle s'applique.</summary>
    public sealed class NtfsAce
    {
        public AceType Type { get; set; }
        public string Trustee { get; set; } = string.Empty;

        /// <summary>Droits, comme icacls : « M », « RX », ou « (RD,WD) » pour des droits spéciaux.</summary>
        public string Rights { get; set; } = "R";

        public AceInheritance Inheritance { get; set; } = AceInheritance.ThisFolderSubfoldersAndFiles;

        [JsonIgnore]
        public NtfsRights Mask => Ntfs.Parse(Rights);

        public static NtfsAce Allow(string trustee, string rights, AceInheritance inheritance = AceInheritance.ThisFolderSubfoldersAndFiles) =>
            new() { Type = AceType.Allow, Trustee = trustee, Rights = rights, Inheritance = inheritance };

        public static NtfsAce Deny(string trustee, string rights, AceInheritance inheritance = AceInheritance.ThisFolderSubfoldersAndFiles) =>
            new() { Type = AceType.Deny, Trustee = trustee, Rights = rights, Inheritance = inheritance };
    }
}
