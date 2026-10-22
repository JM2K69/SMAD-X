# Groupe protégé classé hors du Tier 0

## Ce que l'audit a détecté
Un groupe protégé par AdminSDHolder est classé dans un tier moins sensible que le Tier 0.

## Pourquoi c'est dangereux
Ces groupes donnent, directement ou par des chemins connus, le contrôle des contrôleurs de domaine. Les classer en Tier 1 ou 2 conduit à les administrer depuis des postes moins protégés et à y mettre des comptes moins surveillés.

## Comment corriger
Classer le groupe en Tier 0, et appliquer à ses membres les règles du Tier 0 : comptes dédiés, postes d'administration dédiés, pas de connexion sur les tiers inférieurs.
