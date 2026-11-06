using System;
using System.Collections.Generic;
using System.Globalization;
using SMADX.Services;

namespace SMARX
{
    /// <summary>
    /// Textes de l'interface de SMAR-X, en français et en anglais, sur la traduction du socle commun. Les vues s'y
    /// lient par indexeur (<c>{Binding [Tester.Who], Source={StaticResource T}}</c>).
    /// </summary>
    public sealed class SmarxText : Localizer
    {
        // Créé à la première demande, une seule fois même si deux fils le demandent ensemble : les textes (plus bas)
        // sont alors initialisés. (« ??= » pouvait créer une seconde instance, à la langue du système, qui remplaçait la
        // première.)
        private static readonly Lazy<SmarxText> Shared = new(() => new SmarxText());

        public static SmarxText Instance => Shared.Value;

        private SmarxText()
        {
            var fr = new Dictionary<string, string>();
            var en = new Dictionary<string, string>();
            foreach (var (key, (f, e)) in Texts)
            {
                fr[key] = f;
                en[key] = e;
            }
            AddTranslations("fr-FR", fr);
            AddTranslations("en-US", en);
            CurrentCulture = CultureInfo.GetCultureInfo(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "fr" ? "fr-FR" : "en-US");
        }

        /// <summary>« fr-FR » ou « en-US » : la langue des explications du moteur.</summary>
        public string Culture => CurrentCulture.Name;

        public string T(string fr, string en) => Culture == "fr-FR" ? fr : en;

        private static readonly Dictionary<string, (string Fr, string En)> Texts = new()
        {
            ["App.Title"] = ("SMAR-X — qui peut faire quoi ?", "SMAR-X — who can do what?"),
            ["App.Subtitle"] = ("Simulateur des droits Linux et Windows", "Linux and Windows permissions simulator"),
            ["Button.Open"] = ("Ouvrir…", "Open…"),
            ["Button.Save"] = ("Enregistrer…", "Save…"),
            ["Button.Labs"] = ("Exercices", "Labs"),
            ["Lab.OpenFile"] = ("Ouvrir un fichier d'exercice (.smarx-lab.json)…", "Open a lab file (.smarx-lab.json)…"),
            ["Lab.Show"] = ("Afficher l'exercice en cours", "Show the current lab"),
            ["Lab.Leave"] = ("Quitter l'exercice", "Leave the lab"),
            ["Lab.TrainerCopy.Title"] = ("Exemplaire du formateur", "Trainer's copy"),
            ["Lab.TrainerCopy"] = ("Ce fichier est l'exemplaire du formateur d'un examen : il contient le corrigé. Ouvrez le sujet distribué par votre formateur.",
                                   "This file is the trainer's copy of an exam: it holds the answer key. Open the subject handed out by your trainer."),
            ["Lab.Objectives"] = ("Objectifs", "Objectives"),
            ["Lab.Questions"] = ("Questions", "Questions"),
            ["Lab.Check"] = ("Vérifier", "Check"),
            ["Lab.Restart"] = ("Recommencer", "Restart"),
            ["Lab.Save"] = ("Enregistrer ma copie…", "Save my copy…"),
            ["Lab.Student"] = ("Nom de l'apprenant", "Learner's name"),
            ["Lab.StudentId"] = ("Identifiant", "ID"),
            ["Lab.NeedName"] = ("Indiquez votre nom pour enregistrer votre copie.", "Enter your name to save your copy."),
            ["Lab.Hint"] = ("Indice", "Hint"),
            ["Lab.Guard"] = ("garde", "guard"),
            ["Lab.GuardTip"] = ("Une garde ne rapporte rien : elle doit rester respectée, sinon les objectifs qu'elle protège sont annulés.",
                                "A guard earns nothing: it must stay met, otherwise the objectives it protects are cancelled."),
            ["Lab.Points"] = (" pt", " pt"),
            ["Lab.Completed"] = ("Exercice réussi !", "Lab completed!"),
            ["Lab.Answer"] = ("Votre réponse", "Your answer"),
            ["Button.Sample"] = ("Serveur d'exemple", "Sample server"),
            ["Button.Language"] = ("English", "Français"),
            ["Button.Glossary"] = ("Aide-mémoire", "Cheat sheet"),
            ["Glossary.Title"] = ("SMAR-X — aide-mémoire des abréviations", "SMAR-X — abbreviations cheat sheet"),
            ["Glossary.LinkLinux"] = ("rwx, s, t, +, mask… ?", "rwx, s, t, +, mask…?"),
            ["Glossary.LinkWindows"] = ("F, M, RD, WD, (OI), (CI)… ?", "F, M, RD, WD, (OI), (CI)…?"),
            ["Button.Theme"] = ("Clair / sombre", "Light / dark"),

            ["Tree.Title"] = ("Fichiers du serveur", "Server files"),
            ["Tree.Hint"] = ("Choisissez un fichier ou un dossier.", "Pick a file or a directory."),

            ["Tester.Title"] = ("Tester un accès", "Test an access"),
            ["Tester.Who"] = ("Qui ?", "Who?"),
            ["Tester.What"] = ("Fait quoi ?", "Does what?"),
            ["Tester.On"] = ("Sur quoi ?", "On what?"),
            ["Tester.Allowed"] = ("Autorisé", "Allowed"),
            ["Tester.Denied"] = ("Refusé", "Denied"),
            ["Tester.Path"] = ("Le chemin parcouru", "The path walked"),
            ["Tester.From"] = ("D'où ?", "From where?"),
            ["Tester.Steps"] = ("Ce que vérifie le noyau, dans l'ordre", "What the kernel checks, in order"),
            ["Tester.Local"] = ("Accès local : session ouverte sur le serveur (console, SSH).", "Local access: session open on the server (console, SSH)."),

            ["Tester.LocalWindows"] = ("Accès local : session ouverte sur le serveur (console, RDP).", "Local access: session open on the server (console, RDP)."),
            ["Tester.Elevated"] = ("Session élevée (« Exécuter en tant qu'administrateur ») : sinon, l'UAC laisse Administrateurs en refus seulement",
                                   "Elevated session (\"Run as administrator\"): otherwise UAC keeps Administrators deny-only"),
            ["Tester.StepsWindows"] = ("Ce que vérifie Windows (AccessCheck), dans l'ordre", "What Windows checks (AccessCheck), in order"),

            ["Matrix.Title"] = ("Matrice d'accès", "Access matrix"),
            ["Matrix.Hint"] = ("Chaque compte, chaque opération. Cliquez une case pour voir pourquoi.", "Every account, every operation. Click a cell to see why."),
            ["Matrix.Account"] = ("Compte", "Account"),

            ["Rights.Title"] = ("Droits", "Permissions"),
            ["Rights.Owner"] = ("Propriétaire", "Owner"),
            ["Rights.Group"] = ("Groupe", "Group"),
            ["Rights.Mask"] = ("Masque", "Mask"),
            ["Rights.Other"] = ("Autres", "Others"),
            ["Rights.Class"] = ("Qui", "Who"),
            ["Rights.Special"] = ("Droits spéciaux", "Special bits"),
            ["Rights.SetUid"] = ("setuid : le programme s'exécute sous l'identité de son propriétaire", "setuid: the program runs as its owner"),
            ["Rights.SetGid"] = ("setgid : ce qui est créé dans le dossier prend son groupe", "setgid: what is created in the directory gets its group"),
            ["Rights.Sticky"] = ("sticky : seuls les propriétaires suppriment leurs fichiers", "sticky: only owners delete their files"),
            ["Rights.Acl"] = ("ACL POSIX", "POSIX ACL"),
            ["Rights.AclHint"] = ("Comme setfacl, modifier une entrée recalcule le masque ; le masque limite les entrées nommées et le groupe propriétaire.",
                                  "Like setfacl, changing an entry recalculates the mask; the mask limits named entries and the owning group."),
            ["Rights.OwningGroup"] = ("Groupe propriétaire", "Owning group"),
            ["Rights.NoAcl"] = ("Pas d'ACL étendue : seuls le propriétaire, le groupe et les autres du mode comptent. Ajoutez un utilisateur ou un groupe pour en créer une.",
                                "No extended ACL: only the mode's owner, group and others count. Add a user or a group to create one."),
            ["Rights.AddUser"] = ("+ utilisateur", "+ user"),
            ["Rights.AddGroup"] = ("+ groupe", "+ group"),
            ["Rights.RemoveAcl"] = ("Supprimer l'ACL (setfacl -b)", "Remove the ACL (setfacl -b)"),
            ["Rights.Remove"] = ("Retirer", "Remove"),
            ["Default.Title"] = ("ACL par défaut", "Default ACL"),
            ["Default.Hint"] = ("Reçue par ce qui sera créé dans ce dossier (setfacl -d). Elle ne change pas l'accès au dossier lui-même.",
                                "Given to what will be created in this directory (setfacl -d). It does not change access to the directory itself."),
            ["Default.None"] = ("Pas d'ACL par défaut : ce qui est créé ici reçoit le mode demandé, moins l'umask du compte. Ajouter une entrée la crée, avec une copie des droits du propriétaire, du groupe et des autres de ce dossier.",
                                "No default ACL: what is created here gets the requested mode, minus the account's umask. Adding an entry creates it, with a copy of this directory's owner, group and others permissions."),
            ["Default.Mask"] = ("Masque, recalculé à chaque modification : l'union de group:: et des entrées nommées. Il reste après le retrait de la dernière entrée nommée.",
                                "Mask, recalculated on each change: the union of group:: and the named entries. It stays after the last named entry is removed."),
            ["Default.Remove"] = ("Supprimer l'ACL par défaut (setfacl -k)", "Remove the default ACL (setfacl -k)"),
            ["Rights.Commands"] = ("Commandes équivalentes", "Equivalent commands"),

            ["Create.Title"] = ("Ce que recevrait un nouvel élément", "What a new item would get"),
            ["Create.TitleWindows"] = ("Ce que recevrait un nouveau fichier créé ici", "What a new file created here would get"),

            ["Ntfs.Title"] = ("Autorisations NTFS", "NTFS permissions"),
            ["Ntfs.InheritanceOn"] = ("Héritage activé : les ACE du dossier parent s'ajoutent après les ACE explicites.",
                                      "Inheritance enabled: the parent folder's ACEs come after the explicit ACEs."),
            ["Ntfs.InheritanceOff"] = ("Héritage désactivé : seules les ACE explicites comptent.", "Inheritance disabled: only the explicit ACEs count."),
            ["Ntfs.DisableCopy"] = ("Désactiver, en copiant les héritées", "Disable, copying the inherited ones"),
            ["Ntfs.DisableRemove"] = ("Désactiver, en retirant les héritées", "Disable, removing the inherited ones"),
            ["Ntfs.Enable"] = ("Réactiver l'héritage", "Enable inheritance"),
            ["Ntfs.Dacl"] = ("Liste des ACE, dans l'ordre où Windows les lit", "ACE list, in the order Windows reads them"),
            ["Ntfs.Deny"] = ("Refuser", "Deny"),
            ["Ntfs.Add"] = ("Ajouter une ACE", "Add an ACE"),
            ["Ntfs.AddButton"] = ("Ajouter", "Add"),

            ["Sec.ObjectName"] = ("Nom de l'objet :", "Object name:"),
            ["Sec.Names"] = ("Noms de groupes ou d'utilisateurs :", "Group or user names:"),
            ["Sec.ToChange"] = ("Pour modifier les autorisations, cliquez sur Modifier.", "To change permissions, click Edit."),
            ["Sec.Edit"] = ("Modifier…", "Edit…"),
            ["Sec.Allow"] = ("Autoriser", "Allow"),
            ["Sec.Deny"] = ("Refuser", "Deny"),
            ["Sec.ForAdvanced"] = ("Pour les autorisations spéciales et les paramètres avancés, cliquez sur Avancé.",
                                   "For special permissions or advanced settings, click Advanced."),
            ["Sec.Advanced"] = ("Avancé", "Advanced"),
            ["Sec.Legend"] = ("✓ noire : posée sur cet objet   ·   ✓ grise : héritée, elle se change sur le dossier parent   ·   Autorisations spéciales : toujours grise",
                              "black ✓: set on this object   ·   gray ✓: inherited, change it on the parent folder   ·   Special permissions: always gray"),
            ["Sec.Add"] = ("Ajouter", "Add"),
            ["Sec.Remove"] = ("Supprimer", "Remove"),
            ["Sec.Equivalent"] = ("La même chose avec icacls", "The same with icacls"),
            ["Sec.Ok"] = ("OK", "OK"),
            ["Sec.Cancel"] = ("Annuler", "Cancel"),
            ["Sec.Name"] = ("Nom :", "Name:"),
            ["Sec.Owner"] = ("Propriétaire :", "Owner:"),
            ["Sec.Permissions"] = ("Autorisations", "Permissions"),
            ["Sec.Effective"] = ("Accès effectif", "Effective Access"),
            ["Sec.AdvancedHint"] = ("Chaque entrée réunit les ACE d'un même compte, de même type, mêmes droits et même origine. La liste des ACE (Autorisations NTFS) les montre une par une, dans l'ordre où Windows les lit.",
                                    "Each entry gathers the ACEs of one account with the same type, rights and origin. The ACE list (NTFS permissions) shows them one by one, in the order Windows reads them."),
            ["Sec.Entries"] = ("Entrées d'autorisations :", "Permission entries:"),
            ["Sec.Type"] = ("Type", "Type"),
            ["Sec.Principal"] = ("Principal", "Principal"),
            ["Sec.Access"] = ("Accès", "Access"),
            ["Sec.InheritedFrom"] = ("Hérité de", "Inherited from"),
            ["Sec.AppliesTo"] = ("S'applique à", "Applies to"),
            ["Sec.Disable"] = ("Désactiver l'héritage", "Disable inheritance"),
            ["Sec.Enable"] = ("Activer l'héritage", "Enable inheritance"),
            ["Sec.Question"] = ("Que voulez-vous faire avec les autorisations héritées actuelles ?", "What would you like to do with the current inherited permissions?"),
            ["Sec.Convert"] = ("Convertir les autorisations héritées en autorisations explicites sur cet objet",
                               "Convert inherited permissions into explicit permissions on this object"),
            ["Sec.RemoveAll"] = ("Supprimer toutes les autorisations héritées de cet objet", "Remove all inherited permissions from this object"),
            ["Sec.EffectiveUser"] = ("Utilisateur :", "User:"),
            ["Sec.Elevated"] = ("Session élevée (UAC)", "Elevated session (UAC)"),
            ["Sec.EffectiveHint"] = ("Ce que le compte obtient en ouvrant l'objet sur le serveur : AccessCheck sur sa DACL, et ce que NTFS accorde par le dossier parent.",
                                     "What the account gets when it opens the object on the server: AccessCheck on its DACL, and what NTFS grants through the parent folder."),

            ["Share.Hint"] = ("Partager le dossier, choisir ses autorisations de partage.", "Share the folder, choose its share permissions."),
            ["Share.Advanced"] = ("Partage avancé…", "Advanced Sharing…"),
            ["Share.ShareThis"] = ("Partager ce dossier", "Share this folder"),
            ["Share.Names"] = ("Nom du partage :", "Share name:"),
            ["Share.NewName"] = ("nouveau nom", "new name"),
            ["Share.Comment"] = ("Commentaires :", "Comments:"),
            ["Share.Abe"] = ("Énumération basée sur l'accès : ne montrer à chacun que ce qu'il peut lire (Gestionnaire de serveur, ou Set-SmbShare -FolderEnumerationMode AccessBased)",
                             "Access-based enumeration: show each user only what they can read (Server Manager, or Set-SmbShare -FolderEnumerationMode AccessBased)"),
            ["Share.Permissions"] = ("Autorisations du partage", "Share permissions"),
            ["Share.Rule"] = ("Par le réseau, il faut que le partage ET NTFS accordent : le plus restrictif des deux l'emporte. Microsoft conseille un partage large (Utilisateurs authentifiés : Modifier) et le tri par NTFS.",
                              "Over the network, the share AND NTFS must both grant: the more restrictive of the two wins. Microsoft advises a broad share (Authenticated Users: Change) and sorting with NTFS."),
            ["Share.PowerShell"] = ("La même chose en PowerShell", "The same in PowerShell"),

            ["Samba.Hint"] = ("Partager le dossier avec Samba : une section de smb.conf.", "Share the directory with Samba: a section of smb.conf."),
            ["Samba.Button"] = ("Partage Samba…", "Samba sharing…"),
            ["Samba.ReadOnly"] = ("personne n'écrit, sauf la write list (le défaut de Samba)", "nobody writes, except the write list (Samba's default)"),
            ["Samba.Users"] = ("alice @compta", "alice @compta"),
            ["Samba.Hide"] = ("une liste ne montre pas ce que le compte ne peut pas lire", "a listing does not show what the account cannot read"),
            ["Samba.Browseable"] = ("le partage apparaît dans la liste des partages du serveur", "the share appears in the server's list of shares"),
            ["Samba.Rule"] = ("Samba décide d'abord (valid users, read only, write list), puis le noyau, sous l'identité Unix du compte (ou force user, force group) : write list ne donne aucun droit Unix. Un « @ » désigne un groupe.",
                              "Samba decides first (valid users, read only, write list), then the kernel, under the account's Unix identity (or force user, force group): the write list gives no Unix right. An \"@\" names a group."),
            ["Samba.Config"] = ("La section de smb.conf", "The smb.conf section"),

            ["Accounts.Title"] = ("Comptes", "Accounts"),
            ["Accounts.Hint"] = ("Choisissez un compte pour modifier ses groupes.", "Pick an account to change its groups."),
            ["Accounts.Primary"] = ("Groupe principal", "Primary group"),
            ["Accounts.Groups"] = ("Membre de", "Member of"),
            ["Accounts.NewUser"] = ("nouveau compte", "new account"),
            ["Accounts.AddUser"] = ("+ compte", "+ account"),
            ["Accounts.NewGroup"] = ("nouveau groupe", "new group"),
            ["Accounts.AddGroup"] = ("+ groupe", "+ group"),
            ["Whoami.Open"] = ("whoami de ce compte…", "whoami of this account…"),
            ["Whoami.Account"] = ("Compte", "Account"),
            ["Whoami.Local"] = ("Session sur le serveur", "Session on the server"),
            ["Whoami.Network"] = ("Session réseau (par un partage)", "Network session (through a share)"),
            ["Whoami.Elevated"] = ("Session élevée", "Elevated session"),
            ["Whoami.Relogon"] = ("Fermer et rouvrir la session", "Sign out and in again"),
            ["Whoami.Name"] = ("Nom du groupe", "Group name"),
            ["Whoami.Attributes"] = ("Attributs", "Attributes"),
            ["Whoami.Why"] = ("Pourquoi", "Why"),
            ["Whoami.SessionText"] = ("whoami /user /groups, dans la session ouverte", "whoami /user /groups, in the open session"),
            ["Whoami.Note"] = ("Le texte est celui d'un Windows en anglais, comme les machines du labo. Un vrai jeton porte quelques identités de plus (LOCAL, CONSOLE LOGON, This Organization, Domain Users…) : elles ne changent rien aux accès que simule SMAR-X, qui ne les montre pas.",
                               "The text is that of an English Windows, like the lab's computers. A real token holds a few more identities (LOCAL, CONSOLE LOGON, This Organization, Domain Users…): they change nothing to the accesses SMAR-X simulates, which does not show them."),
            ["Accounts.HintWindows"] = ("Choisissez un compte : son whoami, ou « Comptes et groupes… » pour ses groupes. Cette fenêtre, comme la console Active Directory, crée les comptes et les groupes et change leurs membres.",
                                        "Pick an account: its whoami, or \u201cAccounts and groups\u2026\u201d for its groups. That window, like the Active Directory console, creates accounts and groups and changes their members."),
            ["Accounts.Directory"] = ("Comptes et groupes…", "Accounts and groups…"),
            ["Directory.TreeTitle"] = ("Comptes et groupes", "Accounts and groups"),
            ["Directory.TreeHint"] = ("AGDLP : les comptes dans des groupes globaux (G_), les groupes globaux dans des groupes de domaine local (DL_), et les autorisations aux seuls DL_.",
                                      "AGDLP: accounts in global groups (G_), global groups in domain local groups (DL_), and permissions to DL_ groups only."),
            ["Directory.Pick"] = ("Choisissez un compte ou un groupe dans l'arbre.", "Pick an account or a group in the tree."),
            ["Directory.MemberOf"] = ("Membre de", "Member Of"),
            ["Directory.MemberOfHint"] = ("Les groupes dont l'objet est membre directement. Ajouter… propose les groupes ; ceux qu'Active Directory refuserait sont grisés, avec leur raison.",
                                          "The groups the object is a direct member of. Add\u2026 offers the groups; those Active Directory would refuse are greyed out, with their reason."),
            ["Directory.Members"] = ("Membres", "Members"),
            ["Directory.MembersHint"] = ("Les comptes et les groupes membres directs du groupe.", "The accounts and groups that are direct members of the group."),
            ["Directory.NoMembers"] = ("Un compte n'a pas de membres : choisissez un groupe pour voir et changer les siens.",
                                       "An account has no members: pick a group to see and change its members."),
            ["Directory.Nesting"] = ("Imbrication", "Nesting"),
            ["Directory.NestingHint"] = ("L'objet, ses groupes, puis les groupes de ces groupes : chaque chemin mène à un groupe que le jeton recevra. whoami aplatit cet arbre en une liste.",
                                         "The object, its groups, then the groups of those groups: each path leads to a group the token will receive. whoami flattens this tree into a list."),
            ["Directory.Paths"] = ("Chemins", "Paths"),
            ["Directory.Nested"] = ("Reçus par imbrication", "Received through nesting"),
            ["Directory.Commands"] = ("PowerShell (sur un vrai serveur)", "PowerShell (on a real server)"),
            ["Directory.New"] = ("Nouveau", "New"),
            ["Directory.NewName"] = ("nom du compte ou du groupe", "account or group name"),
            ["Directory.Create"] = ("Créer", "Create"),
            ["Directory.Add"] = ("Ajouter…", "Add…"),
            ["Directory.Remove"] = ("Supprimer", "Remove"),
            ["Directory.Close"] = ("Fermer", "Close"),
            ["Picker.Title"] = ("Sélectionner des utilisateurs ou des groupes", "Select users or groups"),
            ["Picker.Hint"] = ("Cochez ceux à ajouter. Une ligne grisée est refusée par Active Directory ; sa raison est dessous.",
                               "Tick those to add. A greyed line is refused by Active Directory; its reason is below it."),
            ["Picker.Filter"] = ("Filtrer par nom…", "Filter by name…"),
            ["Legend.Windows"] = ("(I) héritée   ·   (OI) les fichiers en héritent   ·   (CI) les sous-dossiers en héritent   ·   (IO) héritage seulement   ·   un refus ne retire pas un droit déjà accordé",
                                  "(I) inherited   ·   (OI) files inherit   ·   (CI) subfolders inherit   ·   (IO) inherit only   ·   a deny does not take back a right already granted"),
            ["Legend"] = ("r : lire (un dossier : lister)   ·   w : modifier (un dossier : créer, renommer, supprimer dedans)   ·   x : exécuter (un dossier : le traverser)",
                          "r: read (a directory: list)   ·   w: write (a directory: create, rename, delete inside)   ·   x: execute (a directory: traverse it)"),
        };
    }
}
