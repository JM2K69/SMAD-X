# 👥 Groupe Group Policy Creator Owners

Groupe **global** créé automatiquement lors de la promotion du domaine.

## Rôle
- Membres autorisés à créer et modifier des **GPOs** dans le domaine
- Un membre peut modifier uniquement les GPOs qu'il a créées (sauf Domain Admins)

## Appartenance par défaut
Membres : **Administrator**

## ⚠️ Sécurité
> 🟠 **Tier 1 — Groupe à surveiller**

- Limiter les membres — la création de GPOs malveillantes est un vecteur d'attaque courant
- Surveiller la création de nouvelles GPOs et leurs liaisons (Event ID 5136, 5137)
- Toute GPO liée à l'OU Domain Controllers est potentiellement dangereuse