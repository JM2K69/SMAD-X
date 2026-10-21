# 🖧 DC02 — Contrôleur de domaine secondaire — Site Paris (Tier 0)

Contrôleur de domaine secondaire hébergé dans le site **Site-Paris** (10.0.1.0/24).

## Rôle
- Assure la **réplication AD** depuis DC01 au sein du même site
- Prend en charge les authentifications en cas d'indisponibilité de DC01
- Héberge une réplique complète du **catalogue global** pour la forêt
- Idéal pour la **distribution de charge Kerberos** sur le site principal

## Rôles FSMO
- Aucun rôle FSMO principal (DC secondaire)
- Peut être promu en cas de défaillance de DC01 via `Move-ADDirectoryServerOperationMasterRole`

## Infrastructure réseau
- Adresse IP : 10.0.1.2/24
- Site AD : **Site-Paris**
- Réplication intra-site : immédiate (< 15 secondes)

## ⚠️ Sécurité
> 🔴 **Tier 0 — Infrastructure AD critique**

- Isolé dans le même VLAN sécurisé que DC01 (VLAN Tier 0)
- Surveillance identique à DC01 (Event IDs 4624, 4662, 4769, 4776)
- Inclus dans les sauvegardes System State planifiées