# ⚙️ SRV-APP-01 — Serveur d'applications (Tier 1)

Serveur hébergeant les applications métier de l'organisation.

## Rôle
- Exécute les applications d'entreprise (ERP, intranet, services web internes, etc.)
- Utilise des **comptes de service gMSA** pour les identités applicatives
- Communique avec les serveurs de base de données Tier 1

## ⚠️ Sécurité
> 🟠 **Tier 1 — Applications critiques**

- Utiliser des **gMSA** (Group Managed Service Accounts) pour tous les comptes de service — jamais de comptes utilisateurs avec mot de passe statique
- Activer **Windows Defender Application Control (WDAC)** pour restreindre les exécutables autorisés
- Isoler les applications dans des **comptes de service dédiés** avec le strict minimum de permissions
- Appliquer les mises à jour de sécurité mensuellement (Patch Tuesday)
- Surveiller les processus inhabituels (Event ID 4688 avec commandline logging activé)