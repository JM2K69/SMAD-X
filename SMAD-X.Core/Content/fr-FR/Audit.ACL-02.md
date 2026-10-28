# Chemin de contrôle vers le domaine par les permissions

## Ce que l'audit a détecté
Depuis un compte ou un groupe qui n'est pas privilégié (ou depuis n'importe quel utilisateur du domaine), une suite de permissions mène au contrôle du domaine. Le constat porte sur la **première permission** du chemin : l'objet qu'elle permet de contrôler, et qui la détient.

## Pourquoi c'est dangereux
Aucune de ces permissions n'a l'air grave seule : réinitialiser un mot de passe, écrire un attribut, modifier les membres d'un groupe, être propriétaire d'une OU. Enchaînées, elles donnent le domaine, et c'est ainsi que les outils d'attaque (BloodHound, ADCP de l'ANSSI) les recherchent. Le chemin complet s'affiche dans **Analyse > Chemins d'attaque**.

## Comment corriger
Couper le chemin à sa première permission : retirer l'ACE, la confier à un groupe d'administration du tier de l'objet visé, ou rendre l'objet protégé (un compte privilégié doit être membre d'un groupe protégé, avec adminCount à 1). Vérifier ensuite que plus aucun chemin ne part de la même source.
