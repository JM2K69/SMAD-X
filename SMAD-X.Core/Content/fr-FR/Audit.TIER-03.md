# Violation du cloisonnement : délégation à un tier moins sensible

## Ce que l'audit a détecté
Une délégation qui donne le contrôle (contrôle total, écriture d'attributs, réinitialisation de mot de passe, modification des permissions ou des membres, réplication, lecture du mot de passe LAPS…) accorde à un compte ou groupe d'un tier moins sensible des droits sur un objet d'un tier plus sensible ; ou ce compte est administrateur local d'une machine d'un tier plus sensible.

## Pourquoi c'est dangereux
Ces droits suffisent à prendre le contrôle des objets visés : réinitialiser le mot de passe d'un administrateur, modifier l'appartenance d'un groupe, écrire un attribut de délégation… C'est un **chemin de contrôle** du tier le moins sensible vers le plus sensible.

## Comment corriger
Retirer la délégation, ou la confier à un groupe du tier de l'objet visé.
