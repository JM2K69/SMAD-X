# 🏛️ OU Administration (Tier 0)

Unité d'organisation contenant les **comptes et groupes d'administration de plus haut niveau** du domaine.

## Rôle
- Héberge les comptes administrateurs nominatifs (non-partagés) utilisés uniquement pour les tâches d'administration de domaine
- Contient les groupes d'administration Tier 0
- Cible de GPOs de durcissement spécifiques aux comptes privilégiés

## ⚠️ Sécurité
> 🔴 **Tier 0 — Zone critique**

- Appliquer une **GPO de durcissement stricte** sur cette OU (restrictions logon, audit intensifié)
- Bloquer l'héritage GPO et ne lier que des GPOs validées
- Accès en lecture/écriture sur cette OU : **Domain Admins uniquement**
- Surveiller tout objet créé ou modifié dans cette OU (Event ID 5137, 5136)
- Ces comptes doivent être utilisés exclusivement depuis des **PAW (Privileged Access Workstation)**