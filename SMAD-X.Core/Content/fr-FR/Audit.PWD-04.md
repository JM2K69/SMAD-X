# Mot de passe d'un compte privilégié inchangé depuis plus de 3 ans

## Ce que l'audit a détecté
Le mot de passe d'un compte privilégié (pwdLastSet) n'a pas changé depuis plus de trois ans.

## Pourquoi c'est dangereux
Plus un mot de passe d'administrateur est ancien, plus il a eu d'occasions de fuiter, et plus sa forme stockée (hash NTLM, éventuellement ancien algorithme) a de chances d'avoir été copiée.

## Comment corriger
Changer le mot de passe et appliquer une politique de mot de passe dédiée aux comptes d'administration (PSO).
