# 🔑 Password Settings Container

Container système contenant tous les **Password Settings Objects (PSO)** du domaine.

## Rôle
- Permet de définir des **politiques de mot de passe granulaires** par groupe ou utilisateur (Fine-Grained Password Policy — FGPP)
- Disponible depuis Windows Server 2008 (niveau fonctionnel de domaine 2008+)
- Priorité : le PSO avec la plus faible valeur `msDS-PasswordSettingsPrecedence` s'applique en cas de conflit

## ⚠️ Sécurité
> 🔴 **Tier 0 — Politiques de mots de passe**

- Créer un PSO strict pour les comptes Tier 0 (longueur ≥ 16, rotation ≤ 60 jours, verrouillage après 3 tentatives)
- Le PSO l'emporte sur la Default Domain Policy pour les comptes ciblés
- Surveiller les modifications des PSOs (Event ID 5136)