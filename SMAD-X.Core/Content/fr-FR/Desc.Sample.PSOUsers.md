# 🔑 PSO-Standard-Users — Politique mots de passe utilisateurs

Password Settings Object appliqué aux **comptes utilisateurs standards Tier 2**.

## Paramètres de sécurité standard
| Paramètre | Valeur |
|---|---|
| Longueur minimale | **12 caractères** |
| Complexité | Activée |
| Historique | 12 mots de passe |
| Âge maximum | **90 jours** |
| Âge minimum | 1 jour |
| Seuil de verrouillage | **5 tentatives** |
| Durée de verrouillage | **30 minutes** |

## Objets ciblés
- Utilisateurs **jdoe**, **asmith**

## ⚠️ Sécurité
> 🟢 **Tier 2 — Politique standard**

- Préférence : **50** (priorité inférieure à PSO-Tier0-Admins)
- Activer le **self-service de réinitialisation** (SSPR) pour réduire la charge du helpdesk
- Envisager une intégration avec **Microsoft Entra ID Password Protection** pour bloquer les mots de passe courants/compromis (HaveIBeenPwned)