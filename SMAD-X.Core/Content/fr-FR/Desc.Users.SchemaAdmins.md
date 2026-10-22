# 👥 Groupe Schema Admins

Groupe **universel** créé automatiquement. Existe uniquement dans le **domaine racine de la forêt**.

## Rôle
- Seuls membres autorisés à modifier le **schéma Active Directory** (structure des classes et attributs)
- Modifications du schéma sont irréversibles et s'appliquent à toute la forêt

## Appartenance par défaut
Membres : **Administrator** (domaine racine uniquement)

## ⚠️ Sécurité
> 🔴 **Tier 0 — Groupe ultra-critique (forêt entière)**

- **Maintenir vide** en permanence — n'ajouter des membres que le temps d'une modification de schéma planifiée
- Toute modification du schéma doit être testée en environnement de test d'abord
- Surveiller toute modification de ce groupe — Event ID 4728, 4729
- Activer un processus de change management strict pour les modifications de schéma