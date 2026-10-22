# 👥 Groupe DnsUpdateProxy

Groupe **global** créé automatiquement lors de la promotion du domaine.

## Rôle
- Membres autorisés à **enregistrer des enregistrements DNS** pour le compte d'autres clients
- Utilisé typiquement par les serveurs **DHCP** pour la mise à jour dynamique DNS

## ⚠️ Sécurité
> 🟠 **Tier 1 — Risque de squatting DNS**

- **Limiter** les membres aux seuls serveurs DHCP autorisés
- Les enregistrements créés par ce groupe sont initialement sans propriétaire — vecteur de **DNS hijacking**
- Configurer le serveur DHCP avec un compte dédié plutôt que d'utiliser ce groupe si possible
- Si utilisé, activer le **DNS dynamic update credentials** sur le serveur DHCP