# Exercices

Un exercice place l'apprenant dans une situation réaliste (une organisation, un rôle, un événement déclencheur et des contraintes métier), puis **corrige automatiquement** son travail grâce à l'audit, aux chemins d'attaque et à la comparaison avec le domaine de départ.

## Ouvrir un exercice

| Menu | Contenu |
|---|---|
| **Exercices > Exercices fournis** | cinq exercices généraux, bilingues |
| **Exercices > Chemins d'attaque** | douze exercices, chacun dans la langue de l'interface, avec son propre domaine |
| **Exercices > Ouvrir un exercice...** | un fichier `.smadx-lab.json` reçu d'un formateur |

La liste complète est dans le [catalogue](catalogue.md).

L'ouverture remplace le domaine actuel par le domaine de départ de l'exercice. Ses dates sont décalées pour que l'exercice se joue « aujourd'hui » : un compte connecté hier le reste, quel que soit le jour où l'on fait l'exercice.

## La fenêtre d'exercice

![Énoncé d'un exercice](../assets/screens/lab-statement.png)

- **À gauche, l'énoncé** : le contexte, votre mission et vos outils. Il ne donne pas la réponse.
- **À droite, les objectifs**, avec leurs points.
- **Indice** révèle une piste pour l'objectif.
- **Vérifier** corrige tous les objectifs sur l'état actuel du domaine.
- **Recommencer** recharge le domaine de départ.
- **Exporter le résultat** produit un compte rendu Markdown (énoncé, objectifs, résultat, nom de l'apprenant), à rendre au formateur.

## La correction

![Exercice vérifié](../assets/screens/lab-checked.png)

Chaque objectif est une vérification automatique. Un objectif non atteint explique ce qui manque. Pour un chemin d'attaque, il montre le **chemin restant**, étape par étape.

Les captures montrent un exercice de démonstration, construit pour la documentation et absent de l'application. Les solutions des exercices fournis ne sont pas publiées.

Les exercices contiennent aussi des **objectifs de garde**, déjà atteints au départ, qui refusent les solutions brutales : supprimer le compte de service, retirer au support sa délégation légitime, vider un groupe métier…

## Écrire ses propres exercices

Le [mode opératoire](creer.md) explique comment créer un exercice : depuis l'application, sans programmer, ou avec les générateurs du dépôt.
