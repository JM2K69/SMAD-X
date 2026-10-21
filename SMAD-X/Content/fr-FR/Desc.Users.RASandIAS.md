# 👥 Groupe RAS and IAS Servers

Groupe **domaine local** créé automatiquement lors de la promotion du domaine.

## Rôle
- Membres autorisés à accéder aux **propriétés d'accès réseau** des comptes utilisateurs
- Utilisé par les serveurs **NPS (Network Policy Server)**, **RADIUS** et **VPN**

## ⚠️ Sécurité
> 🟠 **Tier 1 — Accès aux attributs d'authentification réseau**

- Limiter aux serveurs NPS/RADIUS légitimes
- Un serveur RAS compromis peut intercepter ou manipuler les connexions VPN