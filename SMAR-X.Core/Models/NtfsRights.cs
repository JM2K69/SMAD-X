using System;
using System.Collections.Generic;
using System.Linq;

namespace SMARX.Models
{
    /// <summary>
    /// Droits NTFS (masque d'accès d'un fichier ou d'un dossier), avec les valeurs de Windows. Les autorisations de base
    /// de l'onglet Sécurité (Contrôle total, Modification…) sont des combinaisons de ces droits spéciaux.
    /// </summary>
    [Flags]
    public enum NtfsRights
    {
        None = 0,
        /// <summary>Liste du dossier / lecture de données.</summary>
        ReadData = 0x1,
        /// <summary>Création de fichiers / écriture de données.</summary>
        WriteData = 0x2,
        /// <summary>Création de dossiers / ajout de données.</summary>
        AppendData = 0x4,
        ReadExtendedAttributes = 0x8,
        WriteExtendedAttributes = 0x10,
        /// <summary>Parcours du dossier / exécuter le fichier.</summary>
        ExecuteFile = 0x20,
        /// <summary>Suppression de sous-dossier et fichier : sur un dossier, supprimer ce qu'il contient.</summary>
        DeleteSubdirectoriesAndFiles = 0x40,
        ReadAttributes = 0x80,
        WriteAttributes = 0x100,
        Delete = 0x10000,
        ReadPermissions = 0x20000,
        ChangePermissions = 0x40000,
        TakeOwnership = 0x80000,
        Synchronize = 0x100000,

        Read = ReadData | ReadExtendedAttributes | ReadAttributes | ReadPermissions | Synchronize,                 // 0x120089
        Write = WriteData | AppendData | WriteExtendedAttributes | WriteAttributes | Synchronize,                  // 0x100116
        ReadAndExecute = Read | ExecuteFile,                                                                      // 0x1200A9
        Modify = ReadAndExecute | Write | Delete,                                                                 // 0x1301BF
        FullControl = Modify | DeleteSubdirectoriesAndFiles | ChangePermissions | TakeOwnership                    // 0x1F01FF
    }

    /// <summary>Les droits sous forme lisible : autorisations de base et abréviations d'icacls.</summary>
    public static class Ntfs
    {
        /// <summary>Autorisations de base, de la plus large à la plus étroite, avec leur abréviation icacls.</summary>
        public static readonly (string Code, NtfsRights Rights)[] Simple =
        [
            ("F", NtfsRights.FullControl), ("M", NtfsRights.Modify), ("RX", NtfsRights.ReadAndExecute),
            ("R", NtfsRights.Read), ("W", NtfsRights.Write)
        ];

        /// <summary>Droits spéciaux, dans l'ordre de la fenêtre « Autorisations avancées », avec leur abréviation icacls.</summary>
        public static readonly (string Code, NtfsRights Right)[] Special =
        [
            ("X", NtfsRights.ExecuteFile), ("RD", NtfsRights.ReadData), ("RA", NtfsRights.ReadAttributes),
            ("REA", NtfsRights.ReadExtendedAttributes), ("WD", NtfsRights.WriteData), ("AD", NtfsRights.AppendData),
            ("WA", NtfsRights.WriteAttributes), ("WEA", NtfsRights.WriteExtendedAttributes),
            ("DC", NtfsRights.DeleteSubdirectoriesAndFiles), ("DE", NtfsRights.Delete), ("RC", NtfsRights.ReadPermissions),
            ("WDAC", NtfsRights.ChangePermissions), ("WO", NtfsRights.TakeOwnership), ("S", NtfsRights.Synchronize)
        ];

        /// <summary>
        /// « Refuser Écriture » tel que l'écrit l'onglet Sécurité de Windows : sans Synchroniser, que toute ouverture de
        /// fichier demande ; refuser aussi Synchroniser (icacls /deny (W)) empêcherait même de lire.
        /// </summary>
        public const string DenyWrite = "(WD,AD,WA,WEA)";

        /// <summary>Autorisation de base qui correspond aux droits, Synchroniser mis à part (un refus de l'onglet Sécurité l'omet), ou null.</summary>
        public static NtfsRights? SimpleOf(NtfsRights rights) =>
            Simple.Select(s => (NtfsRights?)s.Rights).FirstOrDefault(s => s == rights || s == (rights | NtfsRights.Synchronize));

        /// <summary>« F », « M », « RX »… ou, pour une combinaison spéciale, « (RD,WD) », comme icacls.</summary>
        public static string Format(NtfsRights rights)
        {
            foreach (var (code, value) in Simple)
                if (rights == value) return code;
            return "(" + string.Join(",", Special.Where(s => (rights & s.Right) == s.Right).Select(s => s.Code)) + ")";
        }

        /// <summary>Lit « M », « RX », « (RD,WD) » ou « RD,WD ».</summary>
        /// <exception cref="FormatException">Abréviation inconnue.</exception>
        public static NtfsRights Parse(string text)
        {
            var rights = NtfsRights.None;
            foreach (var token in text.Trim('(', ')').Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var simple = Simple.Where(s => s.Code == token).Select(s => (NtfsRights?)s.Rights).FirstOrDefault();
                var special = Special.Where(s => s.Code == token).Select(s => (NtfsRights?)s.Right).FirstOrDefault();
                rights |= simple ?? special ?? throw new FormatException($"Droit « {token} » inconnu.");
            }
            return rights;
        }

        /// <summary>Les droits spéciaux d'un masque, un par un.</summary>
        public static IEnumerable<NtfsRights> Split(NtfsRights rights) => Special.Select(s => s.Right).Where(r => (rights & r) == r);
    }

    public enum AceType
    {
        Allow,
        Deny
    }

    /// <summary>Héritage d'une ACE posée sur un dossier, comme les drapeaux d'icacls.</summary>
    [Flags]
    public enum AceInheritance
    {
        None = 0,
        /// <summary>(OI) : les fichiers en héritent.</summary>
        ObjectInherit = 1,
        /// <summary>(CI) : les sous-dossiers en héritent.</summary>
        ContainerInherit = 2,
        /// <summary>(IO) : ne s'applique pas au dossier lui-même, seulement à ce qui en hérite.</summary>
        InheritOnly = 4,
        /// <summary>(NP) : se transmet aux enfants directs seulement, pas plus bas.</summary>
        NoPropagate = 8,

        /// <summary>Ce dossier, les sous-dossiers et les fichiers (le cas par défaut).</summary>
        ThisFolderSubfoldersAndFiles = ObjectInherit | ContainerInherit
    }
}
