# 👥 Groupe Domain Users

Groupe **global** créé automatiquement lors de la promotion du domaine.

## Rôle
- Contient **tous** les comptes utilisateurs du domaine (ajout automatique)
- Utilisé comme groupe de base pour les permissions sur les ressources partagées
- Membre du groupe **Users** local de chaque machine jointe

## ⚠️ Sécurité
> 🟢 **Tier 2 — Groupe standard**

- Éviter d'accorder des permissions élevées à ce groupe — il contient tous les utilisateurs
- Surveiller les ressources accessibles par ce groupe (audit des partages, des GPOs)
- Utile pour définir des politiques s'appliquant à tous les utilisateurs du domaine