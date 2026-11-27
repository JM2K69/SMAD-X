using SMADX.Models;

namespace SMAGX.Labs;

/// <summary>
/// Les scénarios fournis avec SMAG-X, un parcours à difficulté croissante : sept seul (du premier commit à la
/// réécriture de l'historique, en passant par la forge et SSH), puis trois à deux (sur un SMAG-X, ou sur deux en
/// réseau). Leur solution n'est pas ici : les tests la jouent et vérifient que chaque objectif est atteint.
/// </summary>
public static class SmagxBuiltInLabs
{
    public static IReadOnlyList<SmagxLab> All() =>
    [
        FirstSteps(), ThreeAreas(), BranchAndMerge(), Conflict(), Publish(), Contribute(), BackInTime(),
        PairWork(), PairConflict(), PairReview()
    ];

    private static LocalizedText L(string fr, string en) => LocalizedText.Of(fr, en);

    private static SmagxObjective O(string id, int points, LocalizedText title, LocalizedText hint, SmagxCheck check, bool guard = false) =>
        new() { Id = id, Points = guard ? 0 : points, Title = title, Hint = hint, Check = check, Guard = guard };

    private static readonly SmagxRole Me = new()
    {
        Id = "moi",
        Brief = L("Vous travaillez seul, sous votre propre identité.", "You work alone, under your own identity.")
    };

    // L'identité de l'élève déjà donnée à Git (à partir du deuxième scénario)
    private const string Identity = """
        $ git config --global user.name "{name}"
        $ git config --global user.email "{email}"
        $ git config --global init.defaultBranch main
        """;

    // ─── 1. Premiers pas ────────────────────────────────────────────────────

    public static SmagxLab FirstSteps() => new()
    {
        Id = "premiers-pas",
        Step = 1,
        Level = LabLevel.Beginner,
        DurationMinutes = 15,
        Title = L("Premiers pas", "First steps"),
        Statement = L(
            """
            # Premiers pas

            Vous arrivez sur un poste neuf : Git ne vous connaît pas encore, et le dossier `~/projet` est vide.

            ## Votre mission

            1. Présentez-vous à Git : votre nom et votre adresse, valables pour tous vos dépôts.
            2. Faites de `~/projet` un dépôt Git.
            3. Créez une page `index.html` (panneau **Fichiers**, ou `echo`) et enregistrez-la dans un premier commit.
            4. Modifiez la page, puis enregistrez cette nouvelle version dans un deuxième commit.
            5. Terminez sans rien laisser en attente.

            Cliquez sur **Vérifier** pour voir où vous en êtes.
            """,
            """
            # First steps

            You arrive on a new workstation: Git does not know you yet, and the `~/projet` folder is empty.

            ## Your mission

            1. Introduce yourself to Git: your name and your email, valid for all your repositories.
            2. Turn `~/projet` into a Git repository.
            3. Create an `index.html` page (**Files** panel, or `echo`) and save it in a first commit.
            4. Change the page, then save this new version in a second commit.
            5. Finish with nothing left waiting.

            Click **Check** to see where you stand.
            """),
        Roles = [Me],
        Objectives =
        [
            O("nom", 1, L("Git connaît votre nom", "Git knows your name"),
                L("git config, avec l'option qui vaut pour tous vos dépôts, et la clé user.name.", "git config, with the option valid for all your repositories, and the user.name key."),
                new SmagxCheck { Type = SmagxCheckType.Config, Role = "moi", Key = "user.name", Value = "{name}" }),
            O("adresse", 1, L("Git connaît votre adresse", "Git knows your email"),
                L("La même commande, avec la clé user.email.", "The same command, with the user.email key."),
                new SmagxCheck { Type = SmagxCheckType.Config, Role = "moi", Key = "user.email", Value = "{email}" }),
            O("depot", 1, L("~/projet est un dépôt", "~/projet is a repository"),
                L("Une seule commande, tapée dans ~/projet.", "A single command, typed in ~/projet."),
                new SmagxCheck { Type = SmagxCheckType.Repository, Role = "moi" }),
            O("premier", 2, L("index.html est dans un commit", "index.html is in a commit"),
                L("Un commit enregistre ce qui est dans l'index : ajoutez d'abord le fichier (git add).", "A commit saves what is in the index: add the file first (git add)."),
                new SmagxCheck { Type = SmagxCheckType.FileInCommit, Role = "moi", File = "index.html" }),
            O("deuxieme", 2, L("Deux commits dans l'historique", "Two commits in the history"),
                L("Une modification doit de nouveau passer par l'index avant le commit.", "A change must go through the index again before the commit."),
                new SmagxCheck { Type = SmagxCheckType.CommitCount, Role = "moi", Count = 2 }),
            O("propre", 1, L("Rien n'attend dans les zones", "Nothing waits in the areas"),
                L("git status doit dire qu'il n'y a rien à valider.", "git status must say there is nothing to commit."),
                new SmagxCheck { Type = SmagxCheckType.Clean, Role = "moi" })
        ]
    };

    // ─── 2. Les trois zones ─────────────────────────────────────────────────

    public static SmagxLab ThreeAreas() => new()
    {
        Id = "trois-zones",
        Step = 2,
        Level = LabLevel.Beginner,
        DurationMinutes = 20,
        Title = L("Les trois zones", "The three areas"),
        Statement = L(
            """
            # Les trois zones

            Le site de l'agence est déjà un dépôt, avec un premier commit. Depuis, trois choses ont changé dans le répertoire de travail :

            - `style.css` a une nouvelle couleur : elle est bonne, il faut la garder ;
            - `index.html` a été modifié par erreur : cette modification ne doit pas rester ;
            - `brouillon.txt` contient vos notes : il ne doit **jamais** partir dans un commit.

            ## Votre mission

            1. Validez la nouvelle couleur, et seulement elle.
            2. Rendez à `index.html` sa version du dernier commit.
            3. Faites en sorte que Git ignore `brouillon.txt`, et validez ce réglage.
            4. Terminez sans rien laisser en attente.
            """,
            """
            # The three areas

            The agency website is already a repository, with a first commit. Since then, three things changed in the working directory:

            - `style.css` has a new colour: it is right, keep it;
            - `index.html` was changed by mistake: this change must not stay;
            - `brouillon.txt` holds your notes: it must **never** go into a commit.

            ## Your mission

            1. Commit the new colour, and only it.
            2. Give `index.html` back its version from the last commit.
            3. Make Git ignore `brouillon.txt`, and commit this setting.
            4. Finish with nothing left waiting.
            """),
        Roles = [Me],
        Setup = Identity + """

            $ git init
            write index.html <h1>Agence Méridien</h1>\n<p>Bienvenue</p>\n
            write style.css body { margin: 0; }\n
            $ git add .
            $ git commit -m "Première version"
            write style.css body { margin: 0; color: navy; }\n
            append index.html <p>Texte à ne pas garder</p>\n
            write brouillon.txt idées en vrac\n
            """,
        Objectives =
        [
            O("couleur", 2, L("La nouvelle couleur est validée", "The new colour is committed"),
                L("git add choisit ce qui part dans le prochain commit : un fichier à la fois.", "git add picks what goes into the next commit: one file at a time."),
                new SmagxCheck { Type = SmagxCheckType.FileInCommit, Role = "moi", File = "style.css", Text = "navy" }),
            O("erreur", 2, L("index.html a retrouvé sa version validée", "index.html is back to its committed version"),
                L("git status indique la commande qui abandonne les modifications d'un fichier.", "git status shows the command that discards a file's changes."),
                new SmagxCheck { Type = SmagxCheckType.WorkFile, Role = "moi", File = "index.html", Text = "ne pas garder", Not = true }),
            O("ignore", 2, L("Git ignore brouillon.txt", "Git ignores brouillon.txt"),
                L("Un fichier .gitignore à la racine du dépôt liste ce que Git ne doit pas voir.", "A .gitignore file at the root of the repository lists what Git must not see."),
                new SmagxCheck { Type = SmagxCheckType.Ignored, Role = "moi", File = "brouillon.txt" }),
            O("gitignore", 1, L(".gitignore est validé", ".gitignore is committed"),
                L(".gitignore est un fichier comme les autres : il se valide.", ".gitignore is a file like the others: it gets committed."),
                new SmagxCheck { Type = SmagxCheckType.FileInCommit, Role = "moi", File = ".gitignore", Text = "brouillon.txt" }),
            O("jamais", 0, L("brouillon.txt n'est dans aucun commit", "brouillon.txt is in no commit"),
                L("« git add . » ajoute tout, y compris ce qu'on ne voulait pas.", "\"git add .\" adds everything, including what you did not want."),
                new SmagxCheck { Type = SmagxCheckType.FileInCommit, Role = "moi", File = "brouillon.txt", Not = true }, guard: true),
            O("propre", 1, L("Rien n'attend dans les zones", "Nothing waits in the areas"),
                L("git status doit dire qu'il n'y a rien à valider.", "git status must say there is nothing to commit."),
                new SmagxCheck { Type = SmagxCheckType.Clean, Role = "moi" })
        ]
    };

    // ─── 3. Branches et fusion ──────────────────────────────────────────────

    public static SmagxLab BranchAndMerge() => new()
    {
        Id = "branches",
        Step = 3,
        Level = LabLevel.Intermediate,
        DurationMinutes = 25,
        Title = L("Une branche, puis la fusion", "A branch, then the merge"),
        Statement = L(
            """
            # Une branche, puis la fusion

            Votre formateur a préparé une branche `contact` pour la future page de contact. Pendant ce temps, la page d'accueil doit continuer d'avancer sur `main`.

            ## Votre mission

            1. Sur la branche `contact`, ajoutez une page `contact.html` dans un commit.
            2. Sur `main`, ajoutez à `index.html` un lien vers `contact.html` (par exemple `<a href="contact.html">Contact</a>`), dans un commit.
            3. Réunissez `contact` dans `main` : les deux branches ont avancé, la fusion aura son propre commit.
            4. La branche `contact` a fini son travail : supprimez-la, et restez sur `main`.

            Regardez le graphe à chaque étape : où est HEAD, où sont les branches ?
            """,
            """
            # A branch, then the merge

            Your trainer prepared a `contact` branch for the future contact page. Meanwhile, the home page must keep moving on `main`.

            ## Your mission

            1. On the `contact` branch, add a `contact.html` page in a commit.
            2. On `main`, add to `index.html` a link to `contact.html` (for instance `<a href="contact.html">Contact</a>`), in a commit.
            3. Join `contact` into `main`: both branches moved on, the merge will have its own commit.
            4. The `contact` branch is done: delete it, and stay on `main`.

            Watch the graph at each step: where is HEAD, where are the branches?
            """),
        Roles = [Me],
        Setup = Identity + """

            $ git init
            write index.html <h1>Agence Méridien</h1>\n<p>Bienvenue</p>\n
            $ git add index.html
            $ git commit -m "Page d'accueil"
            $ git branch contact
            """,
        Objectives =
        [
            O("page", 2, L("main contient contact.html", "main contains contact.html"),
                L("Changez de branche avant le commit : git switch.", "Switch branch before the commit: git switch."),
                new SmagxCheck { Type = SmagxCheckType.FileInCommit, Role = "moi", Branch = "main", File = "contact.html" }),
            O("lien", 1, L("index.html, sur main, a le lien vers contact.html", "index.html, on main, has the link to contact.html"),
                L("Ce commit-là se fait sur main.", "This commit is made on main."),
                new SmagxCheck { Type = SmagxCheckType.FileInCommit, Role = "moi", Branch = "main", File = "index.html", Text = "contact.html" }),
            O("fusion", 2, L("main a un commit de fusion", "main has a merge commit"),
                L("Depuis main, git merge réunit l'autre branche dans celle où vous êtes.", "From main, git merge joins the other branch into the one you are on."),
                new SmagxCheck { Type = SmagxCheckType.MergeCommit, Role = "moi", Branch = "main" }),
            O("supprimee", 1, L("La branche contact est supprimée", "The contact branch is deleted"),
                L("git branch, avec l'option qui supprime une branche déjà fusionnée, depuis main.", "git branch, with the option that deletes an already merged branch, from main."),
                new SmagxCheck { Type = SmagxCheckType.BranchExists, Role = "moi", Branch = "contact", Not = true })
        ]
    };

    // ─── 4. Un conflit ──────────────────────────────────────────────────────

    public static SmagxLab Conflict() => new()
    {
        Id = "conflit",
        Step = 4,
        Level = LabLevel.Intermediate,
        DurationMinutes = 25,
        Title = L("Résoudre un conflit", "Resolving a conflict"),
        Statement = L(
            """
            # Résoudre un conflit

            Deux idées de titre pour le site : la branche `titre` propose « Agence Méridien, voyages », et `main` est passée à « Méridien Voyages ». Les deux ont modifié la même ligne de `index.html`.

            ## Votre mission

            1. Réunissez `titre` dans `main`.
            2. Git ne peut pas choisir seul : le titre retenu est **`<h1>Agence Méridien Voyages</h1>`**.
            3. Terminez la fusion, sans laisser de marqueur de conflit dans le fichier.
            """,
            """
            # Resolving a conflict

            Two title ideas for the site: the `titre` branch suggests "Agence Méridien, voyages", and `main` moved to "Méridien Voyages". Both changed the same line of `index.html`.

            ## Your mission

            1. Join `titre` into `main`.
            2. Git cannot choose alone: the chosen title is **`<h1>Agence Méridien Voyages</h1>`**.
            3. Finish the merge, without leaving any conflict marker in the file.
            """),
        Roles = [Me],
        Setup = Identity + """

            $ git init
            write index.html <h1>Agence Méridien</h1>\n<p>Bienvenue</p>\n
            $ git add index.html
            $ git commit -m "Page d'accueil"
            $ git switch -c titre
            write index.html <h1>Agence Méridien, voyages</h1>\n<p>Bienvenue</p>\n
            $ git commit -am "Titre plus précis"
            $ git switch main
            write index.html <h1>Méridien Voyages</h1>\n<p>Bienvenue</p>\n
            $ git commit -am "Nouveau nom"
            """,
        Objectives =
        [
            O("fusion", 3, L("La fusion est terminée : main a un commit de fusion", "The merge is finished: main has a merge commit"),
                L("Après le conflit, le commit qui termine la fusion se fait comme les autres : git add, puis git commit.", "After the conflict, the commit that ends the merge is made as usual: git add, then git commit."),
                new SmagxCheck { Type = SmagxCheckType.MergeCommit, Role = "moi", Branch = "main" }),
            O("titre", 2, L("Le titre retenu est validé", "The chosen title is committed"),
                L("Ouvrez index.html : gardez la bonne ligne entre <<<<<<< et >>>>>>>, et retirez les marqueurs.", "Open index.html: keep the right line between <<<<<<< and >>>>>>>, and remove the markers."),
                new SmagxCheck { Type = SmagxCheckType.FileInCommit, Role = "moi", Branch = "main", File = "index.html", Text = "<h1>Agence Méridien Voyages</h1>" }),
            O("marqueurs", 0, L("Aucun marqueur de conflit n'est validé", "No conflict marker is committed"),
                L("Les lignes <<<<<<<, ======= et >>>>>>> doivent disparaître avant git add.", "The <<<<<<<, ======= and >>>>>>> lines must go before git add."),
                new SmagxCheck { Type = SmagxCheckType.FileInCommit, Role = "moi", Branch = "main", File = "index.html", Text = "=======", Not = true }, guard: true)
        ]
    };

    // ─── 5. Publier sur la forge ────────────────────────────────────────────

    public static SmagxLab Publish() => new()
    {
        Id = "publier",
        Step = 5,
        Level = LabLevel.Intermediate,
        DurationMinutes = 25,
        Title = L("Publier sur la forge", "Publishing on the forge"),
        Statement = L(
            """
            # Publier sur la forge

            Votre portfolio a deux commits, sur votre poste seulement. Le projet `classe/portfolio` vous attend, vide, sur la forge **gitlab.example**.

            La forge vous reconnaît à votre **clé SSH** : la clé privée reste sur le poste, la clé publique se donne à la forge.

            ## Votre mission

            1. Créez une paire de clés SSH.
            2. Ajoutez votre clé publique à votre compte : carte **Forge**, bouton **Clés SSH…**.
            3. Testez la connexion : à la première, vérifiez et acceptez la clé du serveur.
            4. Reliez votre dépôt au projet de la forge, sous le nom `origin`.
            5. Envoyez `main`, et faites-la suivre la branche de la forge.
            """,
            """
            # Publishing on the forge

            Your portfolio has two commits, on your workstation only. The `classe/portfolio` project waits for you, empty, on the **gitlab.example** forge.

            The forge recognises you by your **SSH key**: the private key stays on the workstation, the public key is given to the forge.

            ## Your mission

            1. Create an SSH key pair.
            2. Add your public key to your account: **Forge** card, **SSH keys…** button.
            3. Test the connection: the first time, check and accept the server key.
            4. Link your repository to the forge project, under the name `origin`.
            5. Send `main`, and make it track the forge's branch.
            """),
        Roles = [Me],
        Setup = "forge classe/portfolio\n@moi\n" + Identity + """

            $ git init
            write index.html <h1>Portfolio de {name}</h1>\n
            $ git add index.html
            $ git commit -m "Page d'accueil"
            write projets.html <h2>Mes projets</h2>\n
            $ git add projets.html
            $ git commit -m "Page des projets"
            """,
        Objectives =
        [
            O("cle", 2, L("Votre clé publique est sur la forge", "Your public key is on the forge"),
                L("ssh-keygen crée la paire ; cat affiche le fichier .pub à copier dans « Clés SSH… ».", "ssh-keygen creates the pair; cat shows the .pub file to copy into \"SSH keys…\"."),
                new SmagxCheck { Type = SmagxCheckType.KeyOnForge, Role = "moi" }),
            O("serveur", 1, L("Le serveur gitlab.example est accepté", "The gitlab.example server is accepted"),
                L("ssh -T git@gitlab.example : la première fois, ssh demande s'il faut faire confiance au serveur.", "ssh -T git@gitlab.example: the first time, ssh asks whether to trust the server."),
                new SmagxCheck { Type = SmagxCheckType.KnownHost, Role = "moi" }),
            O("origin", 1, L("origin désigne classe/portfolio", "origin points to classe/portfolio"),
                L("git remote, avec l'adresse SSH affichée dans la carte Forge.", "git remote, with the SSH address shown in the Forge card."),
                new SmagxCheck { Type = SmagxCheckType.Remote, Role = "moi", Project = "classe/portfolio" }),
            O("main", 2, L("main est sur la forge", "main is on the forge"),
                L("git push, en nommant le dépôt distant et la branche.", "git push, naming the remote and the branch."),
                new SmagxCheck { Type = SmagxCheckType.ForgeBranch, Project = "classe/portfolio", Branch = "main" }),
            O("suivi", 1, L("main suit la branche de la forge", "main tracks the forge's branch"),
                L("Une option de git push enregistre le lien, pour les push et pull suivants.", "An option of git push records the link, for the next pushes and pulls."),
                new SmagxCheck { Type = SmagxCheckType.Tracks, Role = "moi", Branch = "main" }),
            O("ajour", 1, L("La forge a tous vos commits", "The forge has all your commits"),
                L("main sur le poste et main sur la forge doivent pointer sur le même commit.", "main on the workstation and main on the forge must point to the same commit."),
                new SmagxCheck { Type = SmagxCheckType.Synced, Role = "moi", Branch = "main", Project = "classe/portfolio" })
        ]
    };

    // ─── 6. Contribuer sur GitHub ───────────────────────────────────────────

    public static SmagxLab Contribute() => new()
    {
        Id = "contribuer",
        Step = 6,
        Level = LabLevel.Intermediate,
        DurationMinutes = 25,
        Title = L("Contribuer à un projet sur GitHub", "Contributing to a project on GitHub"),
        Statement = L(
            """
            # Contribuer à un projet sur GitHub

            Votre formateur a publié le livre de recettes de la classe sur **github.example** : le dépôt `classe/recettes`. Vous avez déjà une paire de clés SSH sur ce poste, mais GitHub ne la connaît pas.

            ## Votre mission

            1. Faites connaître votre clé publique à GitHub (carte **Forge**, onglet **GitHub**).
            2. Récupérez le dépôt sur votre poste.
            3. Sur une branche `gaufres`, ajoutez la recette `recettes/gaufres.md`.
            4. Publiez la branche `gaufres` sur GitHub, en la faisant suivre ; `main` ne doit pas bouger.

            Sur une vraie forge, la suite serait une demande de fusion (*pull request*) : regardez ce que GitHub vous répond.
            """,
            """
            # Contributing to a project on GitHub

            Your trainer published the class cookbook on **github.example**: the `classe/recettes` repository. You already have an SSH key pair on this workstation, but GitHub does not know it.

            ## Your mission

            1. Make your public key known to GitHub (**Forge** card, **GitHub** tab).
            2. Get the repository on your workstation.
            3. On a `gaufres` branch, add the recipe `recettes/gaufres.md`.
            4. Publish the `gaufres` branch on GitHub, tracking it; `main` must not move.

            On a real forge, the next step would be a pull request: look at what GitHub answers.
            """),
        Roles = [Me],
        Setup = """
            forge github.example:classe/recettes
            @formateur
            $ git config --global user.name "Claire Petit"
            $ git config --global user.email claire.petit@lycee.example
            $ git config --global init.defaultBranch main
            mkdir recettes
            $ cd recettes
            $ git init
            write README.md # Recettes de la classe\n
            write recettes/crepes.md # Crêpes\n\n- 250 g de farine\n- 4 œufs\n- 50 cl de lait\n
            $ git add .
            $ git commit -m "Premières recettes"
            $ git remote add origin git@github.example:classe/recettes.git
            $ git push -u origin main
            @moi

            """ + Identity + """

            $ ssh-keygen -q -N "" -f ~/.ssh/id_ed25519
            """,
        Objectives =
        [
            O("cle", 1, L("Votre clé publique est sur GitHub", "Your public key is on GitHub"),
                L("La clé est déjà dans ~/.ssh : il reste à donner la partie publique à GitHub.", "The key is already in ~/.ssh: the public part remains to be given to GitHub."),
                new SmagxCheck { Type = SmagxCheckType.KeyOnForge, Role = "moi", Host = "github.example" }),
            O("clone", 1, L("Le dépôt est sur votre poste", "The repository is on your workstation"),
                L("git clone, avec l'adresse SSH du dépôt (onglet GitHub de la carte Forge).", "git clone, with the repository's SSH address (GitHub tab of the Forge card)."),
                new SmagxCheck { Type = SmagxCheckType.Repository, Role = "moi", Repo = "projet/recettes" }),
            O("branche", 2, L("La branche gaufres est sur GitHub", "The gaufres branch is on GitHub"),
                L("Une branche créée sur le poste n'existe pas sur la forge tant qu'on ne l'y a pas poussée.", "A branch created on the workstation does not exist on the forge until it is pushed."),
                new SmagxCheck { Type = SmagxCheckType.ForgeBranch, Host = "github.example", Project = "classe/recettes", Branch = "gaufres" }),
            O("recette", 2, L("La recette est dans gaufres, sur GitHub", "The recipe is in gaufres, on GitHub"),
                L("Le fichier va dans le dossier recettes du dépôt.", "The file goes in the repository's recettes folder."),
                new SmagxCheck { Type = SmagxCheckType.ForgeFile, Host = "github.example", Project = "classe/recettes", Branch = "gaufres", File = "recettes/gaufres.md" }),
            O("auteur", 1, L("Le commit est signé de votre nom", "The commit is signed with your name"),
                L("Le nom vient de git config.", "The name comes from git config."),
                new SmagxCheck { Type = SmagxCheckType.ForgeAuthor, Role = "moi", Host = "github.example", Project = "classe/recettes", Branch = "gaufres" }),
            O("suivi", 1, L("gaufres suit la branche de GitHub", "gaufres tracks GitHub's branch"),
                L("L'option de git push qui enregistre le lien.", "The git push option that records the link."),
                new SmagxCheck { Type = SmagxCheckType.Tracks, Role = "moi", Repo = "projet/recettes", Branch = "gaufres" }),
            O("main", 0, L("main n'a pas bougé sur GitHub", "main did not move on GitHub"),
                L("La recette part sur gaufres, pas sur main.", "The recipe goes to gaufres, not to main."),
                new SmagxCheck { Type = SmagxCheckType.ForgeFile, Host = "github.example", Project = "classe/recettes", Branch = "main", File = "recettes/gaufres.md", Not = true }, guard: true)
        ]
    };

    // ─── 7. Remonter le temps ───────────────────────────────────────────────

    public static SmagxLab BackInTime() => new()
    {
        Id = "remonter-le-temps",
        Step = 7,
        Level = LabLevel.Advanced,
        DurationMinutes = 25,
        Title = L("Remonter le temps", "Going back in time"),
        Statement = L(
            """
            # Remonter le temps

            Trois versions de la page sont validées sur `main`… et un quatrième commit, « Ajout de secret.txt », a validé par erreur un fichier de mots de passe. Rien n'a été publié : l'historique peut encore changer.

            ## Votre mission

            1. Retirez le commit « Ajout de secret.txt » de l'historique de `main`, sans perdre le fichier sur le disque.
            2. Faites en sorte que Git ignore désormais `secret.txt`, et validez ce réglage.
            3. Gardez un repère sur l'ancienne version : une branche `v1` sur le commit « Version 1 », sans déplacer `main`.

            Le graphe montre où pointent HEAD et les branches : vérifiez-le à chaque étape.
            """,
            """
            # Going back in time

            Three versions of the page are committed on `main`… and a fourth commit, "Ajout de secret.txt", committed a password file by mistake. Nothing was published: the history can still change.

            ## Your mission

            1. Remove the "Ajout de secret.txt" commit from the history of `main`, without losing the file on disk.
            2. Make Git ignore `secret.txt` from now on, and commit this setting.
            3. Keep a marker on the old version: a `v1` branch on the "Version 1" commit, without moving `main`.

            The graph shows where HEAD and the branches point: check it at each step.
            """),
        Roles = [Me],
        Setup = Identity + """

            $ git init
            write index.html <h1>Version 1</h1>\n
            $ git add index.html
            $ git commit -m "Version 1"
            write index.html <h1>Version 2</h1>\n
            $ git commit -am "Version 2"
            write index.html <h1>Version 3</h1>\n
            $ git commit -am "Version 3"
            write secret.txt mot de passe : soleil42\n
            $ git add secret.txt
            $ git commit -m "Ajout de secret.txt"
            """,
        Objectives =
        [
            O("retire", 2, L("Le commit fautif n'est plus dans main", "The faulty commit is no longer in main"),
                L("git reset déplace la branche sur un commit antérieur ; selon l'option, il garde ou non vos fichiers.", "git reset moves the branch onto an earlier commit; depending on the option, it keeps your files or not."),
                new SmagxCheck { Type = SmagxCheckType.HistoryContains, Role = "moi", Branch = "main", Text = "Ajout de secret.txt", Not = true }),
            O("ignore", 2, L("secret.txt est sur le disque, ignoré par Git", "secret.txt is on disk, ignored by Git"),
                L("Le fichier doit sortir de l'index et entrer dans .gitignore.", "The file must leave the index and enter .gitignore."),
                new SmagxCheck { Type = SmagxCheckType.Ignored, Role = "moi", File = "secret.txt" }),
            O("gitignore", 1, L(".gitignore est validé", ".gitignore is committed"),
                L("Un commit de plus sur main, avec .gitignore.", "One more commit on main, with .gitignore."),
                new SmagxCheck { Type = SmagxCheckType.FileInCommit, Role = "moi", Branch = "main", File = ".gitignore", Text = "secret.txt" }),
            O("v1", 2, L("La branche v1 est sur « Version 1 »", "Branch v1 is on \"Version 1\""),
                L("git branch accepte un commit de départ : son identifiant (git log --oneline) ou HEAD~n.", "git branch accepts a starting commit: its identifier (git log --oneline) or HEAD~n."),
                new SmagxCheck { Type = SmagxCheckType.CommitMessage, Role = "moi", Branch = "v1", Text = "Version 1" }),
            O("versions", 0, L("Les trois versions restent dans main", "The three versions stay in main"),
                L("Seul le dernier commit doit partir.", "Only the last commit must go."),
                new SmagxCheck { Type = SmagxCheckType.HistoryContains, Role = "moi", Branch = "main", Text = "Version 3" }, guard: true)
        ]
    };

    // ─── À deux : le départ commun ──────────────────────────────────────────

    private static SmagxRole Alice(LocalizedText brief) => new()
    {
        Id = "alice", Name = "Alice Martin", Email = "alice@lycee.example", Login = "alice", Serves = true, Brief = brief
    };

    private static SmagxRole Bob(LocalizedText brief) => new()
    {
        Id = "bob", Name = "Bob Durand", Email = "bob@lycee.example", Login = "bob", Brief = brief
    };

    /// <summary>
    /// Le projet classe/agence publié par le formateur ; chaque rôle a son identité, sa clé (déjà sur la forge) et le
    /// serveur déjà accepté ; avec <paramref name="cloned"/>, il a aussi déjà cloné le projet.
    /// </summary>
    private static string PairSetup(bool cloned) => """
        forge classe/agence
        @formateur
        $ git config --global user.name "Claire Petit"
        $ git config --global user.email claire.petit@lycee.example
        $ git config --global init.defaultBranch main
        mkdir agence
        $ cd agence
        $ git init
        write index.html <h1>Agence Méridien</h1>\n<p>Bienvenue</p>\n
        $ git add index.html
        $ git commit -m "Page d'accueil"
        $ git remote add origin git@gitlab.example:classe/agence.git
        $ git push -u origin main

        """ + Role("alice", "Alice Martin", cloned) + Role("bob", "Bob Durand", cloned);

    private static string Role(string login, string name, bool cloned) => $"""
        @{login}
        $ git config --global user.name "{name}"
        $ git config --global user.email {login}@lycee.example
        $ git config --global init.defaultBranch main
        $ ssh-keygen -q -N "" -f ~/.ssh/id_ed25519
        key
        trust gitlab.example

        """ + (cloned ? "$ git clone git@gitlab.example:classe/agence.git\n$ cd agence\n" : "");

    private const string PairNote = """


        Ce scénario se joue **à deux sur ce SMAG-X** (un poste par rôle, en onglets) ou **sur deux SMAG-X** : celui d'Alice sert la forge, celui de Bob la rejoint (**Travail à deux…**).
        """;

    private const string PairNoteEn = """


        This scenario is played **by two on this SMAG-X** (one workstation per role, in tabs) or **on two SMAG-X**: Alice's serves the forge, Bob's joins it (**Pair work…**).
        """;

    // ─── 8. Travailler à deux ───────────────────────────────────────────────

    public static SmagxLab PairWork() => new()
    {
        Id = "a-deux",
        Step = 8,
        Level = LabLevel.Intermediate,
        DurationMinutes = 30,
        Title = L("Travailler à deux sur un projet", "Working by two on a project"),
        Statement = L(
            """
            # Travailler à deux sur un projet

            Le formateur a publié le site de l'agence sur la forge : `classe/agence`. Alice et Bob ont chacun leur poste, leur clé SSH (déjà ajoutée à leur compte) et vont travailler en même temps sur `main`.

            ## La mission

            - **Alice** récupère le projet et ajoute une page `equipe.html`.
            - **Bob** récupère le projet et ajoute une page `tarifs.html`.
            - Les deux pages doivent arriver sur la forge. Le second à envoyer sera refusé : à lui de récupérer le travail de l'autre avant d'envoyer de nouveau.
            - À la fin, les deux postes sont à jour avec la forge.
            """ + PairNote,
            """
            # Working by two on a project

            The trainer published the agency website on the forge: `classe/agence`. Alice and Bob each have their workstation, their SSH key (already added to their account), and will work at the same time on `main`.

            ## The mission

            - **Alice** gets the project and adds an `equipe.html` page.
            - **Bob** gets the project and adds a `tarifs.html` page.
            - Both pages must reach the forge. The second one to send will be rejected: up to them to get the other's work before sending again.
            - In the end, both workstations are up to date with the forge.
            """ + PairNoteEn),
        Roles =
        [
            Alice(L("Clonez classe/agence, ajoutez equipe.html sur main, envoyez-la sur la forge.", "Clone classe/agence, add equipe.html on main, send it to the forge.")),
            Bob(L("Clonez classe/agence, ajoutez tarifs.html sur main, envoyez-la sur la forge.", "Clone classe/agence, add tarifs.html on main, send it to the forge."))
        ],
        Setup = PairSetup(cloned: false),
        Objectives =
        [
            O("clone-alice", 1, L("Alice a le projet sur son poste", "Alice has the project on her workstation"),
                L("git clone, avec l'adresse SSH du projet.", "git clone, with the project's SSH address."),
                new SmagxCheck { Type = SmagxCheckType.Repository, Role = "alice", Repo = "projet/agence" }),
            O("clone-bob", 1, L("Bob a le projet sur son poste", "Bob has the project on his workstation"),
                L("git clone, avec l'adresse SSH du projet.", "git clone, with the project's SSH address."),
                new SmagxCheck { Type = SmagxCheckType.Repository, Role = "bob", Repo = "projet/agence" }),
            O("equipe", 2, L("equipe.html est sur la forge", "equipe.html is on the forge"),
                L("Commit, puis git push.", "Commit, then git push."),
                new SmagxCheck { Type = SmagxCheckType.ForgeFile, Project = "classe/agence", Branch = "main", File = "equipe.html" }),
            O("tarifs", 2, L("tarifs.html est sur la forge", "tarifs.html is on the forge"),
                L("Refusé ? La forge a des commits que vous n'avez pas : git pull les réunit aux vôtres, puis git push.", "Rejected? The forge has commits you do not have: git pull joins them to yours, then git push."),
                new SmagxCheck { Type = SmagxCheckType.ForgeFile, Project = "classe/agence", Branch = "main", File = "tarifs.html" }),
            O("auteur-alice", 1, L("La forge a un commit d'Alice", "The forge has a commit by Alice"),
                L("Le nom de l'auteur vient de git config.", "The author's name comes from git config."),
                new SmagxCheck { Type = SmagxCheckType.ForgeAuthor, Role = "alice", Project = "classe/agence", Branch = "main" }),
            O("auteur-bob", 1, L("La forge a un commit de Bob", "The forge has a commit by Bob"),
                L("Le nom de l'auteur vient de git config.", "The author's name comes from git config."),
                new SmagxCheck { Type = SmagxCheckType.ForgeAuthor, Role = "bob", Project = "classe/agence", Branch = "main" }),
            O("ajour-alice", 1, L("Le poste d'Alice est à jour", "Alice's workstation is up to date"),
                L("Celui qui a envoyé en premier n'a pas encore le travail de l'autre.", "The first one to send does not have the other's work yet."),
                new SmagxCheck { Type = SmagxCheckType.Synced, Role = "alice", Repo = "projet/agence", Branch = "main", Project = "classe/agence" }),
            O("ajour-bob", 1, L("Le poste de Bob est à jour", "Bob's workstation is up to date"),
                L("Celui qui a envoyé en premier n'a pas encore le travail de l'autre.", "The first one to send does not have the other's work yet."),
                new SmagxCheck { Type = SmagxCheckType.Synced, Role = "bob", Repo = "projet/agence", Branch = "main", Project = "classe/agence" })
        ]
    };

    // ─── 9. Un conflit à deux ───────────────────────────────────────────────

    public static SmagxLab PairConflict() => new()
    {
        Id = "conflit-a-deux",
        Step = 9,
        Level = LabLevel.Advanced,
        DurationMinutes = 30,
        Title = L("Un conflit à deux", "A conflict by two"),
        Statement = L(
            """
            # Un conflit à deux

            Alice et Bob ont chacun cloné `classe/agence`. Ils modifient le titre du site, chacun de son côté, sur `main` :

            - **Alice** le remplace par `<h1>Agence Méridien, voyages</h1>` ;
            - **Bob** le remplace par `<h1>Méridien : séjours</h1>`.

            Le second à envoyer sera refusé, puis tombera sur un conflit en récupérant le travail de l'autre. Après discussion, le titre retenu est **`<h1>Agence Méridien : voyages et séjours</h1>`**.

            ## La mission

            - Chacun valide son titre et l'envoie.
            - Celui qui a le conflit le résout avec le titre retenu, termine la fusion et l'envoie.
            - À la fin, les deux postes ont le titre retenu.
            """ + PairNote,
            """
            # A conflict by two

            Alice and Bob each cloned `classe/agence`. They change the site title, each on their own, on `main`:

            - **Alice** replaces it with `<h1>Agence Méridien, voyages</h1>`;
            - **Bob** replaces it with `<h1>Méridien : séjours</h1>`.

            The second one to send will be rejected, then hit a conflict when getting the other's work. After discussing, the chosen title is **`<h1>Agence Méridien : voyages et séjours</h1>`**.

            ## The mission

            - Each one commits their title and sends it.
            - The one with the conflict resolves it with the chosen title, ends the merge and sends it.
            - In the end, both workstations have the chosen title.
            """ + PairNoteEn),
        Roles =
        [
            Alice(L("Changez le titre en « Agence Méridien, voyages », validez, envoyez.", "Change the title to \"Agence Méridien, voyages\", commit, send.")),
            Bob(L("Changez le titre en « Méridien : séjours », validez, envoyez.", "Change the title to \"Méridien : séjours\", commit, send."))
        ],
        Setup = PairSetup(cloned: true),
        Objectives =
        [
            O("auteur-alice", 1, L("La forge a le commit d'Alice", "The forge has Alice's commit"),
                L("Commit, puis git push.", "Commit, then git push."),
                new SmagxCheck { Type = SmagxCheckType.ForgeAuthor, Role = "alice", Project = "classe/agence", Branch = "main" }),
            O("auteur-bob", 1, L("La forge a le commit de Bob", "The forge has Bob's commit"),
                L("Commit, puis git push.", "Commit, then git push."),
                new SmagxCheck { Type = SmagxCheckType.ForgeAuthor, Role = "bob", Project = "classe/agence", Branch = "main" }),
            O("fusion", 2, L("La fusion est sur la forge", "The merge is on the forge"),
                L("git pull réunit les deux historiques ; en cas de conflit, il s'arrête et attend votre commit.", "git pull joins both histories; on a conflict, it stops and waits for your commit."),
                new SmagxCheck { Type = SmagxCheckType.ForgeMergeCommit, Project = "classe/agence", Branch = "main" }),
            O("titre", 2, L("Le titre retenu est sur la forge", "The chosen title is on the forge"),
                L("Dans index.html, gardez une seule ligne de titre, celle retenue, sans marqueurs.", "In index.html, keep a single title line, the chosen one, without markers."),
                new SmagxCheck { Type = SmagxCheckType.ForgeFile, Project = "classe/agence", Branch = "main", File = "index.html", Text = "<h1>Agence Méridien : voyages et séjours</h1>" }),
            O("marqueurs", 0, L("Aucun marqueur de conflit sur la forge", "No conflict marker on the forge"),
                L("Les lignes <<<<<<<, ======= et >>>>>>> doivent disparaître avant le commit.", "The <<<<<<<, ======= and >>>>>>> lines must go before the commit."),
                new SmagxCheck { Type = SmagxCheckType.ForgeFile, Project = "classe/agence", Branch = "main", File = "index.html", Text = "=======", Not = true }, guard: true),
            O("titre-alice", 1, L("Le main d'Alice a le titre retenu", "Alice's main has the chosen title"),
                L("Le premier à avoir envoyé doit récupérer la fusion.", "The first one who sent must get the merge."),
                new SmagxCheck { Type = SmagxCheckType.FileInCommit, Role = "alice", Repo = "projet/agence", Branch = "main", File = "index.html", Text = "<h1>Agence Méridien : voyages et séjours</h1>" }),
            O("titre-bob", 1, L("Le main de Bob a le titre retenu", "Bob's main has the chosen title"),
                L("Le premier à avoir envoyé doit récupérer la fusion.", "The first one who sent must get the merge."),
                new SmagxCheck { Type = SmagxCheckType.FileInCommit, Role = "bob", Repo = "projet/agence", Branch = "main", File = "index.html", Text = "<h1>Agence Méridien : voyages et séjours</h1>" })
        ]
    };

    // ─── 10. Une branche relue, puis fusionnée ──────────────────────────────

    public static SmagxLab PairReview() => new()
    {
        Id = "branche-relue",
        Step = 10,
        Level = LabLevel.Advanced,
        DurationMinutes = 30,
        Title = L("Une branche relue, puis fusionnée", "A branch reviewed, then merged"),
        Statement = L(
            """
            # Une branche relue, puis fusionnée

            Alice et Bob ont déjà cloné `classe/agence`. Bob a créé une branche `tarifs` pour préparer la page des tarifs sans toucher à `main`. Alice relit avant de fusionner : c'est ce que permet une demande de fusion.

            ## La mission

            - **Bob** ajoute `tarifs.html` sur sa branche `tarifs` et publie cette branche sur la forge.
            - **Alice** récupère la branche de Bob, la relit (son historique, ses fichiers), la fusionne dans son `main` et envoie `main`.
            - **Bob** met son `main` à jour, puis supprime sa branche `tarifs`, devenue inutile.
            """ + PairNote,
            """
            # A branch reviewed, then merged

            Alice and Bob already cloned `classe/agence`. Bob created a `tarifs` branch to prepare the prices page without touching `main`. Alice reviews before merging: that is what a merge request is for.

            ## The mission

            - **Bob** adds `tarifs.html` on his `tarifs` branch and publishes this branch on the forge.
            - **Alice** gets Bob's branch, reviews it (its history, its files), merges it into her `main` and sends `main`.
            - **Bob** updates his `main`, then deletes his `tarifs` branch, no longer needed.
            """ + PairNoteEn),
        Roles =
        [
            Alice(L("Récupérez la branche tarifs de Bob, relisez-la, fusionnez-la dans main, envoyez main.", "Get Bob's tarifs branch, review it, merge it into main, send main.")),
            Bob(L("Sur votre branche tarifs, ajoutez tarifs.html et publiez la branche ; à la fin, mettez main à jour et supprimez tarifs.", "On your tarifs branch, add tarifs.html and publish the branch; in the end, update main and delete tarifs."))
        ],
        Setup = PairSetup(cloned: true) + "@bob\n$ git branch tarifs\n",
        Objectives =
        [
            O("publiee", 1, L("La branche tarifs est sur la forge", "The tarifs branch is on the forge"),
                L("git push, en nommant la branche ; la forge propose alors une demande de fusion.", "git push, naming the branch; the forge then suggests a merge request."),
                new SmagxCheck { Type = SmagxCheckType.ForgeBranch, Project = "classe/agence", Branch = "tarifs" }),
            O("fusion-alice", 2, L("Le main d'Alice contient tarifs.html", "Alice's main contains tarifs.html"),
                L("git fetch récupère origin/tarifs ; git merge le réunit à main.", "git fetch gets origin/tarifs; git merge joins it to main."),
                new SmagxCheck { Type = SmagxCheckType.FileInCommit, Role = "alice", Repo = "projet/agence", Branch = "main", File = "tarifs.html" }),
            O("forge-main", 2, L("main contient tarifs.html sur la forge", "main contains tarifs.html on the forge"),
                L("Alice envoie son main.", "Alice sends her main."),
                new SmagxCheck { Type = SmagxCheckType.ForgeFile, Project = "classe/agence", Branch = "main", File = "tarifs.html" }),
            O("auteur", 1, L("Le commit de Bob est dans main sur la forge", "Bob's commit is in main on the forge"),
                L("La fusion garde les commits de Bob, avec son nom.", "The merge keeps Bob's commits, with his name."),
                new SmagxCheck { Type = SmagxCheckType.ForgeAuthor, Role = "bob", Project = "classe/agence", Branch = "main" }),
            O("main-bob", 1, L("Le main de Bob contient tarifs.html", "Bob's main contains tarifs.html"),
                L("Sur main, git pull.", "On main, git pull."),
                new SmagxCheck { Type = SmagxCheckType.FileInCommit, Role = "bob", Repo = "projet/agence", Branch = "main", File = "tarifs.html" }),
            O("supprimee", 1, L("Bob a supprimé sa branche tarifs", "Bob deleted his tarifs branch"),
                L("Une branche se supprime une fois fusionnée, depuis une autre branche.", "A branch is deleted once merged, from another branch."),
                new SmagxCheck { Type = SmagxCheckType.BranchExists, Role = "bob", Repo = "projet/agence", Branch = "tarifs", Not = true })
        ]
    };
}
