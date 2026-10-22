# 👤 OU Users (Tier 2)

Unité d'organisation contenant les **comptes utilisateurs standards** de l'organisation.

## Rôle
- Héberge les comptes des employés pour leurs usages quotidiens (messagerie, fichiers, applications)
- Cible des GPOs de configuration des postes de travail et des restrictions utilisateurs
- Peut être subdivisée par département (RH, Finance, IT, Direction…)

## ⚠️ Sécurité
> 🟢 **Tier 2 — Utilisateurs standards**

- Appliquer le **principe du moindre privilège** : aucun compte de cette OU ne doit être administrateur local
- Activer **Windows Defender Credential Guard** pour protéger les credentials en mémoire
- Déployer une GPO de **restriction de scripts** (SRP ou WDAC) pour limiter l'exécution de PowerShell, WScript, etc.
- Former les utilisateurs aux **attaques de phishing** et d'ingénierie sociale
- Surveiller les connexions hors horaires habituels et les volumes de données inhabituels