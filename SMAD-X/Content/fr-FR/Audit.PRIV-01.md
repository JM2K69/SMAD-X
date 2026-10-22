# Membre d'un groupe privilégié sans adminCount

## Ce que l'audit a détecté
Un compte est membre (directement ou par imbrication) d'un groupe protégé, mais son attribut adminCount n'est pas à 1.

## Pourquoi c'est dangereux
Toutes les heures, le processus **SDProp** applique aux membres des groupes protégés les permissions de l'objet AdminSDHolder et pose adminCount = 1. Un membre sans adminCount n'a donc pas reçu cette protection : il garde les permissions héritées de son OU, et quelqu'un qui a des droits sur l'OU peut prendre le contrôle d'un administrateur.

## Comment corriger
Vérifier l'appartenance : une imbrication par un groupe de distribution ou très récente peut l'expliquer. Laisser SDProp s'exécuter (ou le déclencher) puis contrôler à nouveau.
