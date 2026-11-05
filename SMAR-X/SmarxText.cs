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
            ["App.Subtitle"] = ("Simulateur des droits Linux", "Linux permissions simulator"),
            ["Button.Open"] = ("Ouvrir…", "Open…"),
            ["Button.Save"] = ("Enregistrer…", "Save…"),
            ["Button.Sample"] = ("Serveur d'exemple", "Sample server"),
            ["Button.Language"] = ("English", "Français"),
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

            ["Accounts.Title"] = ("Comptes", "Accounts"),
            ["Legend"] = ("r : lire (un dossier : lister)   ·   w : modifier (un dossier : créer, renommer, supprimer dedans)   ·   x : exécuter (un dossier : le traverser)",
                          "r: read (a directory: list)   ·   w: write (a directory: create, rename, delete inside)   ·   x: execute (a directory: traverse it)"),
        };
    }
}
