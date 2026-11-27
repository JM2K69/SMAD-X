using SMAGX.Git;
using SMAGX.Machine;

namespace SMAGX.Explain;

/// <summary>
/// Ce que vient de faire une commande, expliqué en français (ou en anglais) à côté de la sortie de Git, qui reste en
/// anglais : la zone touchée, ce qui a bougé, et pour une erreur courante, ce qu'il faut faire.
/// </summary>
public static class Explainer
{
    /// <summary>L'explication, ou null pour une commande du shell qui n'en demande pas (cd, ls…).</summary>
    public static string? Explain(Workstation ws, string line, CommandResult result, string culture)
    {
        string T(string fr, string en) => culture.StartsWith("fr") ? fr : en;
        List<string> words;
        try { words = Shell.Tokenize(line); }
        catch (FormatException) { return T("Un guillemet n'est pas refermé : la commande n'a pas été lancée.", "A quote is not closed: the command did not run."); }
        if (words.Count == 0) return null;
        if (words[0] is "ssh-keygen" or "ssh") return ExplainSsh(words, result, T);
        if (words[0] == "mkdir" && result.ExitCode == 0 && ws.FindRepository(ws.Cwd) is not null)
            return T("Le dossier est créé. Git ne suit pas les dossiers vides : il n'apparaîtra dans git status (et dans un commit) qu'avec un fichier dedans.",
                "The folder is created. Git does not track empty folders: it will only show in git status (and in a commit) with a file inside.");
        if (words[0] != "git")
            return result.ExitCode == 127
                ? T($"« {words[0]} » n'est pas une commande de ce terminal. Les commandes de Git commencent par « git ».", $"\"{words[0]}\" is not a command of this terminal. Git commands start with \"git\".")
                : null;

        var err = result.Err;
        var output = result.Out + err;
        var command = words.Skip(1).FirstOrDefault(w => !w.StartsWith('-')) ?? "";
        var args = words.Skip(2).ToList();
        var repo = ws.FindRepository(ws.Cwd);

        // Les erreurs courantes, quelle que soit la commande
        if (err.Contains("not a git repository"))
            return T("Il n'y a pas de dépôt Git dans ce dossier ni au-dessus. Créez-en un avec « git init », ou placez-vous dans un dépôt avec « cd ».",
                "There is no Git repository in this folder or above. Create one with \"git init\", or move into a repository with \"cd\".");
        if (err.Contains("is not a git command"))
            return T("Cette commande n'existe pas : vérifiez l'orthographe. Git propose la commande la plus proche.",
                "This command does not exist: check the spelling. Git suggests the closest one.");
        if (err.Contains("Author identity unknown"))
        {
            var name = ws.FullName ?? T("Prénom Nom", "First Last");
            var email = ws.Email ?? T("adresse", "address");
            return T($"Git ne sait pas qui vous êtes : chaque commit porte un nom et une adresse. Présentez-vous une fois pour toutes, pour tous vos dépôts :\ngit config --global user.name \"{name}\"\ngit config --global user.email {email}",
                $"Git does not know who you are: every commit carries a name and an email. Introduce yourself once and for all, for all your repositories:\ngit config --global user.name \"{name}\"\ngit config --global user.email {email}");
        }
        if (ConnectionProblem(err, T) is { } connection) return connection;
        if (err.Contains("did not match any file"))
            return T("Aucun fichier ne correspond à ce nom : vérifiez-le avec « ls » ou « git status ».",
                "No file matches this name: check it with \"ls\" or \"git status\".");
        if (err.Contains("would be overwritten by checkout") || err.Contains("would be overwritten by merge"))
            return T("Git refuse : des modifications non validées seraient écrasées. Validez-les avec « git commit », ou annulez-les avec « git restore ».",
                "Git refuses: uncommitted changes would be overwritten. Commit them with \"git commit\", or discard them with \"git restore\".");
        if (err.Contains("you have unmerged files") || err.Contains("you need to resolve your current index first"))
            return T("Une fusion attend : des fichiers sont encore en conflit. Corrigez-les, retirez les marqueurs <<<<<<<, ======= et >>>>>>>, puis « git add » et « git commit ».",
                "A merge is waiting: files are still in conflict. Fix them, remove the <<<<<<<, ======= and >>>>>>> markers, then \"git add\" and \"git commit\".");

        switch (command)
        {
            case "init":
                if (result.ExitCode != 0) return null;
                var branch = repo?.CurrentBranch ?? "main";
                return output.Contains("Reinitialized")
                    ? T("Ce dossier était déjà un dépôt : rien n'a changé.", "This folder already was a repository: nothing changed.")
                    : T($"Un dépôt est créé : le dossier caché .git gardera tout l'historique. HEAD désigne déjà la branche « {branch} », d'où « ({branch}) » dans l'invite, mais elle n'a encore aucun commit : « git branch » ne l'affichera qu'après le premier. Rien n'est encore suivi : « git add » choisit les fichiers.",
                        $"A repository is created: the hidden .git folder will keep the whole history. HEAD already points to branch \"{branch}\", hence \"({branch})\" in the prompt, but it has no commit yet: \"git branch\" only lists it after the first one. Nothing is tracked yet: \"git add\" picks the files.")
                      + (output.Contains("Using 'master'") ? T(" Git suggère de choisir le nom de la branche par défaut (souvent « main ») avec git config --global init.defaultBranch.", " Git suggests choosing the default branch name (often \"main\") with git config --global init.defaultBranch.") : "");
            case "config":
                if (result.ExitCode != 0 || args.Count(a => !a.StartsWith('-')) < 2) return null;
                var settings = args.Where(a => !a.StartsWith('-')).ToList();
                return T($"Réglage enregistré : {settings[0]} vaut « {settings[1]} »{(args.Contains("--global") ? ", pour tous vos dépôts (--global)" : ", pour ce dépôt")}.",
                    $"Setting saved: {settings[0]} is \"{settings[1]}\"{(args.Contains("--global") ? ", for all your repositories (--global)" : ", for this repository")}.");
            case "status":
                return T("« git status » compare les trois zones : le répertoire de travail, l'index et le dernier commit. En vert, ce qui partira dans le prochain commit ; en rouge, ce qui n'y partira pas (encore).",
                    "\"git status\" compares the three areas: the working directory, the index and the last commit. In green, what goes into the next commit; in red, what does not (yet).");
            case "add":
                if (result.ExitCode != 0)
                    return output.Contains("ignored by one of your .gitignore")
                        ? T("Ce fichier est ignoré par .gitignore : Git ne le suivra pas, sauf avec « git add -f ».", "This file is ignored by .gitignore: Git will not track it, unless with \"git add -f\".")
                        : null;
                return T("Les fichiers passent dans l'index (la zone de préparation) : c'est cette version qui partira dans le prochain commit. Une modification faite après devra être ajoutée de nouveau.",
                    "The files go into the index (the staging area): this version goes into the next commit. A change made afterwards must be added again.");
            case "commit":
                if (result.ExitCode == 0 && repo?.Head is { } head)
                {
                    var commit = repo.GetCommit(head);
                    var where = repo.CurrentBranch is { } b ? T($"la branche « {b} » avance sur lui", $"branch \"{b}\" moves forward onto it") : T("HEAD est détachée : aucune branche ne le retient", "HEAD is detached: no branch keeps it");
                    return commit.Parents.Count == 0
                        ? T($"Premier commit : une photo de l'index, signée de votre nom, datée, avec son message. Son identifiant est {head.Short} ; {where}.",
                            $"First commit: a snapshot of the index, signed with your name, dated, with its message. Its identifier is {head.Short}; {where}.")
                        : commit.Parents.Count > 1
                            ? T($"La fusion est terminée : le commit {head.Short} a deux parents, les deux branches réunies.", $"The merge is done: commit {head.Short} has two parents, both branches joined.")
                            : T($"Nouveau commit {head.Short} : une photo de l'index, rattachée au commit précédent ; {where}.", $"New commit {head.Short}: a snapshot of the index, attached to the previous commit; {where}.");
                }
                if (output.Contains("nothing to commit") || output.Contains("no changes added") || output.Contains("nothing added"))
                    return T("Rien à valider : l'index est identique au dernier commit. Choisissez d'abord les modifications avec « git add » (ou « git commit -a » pour les fichiers déjà suivis).",
                        "Nothing to commit: the index matches the last commit. First pick the changes with \"git add\" (or \"git commit -a\" for files already tracked).");
                return null;
            case "log":
                return T("« git log » liste les commits du plus récent au plus ancien : identifiant, auteur, date et message. « (HEAD -> main) » montre où vous êtes.",
                    "\"git log\" lists commits from newest to oldest: identifier, author, date and message. \"(HEAD -> main)\" shows where you are.");
            case "branch":
                if (result.ExitCode != 0)
                    return output.Contains("not fully merged")
                        ? T("Cette branche a des commits qui ne sont dans aucune autre : la supprimer les perdrait. Fusionnez-la d'abord, ou forcez avec -D si vous êtes sûr.",
                            "This branch has commits found nowhere else: deleting it would lose them. Merge it first, or force with -D if you are sure.")
                        : null;
                if (args.Any(a => a is "-d" or "-D"))
                    return T("La branche est supprimée : seule l'étiquette disparaît, les commits fusionnés restent dans l'historique.", "The branch is deleted: only the label goes, merged commits stay in the history.");
                if (args.Any(a => !a.StartsWith('-')))
                    return T("Une branche est créée : une simple étiquette posée sur le commit actuel. Vous n'êtes pas dessus : « git switch » vous y emmène.",
                        "A branch is created: just a label on the current commit. You are not on it: \"git switch\" takes you there.");
                return T("La branche marquée d'une étoile est celle où vous êtes (HEAD).", "The branch marked with a star is the one you are on (HEAD).");
            case "switch" or "checkout":
                if (result.ExitCode != 0) return null;
                if (err.Contains("you are leaving"))
                    return T("Attention : des commits faits en HEAD détachée ne sont plus retenus par aucune branche. Notez leur identifiant : « git branch nom identifiant » les sauverait.",
                        "Careful: commits made on a detached HEAD are no longer kept by any branch. Note their identifier: \"git branch name identifier\" would save them.");
                if (repo?.HeadRef is null)
                    return T("HEAD détachée : vous êtes sur un commit, pas sur une branche. Pour garder des commits faits ici, créez une branche avec « git switch -c nom ».",
                        "Detached HEAD: you are on a commit, not on a branch. To keep commits made here, create a branch with \"git switch -c name\".");
                if (err.Contains("Switched to a new branch"))
                    return T($"Une branche « {repo.CurrentBranch} » est créée sur le commit actuel, et vous êtes dessus.", $"Branch \"{repo.CurrentBranch}\" is created on the current commit, and you are on it.");
                if (err.Contains("Switched to branch"))
                    return T($"Vous êtes sur « {repo.CurrentBranch} » : HEAD la désigne, et vos fichiers ont pris la version de son dernier commit.",
                        $"You are on \"{repo.CurrentBranch}\": HEAD points to it, and your files took the version of its last commit.");
                return null;
            case "merge":
                if (output.Contains("CONFLICT"))
                    return T("Conflit : les deux branches ont modifié les mêmes lignes. Ouvrez le fichier, gardez la bonne version entre <<<<<<< et >>>>>>>, retirez les marqueurs, puis « git add » et « git commit ». « git merge --abort » annule tout.",
                        "Conflict: both branches changed the same lines. Open the file, keep the right version between <<<<<<< and >>>>>>>, remove the markers, then \"git add\" and \"git commit\". \"git merge --abort\" cancels everything.");
                if (output.Contains("Fast-forward"))
                    return T("Avance rapide : votre branche n'avait pas bougé, Git déplace simplement son étiquette jusqu'au dernier commit de l'autre. Pas de commit de fusion.",
                        "Fast-forward: your branch had not moved, Git simply moves its label up to the other one's last commit. No merge commit.");
                if (output.Contains("Merge made"))
                    return T("Les deux branches avaient avancé : Git crée un commit de fusion, qui a deux parents.", "Both branches had moved on: Git creates a merge commit, with two parents.");
                if (output.Contains("Already up to date"))
                    return T("Rien à fusionner : ces commits sont déjà dans votre branche.", "Nothing to merge: these commits are already in your branch.");
                if (args.Contains("--abort") && result.ExitCode == 0)
                    return T("La fusion est annulée : le dépôt et vos fichiers reviennent à leur état d'avant.", "The merge is aborted: the repository and your files are back to how they were.");
                return null;
            case "restore":
                if (result.ExitCode != 0) return null;
                return args.Contains("--staged") || args.Contains("-S")
                    ? T("Le fichier sort de l'index : il ne partira pas dans le prochain commit, mais sa modification reste dans vos fichiers.",
                        "The file leaves the index: it will not go into the next commit, but its change stays in your files.")
                    : T("Le fichier reprend la version de l'index : vos modifications non ajoutées sont perdues.", "The file goes back to the index version: your unadded changes are lost.");
            case "reset":
                if (result.ExitCode != 0) return null;
                if (args.Contains("--hard"))
                    return T("La branche se place sur ce commit, et l'index comme vos fichiers reprennent son contenu : les modifications non validées sont perdues.",
                        "The branch moves to this commit, and the index and your files take its content: uncommitted changes are lost.");
                if (args.Contains("--soft"))
                    return T("Seule la branche se déplace : l'index et vos fichiers ne changent pas, prêts pour un nouveau commit.", "Only the branch moves: the index and your files do not change, ready for a new commit.");
                return T("L'index reprend le contenu du commit visé ; vos fichiers ne changent pas : les modifications sont toujours là, à ajouter de nouveau.",
                    "The index takes the content of the target commit; your files do not change: the changes are still there, to add again.");
            case "rm":
                if (result.ExitCode != 0)
                    return output.Contains("local modifications") || output.Contains("changes staged")
                        ? T("Git refuse de supprimer un fichier modifié : ses modifications seraient perdues. « --cached » le garde sur le disque, « -f » force.",
                            "Git refuses to delete a changed file: its changes would be lost. \"--cached\" keeps it on disk, \"-f\" forces.")
                        : null;
                return args.Contains("--cached")
                    ? T("Le fichier n'est plus suivi : il reste sur le disque, mais sa suppression partira dans le prochain commit. Ajoutez-le à .gitignore pour que Git l'ignore.",
                        "The file is no longer tracked: it stays on disk, but its removal goes into the next commit. Add it to .gitignore so that Git ignores it.")
                    : T("Le fichier est supprimé de vos fichiers et de l'index : sa suppression partira dans le prochain commit.", "The file is removed from your files and the index: its removal goes into the next commit.");
            case "clone":
                if (result.ExitCode != 0) return null;
                return FirstVisit(err, T) + (err.Contains("empty repository")
                    ? T("Le projet de la forge est vide : le dossier est prêt, « origin » désigne la forge. Faites un premier commit, « git push » le publiera.",
                        "The forge project is empty: the folder is ready, \"origin\" points to the forge. Make a first commit, \"git push\" will publish it.")
                    : T("Le projet de la forge est copié sur le poste, avec tout son historique. « origin » désigne la forge, et votre branche suit la sienne (origin/…).",
                        "The forge project is copied to the workstation, with its whole history. \"origin\" points to the forge, and your branch follows its own (origin/…)."));
            case "push":
                if (err.Contains("(fetch first)"))
                    return T("Refusé : la forge a des commits que vous n'avez pas (un camarade a poussé avant vous). Récupérez-les avec « git pull », puis poussez de nouveau.",
                        "Rejected: the forge has commits you do not have (a classmate pushed before you). Get them with \"git pull\", then push again.");
                if (err.Contains("non-fast-forward"))
                    return T("Refusé : votre branche est en retard sur celle de la forge. « git pull » réunit les deux historiques, puis « git push ».",
                        "Rejected: your branch is behind the forge's one. \"git pull\" joins both histories, then \"git push\".");
                if (result.ExitCode != 0) return null;
                return FirstVisit(err, T) + (err.Contains("Everything up-to-date")
                    ? T("Rien à envoyer : la forge a déjà tous vos commits.", "Nothing to send: the forge already has all your commits.")
                    : err.Contains("[new branch]")
                        ? T("La branche est publiée sur la forge : vos camarades la récupèrent avec « git fetch » ou « git pull ».",
                            "The branch is published on the forge: your classmates get it with \"git fetch\" or \"git pull\".")
                        : T("Vos nouveaux commits sont sur la forge ; origin/… a avancé avec eux.", "Your new commits are on the forge; origin/… moved forward with them."));
            case "fetch":
                if (result.ExitCode != 0) return null;
                return FirstVisit(err, T) + T("Les commits de la forge sont téléchargés dans origin/… ; vos branches et vos fichiers ne changent pas. « git merge » ou « git pull » les réunira.",
                    "The forge's commits are downloaded into origin/…; your branches and files do not change. \"git merge\" or \"git pull\" will join them.");
            case "pull":
                if (err.Contains("divergent branches"))
                    return T("Votre branche et celle de la forge ont chacune des commits que l'autre n'a pas : Git demande comment les réunir. « git pull --no-rebase » les fusionne (avec un commit de fusion) ; « git config --global pull.rebase false » en fait le choix par défaut.",
                        "Your branch and the forge's each have commits the other lacks: Git asks how to join them. \"git pull --no-rebase\" merges them (with a merge commit); \"git config --global pull.rebase false\" makes it the default choice.");
                if (output.Contains("CONFLICT"))
                    return T("Conflit : vous et la forge avez modifié les mêmes lignes. Corrigez le fichier, retirez les marqueurs, puis « git add » et « git commit ».",
                        "Conflict: you and the forge changed the same lines. Fix the file, remove the markers, then \"git add\" and \"git commit\".");
                if (result.ExitCode != 0) return null;
                return FirstVisit(err, T) + T("« git pull » = « git fetch » (récupérer les commits de la forge) puis « git merge » (les réunir à votre branche).",
                    "\"git pull\" = \"git fetch\" (get the forge's commits) then \"git merge\" (join them to your branch).");
            default:
                return null;
        }
    }

    // La première connexion à une forge : sa clé est maintenant dans known_hosts
    private static string FirstVisit(string err, Func<string, string, string> T) =>
        err.Contains("Permanently added")
            ? T("Première connexion à ce serveur : sa clé est désormais dans ~/.ssh/known_hosts, et ssh vérifiera à chaque fois qu'elle n'a pas changé. ",
                "First connection to this server: its key is now in ~/.ssh/known_hosts, and ssh will check each time that it has not changed. ")
            : "";

    // Les refus de ssh, pour git comme pour ssh
    private static string? ConnectionProblem(string err, Func<string, string, string> T)
    {
        if (err.Contains("REMOTE HOST IDENTIFICATION HAS CHANGED"))
            return T("La clé du serveur ne correspond plus à celle notée dans ~/.ssh/known_hosts : ssh refuse, c'est peut-être une usurpation. Ici, c'est sans doute known_hosts qui a été modifié : « ssh-keygen -R serveur » oublie l'ancienne clé.",
                "The server key no longer matches the one noted in ~/.ssh/known_hosts: ssh refuses, it may be an impersonation. Here, known_hosts was probably edited: \"ssh-keygen -R server\" forgets the old key.");
        if (err.Contains("Host key verification failed"))
            return T("Vous n'avez pas accepté la clé du serveur : ssh ne se connecte pas. Si c'est bien votre forge, relancez la commande et répondez « yes ».",
                "You did not accept the server key: ssh does not connect. If it is your forge, run the command again and answer \"yes\".");
        if (err.Contains("Permission denied (publickey)"))
            return err.Contains("git@")
                ? T("La forge ne reconnaît aucune de vos clés. Créez une paire avec « ssh-keygen » si ce n'est pas fait, puis ajoutez la clé publique (le fichier .pub) à votre compte : « Clés SSH… » dans la carte Forge.",
                    "The forge does not recognise any of your keys. Create a pair with \"ssh-keygen\" if not done yet, then add the public key (the .pub file) to your account: \"SSH keys…\" in the Forge card.")
                : T("Sur une forge, on se connecte toujours au compte « git » (git@…) : c'est votre clé qui dit qui vous êtes.",
                    "On a forge, you always connect to the \"git\" account (git@…): your key tells who you are.");
        if (err.Contains("Could not resolve hostname"))
            return T("Ce serveur n'existe pas sur le réseau de la salle : les forges sont gitlab.example et github.example.",
                "This server does not exist on the room's network: the forges are gitlab.example and github.example.");
        if (err.Contains("port 22: Connection"))
            return T("La forge ne répond pas : le SMAG-X qui la sert est fermé, ou son serveur est arrêté (« Travail à deux… »).",
                "The forge does not answer: the SMAG-X serving it is closed, or its server is stopped (\"Pair work…\").");
        return null;
    }

    private static string? ExplainSsh(List<string> words, CommandResult result, Func<string, string, string> T)
    {
        var err = result.Err;
        if (ConnectionProblem(err, T) is { } problem) return problem;
        if (words[0] == "ssh")
        {
            if (result.Display.Contains("Welcome to GitLab") || result.Display.Contains("successfully authenticated"))
                return FirstVisit(err, T) + T("La forge vous reconnaît à votre clé : Git pourra cloner et pousser par SSH. Elle n'ouvre pas de terminal, ce test suffit.",
                    "The forge recognises you by your key: Git can clone and push over SSH. It opens no terminal, this test is enough.");
            return null;
        }
        var output = result.Out;
        if (words.Any(w => w.StartsWith("-R")))
            return output.Contains("found")
                ? T("Le serveur est oublié : à la prochaine connexion, ssh demandera de nouveau s'il faut lui faire confiance.",
                    "The server is forgotten: on the next connection, ssh will ask again whether to trust it.")
                : null;
        if (output.Contains("SHA256:") && !output.Contains("Your public key"))
            return T("L'empreinte (SHA256:…) résume la clé : la forge l'affiche à côté de chaque clé, pour les comparer d'un coup d'œil.",
                "The fingerprint (SHA256:…) sums up the key: the forge shows it next to each key, to compare them at a glance.");
        if (result.ExitCode == 0 && output.Contains("Your public key has been saved in "))
        {
            var start = output.IndexOf("Your public key has been saved in ", StringComparison.Ordinal) + "Your public key has been saved in ".Length;
            var pub = output[start..output.IndexOf('\n', start)];
            return T($"Une paire de clés est créée. La clé privée ne quitte jamais le poste ; la clé publique ({pub}) se donne à la forge : affichez-la avec « cat {pub} », copiez-la et ajoutez-la dans « Clés SSH… » de la carte Forge.",
                $"A key pair is created. The private key never leaves the workstation; the public key ({pub}) is given to the forge: show it with \"cat {pub}\", copy it and add it in \"SSH keys…\" of the Forge card.");
        }
        return null;
    }
}
