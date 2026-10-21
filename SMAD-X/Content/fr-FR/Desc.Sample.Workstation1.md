# 💻 WKS-001 — Poste de John Doe (Tier 2)

Poste de travail assigné au développeur John Doe.

## Rôle
- Poste de travail standard Windows 10/11 joint au domaine
- Reçoit les GPOs de l'OU Workstations (configuration, sécurité, déploiement)
- Accès aux ressources réseau via les credentials de jdoe

## ⚠️ Sécurité
> 🟢 **Tier 2**

- BitLocker activé avec stockage de la clé de récupération dans AD
- Pas de droits administrateurs locaux pour jdoe
- Connexions aux ressources Tier 1 uniquement via les groupes autorisés
- EDR déployé et supervision SOC