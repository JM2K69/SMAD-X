# Compte d'ordinateur dont le mot de passe ne change plus

## Ce que l'audit a détecté
Le mot de passe d'un compte d'ordinateur n'a pas changé depuis plus de 90 jours.

## Pourquoi c'est dangereux
Un ordinateur actif renouvelle son mot de passe tous les 30 jours. Au-delà de 90 jours, la machine est probablement **éteinte, volée ou sortie du parc** sans que son compte soit désactivé. Ce compte reste utilisable par quiconque a récupéré sa clé, et il encombre l'inventaire.

## Comment corriger
Désactiver le compte, puis le supprimer après une période d'observation. Mettre en place un processus de sortie de parc qui inclut l'Active Directory.

## Pour aller plus loin
Le point ANSSI vise les **serveurs** ; la règle l'applique à tous les ordinateurs, car le raisonnement est le même pour un poste de travail.
