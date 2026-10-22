# Compte de service managé dont le mot de passe ne change plus

## Ce que l'audit a détecté
Le mot de passe d'un gMSA n'a pas changé depuis plus de 90 jours.

## Pourquoi c'est dangereux
Un gMSA renouvelle normalement son mot de passe automatiquement (tous les 30 jours par défaut). S'il ne le fait plus, c'est que le compte n'est plus utilisé, ou que le renouvellement a été désactivé : on perd tout l'intérêt du gMSA.

## Comment corriger
Vérifier que le service tourne encore ; sinon supprimer le gMSA. Contrôler l'intervalle de renouvellement (msDS-ManagedPasswordInterval).
