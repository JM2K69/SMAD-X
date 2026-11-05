using System;
using System.Collections.Generic;
using System.Linq;
using SMARX.Models;

namespace SMARX.Services.Windows
{
    /// <summary>Une ligne de l'onglet Sécurité de l'Explorateur : une autorisation de base, ou « Autorisations spéciales ».</summary>
    public enum BasicPermission
    {
        FullControl,
        Modify,
        ReadAndExecute,
        /// <summary>Lecture et exécution, pour ce dossier et les sous-dossiers seulement : un dossier n'a pas de fichiers à lire.</summary>
        ListFolderContents,
        Read,
        Write,
        Special
    }

    /// <summary>
    /// Une coche de l'onglet Sécurité : absente, noire (une ACE explicite de l'objet la donne) ou grise (l'héritage la
    /// donne ; elle se change sur le dossier parent).
    /// </summary>
    public enum Mark
    {
        None,
        Explicit,
        Inherited
    }

    public sealed record SecurityRow(BasicPermission Permission, Mark Allow, Mark Deny);

    /// <summary>
    /// Une entrée de la fenêtre « Paramètres de sécurité avancés » : les ACE d'un même compte, de même type, mêmes droits
    /// et même origine sont fusionnées, leurs portées réunies ((OI) + (CI) : ce dossier, les sous-dossiers et les fichiers).
    /// </summary>
    public sealed record AdvancedEntry(AceType Type, string Trustee, NtfsRights Mask, AceInheritance Inheritance, string? InheritedFrom);

    /// <summary>
    /// L'onglet Sécurité de l'Explorateur (Propriétés), comme Windows le calcule, relevé sur un vrai Windows 11
    /// (Fidelity/windows-security-tab.txt) :
    /// <list type="bullet">
    /// <item>une autorisation de base est cochée quand les ACE du compte, d'un même type, donnent tous ses droits à chacune
    /// de ses cibles : ce dossier, les sous-dossiers, les fichiers (« Affichage du contenu du dossier » : ce dossier et
    /// les sous-dossiers) ; (NP) n'y change rien ; Synchroniser ne compte pas (un refus l'omet) ;</item>
    /// <item>les ACE héritées et les explicites se lisent à part : une coche que l'héritage donne est grise, une coche que
    /// seules les explicites donnent est noire ;</item>
    /// <item>« Autorisations spéciales » est cochée, en gris, dès qu'il reste des droits qu'aucune autorisation de base cochée
    /// n'explique : un droit en plus, une portée incomplète ;</item>
    /// <item>la liste des comptes suit l'ordre de leurs SID, écrits en texte.</item>
    /// </list>
    /// </summary>
    public static class NtfsSecurityTab
    {
        private const NtfsRights Sync = NtfsRights.Synchronize;

        /// <summary>Les lignes, dans l'ordre de Windows ; un fichier n'a pas « Affichage du contenu du dossier ».</summary>
        public static IReadOnlyList<BasicPermission> Rows(bool folder) => folder
            ? [BasicPermission.FullControl, BasicPermission.Modify, BasicPermission.ReadAndExecute, BasicPermission.ListFolderContents,
               BasicPermission.Read, BasicPermission.Write, BasicPermission.Special]
            : [BasicPermission.FullControl, BasicPermission.Modify, BasicPermission.ReadAndExecute, BasicPermission.Read,
               BasicPermission.Write, BasicPermission.Special];

        public static NtfsRights Mask(BasicPermission permission) => permission switch
        {
            BasicPermission.FullControl => NtfsRights.FullControl,
            BasicPermission.Modify => NtfsRights.Modify,
            BasicPermission.ReadAndExecute or BasicPermission.ListFolderContents => NtfsRights.ReadAndExecute,
            BasicPermission.Read => NtfsRights.Read,
            BasicPermission.Write => NtfsRights.Write,
            _ => NtfsRights.None
        };

        /// <summary>La portée qu'écrit l'onglet Sécurité pour cette autorisation sur un dossier.</summary>
        public static AceInheritance Inheritance(BasicPermission permission) =>
            permission == BasicPermission.ListFolderContents ? AceInheritance.ContainerInherit : AceInheritance.ThisFolderSubfoldersAndFiles;

        // Cibles d'une ACE : ce dossier (ou ce fichier), les sous-dossiers, les fichiers
        private const int Self = 0, Folders = 1, Files = 2;

        private static IEnumerable<int> Targets(BasicPermission permission, bool folder) =>
            !folder ? [Self] : permission == BasicPermission.ListFolderContents ? [Self, Folders] : [Self, Folders, Files];

        private static NtfsRights[] Coverage(IEnumerable<EffectiveAce> aces, bool folder)
        {
            var cover = new NtfsRights[3];
            foreach (var ace in aces)
            {
                var mask = ace.Mask & ~Sync;
                if (!ace.InheritOnly) cover[Self] |= mask;
                if (folder && ace.Inheritance.HasFlag(AceInheritance.ContainerInherit)) cover[Folders] |= mask;
                if (folder && ace.Inheritance.HasFlag(AceInheritance.ObjectInherit)) cover[Files] |= mask;
            }
            return cover;
        }

        /// <summary>Les autorisations de base que des ACE donnent entièrement, et s'il reste des droits spéciaux.</summary>
        private static (HashSet<BasicPermission> Checked, bool Special) Read(IEnumerable<EffectiveAce> aces, bool folder)
        {
            var cover = Coverage(aces, folder);
            var basics = Rows(folder).Where(p => p != BasicPermission.Special)
                .Where(p => Targets(p, folder).All(t => (cover[t] & Mask(p) & ~Sync) == (Mask(p) & ~Sync)))
                .ToHashSet();
            var explained = new NtfsRights[3];
            foreach (var p in basics)
                foreach (var t in Targets(p, folder))
                    explained[t] |= Mask(p);
            var special = Enumerable.Range(0, 3).Any(t => (cover[t] & ~explained[t] & ~Sync) != 0);
            return (basics, special);
        }

        /// <summary>Les coches d'un compte, ligne par ligne, autoriser puis refuser.</summary>
        public static IReadOnlyList<SecurityRow> Permissions(WindowsHost host, string path, string trustee)
        {
            var node = host.Find(path) ?? throw new ArgumentException($"{path} n'existe pas.", nameof(path));
            var aces = NtfsInheritance.Dacl(host, path).Where(a => Same(a.Trustee, trustee)).ToList();
            Mark[] Column(AceType type)
            {
                var inherited = Read(aces.Where(a => a.Type == type && a.Inherited), node.IsDirectory);
                var own = Read(aces.Where(a => a.Type == type && !a.Inherited), node.IsDirectory);
                return Rows(node.IsDirectory).Select(p => p == BasicPermission.Special
                        ? inherited.Special ? Mark.Inherited : own.Special ? Mark.Explicit : Mark.None
                        : inherited.Checked.Contains(p) ? Mark.Inherited : own.Checked.Contains(p) ? Mark.Explicit : Mark.None)
                    .ToArray();
            }
            var allow = Column(AceType.Allow);
            var deny = Column(AceType.Deny);
            return Rows(node.IsDirectory).Select((p, i) => new SecurityRow(p, allow[i], deny[i])).ToList();
        }

        /// <summary>Les comptes cités par la DACL de l'objet, dans l'ordre de l'onglet Sécurité.</summary>
        public static IReadOnlyList<string> Principals(WindowsHost host, string path) =>
            NtfsInheritance.Dacl(host, path).Select(a => a.Trustee).Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(t => Sid(host, t), StringComparer.Ordinal).ToList();

        /// <summary>
        /// Un SID pour ranger les comptes comme Windows : ceux des identités prédéfinies, sinon un SID local
        /// (S-1-5-21-…-RID) : Administrator 500, puis les comptes et les groupes dans l'ordre de leur création.
        /// </summary>
        public static string Sid(WindowsHost host, string name)
        {
            var known = name switch
            {
                WellKnown.Everyone => "S-1-1-0", WellKnown.CreatorOwner => "S-1-3-0", WellKnown.OwnerRights => "S-1-3-4",
                WellKnown.Network => "S-1-5-2", WellKnown.Interactive => "S-1-5-4", WellKnown.AuthenticatedUsers => "S-1-5-11",
                WellKnown.System => "S-1-5-18", WellKnown.Administrators => "S-1-5-32-544", WellKnown.Users => "S-1-5-32-545",
                _ => null
            };
            if (known != null) return known;
            var user = host.Users.FindIndex(u => Same(u.Name, name));
            if (user >= 0) return "S-1-5-21-0-0-0-" + (host.Users[user].IsBuiltInAdministrator ? 500 : 1001 + user);
            var group = host.Groups.FindIndex(g => Same(g.Name, name));
            return "S-1-5-21-0-0-0-" + (group >= 0 ? 1001 + host.Users.Count + group : 9999);
        }

        /// <summary>Les entrées de « Paramètres de sécurité avancés », dans l'ordre de la DACL, fusionnées.</summary>
        public static IReadOnlyList<AdvancedEntry> Advanced(WindowsHost host, string path)
        {
            var node = host.Find(path) ?? throw new ArgumentException($"{path} n'existe pas.", nameof(path));
            var groups = new List<(EffectiveAce First, int Targets, bool NoPropagate)>();
            foreach (var ace in NtfsInheritance.Dacl(host, path))
            {
                var targets = (ace.InheritOnly ? 0 : 1 << Self)
                              | (ace.Inheritance.HasFlag(AceInheritance.ContainerInherit) ? 1 << Folders : 0)
                              | (ace.Inheritance.HasFlag(AceInheritance.ObjectInherit) ? 1 << Files : 0);
                var noPropagate = ace.Inheritance.HasFlag(AceInheritance.NoPropagate);
                var i = groups.FindIndex(g => g.First.Type == ace.Type && Same(g.First.Trustee, ace.Trustee) && g.First.Mask == ace.Mask
                                              && g.First.InheritedFrom == ace.InheritedFrom && g.NoPropagate == noPropagate);
                if (i < 0) groups.Add((ace, targets, noPropagate));
                else groups[i] = (groups[i].First, groups[i].Targets | targets, noPropagate);
            }
            return groups.Select(g => new AdvancedEntry(g.First.Type, g.First.Trustee, g.First.Mask,
                node.IsDirectory ? Scope(g.Targets, g.NoPropagate) : AceInheritance.None, g.First.InheritedFrom)).ToList();

            static AceInheritance Scope(int targets, bool noPropagate)
            {
                var scope = ((targets & (1 << Folders)) != 0 ? AceInheritance.ContainerInherit : 0)
                            | ((targets & (1 << Files)) != 0 ? AceInheritance.ObjectInherit : 0)
                            | ((targets & (1 << Self)) == 0 ? AceInheritance.InheritOnly : 0);
                return noPropagate ? scope | AceInheritance.NoPropagate : scope;
            }
        }

        /// <summary>
        /// La colonne « Accès » : le nom d'une autorisation de base (« Affichage du contenu du dossier » pour Lecture et
        /// exécution sur ce dossier et les sous-dossiers), « Lecture, écriture et exécution », le nom d'un droit spécial
        /// seul, sinon « Spéciale ».
        /// </summary>
        public static string Access(AdvancedEntry entry, bool folder, string culture)
        {
            var french = !culture.StartsWith("en", StringComparison.OrdinalIgnoreCase);
            var mask = entry.Mask & ~Sync;
            if (mask == (NtfsRights.ReadAndExecute & ~Sync) && folder && (entry.Inheritance & ~AceInheritance.NoPropagate) == AceInheritance.ContainerInherit)
                return Name(BasicPermission.ListFolderContents, culture);
            foreach (var p in new[] { BasicPermission.FullControl, BasicPermission.Modify, BasicPermission.ReadAndExecute, BasicPermission.Read, BasicPermission.Write })
                if (mask == (Mask(p) & ~Sync))
                    return Name(p, culture);
            if (mask == ((NtfsRights.ReadAndExecute | NtfsRights.Write) & ~Sync))
                return french ? "Lecture, écriture et exécution" : "Read, write & execute";
            if (Ntfs.Special.Any(s => s.Right == entry.Mask) || Ntfs.Special.Any(s => s.Right == mask))
                return WindowsExplainer.Rights(Ntfs.Special.First(s => s.Right == entry.Mask || s.Right == mask).Right, culture);
            return french ? "Spéciale" : "Special";
        }

        /// <summary>« S'applique à » : la portée, sans (NP), que la fenêtre ne montre que dans le détail d'une entrée.</summary>
        public static string AppliesTo(AdvancedEntry entry, bool folder, string culture) =>
            folder ? WindowsExplainer.AppliesTo(entry.Inheritance & ~AceInheritance.NoPropagate, true, culture) : "";

        /// <summary>« Hérité de » : « Aucun » pour une entrée explicite, sinon le dossier, terminé par « \ ».</summary>
        public static string InheritedFrom(AdvancedEntry entry, string culture) =>
            entry.InheritedFrom == null ? culture.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? "None" : "Aucun"
            : entry.InheritedFrom.EndsWith('\\') ? entry.InheritedFrom : entry.InheritedFrom + "\\";

        /// <summary>Le nom d'une ligne, comme un Windows français ou anglais.</summary>
        public static string Name(BasicPermission permission, string culture)
        {
            var french = !culture.StartsWith("en", StringComparison.OrdinalIgnoreCase);
            return permission switch
            {
                BasicPermission.FullControl => french ? "Contrôle total" : "Full control",
                BasicPermission.Modify => french ? "Modification" : "Modify",
                BasicPermission.ReadAndExecute => french ? "Lecture et exécution" : "Read & execute",
                BasicPermission.ListFolderContents => french ? "Affichage du contenu du dossier" : "List folder contents",
                BasicPermission.Read => french ? "Lecture" : "Read",
                BasicPermission.Write => french ? "Écriture" : "Write",
                _ => french ? "Autorisations spéciales" : "Special permissions"
            };
        }

        /// <summary>
        /// Le nom d'un compte dans la liste : « Tout le monde », « Système », ou « Administrateurs (SRV-FIC\Administrateurs) »
        /// pour un groupe intégré, un compte ou un groupe local.
        /// </summary>
        public static string Display(WindowsHost host, string name, string culture)
        {
            var french = !culture.StartsWith("en", StringComparison.OrdinalIgnoreCase);
            var shown = WellKnown.Display(name, french);
            return name is WellKnown.Everyone or WellKnown.CreatorOwner or WellKnown.OwnerRights or WellKnown.System
                or WellKnown.AuthenticatedUsers or WellKnown.Interactive or WellKnown.Network
                ? shown
                : $"{shown} ({host.Name}\\{shown})";
        }

        // ── La fenêtre « Autorisations pour … » (bouton Modifier) ──

        /// <summary>
        /// Cocher ou décocher une autorisation de base pour un compte, comme la fenêtre Modifier, relevé sur Windows 11
        /// (Fidelity/windows-security-tab.txt, lignes EDIT) :
        /// <list type="bullet">
        /// <item>décocher une case décoche aussi celles qui la contiennent sur la même portée (Lecture : Lecture et
        /// exécution, Modification, Contrôle total ; Affichage du contenu du dossier, qui ne vise pas les fichiers, reste) ;</item>
        /// <item>cocher une case d'une colonne décoche dans l'autre toutes celles qui ont des droits en commun (refuser la
        /// Lecture ne laisse, côté Autoriser, que l'Écriture) ;</item>
        /// <item>les ACE explicites du compte, dans chaque colonne touchée, sont réécrites : une par portée, avec les
        /// autorisations qui restent cochées (une case contenue dans une autre n'ajoute rien), en fin de liste ; un refus
        /// omet Synchroniser, sauf Contrôle total ; des droits spéciaux en plus sont gardés tels quels.</item>
        /// </list>
        /// Une coche que seul l'héritage donne ne se décoche pas ici : faux.
        /// </summary>
        public static bool Set(WindowsHost host, string path, string trustee, BasicPermission permission, AceType type, bool on)
        {
            var node = host.Find(path) ?? throw new ArgumentException($"{path} n'existe pas.", nameof(path));
            if (permission == BasicPermission.Special) return false;
            var other = type == AceType.Allow ? AceType.Deny : AceType.Allow;
            var checkedHere = Own(host, path, trustee, type);
            if (!on && !checkedHere.Contains(permission)) return false;

            var folder = node.IsDirectory;
            if (on)
            {
                checkedHere.Add(permission);
                var checkedThere = Own(host, path, trustee, other);
                var overlapping = checkedThere.Where(q => (Mask(q) & Mask(permission) & ~Sync) != 0).ToList();
                if (overlapping.Count > 0)
                    Rewrite(node, trustee, other, checkedThere.Except(overlapping).ToHashSet(), folder);
            }
            else
                checkedHere.RemoveWhere(q => q == permission || ((Mask(q) & Mask(permission)) == Mask(permission)
                                                                 && Targets(permission, folder).All(Targets(q, folder).Contains)));
            Rewrite(node, trustee, type, checkedHere, folder);
            return true;
        }

        /// <summary>Les autorisations de base que les ACE explicites du compte cochent dans une colonne.</summary>
        private static HashSet<BasicPermission> Own(WindowsHost host, string path, string trustee, AceType type) =>
            Read(NtfsInheritance.Dacl(host, path).Where(a => !a.Inherited && a.Type == type && Same(a.Trustee, trustee)),
                host.Find(path)!.IsDirectory).Checked;

        /// <summary>Les ACE explicites d'une colonne, réécrites en fin de liste depuis les cases qui restent cochées.</summary>
        private static void Rewrite(NtfsNode node, string trustee, AceType type, HashSet<BasicPermission> kept, bool folder)
        {
            var old = node.Aces.Where(a => a.Type == type && Same(a.Trustee, trustee)).ToList();
            // Droits spéciaux en plus : ce qu'aucune autorisation de base cochée avant n'explique
            var basics = Rows(folder).Where(p => p != BasicPermission.Special).Aggregate(NtfsRights.None, (m, p) => m | Mask(p));
            var extras = old.Select(a => (Ace: a, Bits: a.Mask & ~basics & ~Sync)).Where(x => x.Bits != 0).ToList();
            foreach (var a in old) node.Aces.Remove(a);

            // Une case contenue dans une autre cochée (droits et portée) n'ajoute rien
            var minimal = kept.Where(p => !kept.Any(q => q != p && (Mask(q) & Mask(p)) == Mask(p)
                                                         && Targets(p, folder).All(Targets(q, folder).Contains))).ToList();
            foreach (var scope in Rows(folder).Where(minimal.Contains).Select(p => folder ? Inheritance(p) : AceInheritance.None).Distinct())
            {
                var group = minimal.Where(p => (folder ? Inheritance(p) : AceInheritance.None) == scope).ToList();
                var mask = group.Aggregate(NtfsRights.None, (m, p) => m | Mask(p));
                // Un refus omet Synchroniser, sauf Contrôle total
                if (type == AceType.Deny && !group.Contains(BasicPermission.FullControl)) mask &= ~Sync;
                node.Aces.Add(new NtfsAce { Type = type, Trustee = trustee, Rights = Ntfs.Format(mask), Inheritance = scope });
            }
            foreach (var (ace, bits) in extras)
                node.Aces.Add(new NtfsAce { Type = type, Trustee = trustee, Rights = Ntfs.Format(bits), Inheritance = ace.Inheritance });
        }

        /// <summary>Les ACE explicites du compte, à elles seules, cochent cette autorisation : sa case reste modifiable.</summary>
        public static bool Explicitly(WindowsHost host, string path, string trustee, BasicPermission permission, AceType type) =>
            permission != BasicPermission.Special && Own(host, path, trustee, type).Contains(permission);

        /// <summary>Ajouter un compte : Lecture et exécution (avec Affichage du contenu du dossier et Lecture), comme Windows.</summary>
        public static void Add(WindowsHost host, string path, string trustee)
        {
            var node = host.Find(path) ?? throw new ArgumentException($"{path} n'existe pas.", nameof(path));
            if (node.Aces.Any(a => Same(a.Trustee, trustee))) return;
            node.Aces.Add(NtfsAce.Allow(trustee, "RX", node.IsDirectory ? AceInheritance.ThisFolderSubfoldersAndFiles : AceInheritance.None));
        }

        /// <summary>
        /// Supprimer un compte : ses ACE explicites. Faux s'il a aussi des ACE héritées : Windows refuse, il faut d'abord
        /// désactiver l'héritage.
        /// </summary>
        public static bool Remove(WindowsHost host, string path, string trustee)
        {
            var node = host.Find(path) ?? throw new ArgumentException($"{path} n'existe pas.", nameof(path));
            if (NtfsInheritance.Dacl(host, path).Any(a => a.Inherited && Same(a.Trustee, trustee))) return false;
            node.Aces.RemoveAll(a => Same(a.Trustee, trustee));
            return true;
        }

        /// <summary>
        /// Désactiver l'héritage : convertir les ACE héritées en ACE explicites, telles qu'elles se lisent sur l'objet
        /// (icacls /inheritance:d), ou les supprimer (/inheritance:r).
        /// </summary>
        public static void DisableInheritance(WindowsHost host, string path, bool convert)
        {
            var node = host.Find(path) ?? throw new ArgumentException($"{path} n'existe pas.", nameof(path));
            if (!node.InheritanceEnabled) return;
            var inherited = NtfsInheritance.Dacl(host, path).Where(a => a.Inherited).ToList();
            node.InheritanceEnabled = false;
            if (convert)
                foreach (var a in inherited)
                    node.Aces.Add(new NtfsAce { Type = a.Type, Trustee = a.Trustee, Rights = Ntfs.Format(a.Mask), Inheritance = a.Inheritance });
        }

        private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }
}
