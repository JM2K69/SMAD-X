# Format des fichiers

## Le domaine : `.smad-x.json`

Un fichier JSON lisible, qui se versionne bien dans Git :

```json
{
  "Version": 2,
  "Domain": {
    "Name": "contoso.com",
    "Type": "Domain",
    "DistinguishedName": "DC=contoso,DC=com",
    "Children": [ { "Name": "Users", "Type": "Container", "Children": [ ] } ]
  },
  "SitesTopology": { "Sites": [ ], "SiteLinks": [ ] },
  "TierConfigurations": [ ]
}
```

| Champ | Contenu |
|---|---|
| `Version` | version du format, actuellement **2** |
| `Domain` | l'arbre des objets : nom, type, DN, tier, description Markdown, appartenances, GPO liées, délégations, attributs de sécurité, enfants |
| `SitesTopology` | sites, sous-réseaux, contrôleurs de domaine et liens de réplication |
| `TierConfigurations` | les tiers personnalisés (noms, niveaux, couleurs), s'il y en a |

### Versions

| Version | Structure |
|---|---|
| 1 | l'objet domaine directement à la racine du fichier, sans champ `Version` |
| 2 | le document ci-dessus |

À l'ouverture, un fichier de version 1 est **converti** automatiquement, et la barre d'état le signale. Un fichier écrit par une version **plus récente** de SMAD-X est refusé avec un message qui donne les deux versions, plutôt que d'être lu à moitié.

## L'exercice : `.smadx-lab.json`

Un exercice contient un énoncé, un domaine de départ complet (un document `.smad-x.json`, migré à la lecture comme un fichier normal) et des objectifs. Sa structure est décrite dans [Créer un exercice](exercices/creer.md). La clé `"Exam": true`, écrite seulement si elle est activée, met l'exercice en mode examen.

Le format v3 (SMAD-X 0.10) ajoute :

- `Questions`, les questions de compréhension : `Id`, `Title`, `Hint`, `Kind` (`SingleChoice`, `MultipleChoice`, `TrueFalse`, `Matching`, `Text`), `Points`, `Choices` (`Id`, `Text`), `Prompts` pour une association (`Id`, `Text`), `Partial` ;
- le corrigé des questions, qui n'existe que dans l'exemplaire du formateur : `Correct` sur les bons choix, `Match` sur chaque élément d'une association (l'`Id` de sa proposition), `AcceptedAnswers` pour une réponse écrite ;
- `Partial` sur un objectif : le crédit partiel.

Le **sujet** est le même fichier sans le corrigé des questions et, pour un examen, sans les indices ; les propositions d'une association y sont mélangées. Un examen qui contient encore des indices ou un corrigé est l'exemplaire du formateur : SMAD-X refuse de l'ouvrir.

## La copie : `.smadx-submission.json`

La copie d'un apprenant (`"Format": "smadx-submission"`) contient son nom (`Student`), l'identifiant de l'exercice (`LabId`), la date de préparation du domaine de départ (`StartedOn`), la date de la copie (`SavedAt`), les objectifs dont l'indice a été consulté (`HintsUsed`), l'exercice reçu (`Lab`) et le domaine de l'apprenant (`Work`, un document `.smad-x.json`). Elle ne contient pas de note : SMAD-X Studio la corrige avec l'exemplaire de l'exercice du formateur.

`ElapsedMinutes` donne le temps passé sur l'exercice, séances reprises comprises ; `EngineVersion` et `RulesVersion`, la version de SMAD-X qui a écrit la copie et l'empreinte de ses règles d'audit : Studio prévient si elles diffèrent des siennes. Ces trois champs existent depuis le format v2 (SMAD-X 0.9).

Le format v3 (SMAD-X 0.10) ajoute `Answers`, les réponses aux questions par identifiant de question (`Choices`, les choix cochés ; `Matches`, l'élément et la proposition choisie ; ou `Text`), et `StudentId`, l'identifiant facultatif de l'apprenant.

`SessionId` identifie la session de travail, tirée au hasard à l'ouverture de l'exercice : les identifiants (`Id`) des objets du domaine de travail en sont dérivés. `Seal` est le sceau : le SHA-256, en hexadécimal, de la copie sans la clé `Seal`, écrite en JSON compact. Il ne dépend pas de la mise en page, mais du moindre changement de contenu.

## Les fichiers du formateur

| Fichier | Contenu |
|---|---|
| `1-formateur/domaine-corrige.smad-x.json` | le domaine corrigé d'un exercice créé à partir de recommandations : un document `.smad-x.json`, aux dates du domaine de départ (`<id>.smadx-lab.solution.smad-x.json` pour un exercice enregistré seul) |
| `1-formateur/notes-formateur.md` | les notes du formateur (`<id>.smadx-lab.notes.md` pour un exercice enregistré seul) |
| `.smadx-marks.json` | à côté d'une copie (`"Format": "smadx-marks"`) : `LabId` et `SessionId` de la copie notée, `Questions` (par question, les `Points` donnés à la main et la réponse notée, `Answer`) et l'appréciation, `Comment` |

## Le script PowerShell d'import

**Fichier > Générer le script d'import depuis AD...** produit un script qui lit un AD et écrit un `.smad-x.json`. SMAD-X ne produit aucun script qui modifie un annuaire. Voir [Importer et exporter](export.md).
