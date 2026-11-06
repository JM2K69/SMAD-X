using System;
using System.Collections.Generic;
using System.Linq;
using SMARX.Models;
using SMARX.Services.Windows;

namespace SMARX.Services
{
    /// <summary>Une ligne de l'aide-mémoire : l'abréviation, son nom, et ce qu'elle veut dire (sur un fichier, sur un dossier).</summary>
    public sealed record GlossaryRow(string Code, string Name, string Meaning, string? Value = null);

    /// <summary>
    /// Une commande d'exemple, prête à copier, et ce qu'elle fait. <see cref="Icacls"/> : la commande icacls qui fait la
    /// même chose, quand la commande est celle d'un module PowerShell (NTFSSecurity).
    /// </summary>
    public sealed record GlossaryCommand(string Command, string Meaning, string? Icacls = null)
    {
        public bool HasIcacls => Icacls != null;

        /// <summary>
        /// La commande sur une ligne (et son équivalent icacls devant) : la clé de son relevé sur un vrai système
        /// (SMAR-X.Tests/Fidelity/glossary-*.txt, écrit par tools/SmarxGlossaryCheck).
        /// </summary>
        public string Key => (Icacls is { } icacls ? icacls + " <=> " + Command : Command).Replace("\n", " ;; ");
    }

    /// <summary>Une section de l'aide-mémoire : des abréviations expliquées, ou des commandes d'exemple, ou les deux.</summary>
    public sealed record GlossarySection(string Title, string? Intro, IReadOnlyList<GlossaryRow> Rows, IReadOnlyList<GlossaryCommand>? Commands = null)
    {
        public IReadOnlyList<GlossaryCommand> CommandList => Commands ?? [];
        public bool HasRows => Rows.Count > 0;
        public bool HasCommands => CommandList.Count > 0;
    }

    /// <summary>
    /// Aide-mémoire des abréviations que montre SMAR-X, en français ou en anglais. Les droits Windows viennent des
    /// définitions du moteur (<see cref="Ntfs"/>) : chaque abréviation d'icacls y est, avec sa valeur.
    /// </summary>
    public static partial class Glossary
    {
        public static IReadOnlyList<GlossarySection> Windows(string culture)
        {
            string T(string fr, string en) => Text(culture, fr, en);
            return
            [
                new(T("Droits spéciaux (icacls)", "Special rights (icacls)"),
                    T("Le grain le plus fin : chaque autorisation de base est une combinaison de ces droits. Sur un fichier, un dossier : le même droit ne permet pas la même chose.",
                      "The finest grain: each basic permission is a combination of these rights. On a file, a folder: the same right does not allow the same thing."),
                    Ntfs.Special.Select(s => new GlossaryRow(s.Code, WindowsExplainer.Rights(s.Right, culture), SpecialMeaning(s.Right, culture), $"0x{(int)s.Right:X}")).ToList()),
                new(T("Autorisations de base", "Basic permissions"),
                    T("Ce que coche l'onglet Sécurité ; icacls les abrège ainsi. Un refus posé par l'onglet Sécurité omet S (Synchroniser).",
                      "What the Security tab checks; icacls abbreviates them this way. A deny set from the Security tab leaves out S (Synchronize)."),
                    Ntfs.Simple.Select(s => new GlossaryRow(s.Code, WindowsExplainer.Rights(s.Rights, culture),
                        string.Join(", ", Ntfs.Split(s.Rights).Select(r => Ntfs.Format(r).Trim('(', ')'))), $"0x{(int)s.Rights:X}")).ToList()),
                new(T("Drapeaux d'une ACE", "ACE flags"), null,
                [
                    new("(I)", T("Héritée", "Inherited"), T("Vient d'un dossier parent ; elle se modifie sur ce dossier, pas ici.", "Comes from a parent folder; it is changed on that folder, not here.")),
                    new("(OI)", T("Héritage par les fichiers", "Object inherit"), T("Les fichiers du dossier (et plus bas) reçoivent cette ACE.", "The folder's files (and below) receive this ACE.")),
                    new("(CI)", T("Héritage par les sous-dossiers", "Container inherit"), T("Les sous-dossiers reçoivent cette ACE.", "Subfolders receive this ACE.")),
                    new("(IO)", T("Héritage seulement", "Inherit only"), T("Ne s'applique pas au dossier lui-même, seulement à ce qui en hérite.", "Does not apply to the folder itself, only to what inherits from it.")),
                    new("(NP)", T("Pas de propagation", "No propagate"), T("Seuls les enfants directs en héritent, pas plus bas.", "Only direct children inherit it, not further down.")),
                    new("(DENY)", T("Refuser", "Deny"), T("Refuse les droits qui ne sont pas encore accordés par une ACE lue avant elle.", "Denies the rights not yet granted by an ACE read before it.")),
                    new("(OI)(CI)", T("Ce dossier, les sous-dossiers et les fichiers", "This folder, subfolders and files"), T("Le cas par défaut d'une ACE posée sur un dossier.", "The default for an ACE set on a folder."))
                ]),
                new(T("Identités prédéfinies", "Well-known identities"), null,
                [
                    Identity(WellKnown.Everyone, T("Toute session, y compris invitée.", "Any session, guests included."), culture),
                    Identity(WellKnown.AuthenticatedUsers, T("Tout compte qui s'est authentifié (pas une session anonyme).", "Any account that authenticated (not an anonymous session)."), culture),
                    Identity(WellKnown.Users, T("Groupe local qui contient tous les comptes locaux.", "Local group that contains every local account."), culture),
                    Identity(WellKnown.Administrators, T("Administrateurs locaux ; sans session élevée, l'UAC ne garde ce groupe que pour les refus.", "Local administrators; without an elevated session, UAC keeps this group for denies only."), culture),
                    Identity(WellKnown.Interactive, T("Session ouverte sur la machine elle-même (console, RDP).", "Session opened on the machine itself (console, RDP)."), culture),
                    Identity(WellKnown.Network, T("Accès venu du réseau, par un partage.", "Access coming from the network, through a share."), culture),
                    Identity(WellKnown.CreatorOwner, T("Dans ce qui hérite de l'ACE, devient le propriétaire (le créateur).", "In what inherits the ACE, becomes the owner (the creator)."), culture),
                    Identity(WellKnown.OwnerRights, T("Remplace les droits que le propriétaire a d'office (lire et modifier les autorisations).", "Replaces the rights the owner has implicitly (read and change permissions)."), culture),
                    Identity(WellKnown.System, T("Le système d'exploitation lui-même.", "The operating system itself."), culture)
                ]),
                .. WindowsExamples(culture)
            ];
        }

        private static GlossaryRow Identity(string name, string meaning, string culture) =>
            new(name, WellKnown.Display(name, !culture.StartsWith("en", StringComparison.OrdinalIgnoreCase)), meaning);

        /// <summary>Ce qu'un droit spécial permet, sur un fichier puis sur un dossier.</summary>
        private static string SpecialMeaning(NtfsRights right, string culture)
        {
            string T(string fr, string en) => Text(culture, fr, en);
            return right switch
            {
                NtfsRights.ExecuteFile => T("Fichier : l'exécuter. Dossier : le traverser (non vérifié par défaut : « Contourner la vérification de parcours »).",
                                            "File: run it. Folder: traverse it (not checked by default: \"Bypass traverse checking\")."),
                NtfsRights.ReadData => T("Fichier : lire son contenu. Dossier : lister ce qu'il contient.", "File: read its content. Folder: list what it contains."),
                NtfsRights.ReadAttributes => T("Lire les attributs (lecture seule, caché, système, archive). Accordé aussi par « Liste du dossier » sur le dossier parent.",
                                               "Read the attributes (read-only, hidden, system, archive). Also granted by \"List folder\" on the parent folder."),
                NtfsRights.ReadExtendedAttributes => T("Lire les attributs étendus (données ajoutées par les applications).", "Read the extended attributes (data added by applications)."),
                NtfsRights.WriteData => T("Fichier : modifier son contenu. Dossier : y créer des fichiers.", "File: change its content. Folder: create files in it."),
                NtfsRights.AppendData => T("Fichier : ajouter à la fin. Dossier : y créer des sous-dossiers.", "File: append to it. Folder: create subfolders in it."),
                NtfsRights.WriteAttributes => T("Modifier les attributs.", "Change the attributes."),
                NtfsRights.WriteExtendedAttributes => T("Modifier les attributs étendus.", "Change the extended attributes."),
                NtfsRights.DeleteSubdirectoriesAndFiles => T("Dossier : supprimer ce qu'il contient, même sans « Suppression » sur ces objets. Sans effet sur un fichier.",
                                                             "Folder: delete what it contains, even without \"Delete\" on those objects. No effect on a file."),
                NtfsRights.Delete => T("Supprimer l'objet lui-même. Accordé aussi par « Suppression de sous-dossier et fichier » sur le dossier parent.",
                                       "Delete the object itself. Also granted by \"Delete subfolders and files\" on the parent folder."),
                NtfsRights.ReadPermissions => T("Lire les autorisations et le propriétaire.", "Read the permissions and the owner."),
                NtfsRights.ChangePermissions => T("Modifier les autorisations (la DACL) ; le propriétaire le peut toujours.", "Change the permissions (the DACL); the owner always can."),
                NtfsRights.TakeOwnership => T("Devenir propriétaire.", "Become the owner."),
                NtfsRights.Synchronize => T("Attendre l'objet ; presque toute ouverture le demande, c'est pourquoi un refus l'omet.", "Wait on the object; almost every open asks for it, which is why a deny leaves it out."),
                _ => ""
            };
        }

        public static IReadOnlyList<GlossarySection> Linux(string culture)
        {
            string T(string fr, string en) => Text(culture, fr, en);
            return
            [
                new(T("Droits r, w, x", "Rights r, w, x"),
                    T("Trois droits pour trois classes : le propriétaire, le groupe, les autres. Un compte n'est que dans une classe : la première qui le concerne.",
                      "Three rights for three classes: the owner, the group, the others. An account is in one class only: the first that applies to it."),
                [
                    new("r", T("Lire", "Read"), T("Fichier : lire son contenu. Dossier : lister les noms de ce qu'il contient.", "File: read its content. Directory: list the names of what it contains."), "4"),
                    new("w", T("Écrire", "Write"), T("Fichier : modifier son contenu. Dossier : créer, renommer, supprimer dedans (avec x).", "File: change its content. Directory: create, rename, delete inside (with x)."), "2"),
                    new("x", T("Exécuter", "Execute"), T("Fichier : l'exécuter (un script doit aussi être lisible). Dossier : le traverser, atteindre ce qu'il contient.", "File: run it (a script must also be readable). Directory: traverse it, reach what it contains."), "1")
                ]),
                new(T("Ce que montre ls -l", "What ls -l shows"), T("Exemple : drwxrws--T+", "Example: drwxrws--T+"),
                [
                    new("-", T("Fichier", "File"), T("Premier caractère d'un fichier ordinaire.", "First character of a regular file.")),
                    new("d", T("Dossier", "Directory"), T("Premier caractère d'un dossier.", "First character of a directory.")),
                    new("s / S", "setuid", T("À la place du x du propriétaire : le programme s'exécute avec l'identité de son propriétaire. S : sans x.", "In the owner's x place: the program runs as its owner. S: without x."), "4000"),
                    new("s / S", "setgid", T("À la place du x du groupe : un dossier donne son groupe à ce qui y est créé, et setgid à ses sous-dossiers. S : sans x.", "In the group's x place: a directory gives its group to what is created in it, and setgid to its subdirectories. S: without x."), "2000"),
                    new("t / T", "sticky", T("À la place du x des autres : dans ce dossier, seuls le propriétaire du fichier, celui du dossier et root suppriment. T : sans x.", "In the others' x place: in this directory, only the file's owner, the directory's owner and root delete. T: without x."), "1000"),
                    new("+", T("ACL étendue", "Extended ACL"), T("Des entrées d'ACL s'ajoutent au mode ; les droits du groupe affichés sont alors le masque.", "ACL entries are added to the mode; the group rights shown are then the mask."))
                ]),
                new(T("Mode octal (chmod)", "Octal mode (chmod)"), T("Exemple : 2750 = setgid, propriétaire rwx (7), groupe r-x (5), autres --- (0).", "Example: 2750 = setgid, owner rwx (7), group r-x (5), others --- (0)."),
                [
                    new("7", "rwx", "4 + 2 + 1"),
                    new("6", "rw-", "4 + 2"),
                    new("5", "r-x", "4 + 1"),
                    new("4", "r--", "4"),
                    new("0", "---", T("aucun droit", "no right"))
                ]),
                new(T("Lignes de getfacl", "getfacl lines"), null,
                [
                    new("user::", T("Propriétaire", "Owner"), T("Les droits du propriétaire ; jamais limités par le masque.", "The owner's rights; never limited by the mask.")),
                    new(T("user:nom:", "user:name:"), T("Utilisateur nommé", "Named user"), T("Un compte précis ; limité par le masque.", "A given account; limited by the mask.")),
                    new("group::", T("Groupe propriétaire", "Owning group"), T("Limité par le masque.", "Limited by the mask.")),
                    new(T("group:nom:", "group:name:"), T("Groupe nommé", "Named group"), T("Un groupe précis ; limité par le masque. Il faut qu'une entrée de groupe suffise seule.", "A given group; limited by the mask. One group entry must be enough on its own.")),
                    new("mask::", T("Masque", "Mask"), T("Le plus que peuvent donner les entrées nommées et le groupe propriétaire ; setfacl le recalcule (l'union) à chaque modification.", "The most named entries and the owning group can give; setfacl recalculates it (the union) on each change.")),
                    new("other::", T("Autres", "Others"), T("Ceux qui ne sont ni le propriétaire, ni nommés, ni membres d'un groupe qui a une entrée.", "Those who are not the owner, not named, and not members of a group that has an entry.")),
                    new("#effective:", T("Effectif", "Effective"), T("Ce qui reste d'une entrée une fois le masque appliqué.", "What remains of an entry once the mask is applied.")),
                    new("default:", T("ACL par défaut", "Default ACL"), T("Sur un dossier : l'ACL que reçoit ce qui y est créé ; l'umask ne compte plus.", "On a directory: the ACL that what is created in it receives; the umask no longer counts."))
                ]),
                new("umask", T("Les droits qu'une session retire à ce qu'elle crée (666 pour un fichier, 777 pour un dossier).", "The rights a session takes away from what it creates (666 for a file, 777 for a directory)."),
                [
                    new("022", T("Courant", "Common"), T("Fichier 644 (rw-r--r--), dossier 755 (rwxr-xr-x).", "File 644 (rw-r--r--), directory 755 (rwxr-xr-x).")),
                    new("027", T("Prudent", "Careful"), T("Fichier 640 (rw-r-----), dossier 750 (rwxr-x---).", "File 640 (rw-r-----), directory 750 (rwxr-x---).")),
                    new("077", T("Privé", "Private"), T("Fichier 600 (rw-------), dossier 700 (rwx------).", "File 600 (rw-------), directory 700 (rwx------)."))
                ]),
                .. LinuxExamples(culture)
            ];
        }

        private static string Text(string culture, string fr, string en) => culture.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? en : fr;
    }
}
