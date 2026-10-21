# 🔑 PSO-Tier0-Admins — Politique mots de passe administrateurs

Password Settings Object appliqué aux **comptes d'administration Tier 0**.

## Paramètres de sécurité renforcés
| Paramètre | Valeur |
|---|---|
| Longueur minimale | **16 caractères** |
| Complexité | Activée |
| Historique | 24 mots de passe |
| Âge maximum | **60 jours** |
| Âge minimum | 2 jours |
| Seuil de verrouillage | **3 tentatives** |
| Durée de verrouillage | **1 heure** (ou déverrouillage manuel) |

## Objets ciblés
- Groupe **Domain Admins**
- Compte **Administrator**

## ⚠️ Sécurité
> 🔴 **Tier 0 — Politique critique**

- Préférence : **10** (priorité haute sur la Default Domain Policy)
- Envisager des **passphrases** (ex. : 4 mots aléatoires ≥ 20 caractères) pour faciliter la mémorisation tout en maintenant la sécurité
- Coupler avec l'utilisation d'un **gestionnaire de mots de passe d'entreprise** (CyberArk, BeyondTrust, etc.)
- Ce PSO doit cibler des **groupes**, pas des utilisateurs individuels