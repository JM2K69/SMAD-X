# Compte privilégié hors d'un silo d'authentification appliqué

## Ce que l'audit a détecté
Le domaine a des silos d'authentification, mais un compte privilégié (membre, directement ou par imbrication, d'un groupe comme Domain Admins ou Enterprise Admins) n'est dans aucun silo, son silo est en mode audit, ou il n'est pas membre de Protected Users.

## Pourquoi c'est dangereux
Un silo appliqué n'autorise ses comptes à s'authentifier que depuis ses propres ordinateurs : les contrôleurs de domaine et les postes d'administration du Tier 0. Un administrateur du silo ne peut alors plus ouvrir de session sur un poste de travail ou un serveur moins sensible, et ses identifiants volés ne servent pas depuis une autre machine. En mode audit, le silo ne refuse rien : les refus sont seulement journalisés par les contrôleurs de domaine. Et la stratégie du silo ne restreint que Kerberos : un compte qui peut encore s'authentifier par NTLM la contourne. Hors de tout silo, le compte s'authentifie depuis n'importe quel ordinateur.

## Comment corriger
Mettre chaque compte privilégié dans le silo du Tier 0, avec les ordinateurs depuis lesquels il administre (contrôleurs de domaine, postes d'administration), l'ajouter à Protected Users pour qu'il n'utilise que Kerberos, puis passer le silo et sa stratégie en mode « appliquer », après avoir vérifié dans les journaux du mode audit qu'aucun usage légitime n'est refusé.

## Pour aller plus loin
Le modèle de Microsoft crée ses silos en mode audit et laisse à l'administrateur le soin d'y mettre les comptes d'administration. Pourquoi ne pas les appliquer dès le déploiement ?
