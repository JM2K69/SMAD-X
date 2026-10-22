# 🖥️ OU Servers (Tier 1)

Unité d'organisation contenant les **serveurs d'infrastructure** du domaine.

## Rôle
- Héberge les serveurs de fichiers, serveurs d'applications, serveurs d'impression, etc.
- Séparé des postes utilisateurs (Tier 2) et des contrôleurs de domaine (Tier 0)
- Cible de GPOs de configuration et de durcissement des serveurs

## ⚠️ Sécurité
> 🟠 **Tier 1 — Infrastructure**

- Appliquer les **CIS Benchmarks** ou recommandations ANSSI pour Windows Server via GPO
- Bloquer les comptes Tier 2 (utilisateurs standards) de se connecter aux serveurs Tier 1
- Activer **Windows Defender Credential Guard** sur tous les serveurs
- Surveiller les connexions interactives (Event ID 4624 type 2 et 10)
- Déployer un **EDR** sur tous les serveurs