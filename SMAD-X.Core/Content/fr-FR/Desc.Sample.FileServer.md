# 💾 SRV-FILE-01 — Serveur de fichiers (Tier 1)

Serveur de fichiers principal de l'organisation.

## Rôle
- Héberge les partages réseau de l'organisation (données métier, profils itinérants, dossiers de travail)
- Utilise des **ACLs NTFS** et des **Share Permissions** pour le contrôle d'accès
- Partages typiques : `\\SRV-FILE-01\Data`, `\\SRV-FILE-01\Profiles$`, `\\SRV-FILE-01\Home$`

## ⚠️ Sécurité
> 🟠 **Tier 1 — Données sensibles**

- Activer **SMB Signing** obligatoire (prévention des attaques MITM / Relay)
- Désactiver **SMBv1** (vulnérable à EternalBlue / WannaCry)
- Surveiller les accès aux partages (Event ID 5140, 5145) pour détecter les ransomwares
- Déployer un **honeypot file** dans chaque partage (fichier leurre alertant sur accès anormal)
- Implémenter le **principe du moindre privilège** sur les partages et les ACLs NTFS
- Sauvegardes régulières avec des **sauvegardes immuables** hors ligne