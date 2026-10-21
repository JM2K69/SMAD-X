# Domaine contoso.com

Domaine Active Directory de démonstration créé par SMAD-X.

## Présentation
Ce domaine illustre une architecture AD typique d'une organisation de taille intermédiaire, segmentée selon le **modèle de tiering Microsoft** (Tier 0 / Tier 1 / Tier 2).

## Structure
| OU / Container | Rôle |
|---|---|
| Admin | Comptes et groupes d'administration Tier 0 |
| Servers | Serveurs d'infrastructure Tier 1 |
| Users | Comptes utilisateurs standards Tier 2 |
| Workstations | Postes de travail Tier 2 |
| Domain Controllers | Contrôleurs de domaine Tier 0 |

## ⚠️ Sécurité
> Cette structure est fournie à titre pédagogique. Adapter au contexte réel avant déploiement.