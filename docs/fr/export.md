# Importer et exporter

## Enregistrer

**Fichier > Enregistrer...** (`Ctrl+S`) écrit le domaine et sa topologie de sites dans un fichier `.smad-x.json`. **Fichier > Exporter en JSON...** (`Ctrl+E`) produit le même contenu sous un autre nom. Le format est décrit dans [Format des fichiers](format.md).

## Depuis un vrai Active Directory : le script d'import

**Fichier > Générer le script d'import depuis AD...** (`Ctrl+I`) produit un script PowerShell à exécuter sur une machine du domaine, avec le module ActiveDirectory. Il lit la structure, les attributs de sécurité (`userAccountControl`, SPN, délégation, `adminCount`, propriétaires, dates `pwdLastSet` et `lastLogonTimestamp`), les délégations et la topologie des sites, puis écrit un `.smad-x.json` à ouvrir dans SMAD-X.

Des permissions, il garde celles qui donnent un contrôle, classées dans les catégories de SMAD-X (voir [Modéliser](modelisation.md#les-delegations)) : sur les OU, les conteneurs et le domaine, toutes ; sur un compte, un groupe, un ordinateur ou une GPO, les ACE posées sur l'objet lui-même, sans celles que sa classe donne partout (Account Operators, Cert Publishers…). Il lit aussi l'expiration du mot de passe LAPS de chaque ordinateur et qui peut le lire (ancien LAPS ou Windows LAPS, d'après le schéma de la forêt), et qui peut lire le mot de passe de chaque gMSA. Les administrateurs locaux ne sont pas dans l'annuaire : chaque ordinateur reçoit ceux d'une machine jointe (Domain Admins, Builtin Administrators sur un DC). Les sessions ne sont pas collectées : ajoutez-les dans SMAD-X pour montrer où un administrateur se connecte.

C'est la manière la plus rapide d'**auditer un domaine existant** et d'y chercher des chemins d'attaque, sans installer d'agent ni donner de droit à SMAD-X : le script ne fait que lire.

Seules les permissions qui donnent un contrôle sont reprises : contrôle total, modification des permissions ou du propriétaire, réinitialisation de mot de passe, écriture des membres d'un groupe ou de tous les attributs, réplication de l'annuaire. Les lectures et les permissions refusées (`Deny`) sont ignorées.

!!! note "Lire un annuaire, jamais l'écrire"
    SMAD-X ne produit aucun script qui modifie un vrai Active Directory : ce que l'on apprend dans le simulateur se refait à la main, en connaissance de cause, dans un domaine de test.

## Le rapport de documentation

**Fichier > Exporter le rapport de documentation...** (`Ctrl+Shift+R`) :

| Option | Choix |
|---|---|
| Contenu | tout le domaine en un fichier, ou une sélection d'objets |
| Formats | Markdown (toujours), DOCX, PDF |
| Thème | WordLike, Plain, Technical Document, GitHub, Compact, Report |

Le PDF embarque ses polices de secours (Noto, dont les emoji et les symboles) : il s'affiche de la même façon sur tous les systèmes.

## Autres exports

| Export | Où |
|---|---|
| Image de l'arbre | bouton **Exporter en PNG** |
| Délégations | fenêtre **Délégations** > **Export CSV** |
| Différences entre deux fichiers | **Analyse > Évolution du domaine...** (`Ctrl+T`), export CSV |
| Compte rendu d'exercice | fenêtre d'exercice > **Exporter le résultat** (Markdown) |
| Copie d'exercice, à rendre au formateur | fenêtre d'exercice > **Enregistrer ma copie** (`.smadx-submission.json`) |
