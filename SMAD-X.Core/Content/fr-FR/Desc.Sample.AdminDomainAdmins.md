# 👥 GG-Tier0-Admins (OU Admin)

Groupe global des **administrateurs du Tier 0** : les comptes d'administration nominatifs (`adm-…`).

## Rôle
- Le groupe est membre de **Domain Admins** : on donne les droits à un groupe métier plutôt qu'à des comptes un par un (modèle AGDLP)
- Ajouter ou retirer un administrateur se fait ici, sans toucher aux groupes intégrés

## ⚠️ Sécurité
> 🔴 **Tier 0 — Groupe ultra-privilégié**

- Maintenir le nombre de membres **au minimum absolu**
- Auditer les membres chaque mois ; tout ajout passe par un processus de validation
- Surveiller Event ID 4728 (ajout d'un membre) et 4729 (retrait)
