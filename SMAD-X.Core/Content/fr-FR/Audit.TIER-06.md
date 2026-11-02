# Objet rangé hors des OU du modèle de cloisonnement

## Ce que l'audit a détecté
Le domaine suit un modèle de cloisonnement, mais un compte, un ordinateur ou un groupe est resté hors de ses OU classées : dans le conteneur Users ou Computers, dans une ancienne OU, ou directement dans une OU de regroupement du modèle.

## Pourquoi c'est dangereux
Dans un modèle de cloisonnement, c'est l'OU qui fait le tier : elle porte les GPO de restriction de connexion, les délégations limitées à une équipe et les administrateurs locaux. Un objet rangé ailleurs n'en reçoit aucune. Un serveur laissé dans Computers n'est protégé par aucune restriction de connexion, et un compte laissé dans Users reste à la portée des délégations héritées de l'ancienne organisation.

## Comment corriger
Ranger l'objet dans l'OU de son tier et de son rôle (serveurs, postes, utilisateurs, groupes, comptes de service, comptes d'administration). Un objet dont le tier n'est pas encore décidé va dans l'OU de transit du modèle (Staging, Provisioning), le temps de le classer.

## Pour aller plus loin
HardenAD redirige la création des nouveaux comptes et ordinateurs vers son OU Provisioning (commandes redircmp et redirusr). Pourquoi est-ce utile, et que se passerait-il sans cette redirection ?
