# Mot de passe qui n'expire jamais

## Ce que l'audit a détecté
L'option « Le mot de passe n'expire jamais » est cochée sur un compte actif.

## Pourquoi c'est dangereux
Le mot de passe échappe à la politique de renouvellement du domaine. C'est fréquent sur les comptes de service, souvent oubliés, dont le mot de passe finit par circuler dans des scripts ou des documentations.

## Comment corriger
- Décocher l'option et soumettre le compte à la politique du domaine.
- Pour un compte de service : préférer un **gMSA**, dont le mot de passe change automatiquement.

## Pour aller plus loin
Le NIST (SP 800-63B) ne recommande plus le renouvellement périodique forcé pour les utilisateurs, à condition d'imposer des mots de passe longs, non compromis, et de forcer le changement en cas de fuite. L'ANSSI conserve ce point de contrôle : comparez les deux approches.
