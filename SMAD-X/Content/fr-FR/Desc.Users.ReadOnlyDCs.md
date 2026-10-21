# 👥 Groupe Read-Only Domain Controllers

Groupe **global** créé automatiquement lors de la promotion du domaine.

## Rôle
- Contient les **RODC** (Read-Only Domain Controllers) du domaine
- Les RODCs répliquent uniquement les objets autorisés via la **Password Replication Policy**

## ⚠️ Sécurité
> 🔴 **Tier 0 — Groupe critique**

- Surveiller les ajouts à ce groupe
- La compromission d'un RODC expose uniquement les comptes dont le mot de passe est mis en cache sur ce RODC
- Configurer soigneusement la **Allowed RODC Password Replication Group** pour limiter l'exposition