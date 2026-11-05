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
        private static SmarxText? _instance;

        // Créé à la première demande : les textes (plus bas) sont alors initialisés
        public static SmarxText Instance => _instance ??= new SmarxText();

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
            ["Rights.DefaultAcl"] = ("ACL par défaut (reçue par ce qui est créé ici)", "Default ACL (given to what is created here)"),
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

            ["Accounts.Title"] = ("Comptes", "Accounts"),
            ["Accounts.Hint"] = ("Choisissez un compte pour modifier ses groupes.", "Pick an account to change its groups."),
            ["Accounts.Primary"] = ("Groupe principal", "Primary group"),
            ["Accounts.Groups"] = ("Membre de", "Member of"),
            ["Accounts.NewUser"] = ("nouveau compte", "new account"),
            ["Accounts.AddUser"] = ("+ compte", "+ account"),
            ["Accounts.NewGroup"] = ("nouveau groupe", "new group"),
            ["Accounts.AddGroup"] = ("+ groupe", "+ group"),
            ["Legend.Windows"] = ("(I) héritée   ·   (OI) les fichiers en héritent   ·   (CI) les sous-dossiers en héritent   ·   (IO) héritage seulement   ·   un refus ne retire pas un droit déjà accordé",
                                  "(I) inherited   ·   (OI) files inherit   ·   (CI) subfolders inherit   ·   (IO) inherit only   ·   a deny does not take back a right already granted"),
            ["Legend"] = ("r : lire (un dossier : lister)   ·   w : modifier (un dossier : créer, renommer, supprimer dedans)   ·   x : exécuter (un dossier : le traverser)",
                          "r: read (a directory: list)   ·   w: write (a directory: create, rename, delete inside)   ·   x: execute (a directory: traverse it)"),
        };
    }
}
