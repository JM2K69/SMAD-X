using System.Globalization;
using SMADX.Services;

namespace SMAGX;

/// <summary>
/// Textes de l'interface de SMAG-X, en français et en anglais, sur la traduction du socle commun. Les vues les lisent
/// par ressource ({DynamicResource Clé}), réécrites à chaque changement de langue (voir App).
/// Les sorties de Git, elles, restent en anglais, comme le vrai Git par défaut.
/// </summary>
public sealed class SmagxText : Localizer
{
    private static readonly Lazy<SmagxText> Shared = new(() => new SmagxText());

    public static SmagxText Instance => Shared.Value;

    private SmagxText()
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

    /// <summary>« fr-FR » ou « en-US ».</summary>
    public string Culture => CurrentCulture.Name;

    public bool French => Culture == "fr-FR";

    public string T(string fr, string en) => French ? fr : en;

    private static readonly Dictionary<string, (string Fr, string En)> Texts = new()
    {
        ["App.Title"] = ("SMAG-X : apprendre Git", "SMAG-X: learn Git"),
        ["App.Subtitle"] = ("Simulateur Git : terminal, graphe et trois zones", "Git simulator: terminal, graph and three areas"),
        ["Button.Identity"] = ("Identité…", "Identity…"),
        ["Button.Collaborate"] = ("Travail à deux…", "Pair work…"),
        ["Button.Scenarios"] = ("Scénarios…", "Scenarios…"),
        ["Scenario.PickerTitle"] = ("Scénarios : le parcours", "Scenarios: the path"),
        ["Scenario.Solo"] = ("Seul", "Alone"),
        ["Scenario.Pair"] = ("À deux", "By two"),
        ["Scenario.Start"] = ("Commencer", "Start"),
        ["Scenario.SoloNote"] = ("Un poste neuf, sous votre identité : vos postes actuels et la forge repartent à zéro.",
            "A new workstation, under your identity: your current workstations and the forge start from scratch."),
        ["Scenario.Where"] = ("Où le jouer ?", "Where to play it?"),
        ["Scenario.Objectives"] = ("Objectifs", "Objectives"),
        ["Scenario.Check"] = ("Vérifier", "Check"),
        ["Scenario.Restart"] = ("Recommencer", "Restart"),
        ["Scenario.Quit"] = ("Quitter le scénario", "Leave the scenario"),
        ["Scenario.Hint"] = ("Indice", "Hint"),
        ["Scenario.Guard"] = ("garde", "guard"),
        ["Scenario.Completed"] = ("Bravo, tous les objectifs sont atteints !", "Well done, every objective is reached!"),
        ["Stations.Add"] = ("Ajouter un poste", "Add a workstation"),
        ["Stations.Remove"] = ("Retirer ce poste", "Remove this workstation"),
        ["Stations.Hint"] = ("Un autre poste sur la même forge : un camarade à côté de vous, ou vous dans un second rôle.",
            "Another workstation on the same forge: a classmate next to you, or yourself in a second role."),
        ["Collab.Title"] = ("Travail à deux", "Pair work"),
        ["Collab.Intro"] = ("Deux élèves, une même forge. Sur un seul ordinateur, ajoutez un poste et jouez les deux rôles. Sur deux ordinateurs de la salle, l'un sert sa forge, l'autre la rejoint : chacun garde ses postes, la forge est commune.",
            "Two students, one forge. On a single computer, add a workstation and play both roles. On two computers in the room, one serves its forge and the other joins it: each keeps its workstations, the forge is shared."),
        ["Collab.ServeTitle"] = ("Servir ma forge", "Serve my forge"),
        ["Collab.ServeIntro"] = ("Votre camarade se connecte à ce SMAG-X : vos projets deviennent les siens. Donnez-lui l'adresse et le code, et gardez SMAG-X ouvert. Si Windows demande l'accès au réseau, autorisez les réseaux privés.",
            "Your classmate connects to this SMAG-X: your projects become theirs too. Give them the address and the code, and keep SMAG-X open. If Windows asks for network access, allow private networks."),
        ["Collab.Start"] = ("Démarrer le serveur", "Start the server"),
        ["Collab.Stop"] = ("Arrêter le serveur", "Stop the server"),
        ["Collab.Address"] = ("Adresse de cet ordinateur", "This computer's address"),
        ["Collab.Code"] = ("Code", "Code"),
        ["Collab.Others"] = ("Autres adresses de ce poste, si celle-ci ne répond pas : ", "Other addresses of this computer, if this one does not answer: "),
        ["Collab.JoinTitle"] = ("Rejoindre un camarade", "Join a classmate"),
        ["Collab.JoinIntro"] = ("Votre camarade a démarré le serveur : tapez l'adresse et le code affichés chez lui. Vos postes utiliseront sa forge.",
            "Your classmate has started the server: type the address and the code shown on their screen. Your workstations will use their forge."),
        ["Collab.JoinAddress"] = ("Adresse du camarade", "Classmate's address"),
        ["Collab.Join"] = ("Se connecter", "Connect"),
        ["Collab.Leave"] = ("Revenir à ma forge", "Back to my forge"),
        ["Collab.Close"] = ("Fermer", "Close"),
        ["Forge.Unreachable"] = ("La forge du camarade ne répond pas : son SMAG-X est-il ouvert, son serveur démarré ?",
            "The classmate's forge does not answer: is their SMAG-X open, their server started?"),
        ["Button.NewSession"] = ("Nouvelle session", "New session"),
        ["Button.Language"] = ("English", "Français"),
        ["Button.Theme"] = ("Thème", "Theme"),
        ["Terminal.Title"] = ("Terminal", "Terminal"),
        ["Terminal.Hint"] = ("Tapez une commande (git, cd, ls, cat, echo, mkdir…), puis Entrée. Flèches haut et bas : commandes précédentes. Ctrl+L : effacer.",
            "Type a command (git, cd, ls, cat, echo, mkdir…), then Enter. Up and down arrows: previous commands. Ctrl+L: clear."),
        ["Graph.Title"] = ("Historique", "History"),
        ["Graph.Empty"] = ("Pas encore de commit : l'historique s'affichera ici.", "No commits yet: the history will show here."),
        ["Graph.NoRepository"] = ("Pas de dépôt dans ce dossier : « git init » en crée un.", "No repository in this folder: \"git init\" creates one."),
        ["Zones.Work"] = ("Répertoire de travail", "Working directory"),
        ["Zones.Index"] = ("Index (zone de préparation)", "Index (staging area)"),
        ["Zones.Repository"] = ("Dépôt local", "Local repository"),
        ["Zones.Clean"] = ("Rien à signaler", "Nothing to report"),
        ["Zones.Empty"] = ("Vide", "Empty"),
        ["Files.Title"] = ("Fichiers", "Files"),
        ["Files.New"] = ("Nouveau fichier", "New file"),
        ["Files.NewName"] = ("nom-du-fichier.txt", "file-name.txt"),
        ["Files.Save"] = ("Enregistrer", "Save"),
        ["Files.Delete"] = ("Supprimer", "Delete"),
        ["Files.None"] = ("Choisissez un fichier pour le modifier.", "Pick a file to edit it."),
        ["Files.EmptyFolder"] = ("vide", "empty"),
        ["Files.Ignored"] = ("ignoré", "ignored"),
        ["Files.IgnoredTip"] = ("Ignoré par un .gitignore : git status ne le montre pas, et « git add . » ne l'ajoute pas.",
            "Ignored by a .gitignore: git status does not show it, and \"git add .\" does not add it."),
        ["Files.EmptyFolderTip"] = ("Git ne suit pas les dossiers vides : celui-ci n'apparaîtra dans git status qu'avec un fichier dedans.",
            "Git does not track empty folders: this one will only show in git status with a file inside."),
        ["Explain.Title"] = ("Ce qui vient de se passer", "What just happened"),
        ["Forge.Title"] = ("Forge", "Forge"),
        ["Forge.Intro"] = ("Les projets partagés, comme sur GitLab : créez-en un, puis clonez-le avec son adresse SSH. La forge vous reconnaît à votre clé SSH.",
            "Shared projects, as on GitLab: create one, then clone it with its SSH address. The forge recognises you by your SSH key."),
        ["Forge.IntroGitHub"] = ("Les dépôts partagés, comme sur GitHub : créez-en un, puis clonez-le avec son adresse SSH. GitHub vous reconnaît à votre clé SSH.",
            "Shared repositories, as on GitHub: create one, then clone it with its SSH address. GitHub recognises you by your SSH key."),
        ["Forge.Keys"] = ("Clés SSH…", "SSH keys…"),
        ["Keys.Intro"] = ("Collez ici votre clé publique : la ligne entière du fichier .pub (cat ~/.ssh/id_ed25519.pub). La forge vous reconnaîtra à cette clé. La clé privée, elle, ne quitte jamais le poste.",
            "Paste your public key here: the whole line of the .pub file (cat ~/.ssh/id_ed25519.pub). The forge will recognise you by this key. The private key never leaves the workstation."),
        ["Keys.None"] = ("Aucune clé pour l'instant : la forge refuse les connexions SSH de ce compte (« Permission denied (publickey) »).",
            "No key yet: the forge refuses SSH connections for this account (\"Permission denied (publickey)\")."),
        ["Keys.Key"] = ("Clé publique", "Public key"),
        ["Keys.TitleLabel"] = ("Titre (facultatif)", "Title (optional)"),
        ["Keys.TitleHint"] = ("Poste du lycée (sinon, le commentaire de la clé)", "School workstation (otherwise, the key comment)"),
        ["Keys.Add"] = ("Ajouter la clé", "Add the key"),
        ["Keys.Remove"] = ("Supprimer", "Delete"),
        ["Keys.Private"] = ("C'est une clé privée ! Elle ne doit jamais quitter votre poste. Collez la clé publique, celle du fichier .pub.",
            "This is a private key! It must never leave your workstation. Paste the public key, the one in the .pub file."),
        ["Keys.Invalid"] = ("Ce n'est pas une clé publique SSH : collez la ligne entière du fichier .pub (ssh-ed25519 AAAA… commentaire).",
            "This is not an SSH public key: paste the whole line of the .pub file (ssh-ed25519 AAAA… comment)."),
        ["Keys.Taken"] = ("Cette clé est déjà enregistrée sur la forge (sur ce compte ou un autre) : une clé ne désigne qu'un seul compte.",
            "This key is already registered on the forge (on this account or another one): a key points to a single account."),
        ["Forge.NewName"] = ("classe/projet", "class/project"),
        ["Forge.New"] = ("Nouveau projet", "New project"),
        ["Forge.Empty"] = ("Aucun projet pour l'instant.", "No project yet."),
        ["Explain.Start"] = ("Commencez par vous présenter à Git (git config), puis créez un dépôt avec « git init ».",
            "Start by introducing yourself to Git (git config), then create a repository with \"git init\"."),
        ["Identity.Title"] = ("Votre identité", "Your identity"),
        ["Identity.Intro"] = ("C'est votre compte sur le poste ; elle servira aussi à votre compte sur la forge et à votre clé SSH. Git, lui, ne la connaît pas encore : à vous de la lui donner.",
            "It is your account on the workstation; it will also be used for your forge account and your SSH key. Git does not know it yet: it is up to you to give it."),
        ["Identity.Name"] = ("Prénom et nom", "First and last name"),
        ["Identity.Email"] = ("Adresse électronique", "Email address"),
        ["Identity.Login"] = ("Nom d'utilisateur sur le poste", "User name on the workstation"),
        ["Identity.Ok"] = ("Commencer", "Start"),
        ["Identity.AnotherTitle"] = ("Un autre poste", "Another workstation"),
        ["Identity.AnotherIntro"] = ("Le compte d'un autre élève, sur un autre poste de la salle : il partage la même forge. Un camarade peut s'asseoir à côté de vous, ou vous pouvez jouer les deux rôles. Git ne connaît pas encore ce compte non plus.",
            "The account of another student, on another workstation in the room: it shares the same forge. A classmate can sit next to you, or you can play both roles. Git does not know this account yet either."),
        ["Identity.AnotherOk"] = ("Ajouter le poste", "Add the workstation"),
        ["Identity.Cancel"] = ("Annuler", "Cancel"),
        ["Editor.Title"] = ("Éditeur de Git", "Git editor"),
        ["Editor.Intro"] = ("Git attend un texte : les lignes qui commencent par # sont ignorées. Un message vide annule.",
            "Git is waiting for text: lines starting with # are ignored. An empty message cancels."),
        ["Editor.Save"] = ("Enregistrer et fermer", "Save and close"),
        ["Editor.Cancel"] = ("Abandonner", "Abort"),
        ["Status.Branch"] = ("Branche", "Branch"),
        ["Status.Detached"] = ("HEAD détachée", "Detached HEAD"),
    };
}
