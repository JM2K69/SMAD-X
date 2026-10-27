# Tout utilisateur peut joindre des ordinateurs au domaine

## Ce que l'audit a détecté
L'attribut ms-DS-MachineAccountQuota du domaine vaut plus que 0 : tout utilisateur authentifié peut créer des comptes d'ordinateur, 10 par défaut.

## Pourquoi c'est dangereux
Le créateur d'un compte d'ordinateur en devient le propriétaire, avec un contrôle total sur lui. Un attaquant qui détient le moindre compte peut ainsi fabriquer des ordinateurs à sa main : c'est la première étape de la délégation contrainte basée sur les ressources (RBCD) et de l'attaque noPac (CVE-2021-42287). L'ANSSI classe ce point au niveau 4.

## Comment corriger
Mettre le quota à 0 (sélectionnez le domaine, section « Politique de mot de passe »), puis déléguer la jonction de postes à un groupe dédié, sur l'OU des postes de travail (droit « Créer des objets ordinateur »).
