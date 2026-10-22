# Compte sans préauthentification Kerberos

## Ce que l'audit a détecté
L'option « Ne pas exiger la préauthentification Kerberos » est cochée sur un compte.

## Pourquoi c'est dangereux
N'importe qui sur le réseau peut demander un TGT pour ce compte et obtenir une donnée chiffrée avec son mot de passe, puis la casser hors ligne (**AS-REP roasting**). Le compte compromis sert ensuite de point d'entrée dans le domaine.

## Comment corriger
- Décocher l'option.
- Si une application ancienne l'exige vraiment : mot de passe très long, compte sans aucun privilège, et surveillance des demandes de TGT (événement 4768 avec un type de préauthentification 0).
