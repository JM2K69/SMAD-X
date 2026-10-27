# Compte privilégié soumis à une politique de mot de passe faible

## Ce que l'audit a détecté
Un compte privilégié suit une politique de mot de passe (celle du domaine ou un PSO) qui accepte moins de 8 caractères, ou qui n'impose aucun renouvellement en 3 ans.

## Pourquoi c'est dangereux
Le mot de passe d'un administrateur du domaine ouvre tout l'annuaire. Court, il se casse hors ligne ; jamais renouvelé, une empreinte volée il y a des années reste valable. L'ANSSI classe ce point au niveau 2.

## Comment corriger
Appliquer aux comptes privilégiés un PSO dédié (au groupe des administrateurs) : au moins 8 caractères, et plutôt 15 ou plus, et une durée de vie maximale de 3 ans au plus. Vérifier qu'aucun PSO plus faible, de plus petite priorité, ne s'applique à eux.

## Pour aller plus loin
Un renouvellement trop fréquent est contre-productif : l'ANSSI comme le NIST rappellent qu'il pousse à choisir des mots de passe plus faibles. Pourquoi l'ANSSI fixe-t-elle tout de même un maximum de 3 ans pour les comptes privilégiés ?
