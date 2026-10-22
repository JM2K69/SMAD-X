# 👥 Groupe Enterprise Key Admins

Groupe **universel** créé automatiquement (Windows Server 2016+). Domaine racine de la forêt.

## Rôle
- Équivalent de **Key Admins** mais avec portée sur **tous les domaines de la forêt**
- Gestion des attributs `msDS-KeyCredentialLink` au niveau forêt

## ⚠️ Sécurité
> 🔴 **Tier 0 — Risque Shadow Credentials (forêt entière)**

- Même vecteur d'attaque que **Key Admins** mais avec portée forêt
- **Maintenir vide** en permanence sauf besoin opérationnel spécifique
- Surveiller toute modification — Event ID 4728, 4729 et 5136