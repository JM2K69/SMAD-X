# 👥 Groupe Enterprise Admins

Groupe **universel** créé automatiquement. Existe uniquement dans le **domaine racine de la forêt**.

## Rôle
- Administrateurs de l'entreprise avec contrôle sur **tous les domaines de la forêt**
- Peut ajouter/supprimer des domaines de la forêt
- Automatiquement membre de **Administrators** dans chaque domaine de la forêt

## Appartenance par défaut
Membres : **Administrator** (domaine racine uniquement)

## ⚠️ Sécurité
> 🔴 **Tier 0 — Groupe ultra-critique (forêt entière)**

- **Maintenir vide** en permanence — n'ajouter des membres que pour des opérations de niveau forêt
- Vecteur d'attaque transversal à tous les domaines de la forêt
- Surveiller toute modification — Event ID 4728, 4729
- Les membres ont un accès administrateur sur **tous** les contrôleurs de domaine de la forêt