# Délégation Kerberos non contrainte

## Ce que l'audit a détecté
Un compte qui n'est pas un contrôleur de domaine est configuré en délégation non contrainte (TRUSTED_FOR_DELEGATION).

## Pourquoi c'est dangereux
Quand un utilisateur s'authentifie sur ce serveur, son **TGT** est copié dans la mémoire du serveur pour que celui-ci puisse agir en son nom partout dans le domaine. Un attaquant qui prend la main sur le serveur récupère les TGT de tous ceux qui s'y connectent, et peut même forcer un contrôleur de domaine à s'y authentifier (attaques de type « printer bug ») pour voler son TGT.

## Comment corriger
- Passer en **délégation contrainte** vers les seuls services nécessaires, ou en délégation contrainte basée sur la ressource.
- Protéger les comptes privilégiés : groupe **Protected Users** ou option « Le compte est sensible et ne peut pas être délégué ».

## Pour aller plus loin
La délégation non contrainte est normale sur un contrôleur de domaine, c'est pourquoi la règle les exclut.
