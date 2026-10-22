# Violation du cloisonnement : délégation à un tier moins sensible

## Ce que l'audit a détecté
Une délégation (contrôle total, écriture d'attributs ou réinitialisation de mot de passe) accorde à un compte ou groupe d'un tier moins sensible des droits sur un objet d'un tier plus sensible.

## Pourquoi c'est dangereux
Ces droits suffisent à prendre le contrôle des objets visés : réinitialiser le mot de passe d'un administrateur, modifier l'appartenance d'un groupe, écrire un attribut de délégation… C'est un **chemin de contrôle** du tier le moins sensible vers le plus sensible.

## Comment corriger
Retirer la délégation, ou la confier à un groupe du tier de l'objet visé.
