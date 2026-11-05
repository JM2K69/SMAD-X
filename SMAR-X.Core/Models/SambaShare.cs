using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace SMARX.Models
{
    /// <summary>
    /// Un partage Samba, comme une section de smb.conf : le dossier partagé, qui s'y connecte (valid users, invalid
    /// users), qui y écrit (read only, read list, write list), sous quelle identité Unix (force user, force group), avec
    /// quels droits pour ce qui y est créé (create mask, directory mask, force create mode, force directory mode), et ce
    /// que la liste cache (hide unreadable). Un nom précédé de « @ » désigne un groupe Unix.
    /// </summary>
    public sealed class SambaShare
    {
        public string Name { get; set; } = string.Empty;

        /// <summary>Le dossier partagé : « /srv/compta ».</summary>
        public string Path { get; set; } = string.Empty;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Comment { get; set; }

        /// <summary>read only = yes (le défaut de Samba) : personne n'écrit, sauf la write list.</summary>
        public bool ReadOnly { get; set; } = true;

        public List<string> ValidUsers { get; set; } = new();
        public List<string> InvalidUsers { get; set; } = new();

        /// <summary>Ces comptes n'écrivent pas, même si le partage est en écriture.</summary>
        public List<string> ReadList { get; set; } = new();

        /// <summary>Ces comptes écrivent, même si le partage est en lecture seule.</summary>
        public List<string> WriteList { get; set; } = new();

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ForceUser { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? ForceGroup { get; set; }

        public string CreateMask { get; set; } = "0744";
        public string ForceCreateMode { get; set; } = "0000";
        public string DirectoryMask { get; set; } = "0755";
        public string ForceDirectoryMode { get; set; } = "0000";

        /// <summary>Une liste de dossier ne montre pas ce que le compte ne peut pas lire.</summary>
        public bool HideUnreadable { get; set; }

        /// <summary>Le partage apparaît dans la liste des partages du serveur.</summary>
        public bool Browseable { get; set; } = true;

        /// <summary>La section de smb.conf ; <paramref name="prefix"/> place le dossier ailleurs (« /w » pour une vérification).</summary>
        public IReadOnlyList<string> Config(string prefix = "")
        {
            var lines = new List<string> { $"[{Name}]", $"   path = {prefix}{Path}" };
            if (Comment != null) lines.Add($"   comment = {Comment}");
            lines.Add($"   read only = {(ReadOnly ? "yes" : "no")}");
            void List(string key, List<string> values) { if (values.Count > 0) lines.Add($"   {key} = {string.Join(' ', values)}"); }
            List("valid users", ValidUsers);
            List("invalid users", InvalidUsers);
            List("read list", ReadList);
            List("write list", WriteList);
            if (ForceUser != null) lines.Add($"   force user = {ForceUser}");
            if (ForceGroup != null) lines.Add($"   force group = {ForceGroup}");
            lines.Add($"   create mask = {CreateMask}");
            if (ForceCreateMode != "0000") lines.Add($"   force create mode = {ForceCreateMode}");
            lines.Add($"   directory mask = {DirectoryMask}");
            if (ForceDirectoryMode != "0000") lines.Add($"   force directory mode = {ForceDirectoryMode}");
            if (HideUnreadable) lines.Add("   hide unreadable = yes");
            if (!Browseable) lines.Add("   browseable = no");
            return lines;
        }

        /// <summary>L'objet est dans le dossier partagé (ou est ce dossier).</summary>
        public bool Contains(string path) => path == Path || path.StartsWith(Path.TrimEnd('/') + "/");

        /// <summary>Le chemin réseau : « \\srv-fic\compta\budget.ods ».</summary>
        public string Unc(string server, string path)
        {
            var rest = path.Length > Path.TrimEnd('/').Length ? path[(Path.TrimEnd('/').Length + 1)..].Replace('/', '\\') : "";
            return $@"\\{server}\{Name}" + (rest.Length > 0 ? "\\" + rest : "");
        }

        /// <summary>Le compte est dans la liste : par son nom, ou par un groupe « @groupe » dont il est membre.</summary>
        public static bool Matches(IEnumerable<string> list, LinuxUser user) =>
            list.Any(entry => entry.StartsWith('@') ? user.IsMemberOf(entry[1..]) : entry == user.Name);
    }
}
