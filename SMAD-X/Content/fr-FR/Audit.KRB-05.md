# Mot de passe du compte krbtgt trop ancien

## Ce que l'audit a détecté
Le mot de passe du compte `krbtgt` n'a pas été changé depuis plus d'un an.

## Pourquoi c'est dangereux
Tous les tickets Kerberos (TGT) du domaine sont signés avec la clé de `krbtgt`. Un attaquant qui l'a volée un jour (par exemple via DCSync) peut forger des **Golden Tickets** : des tickets d'administrateur valides aussi longtemps que cette clé ne change pas. Ne jamais renouveler `krbtgt`, c'est laisser une porte ouverte permanente après une compromission ancienne.

## Comment corriger
- Renouveler le mot de passe **deux fois**, en laissant passer la durée de vie maximale d'un ticket (10 heures par défaut) entre les deux : le domaine garde l'ancienne clé en mémoire.
- Le faire au moins une fois par an, et immédiatement après toute compromission.
- Microsoft fournit une procédure et un script de réinitialisation (voir références).
