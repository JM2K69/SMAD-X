# Groupe privilégié qui devrait être vide

## Ce que l'audit a détecté
Un groupe qui devrait rester vide hors opérations ponctuelles a des membres : Schema Admins, Enterprise Admins ou un groupe « Operators ».

## Pourquoi c'est dangereux
- **Schema Admins** et **Enterprise Admins** donnent le contrôle de toute la forêt : ils ne doivent être remplis que le temps d'une opération.
- **Account, Server, Print et Backup Operators** sont des groupes anciens aux droits mal compris : ils permettent d'ouvrir une session sur les DC et, par plusieurs chemins connus, de devenir administrateur du domaine.

## Comment corriger
Vider ces groupes et attribuer les droits nécessaires par délégation fine. Ajouter temporairement un compte à Schema Admins seulement pour une extension de schéma, puis le retirer.

## Pour aller plus loin
Dans un domaine neuf, le compte Administrator est membre d'Enterprise Admins et de Schema Admins : l'ANSSI recommande de les vider quand même.
