# 👥 Groupe Domain Admins

Groupe **global** créé automatiquement lors de la promotion du domaine.

## Rôle
- Administrateurs désignés du domaine
- Membres automatiquement ajoutés au groupe **Administrators** de chaque machine jointe au domaine
- Contrôle total sur tous les objets du domaine

## Appartenance par défaut
Membres : **Administrator**

## ⚠️ Sécurité
> 🔴 **Tier 0 — Groupe ultra-privilégié**

- Limiter le nombre de membres au strict minimum (principe du moindre privilège)
- Ne jamais utiliser un compte Domain Admin pour les tâches quotidiennes
- Utiliser des comptes dédiés à l'administration (PAW — Privileged Access Workstation)
- Activer la surveillance de toute modification de ce groupe (Event ID 4728, 4729)
- Les membres de Domain Admins sont membres de **Administrators** sur **toutes** les machines du domaine — vecteur de mouvement latéral majeur