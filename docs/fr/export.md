# Importer et exporter

## Enregistrer

**Fichier > Enregistrer...** (`Ctrl+S`) écrit le domaine et sa topologie de sites dans un fichier `.smad-x.json`. **Fichier > Exporter en JSON...** (`Ctrl+E`) produit le même contenu sous un autre nom. Le format est décrit dans [Format des fichiers](format.md).

## Vers un vrai Active Directory : le script PowerShell

**Fichier > Exporter en PowerShell...** (`Ctrl+P`) génère un script qui recrée la structure dans un AD réel : OU, comptes, groupes et appartenances, GPO liées, PSO, et les attributs de sécurité modélisés.

!!! danger "Relisez avant d'exécuter"
    Le script s'exécute avec vos droits, sur l'annuaire de votre choix. Lancez-le d'abord dans un **domaine de test**, jamais en production sans l'avoir relu.

Le script est écrit pour être sûr :

- les noms sont échappés, si bien qu'une apostrophe ou un `$` dans un nom ou une description ne casse pas le script et n'est jamais interprété ;
- le fichier est encodé en UTF-8 avec BOM, pour que Windows PowerShell 5.1 lise correctement les accents ;
- les attributs de sécurité sont **ajoutés** sans écraser l'existant.

## Depuis un vrai Active Directory : le script d'import

**Fichier > Générer le script d'import depuis AD...** (`Ctrl+I`) produit un script PowerShell à exécuter sur une machine du domaine, avec le module ActiveDirectory. Il lit la structure, les attributs de sécurité (`userAccountControl`, SPN, délégation, `adminCount`, propriétaires, dates `pwdLastSet` et `lastLogonTimestamp`), les délégations et la topologie des sites, puis écrit un `.smad-x.json` à ouvrir dans SMAD-X.

C'est la manière la plus rapide d'**auditer un domaine existant** et d'y chercher des chemins d'attaque, sans installer d'agent ni donner de droit à SMAD-X : le script ne fait que lire.

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
| Image de l'arbre | bouton **Export PNG** |
| Délégations | fenêtre **Délégations** > **Export CSV** |
| Différences entre deux fichiers | **Analyse > Domain Timeline** (`Ctrl+T`), export CSV |
| Compte rendu d'exercice | fenêtre d'exercice > **Exporter le résultat** (Markdown) |
