# Créer un exercice SMAD-X

Un exercice (« lab ») est un fichier `.smadx-lab.json`. Il contient un énoncé, un domaine de départ et des objectifs que SMAD-X corrige automatiquement, grâce à l'audit de sécurité, aux chemins d'attaque et à la comparaison avec le domaine de départ.

Ce mode opératoire va de l'idée au fichier testé. Les exercices fournis avec l'application (menu **Exercices**) ont été écrits ainsi, et servent de modèles.

---

## 1. Choisir la méthode

| Méthode | Pour qui | Résultat |
|---|---|---|
| **A. Un domaine fait à la main** | un formateur, sans programmer | un domaine préparé dans SMAD-X, mis en forme dans SMAD-X Studio |
| **B. Par un générateur** | un contributeur du dépôt | un exercice embarqué dans l'exe, testé automatiquement |
| **C. Tout dans [SMAD-X Studio](studio.md)** | un formateur | un exercice tiré d'une recommandation de l'ANSSI ou d'un scénario, dans une fenêtre ou en ligne de commande |

Tous produisent le même format : on peut commencer par A ou C et finir par B.

---

## 2. Méthode A : un domaine fait à la main

### 2.1 Préparer le domaine de départ
1. **Fichier > Nouveau domaine** (ou ouvrez un domaine enregistré).
2. Construisez la situation de départ : OU, comptes, groupes, délégations, options de la section **Sécurité**.
3. Ajoutez la ou les **faiblesses** que l'apprenant devra trouver. Vérifiez qu'elles se voient :
   - **Analyse > Audit de sécurité** (`Ctrl+Shift+A`) : le constat attendu apparaît ;
   - **Analyse > Chemins d'attaque** (`Ctrl+Shift+K`) : le chemin attendu apparaît.
4. Peuplez le domaine comme un vrai AD (services, utilisateurs, postes) : la faiblesse doit être à chercher, pas posée seule au milieu du vide.
5. Enregistrez le domaine (`.smad-x.json`), pour pouvoir y revenir.

### 2.2 Ouvrir le domaine dans SMAD-X Studio
Lancez [SMAD-X Studio](studio.md), livré dans la même archive que SMAD-X, puis **Fichier > Nouvel exercice à partir d'un domaine (.smad-x.json)...** et choisissez le domaine enregistré.

Studio en fait le domaine de départ d'un nouvel exercice, pose le modèle d'énoncé et propose les objectifs tirés de l'audit et des chemins d'attaque.

### 2.3 Compléter l'exercice dans Studio
- onglet **Exercice** : titre, identifiant, niveau, durée et, si besoin, mode examen ;
- onglet **Énoncé** : l'énoncé en Markdown (voir § 4) ;
- onglet **Objectifs** : gardez les objectifs utiles, ajoutez les autres et les gardes (voir § 5) ;
- onglet **Questions**, facultatif : des questions de compréhension (choix unique ou multiple, oui / non, association, réponse écrite) ;
- onglet **Validation** : Studio vérifie l'exercice comme le font les tests des exercices fournis ;
- **Fichier > Enregistrer dans un dossier...** : Studio range l'exercice dans un dossier à son nom, par rôle : votre exemplaire et ses notes dans `1-formateur`, le sujet à distribuer (sans le corrigé des questions et, pour un examen, sans les indices) dans `2-a-distribuer` ; les copies rendues iront dans `3-copies-rendues` (voir [SMAD-X Studio](studio.md#les-fichiers)).

Le fichier reste du JSON lisible (voir § 3). Ne modifiez pas `Start` à la main : refaites plutôt le domaine dans SMAD-X et renvoyez-le dans Studio.

### 2.4 Tester
1. **Exercices > Ouvrir un exercice...** et choisissez le fichier.
2. Cliquez sur **Vérifier** sans rien changer : aucun objectif principal ne doit être atteint, et les objectifs de garde (voir § 5.2) doivent déjà l'être.
3. Faites la correction attendue, puis **Vérifier** : tout doit être atteint.
4. **Recommencer**, puis essayez les mauvaises solutions (supprimer le compte, vider le groupe…) : un objectif doit échouer à chaque fois.
5. Indiquez un nom, puis **Enregistrer ma copie** : ajoutez cette copie dans l'onglet **Copies** de Studio pour vérifier la note qu'obtiendra l'apprenant.

---

## 3. Le format `.smadx-lab.json`

```json
{
  "Format": "smadx-lab",
  "FormatVersion": 1,
  "Id": "mon-exercice",
  "Title": { "fr-FR": "Contoso : la revue des droits" },
  "Language": "fr-FR",
  "Category": "attack-paths",
  "Level": "Intermediate",
  "DurationMinutes": 25,
  "Statement": { "fr-FR": "# Titre\n\n## Contexte\n..." },
  "ReferenceDate": "2026-10-22T00:00:00",
  "Start": { "Version": 2, "Domain": { "...": "..." }, "SitesTopology": { "...": "..." } },
  "Objectives": [
    {
      "Id": "o1",
      "Points": 4,
      "Title": { "fr-FR": "Le chemin depuis « GG-Equipe » est coupé" },
      "Hint":  { "fr-FR": "Regardez de quels groupes GG-Equipe est membre." },
      "Check": { "Type": "NoAttackPath", "Object": "GG-Equipe" }
    }
  ]
}
```

| Champ | Rôle |
|---|---|
| `Format`, `FormatVersion` | `"smadx-lab"` et `3` (v2 : gardes marquées, réglages d'examen ; v3 : questions et crédit partiel). Un fichier v1 est lu et ses gardes reconnues ; un fichier d'une version plus récente est refusé avec un message clair. |
| `EngineVersion` | Version de SMAD-X qui a écrit l'exercice ; la correction dépend de ses règles d'audit. |
| `Exam`, `ExamCheck`, `ExamClosedTools` | Facultatifs. Mode examen, « Vérifier » permis en examen, outils fermés (`Audit`, `FixGuidance`, `AttackPaths`, `Comparison`). |
| `Id` | Identifiant stable, sans espace. Il sert aussi au nom du compte rendu. |
| `Title`, `Statement` | Textes par langue (`fr-FR`, `en-US`). Si la langue de l'interface manque, SMAD-X prend le français, puis le premier texte. |
| `Language` | Facultatif. À renseigner quand le **domaine** est écrit dans une seule langue (noms d'OU, de comptes…) : le menu ne propose alors l'exercice que dans cette langue. Laisser vide pour un exercice bilingue. |
| `Category` | Facultatif. `"attack-paths"` range l'exercice dans **Exercices > Chemins d'attaque** ; vide, il va dans **Exercices fournis**. |
| `Level` | `Beginner`, `Intermediate` ou `Advanced`. |
| `DurationMinutes` | Durée indicative. |
| `ReferenceDate` | Date à laquelle les dates du domaine ont été figées. À l'ouverture, toutes les dates sont décalées de « aujourd'hui − ReferenceDate » : un compte connecté « hier » le restera, quel que soit le jour où l'exercice est fait. |
| `Start` | Le domaine de départ : un document `.smad-x.json` complet, migré à la lecture comme un fichier normal. |
| `Objectives` | Les objectifs corrigés (§ 5). Le total des points est calculé, sans les gardes. |
| `Questions` | Facultatif. Les questions de compréhension, avec leur corrigé dans l'exemplaire du formateur (voir [Format des fichiers](../format.md)). |

---

## 4. Écrire un bon énoncé

L'énoncé est en Markdown : titres `#`, listes, **gras**, `code`, liens. Structure recommandée :

```markdown
# Organisation : le problème en quelques mots

## Contexte
Qui est l'organisation (activité, taille), qui vous êtes (administrateur arrivé il y a un mois,
prestataire, auditeur), et l'événement qui déclenche le travail : test d'intrusion, alerte de l'EDR,
bulletin du CERT-FR, audit avant certification, passation avec un ancien administrateur…

## Votre mission
- Ce qu'il faut trouver et corriger.
- Les contraintes métier : « l'application métier doit continuer de tourner », « l'équipe
  garde ses délégations ».

## Vos outils
- Analyse > Chemins d'attaque, Audit de sécurité, Avant / après…
```

Règles :
- **Ne donnez pas la réponse** : pas de chemin détaillé ni de correction dans l'énoncé. Ces éléments vont dans les **indices** des objectifs, que l'apprenant révèle un à un.
- **Donnez des contraintes réalistes** : ce sont elles qui empêchent la solution brutale (« supprimer le compte ») et obligent à comprendre.
- **Ne publiez pas les solutions** : gardez-les pour vous, ou dans les tests du dépôt.
- **Une organisation crédible et fictive** : utilisez des noms fictifs (Contoso, Wide World Importers… ou des noms français inventés). Jamais une vraie organisation.
- **Un domaine par exercice** : changez le nom de domaine d'un exercice à l'autre.

---

## 5. Écrire les objectifs

Chaque objectif a un `Id` unique (`o1`, `o2`…), des `Points`, un `Title`, un `Hint` et une vérification `Check`.

### 5.1 Les vérifications disponibles

| `Type` | Champs utilisés | Réussi quand |
|---|---|---|
| `RuleClear` | `RuleId`, `Object` (facultatif) | la règle d'audit ne remonte plus de constat (sur cet objet si `Object` est donné) |
| `NoFindingAtOrAbove` | `Severity` (`Critical`, `High`, `Medium`, `Low`) | plus aucun constat de cette gravité ou plus grave |
| `ScoreAtLeast` | `Score` | le score d'audit atteint au moins `Score` |
| `NoAttackPath` | `Object` (facultatif) | plus aucun chemin vers le contrôle du domaine depuis `Object` ; `"any-domain-user"` vise « n'importe quel utilisateur » ; sans `Object`, depuis aucune source hors Tier 0 |
| `ObjectExists` / `ObjectAbsent` | `Object`, `ObjectType` (facultatif) | l'objet existe / n'existe plus |
| `MemberOf` / `NotMemberOf` | `Object`, `Group`, `Effective` | l'objet est / n'est plus membre du groupe ; `Effective: true` compte l'imbrication, `false` seulement l'appartenance directe |
| `HasFlag` / `NotHasFlag` | `Object`, `Flag` (ex. `DontRequirePreauth`) | l'option de compte est / n'est plus activée |
| `TierIs` | `Object`, `Tier` | l'objet est classé dans ce tier |
| `DelegationExists` | `Trustee`, `Target`, `RightCategory` | le mandataire a une délégation directe de cette catégorie sur la cible |
| `DelegationOnly` | `Trustee`, `AllowedCategories` | le mandataire a au moins une délégation, toutes dans les catégories autorisées |
| `NotModified` | `Object` | l'objet n'a subi aucun changement par rapport au départ |

Les identifiants de règles d'audit (`KRB-01`, `DLG-01`, `PRIV-04`…) sont ceux de la fenêtre Audit ; chaque fiche y donne ses références ANSSI, NIST et MITRE. `Object` désigne un objet par son nom, qui doit être unique dans le domaine de l'exercice.

### 5.2 Les objectifs de garde
Un bon exercice combine :
1. **les objectifs principaux** : couper le chemin (`NoAttackPath`), lever la règle d'audit (`RuleClear`) ;
2. **les gardes** (`"Guard": true`, 0 point), déjà atteintes au départ, qui échouent si l'apprenant « triche » ; une garde violée annule les objectifs qui concernaient l'objet protégé au départ (son constat, les chemins qui le traversaient, les vérifications qui le nomment) :
   - `ObjectExists` sur le compte de service, le serveur ou le contrôleur de domaine qu'il ne faut pas supprimer ;
   - `MemberOf` sur un utilisateur qui doit rester dans son équipe ;
   - `DelegationExists` sur une délégation légitime (le support garde la réinitialisation) ;
   - `NotModified` sur un objet auquel il ne faut pas toucher.

Pensez aussi à l'objectif « **plus aucun chemin hors Tier 0** » (`NoAttackPath` sans `Object`) : il montre qu'un seul maillon coupé ne suffit pas toujours. Quand il échoue, le correcteur affiche le chemin restant étape par étape.

### 5.3 Points et indices
- 3 à 4 points pour l'objectif principal, 2 pour l'objectif « hors Tier 0 » et les règles critiques ; une garde ne rapporte rien.
- **Crédit partiel** (`"Partial": true`) pour un objectif qui se compte (constats d'une règle sur tout le domaine, gravité, score, chemins hors Tier 0) : quand il y a plusieurs comptes à corriger, l'apprenant qui en corrige une partie obtient la part correspondante des points.
- Un indice par objectif, qui met sur la piste sans donner la manipulation exacte : « Regardez de quels groupes X est lui-même membre » plutôt que « Retirez X de Domain Admins ».

---

## 6. Méthode B : un exercice fourni avec l'application

Les exercices fournis sont dans `SMAD-X.Core/Labs/`, embarqués dans l'exe (voir `SMAD-X.Core.csproj`). Ils sont **générés**, jamais écrits à la main :

| Générateur | Exercices | Commande |
|---|---|---|
| `tools/LabGenerator` | exercices généraux bilingues (`01-premier-audit`…) | `dotnet run --project tools/LabGenerator -- SMAD-X.Core/Labs` |
| `tools/SampleGenerator` | série « Chemins d'attaque », un fichier par langue (`chemin-*`, `path-*`), plus les exemples de `Samples/AttackPaths` | `dotnet run --project tools/SampleGenerator -- Samples/AttackPaths SMAD-X.Core/Labs` |

Pour ajouter un scénario de chemin d'attaque dans `tools/SampleGenerator/Program.cs` :
1. Ajoutez un appel `Scenario("13-mon-scenario", "domaine-fr.local", "domaine-en.local", d => { ... })`, avec un nom de domaine différent dans chaque langue.
2. Dans la fonction, créez les objets avec `Ou`, `User`, `Group`, `Computer`, `Delegate`. Écrivez les noms et les descriptions avec `T("français", "english")`.
3. Décrivez l'exercice avec `Lab(titre, niveau, minutes, contexte, mission, objectifs...)`, en utilisant `CutPath`, `NoPathLeft`, `RuleClear`, `Exists` et `Obj`.
4. Renvoyez le titre et l'explication de l'exemple, avec les sections `## Le chemin` et `## Le corriger`.
5. Régénérez, puis lancez les tests.

Le générateur est **déterministe** : identifiants tirés du DN, dates figées avant la date de référence, langue fixée par fichier. Une régénération ne modifie donc que ce qui a réellement changé. Le domaine de départ est vérifié : le générateur s'arrête si le domaine « sain », avant ajout de la faiblesse, remonte un constat d'audit.

### Tests automatiques
Chaque exercice fourni doit avoir ses tests (modèles : `SMAD-X.Tests/LabTests.cs` et `SMAD-X.Tests/AttackPathLabTests.cs`) :
- **non résolu au départ** : les objectifs principaux échouent, les gardes réussissent ;
- **le raccourci ne paie pas** : supprimer l'objet qu'une garde protège rapporte au plus la moitié des points ;
- **la solution obtient tous les points** ;
- **chaque raccourci est refusé** : supprimer l'objet gardé, retirer le membre, retirer la délégation légitime ;
- **l'énoncé ne contient pas la réponse** et chaque objectif a un indice.

```bash
dotnet test SMAD-X.slnx
```

---

## 7. Liste de contrôle

- [ ] Le domaine porte un nom propre à l'exercice, et il est peuplé comme un vrai AD.
- [ ] La faiblesse se voit dans l'audit ou dans les chemins d'attaque.
- [ ] L'énoncé donne un contexte réaliste, une mission et des contraintes, mais pas la réponse.
- [ ] Chaque objectif a un indice ; les points reflètent l'importance.
- [ ] Des objectifs de garde interdisent les solutions brutales.
- [ ] Au départ : objectifs principaux en échec, gardes atteintes.
- [ ] La solution attendue obtient tous les points ; chaque raccourci fait échouer une garde.
- [ ] `Language` est renseigné si le domaine n'existe que dans une langue ; `Category` si l'exercice appartient à une série.
- [ ] Pour un exercice fourni : généré, testé, et `dotnet test` est vert.
