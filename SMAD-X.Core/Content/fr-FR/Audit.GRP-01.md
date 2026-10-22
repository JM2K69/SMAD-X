# Boucle d'imbrication de groupes

## Ce que l'audit a détecté
Un groupe est, par imbrication, membre de lui-même (A membre de B, B membre de A…).

## Pourquoi c'est dangereux
Les boucles rendent les droits effectifs très difficiles à comprendre et à auditer, et certains outils les gèrent mal. Elles sont presque toujours le résultat d'une erreur.

## Comment corriger
Casser la boucle et revoir l'organisation des groupes (modèle AGDLP : comptes dans des groupes globaux, eux-mêmes dans des groupes de domaine local qui portent les droits).
