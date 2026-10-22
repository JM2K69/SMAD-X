# Compte utilisateur vulnérable au Kerberoasting

## Ce que l'audit a détecté
Un compte **utilisateur** (non privilégié) porte un SPN : c'est généralement un compte de service.

## Pourquoi c'est dangereux
Comme pour les comptes privilégiés, tout utilisateur du domaine peut obtenir un ticket chiffré avec le mot de passe de ce compte et le casser hors ligne. Les comptes de service ont souvent des mots de passe anciens, jamais changés, et des droits importants sur les serveurs qu'ils font tourner.

## Comment corriger
- Migrer le service vers un **gMSA** quand l'application le permet.
- Sinon : mot de passe long et aléatoire, chiffrement AES, et inventaire des comptes de service (CIS 5.5).
- Supprimer les SPN qui ne correspondent plus à aucun service.

## Pour aller plus loin
Un gMSA porte souvent un SPN sans risque : son mot de passe ne peut pas être cassé en pratique. C'est pourquoi la règle ne vise que les comptes utilisateurs.
