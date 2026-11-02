# Objet en attente dans une OU de transit

## Ce que l'audit a détecté
Un compte, un ordinateur ou un groupe se trouve dans une OU de transit du modèle de cloisonnement : Staging (serveurs et PAW à préparer), Quarantine (ordinateurs isolés) ou Provisioning (nouveaux objets).

## Pourquoi c'est dangereux
Une OU de transit sert à attendre, pas à travailler : elle porte des restrictions faites pour un objet en cours d'installation ou isolé, pas celles de son tier définitif. Un serveur oublié dans Staging fonctionne sans les GPO et les administrateurs locaux de son tier ; un ordinateur oublié en quarantaine finit souvent par être déplacé sans vérification.

## Comment corriger
Terminer la préparation de l'objet, puis le ranger dans l'OU de son tier ; ou le désactiver et le placer dans l'OU des objets désactivés s'il ne sert plus.

## Pour aller plus loin
Pourquoi les modèles créent-ils des OU de transit plutôt que de laisser les objets dans les conteneurs Users et Computers en attendant leur classement ?
