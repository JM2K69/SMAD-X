# 👥 Groupe Domain Admins (OU Admin)

Instance locale du groupe **Domain Admins** dans l'OU Administration.

## Rôle
- Regroupe les comptes administrateurs nominatifs ayant les droits Domain Admins
- Utilisé pour déléguer l'administration du domaine à des personnes identifiées

## ⚠️ Sécurité
> 🔴 **Tier 0 — Groupe ultra-privilégié**

- Maintenir le nombre de membres **au minimum absolu** (idéalement 2-3 comptes de breakglass)
- Auditer les membres mensuellement
- Tout ajout à ce groupe doit passer par un processus de change management
- Surveiller Event ID 4728 (ajout membre) et 4729 (suppression membre)