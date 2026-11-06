using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using SMARX.Models;
using static SMARX.Models.NtfsAce;

namespace SMARX.Labs
{
    /// <summary>Le serveur d'un exercice généré : Windows, Linux, ou les deux avec le même besoin.</summary>
    public enum SmarxPlatform { Windows, Linux, Both }

    /// <summary>
    /// Un service de l'entreprise : son nom affiché, son nom de groupe et de dossier (sans accent ni espace), son nom
    /// Linux (en minuscules) et ses comptes. Sous Windows : G_Nom, DL_Nom_L, DL_Nom_LM, D:\Partages\Nom.
    /// </summary>
    public sealed record SmarxService(string Display, string Name, string LinuxName, IReadOnlyList<string> Users)
    {
        public string Global => "G_" + Name;
        public string ReadGroup => $"DL_{Name}_L";
        public string ModifyGroup => $"DL_{Name}_LM";
        public string WindowsFolder => SmarxOrganization.WindowsShareRoot + "\\" + Name;
        public string FileName => Name.ToLowerInvariant() + "-dossier.xlsx";
        public string WindowsFile => WindowsFolder + "\\" + FileName;
        public string LinuxFolder => "/srv/" + LinuxName;
        public string LinuxFileName => LinuxName + "-dossier.ods";
        public string LinuxFile => LinuxFolder + "/" + LinuxFileName;
    }

    /// <summary>Une règle de gestion : les membres de <see cref="Who"/> lisent (ou modifient) le dossier de <see cref="Folder"/>.</summary>
    public sealed record SmarxAccessRule(SmarxService Who, SmarxService Folder, bool Modify);

    /// <summary>
    /// L'entreprise d'un exercice généré : son nom (qui donne le domaine), ses services et la plateforme. Chaque service
    /// a deux comptes. Les règles de gestion : chaque service lit et modifie son dossier ; la Direction, s'il y en a une,
    /// lit et modifie tous les dossiers ; le deuxième service lit le dossier du premier.
    /// </summary>
    public sealed class SmarxOrganization
    {
        public const int MinServices = 2;
        public const int MaxServices = 8;
        public const string WindowsServer = "SRV-FIC";
        public const string LinuxServer = "srv-fic";
        public const string WindowsShareRoot = @"D:\Partages";
        public const string WindowsShare = "Partages";

        private static readonly string[] FirstNames =
            ["alice", "bruno", "chloe", "david", "emma", "farid", "gaelle", "hugo", "ines", "jules", "karim", "lea", "malik", "nina", "oscar", "paula"];

        public SmarxOrganization(string company, IEnumerable<string> services, SmarxPlatform platform)
        {
            Company = string.IsNullOrWhiteSpace(company) ? "Lumen" : company.Trim();
            Platform = platform;
            Domain = NetBios(Company);
            var list = new List<SmarxService>();
            foreach (var display in services.Select(s => s.Trim()).Where(s => s.Length > 0))
            {
                var name = Identifier(display);
                if (name.Length == 0 || list.Any(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase))) continue;
                var users = FirstNames.Skip(list.Count * 2).Take(2).ToList();
                list.Add(new SmarxService(display, name, name.ToLowerInvariant(), users));
            }
            Services = list.Take(MaxServices).ToList();
            Rules = DefaultRules(Services);
        }

        public string Company { get; }
        public SmarxPlatform Platform { get; }

        /// <summary>Nom NetBIOS du domaine : les lettres et chiffres du nom de l'entreprise, en majuscules (« LUMEN »).</summary>
        public string Domain { get; }
        public string DnsDomain => Domain.ToLowerInvariant() + ".local";
        public IReadOnlyList<SmarxService> Services { get; }
        public IReadOnlyList<SmarxAccessRule> Rules { get; }
        public IEnumerable<string> Users => Services.SelectMany(s => s.Users);

        public bool HasWindows => Platform != SmarxPlatform.Linux;
        public bool HasLinux => Platform != SmarxPlatform.Windows;

        /// <summary>L'organisation d'exemple : Lumen, quatre services dont une Direction.</summary>
        public static SmarxOrganization Sample(SmarxPlatform platform = SmarxPlatform.Windows) =>
            new("Lumen", ["Compta", "RH", "Ventes", "Direction"], platform);

        /// <summary>Ce qui empêche de générer : trop peu ou trop de services, des noms en double. Null si tout va bien.</summary>
        public static string? Problem(IEnumerable<string> services, string culture)
        {
            var names = services.Select(s => Identifier(s.Trim())).Where(n => n.Length > 0).ToList();
            if (names.Count < MinServices)
                return T(culture, $"Il faut au moins {MinServices} services.", $"At least {MinServices} departments are needed.");
            if (names.Count > MaxServices)
                return T(culture, $"Au plus {MaxServices} services.", $"At most {MaxServices} departments.");
            var duplicate = names.GroupBy(n => n, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
            return duplicate == null ? null
                : T(culture, $"Le service {duplicate.Key} apparaît deux fois.", $"Department {duplicate.Key} appears twice.");
        }

        /// <summary>La Direction : le service dont le nom commence par « direction », s'il y en a un.</summary>
        public SmarxService? Direction => Services.FirstOrDefault(s => s.LinuxName.StartsWith("direction", StringComparison.Ordinal));

        private static IReadOnlyList<SmarxAccessRule> DefaultRules(IReadOnlyList<SmarxService> services)
        {
            var rules = services.Select(s => new SmarxAccessRule(s, s, true)).ToList();
            var direction = services.FirstOrDefault(s => s.LinuxName.StartsWith("direction", StringComparison.Ordinal));
            if (direction != null)
                rules.AddRange(services.Where(s => s != direction).Select(s => new SmarxAccessRule(direction, s, true)));
            var others = services.Where(s => s != direction).ToList();
            if (others.Count >= 2) rules.Add(new SmarxAccessRule(others[1], others[0], false));
            return rules;
        }

        /// <summary>Les règles qui donnent un droit sur le dossier d'un service.</summary>
        public IEnumerable<SmarxAccessRule> On(SmarxService folder) => Rules.Where(r => r.Folder == folder);

        /// <summary>Le droit d'un service sur le dossier d'un autre : null (aucun), false (lecture), true (modification).</summary>
        public bool? Right(SmarxService who, SmarxService folder) =>
            Rules.Where(r => r.Who == who && r.Folder == folder).Select(r => (bool?)r.Modify).OrderByDescending(m => m).FirstOrDefault();

        // ─── Le serveur Windows : AGDLP, partage large ───────────────────────────

        /// <summary>
        /// Le serveur Windows tel qu'il doit être : membre du domaine, les comptes dans leur G_, chaque G_ dans les DL_ que
        /// lui donnent les règles, chaque dossier de service à héritage désactivé avec Administrateurs, Système, DL_x_LM
        /// (Modification) et DL_x_L (Lecture et exécution), et le partage Partages en Utilisateurs authentifiés : Modifier.
        /// </summary>
        public WindowsHost BuildWindows()
        {
            const string Admins = WellKnown.Administrators;
            var host = new WindowsHost { Name = WindowsServer, Domain = Domain };
            host.Groups.Add(new WindowsGroup { Name = Admins, Description = "Administrateurs du serveur" });
            host.Groups.Add(new WindowsGroup { Name = WellKnown.Users, Description = "Tous les comptes" });
            foreach (var s in Services)
                host.Groups.Add(new WindowsGroup { Name = s.Global, Description = s.Display, Scope = WindowsGroupScope.Global });
            foreach (var s in Services)
            {
                host.Groups.Add(new WindowsGroup { Name = s.ReadGroup, Description = $"{s.WindowsFolder} : lecture", Scope = WindowsGroupScope.DomainLocal });
                host.Groups.Add(new WindowsGroup { Name = s.ModifyGroup, Description = $"{s.WindowsFolder} : lecture et modification", Scope = WindowsGroupScope.DomainLocal });
            }
            foreach (var rule in Rules)
                host.Group(rule.Who.Global)!.Groups.Add(rule.Modify ? rule.Folder.ModifyGroup : rule.Folder.ReadGroup);

            host.Users.Add(new WindowsUser { Name = "Administrator", Groups = [Admins] });
            foreach (var s in Services)
            foreach (var u in s.Users)
                host.Users.Add(new WindowsUser { Name = u, Domain = true, Groups = [s.Global] });

            host.Root = NtfsNode.Directory("D:", Admins,
                    Allow(Admins, "F"), Allow(WellKnown.System, "F"), Allow(WellKnown.Users, "RX"),
                    Allow(WellKnown.CreatorOwner, "F", AceInheritance.ThisFolderSubfoldersAndFiles | AceInheritance.InheritOnly))
                .With(NtfsNode.Directory("Partages", Admins,
                        Allow(Admins, "F"), Allow(WellKnown.System, "F"), Allow(WellKnown.AuthenticatedUsers, "RX", AceInheritance.None))
                    .Protected()
                    .With(Services.Select(s => NtfsNode.Directory(s.Name, Admins,
                            Allow(Admins, "F"), Allow(WellKnown.System, "F"), Allow(s.ModifyGroup, "M"), Allow(s.ReadGroup, "RX"))
                        .Protected()
                        .With(NtfsNode.File(s.FileName, s.Users[0]))).ToArray()));
            host.Shares.Add(new SmbShare
            {
                Name = WindowsShare, Path = WindowsShareRoot, Description = "Dossiers des services", AccessBasedEnumeration = true,
                Permissions = [ShareAce.Allow(WellKnown.AuthenticatedUsers, ShareRight.Change), ShareAce.Allow(Admins, ShareRight.FullControl)]
            });
            return host;
        }

        // ─── Le serveur Linux : groupes de service, setgid, ACL ──────────────────

        /// <summary>
        /// Le serveur Linux tel qu'il doit être : chaque compte a son groupe personnel et le groupe de son service ; chaque
        /// dossier de service est à root, au groupe du service, en 2770 ; les autres services y ont une entrée d'ACL
        /// (r-x ou rwx), reprise dans l'ACL par défaut pour ce qui sera créé ; chaque fichier est en 0660.
        /// </summary>
        public LinuxHost BuildLinux()
        {
            var host = new LinuxHost { Name = LinuxServer };
            host.Groups.Add(new LinuxGroup { Name = "root", Gid = 0 });
            host.Users.Add(new LinuxUser { Name = "root", Uid = 0, PrimaryGroup = "root" });
            var uid = 1001;
            foreach (var s in Services)
            foreach (var u in s.Users)
            {
                host.Groups.Add(new LinuxGroup { Name = u, Gid = uid });
                host.Users.Add(new LinuxUser { Name = u, Uid = uid, PrimaryGroup = u, Groups = [s.LinuxName] });
                uid++;
            }
            for (var i = 0; i < Services.Count; i++)
                host.Groups.Add(new LinuxGroup { Name = Services[i].LinuxName, Gid = 2001 + i });

            var folders = new List<LinuxNode>();
            foreach (var s in Services)
            {
                var file = LinuxNode.File(s.LinuxFileName, s.Users[0], s.LinuxName, "0660");
                var folder = LinuxNode.Directory(s.LinuxName, "root", s.LinuxName, "2770", file);
                var others = On(s).Where(r => r.Who != s).ToList();
                if (others.Count > 0)
                {
                    folder.Acl = new PosixAcl { GroupObj = "rwx", Groups = others.Select(r => AclEntry.Of(r.Who.LinuxName, r.Modify ? "rwx" : "r-x")).ToList() };
                    file.Acl = new PosixAcl { GroupObj = "rw-", Groups = others.Select(r => AclEntry.Of(r.Who.LinuxName, r.Modify ? "rw-" : "r--")).ToList() };
                }
                folder.DefaultAcl = new PosixDefaultAcl
                {
                    UserObj = "rwx", GroupObj = "rwx", Other = "---",
                    Groups = others.Select(r => AclEntry.Of(r.Who.LinuxName, r.Modify ? "rwx" : "r-x")).ToList()
                };
                folders.Add(folder);
            }
            host.Root = LinuxNode.Directory("", "root", "root", "0755",
                LinuxNode.Directory("srv", "root", "root", "0755", folders.ToArray()));
            return host;
        }

        // ─── Noms ────────────────────────────────────────────────────────────────

        /// <summary>Un nom de groupe ou de dossier : sans accent, lettres et chiffres seulement, la première en majuscule.</summary>
        public static string Identifier(string text)
        {
            var decomposed = text.Normalize(NormalizationForm.FormD);
            var letters = new StringBuilder();
            foreach (var c in decomposed)
                if (char.IsAsciiLetterOrDigit(c)) letters.Append(c);
            if (letters.Length == 0) return "";
            letters[0] = char.ToUpperInvariant(letters[0]);
            return letters.Length > 20 ? letters.ToString(0, 20) : letters.ToString();
        }

        private static string NetBios(string company)
        {
            var name = Identifier(company).ToUpperInvariant();
            return name.Length == 0 ? "CORP" : name.Length > 15 ? name[..15] : name;
        }

        private static string T(string culture, string fr, string en) =>
            culture.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? en : fr;
    }
}
