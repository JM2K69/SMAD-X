# Compte privilégié non protégé contre la délégation

## Ce que l'audit a détecté
Un compte privilégié n'est ni membre du groupe **Protected Users**, ni marqué « Le compte est sensible et ne peut pas être délégué ».

## Pourquoi c'est dangereux
Les identifiants d'un administrateur peuvent alors être délégués à un serveur compromis, ou laissés en mémoire sous des formes réutilisables (NTLM, DES, RC4). Un attaquant peut les rejouer (**pass-the-ticket**, pass-the-hash).

## Comment corriger
- Ajouter les comptes d'administration au groupe **Protected Users** : plus de NTLM, plus de délégation, tickets de 4 heures, pas de mise en cache des identifiants.
- À défaut, cocher « Le compte est sensible et ne peut pas être délégué ».

## Pour aller plus loin
Tester avant de généraliser : Protected Users bloque NTLM, ce qui peut casser d'anciennes applications. Ne jamais y mettre les comptes de service ni les comptes d'ordinateur.
