# Objet critique avec un propriétaire inadapté

## Ce que l'audit a détecté
Le propriétaire d'un objet critique (domaine, groupe protégé, compte privilégié, contrôleur de domaine, GPO liée au domaine ou à l'OU Domain Controllers) n'est ni un groupe d'administration, ni un compte privilégié.

## Pourquoi c'est dangereux
Le propriétaire d'un objet peut **toujours modifier ses permissions**, quelles que soient les ACL en place. Il peut donc s'accorder le contrôle total sur un administrateur ou un groupe privilégié : c'est un chemin d'attaque direct.

## Comment corriger
Rendre propriétaires Domain Admins (ou Enterprise Admins pour la forêt). Un objet créé par un compte non administrateur hérite de ce compte comme propriétaire : vérifier les délégations de création.
