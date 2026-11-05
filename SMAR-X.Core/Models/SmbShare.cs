using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SMARX.Models
{
    /// <summary>
    /// Une autorisation de partage, comme la fenêtre « Autorisations » du Partage avancé : Lecture, Modifier, Contrôle
    /// total. Les masques sont ceux que Windows écrit dans le descripteur de sécurité du partage.
    /// </summary>
    public enum ShareRight
    {
        /// <summary>Lecture : lire et exécuter, lister (0x1200A9).</summary>
        Read,
        /// <summary>Modifier : Lecture, plus écrire et supprimer (0x1301BF).</summary>
        Change,
        /// <summary>Contrôle total : Modifier, plus changer les autorisations et prendre possession (0x1F01FF).</summary>
        FullControl
    }

    public sealed class ShareAce
    {
        public AceType Type { get; set; }
        public string Trustee { get; set; } = string.Empty;
        public ShareRight Right { get; set; } = ShareRight.Read;

        [JsonIgnore]
        public NtfsRights Mask => MaskOf(Right);

        public static NtfsRights MaskOf(ShareRight right) => right switch
        {
            ShareRight.Read => NtfsRights.ReadAndExecute,
            ShareRight.Change => NtfsRights.Modify,
            _ => NtfsRights.FullControl
        };

        public static ShareAce Allow(string trustee, ShareRight right) => new() { Type = AceType.Allow, Trustee = trustee, Right = right };
        public static ShareAce Deny(string trustee, ShareRight right) => new() { Type = AceType.Deny, Trustee = trustee, Right = right };
    }

    /// <summary>
    /// Un partage SMB : un nom (« Partages », ou « Compta$ » caché de la liste), le dossier partagé, ses autorisations de
    /// partage, et l'énumération basée sur l'accès (ABE), qui cache à chacun ce qu'il ne peut pas lire.
    /// </summary>
    public sealed class SmbShare
    {
        public string Name { get; set; } = string.Empty;

        /// <summary>Le dossier partagé, sur le serveur : « D:\Partages ».</summary>
        public string Path { get; set; } = string.Empty;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Description { get; set; }

        public List<ShareAce> Permissions { get; set; } = new();

        /// <summary>Énumération basée sur l'accès : un dossier listé par le partage ne montre que ce que le compte peut lire.</summary>
        public bool AccessBasedEnumeration { get; set; }

        /// <summary>Un nom terminé par « $ » ne s'affiche pas dans la liste des partages du serveur.</summary>
        [JsonIgnore]
        public bool IsHidden => Name.EndsWith('$');

        /// <summary>Le chemin réseau d'un objet du partage : « \\SRV-FIC\Partages\Compta ».</summary>
        public string Unc(string server, string path)
        {
            var rest = path.Length > Path.TrimEnd('\\').Length ? path[(Path.TrimEnd('\\').Length + 1)..] : "";
            return $@"\\{server}\{Name}" + (rest.Length > 0 ? "\\" + rest : "");
        }

        /// <summary>L'objet est dans le dossier partagé (ou est ce dossier).</summary>
        public bool Contains(string path)
        {
            var root = Path.TrimEnd('\\');
            return string.Equals(path.TrimEnd('\\'), root, StringComparison.OrdinalIgnoreCase)
                   || path.StartsWith(root + "\\", StringComparison.OrdinalIgnoreCase);
        }
    }
}
