# Compte dormant

## Ce que l'audit a détecté
Un compte utilisateur actif ne s'est pas connecté depuis plus de 45 jours (ou jamais).

## Pourquoi c'est dangereux
Un compte dormant appartient souvent à une personne partie, à un prestataire ou à un test oublié. Personne ne remarquera qu'il est utilisé par un attaquant, et son mot de passe n'est plus surveillé.

## Comment corriger
- Désactiver le compte, puis le supprimer après une période d'observation.
- Relier le cycle de vie des comptes au processus RH (arrivées, départs, mobilité).

## Pour aller plus loin
Le seuil de 45 jours est celui des CIS Controls (5.3). L'attribut lastLogonTimestamp n'est répliqué qu'avec un retard de 9 à 14 jours : il donne un ordre de grandeur, pas une date exacte.
