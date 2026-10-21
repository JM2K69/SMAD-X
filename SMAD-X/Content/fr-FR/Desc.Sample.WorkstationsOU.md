# 💻 OU Workstations (Tier 2)

Unité d'organisation contenant les **postes de travail des utilisateurs** de l'organisation.

## Rôle
- Héberge les ordinateurs portables et fixes des employés
- Cible des GPOs de configuration, de sécurité et de déploiement logiciel
- Point d'entrée principal des attaquants dans un réseau d'entreprise

## ⚠️ Sécurité
> 🟢 **Tier 2 — Premier vecteur d'intrusion**

- Appliquer les **CIS Benchmarks** ou ANSSI pour Windows 10/11 via GPO
- Activer **BitLocker** sur tous les postes (chiffrement disque)
- Désactiver **LLMNR** et **NetBIOS over TCP/IP** (prévention Responder / MITM)
- Restreindre les ports USB et médias amovibles (via GPO ou solution MDM)
- Déployer un **EDR** sur tous les postes
- Activer **Windows Defender Credential Guard** et **Device Guard**
- Surveiller les connexions à des partages réseau inhabituels (mouvement latéral)