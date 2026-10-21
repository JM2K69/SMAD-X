# 🖧 DC01 — Contrôleur de domaine principal (Tier 0)

Premier contrôleur de domaine du domaine contoso.com.

## Rôle
- Héberge l'ensemble des **rôles FSMO** lors de l'installation initiale :
  - **Schema Master** : contrôle les modifications du schéma AD
  - **Domain Naming Master** : gère l'ajout/suppression de domaines dans la forêt
  - **RID Master** : distribue les pools de RIDs pour la création d'objets de sécurité
  - **PDC Emulator** : synchronisation de l'heure, gestion des verrouillages de compte, compatibilité NT4
  - **Infrastructure Master** : maintient les références inter-domaines

## ⚠️ Sécurité
> 🔴 **Tier 0 — Infrastructure AD critique**

- Ne jamais installer d'applications tierces sur un DC (surface d'attaque minimale)
- Activer **Windows Defender Credential Guard** sur le DC
- Restreindre les connexions réseau entrant/sortant au strict nécessaire (pare-feu Windows)
- Surveiller les Event IDs critiques : 4662 (accès objet AD), 4769 (ticket Kerberos), 4776 (NTLM)
- Mettre en place un **DC de secours** dans un site/VLAN séparé
- Inclure dans un plan de **Disaster Recovery** avec sauvegardes System State testées