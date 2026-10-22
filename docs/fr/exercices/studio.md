# SMAD-X Studio

**SMAD-X Studio** est l'outil du formateur : un second exécutable, livré dans la même archive que SMAD-X, qui crée des exercices, les vérifie et produit la fiche du formateur. Il fait, avec une fenêtre, ce que le [mode opératoire](creer.md) décrit à la main.

Studio ne remplace pas SMAD-X : le domaine se prépare et se corrige dans SMAD-X, l'exercice se met en forme dans Studio, et l'apprenant le fait dans SMAD-X (**Exercices > Ouvrir un exercice...**).

![Accueil de SMAD-X Studio](../assets/screens/studio-home.png)

---

## Trois façons de commencer

| Menu **Fichier** | Point de départ | Ce que Studio prépare |
|---|---|---|
| **Nouvel exercice à partir d'une recommandation...** | un manque de configuration, parmi les règles d'audit et les points de contrôle de l'ANSSI | un domaine sain et peuplé, avec ce seul manque, l'énoncé, les objectifs et les notes du formateur |
| **Nouvel exercice à partir d'un domaine (.smad-x.json)...** | un domaine préparé et enregistré dans SMAD-X | le modèle d'énoncé et les objectifs proposés par l'audit et les chemins d'attaque |
| **Convertir un scénario en exercice...** | un scénario `.smad-x.json` dont la description suit le plan *Contexte / Le chemin / Le corriger* | l'énoncé tiré du contexte ; le chemin et la correction passent dans les notes du formateur |

**Ouvrir un exercice...** reprend un fichier `.smadx-lab.json` existant, avec ses notes.

### À partir d'une recommandation

![Choix de la recommandation](../assets/screens/studio-recipe.png)

Chaque règle d'audit a sa recette, avec le point de contrôle ANSSI correspondant et son niveau (N1 à N4). La liste se filtre par identifiant de règle, par point ANSSI ou par mot du titre. Choisissez la langue de l'exercice et le nom du domaine : Studio construit un annuaire réaliste (services, utilisateurs, postes), vérifie qu'il est sain, puis y injecte **uniquement** le manque choisi.

#### Combiner plusieurs recommandations

Cochez plusieurs lignes pour réunir leurs manques **dans le même domaine** : l'exercice reçoit un objectif par règle visée, les objectifs proposés par l'audit et les chemins d'attaque, et les gardes de chaque recette. Le niveau et la durée augmentent avec le nombre de manques, et les notes du formateur ont une section par règle. Sans case cochée, c'est la ligne surlignée qui est utilisée ; **Tout décocher** revient à une seule recommandation.

Certaines recettes ne vont pas ensemble : deux recettes qui créent le même objet (KRB-01 et KRB-02 créent toutes deux le compte de service de l'application), ou une recette qui annule le manque d'une autre. Studio l'explique sous la liste et désactive **Créer l'exercice** tant que la combinaison n'est pas possible.

L'énoncé produit reste général (« vérifiez la conformité avant l'audit annuel ») : il ne nomme ni la règle ni l'objet à corriger. À vous de l'enrichir avec le contexte de votre organisation.

---

## Les onglets

### Exercice

![Onglet Exercice](../assets/screens/studio-lab.png)

Titre, identifiant (le nom du fichier), langue, série du menu (exercices généraux ou chemins d'attaque), niveau et durée. Le résumé du domaine de départ indique le nombre d'objets, les chemins d'attaque hors Tier 0 et le score d'audit.

Un exercice est **bilingue** ou n'existe que dans **une langue**. La liste *Langue des textes en cours d'édition*, en haut de la fenêtre, choisit les textes que vous modifiez.

### Énoncé

![Onglet Énoncé](../assets/screens/studio-statement.png)

L'énoncé s'écrit en Markdown, avec un aperçu tel que l'apprenant le verra. **Insérer le modèle** pose le plan *Contexte / Votre mission / Vos outils*.

!!! warning "L'énoncé ne donne jamais la réponse"
    Il décrit la situation, la mission et les contraintes. Le chemin d'attaque et la correction vont dans les **notes du formateur**. La validation refuse un énoncé qui contient une section de solution.

### Objectifs

![Onglet Objectifs](../assets/screens/studio-objectives.png)

À gauche, la liste des objectifs et le total des points ; à droite, l'objectif sélectionné : titre, indice, points et vérification. Seuls les champs utiles à la vérification choisie s'affichent, et les noms d'objets se complètent à partir du domaine de départ. Les vérifications disponibles sont décrites dans le [mode opératoire](creer.md).

**Proposer des objectifs** analyse le domaine de départ et propose ce qui n'est pas encore dans l'exercice :

![Propositions d'objectifs](../assets/screens/studio-suggestions.png)

- un objectif par chemin d'attaque hors Tier 0, et « plus aucun chemin vers le contrôle du domaine » ;
- un objectif par règle d'audit en échec ;
- des **gardes**, déjà atteintes au départ, qui refusent une solution brutale (supprimer le compte au lieu de le corriger).

Les propositions recommandées sont cochées ; **Ajouter la sélection** les ajoute à la liste.

### Validation

![Onglet Validation](../assets/screens/studio-validation.png)

**Valider l'exercice** fait les vérifications des tests des exercices fournis :

- textes présents dans chaque langue de l'exercice, identifiants uniques, points positifs ;
- aucun objectif principal atteint au départ, toutes les gardes atteintes, et chaque garde réellement efficace ;
- objets et règles cités qui existent dans le domaine de départ ;
- aucune solution visible par l'apprenant, ni dans l'énoncé, ni dans les descriptions du domaine.

Pour aller plus loin, corrigez le domaine dans SMAD-X comme le ferait un apprenant, enregistrez-le, puis **Choisir le domaine corrigé (solution)...** : Studio vérifie que cette correction obtient tous les points.

### Fiche formateur

Les **notes du formateur** (la solution, les points de vigilance) et l'aperçu de la fiche formateur : objectifs, gardes, chemins d'attaque et audit du domaine de départ, puis les notes. **Exporter la fiche formateur...** l'enregistre en Markdown.

---

## Les fichiers

| Fichier | Contenu | À donner aux apprenants |
|---|---|---|
| `<id>.smadx-lab.json` | l'exercice : énoncé, domaine de départ, objectifs | **oui** |
| `<id>.smadx-lab.notes.md` | les notes du formateur, enregistrées à côté de l'exercice | **non** |
| `<id>.formateur.md` | la fiche formateur exportée | **non** |

Les notes ne sont jamais écrites dans l'exercice : distribuer le seul fichier `.smadx-lab.json` ne révèle pas la solution.

---

## En ligne de commande

Studio produit aussi des exercices en série, sans fenêtre :

```bash
SMAD-X.Studio list-recipes --lang fr
```

```bash
SMAD-X.Studio recipe KRB-01 DLG-02 --lang fr --domain formation.local --out exercices
```

```bash
SMAD-X.Studio recipe KRB-01 DLG-02 ACL-01 --combine --lang fr --out exercices
```

```bash
SMAD-X.Studio recipe all --lang en --out labs
```

```bash
SMAD-X.Studio convert scenarios --lang fr --out exercices
```

```bash
SMAD-X.Studio validate exercices --solution corrige.smad-x.json
```

| Commande | Rôle |
|---|---|
| `list-recipes` | liste les recettes : règle, gravité, point ANSSI, titre |
| `recipe <règle>...` ou `recipe all` | crée un exercice par recette ; avec `--combine`, un seul exercice qui les réunit |
| `convert <fichier ou dossier>...` | convertit des scénarios `.smad-x.json` |
| `validate <fichier ou dossier>...` | valide des exercices, avec un domaine corrigé facultatif |

| Option | Effet |
|---|---|
| `--lang fr` ou `--lang en` | langue de l'exercice et des messages |
| `--domain <nom>` | nom du domaine créé par une recette (`corp.example.lab` par défaut) |
| `--out <dossier>` | dossier de sortie (le dossier courant par défaut) |
| `--date aaaa-mm-jj` | date de référence : la même date donne le même fichier, octet pour octet |
| `--solution <fichier>` | domaine corrigé à noter pendant la validation |
| `--combine` | `recipe` : un seul exercice avec tous les manques dans le même domaine |
| `--verbose` | affiche aussi les vérifications réussies |

Chaque exercice écrit est validé aussitôt. Le code de sortie vaut **0** si tout va bien, **1** si un exercice a des erreurs ou si les recettes ne peuvent pas être combinées (il est quand même écrit, pour être corrigé dans Studio) et **2** pour une commande incorrecte.
