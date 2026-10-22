# 👤 Compte Administrateur (Tier 0)

Compte administrateur **nominatif** dédié à l'administration du domaine.

## Rôle
- Utilisé uniquement pour les tâches d'administration Active Directory nécessitant des droits Domain Admins
- Distinct du compte `Administrator` intégré du domaine
- Ne doit jamais être utilisé pour les tâches quotidiennes

## ⚠️ Sécurité
> 🔴 **Tier 0 — Compte à haute valeur**

- Utiliser exclusivement depuis une **PAW (Privileged Access Workstation)**
- Activer **MFA** (Windows Hello for Business, carte à puce)
- Ajouter au groupe **Protected Users** pour bloquer NTLM et délégation Kerberos
- Surveiller toute ouverture de session (Event ID 4624, 4625, 4648)
- Ne jamais utiliser ce compte pour naviguer sur Internet ou lire des e-mails
- Rotation du mot de passe tous les 60 jours minimum