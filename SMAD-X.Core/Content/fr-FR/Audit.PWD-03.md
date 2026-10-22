# Mot de passe non requis

## Ce que l'audit a détecté
L'option PASSWD_NOTREQD est positionnée sur un compte actif.

## Pourquoi c'est dangereux
Le compte peut recevoir un **mot de passe vide**, quelle que soit la politique de mot de passe du domaine. Un attaquant qui énumère les comptes peut tenter une connexion sans mot de passe.

## Comment corriger
Retirer l'option (`Set-ADAccountControl -PasswordNotRequired $false`) puis définir un mot de passe conforme. L'option n'est pas visible dans la console graphique : seul un audit la révèle.
