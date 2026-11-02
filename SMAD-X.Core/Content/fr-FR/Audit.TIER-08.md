# Compte ou groupe privilégié rangé hors du Tier 0

## Ce que l'audit a détecté
Un compte ou un groupe qui contrôle le domaine (membre, directement ou par imbrication, d'un groupe comme Domain Admins, Enterprise Admins ou Account Operators) est rangé dans une OU d'un tier moins sensible du modèle de cloisonnement : celle des utilisateurs, des serveurs ou d'un tier Legacy.

## Pourquoi c'est dangereux
Les OU d'un tier moins sensible sont administrées par les équipes de ce tier : le support réinitialise les mots de passe des utilisateurs, les administrateurs des serveurs gèrent leurs comptes de service. Un compte du Tier 0 rangé chez eux reçoit leurs GPO et se trouve à la portée de leurs délégations, sauf la protection d'AdminSDHolder, qui ne couvre ni les GPO ni les liens de GPO posés sur l'OU. C'est le contraire du cloisonnement : un tier moins sensible agit sur le Tier 0.

## Comment corriger
Ranger le compte ou le groupe dans l'OU des comptes ou des groupes d'administration du Tier 0. Si c'est un compte de travail quotidien devenu privilégié, le retirer du groupe privilégié et créer un compte d'administration distinct, réservé au Tier 0.

## Pour aller plus loin
Le modèle Microsoft et HardenAD séparent les comptes d'administration de chaque tier dans une OU dédiée. Pourquoi un administrateur a-t-il alors besoin de plusieurs comptes ?
