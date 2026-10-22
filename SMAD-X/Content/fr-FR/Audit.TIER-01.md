# Violation du cloisonnement : appartenance à un groupe plus sensible

## Ce que l'audit a détecté
Un compte ou un groupe d'un tier est membre (directement ou par imbrication) d'un groupe d'un tier plus sensible.

## Pourquoi c'est dangereux
Le modèle en tiers repose sur une règle simple : **un tier ne doit jamais pouvoir contrôler un tier plus sensible**. Un compte de Tier 2, utilisé sur des postes exposés, qui devient membre d'un groupe de Tier 0 rend le domaine aussi fragile que le plus exposé des postes.

## Comment corriger
- Retirer l'appartenance, ou reclasser l'objet dans le bon tier.
- Créer des comptes d'administration dédiés par tier (un même administrateur a autant de comptes que de tiers qu'il administre).
