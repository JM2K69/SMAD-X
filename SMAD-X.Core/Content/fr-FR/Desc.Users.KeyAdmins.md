# 👥 Groupe Key Admins

Groupe **global** créé automatiquement (Windows Server 2016+).

## Rôle
- Membres autorisés à effectuer des actions administratives sur les **attributs msDS-KeyCredentialLink**
- Utilisé pour la gestion des **Windows Hello for Business** et des clés FIDO2 au niveau du domaine

## ⚠️ Sécurité
> 🔴 **Tier 0 — Risque Shadow Credentials**

- Vecteur d'attaque **Shadow Credentials** : un membre peut ajouter des credentials alternatifs à n'importe quel compte du domaine
- Attaque documentée : `Whisker`, `pyWhisker`
- Surveiller les modifications de l'attribut `msDS-KeyCredentialLink` — Event ID 5136
- Limiter les membres au strict minimum