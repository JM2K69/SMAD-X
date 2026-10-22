# Mot de passe machine d'un contrôleur de domaine inchangé

## Ce que l'audit a détecté
Le mot de passe du compte d'ordinateur d'un DC n'a pas changé depuis plus de 45 jours.

## Pourquoi c'est dangereux
Un ordinateur renouvelle normalement son mot de passe tous les 30 jours. Un DC qui ne le fait plus est soit **éteint ou abandonné** (mais toujours connu du domaine), soit dans un état anormal (renouvellement désactivé, problème de réplication). Une clé machine figée facilite la fabrication de tickets de service (Silver Tickets) après une compromission.

## Comment corriger
Vérifier l'état du DC : le remettre en service, le rétrograder proprement s'il est abandonné, ou corriger la stratégie qui bloque le renouvellement.
