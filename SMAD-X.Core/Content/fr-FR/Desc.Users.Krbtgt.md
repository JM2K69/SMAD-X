# 🔑 Compte krbtgt

Compte de service **Kerberos Key Distribution Center (KDC)** créé automatiquement lors de la promotion du domaine.

## Rôle
- Utilisé exclusivement par le service **KDC** pour signer et chiffrer les tickets Kerberos (TGT)
- Son mot de passe sert de clé de chiffrement pour tous les **Ticket Granting Tickets** du domaine
- Désactivé par défaut — ne jamais activer

## Appartenance par défaut
| Groupe | Portée |
|---|---|
| Domain Users | Global |

## ⚠️ Sécurité
> 🔴 **Tier 0 — Compte ultra-critique**

- **Ne jamais supprimer** ce compte — cela rend le domaine inutilisable
- **Ne jamais activer** ce compte interactivement
- La compromission de ce compte permet de créer des **Golden Tickets** (attaque Mimikatz)
- **Renouveler le mot de passe DEUX FOIS** après une compromission suspectée (deux resets consécutifs nécessaires pour invalider les tickets existants)
- Renouvellement préventif recommandé **tous les 180 jours** en production
- Surveiller les modifications du mot de passe (Event ID 4723, 4724)
- Depuis Windows Server 2016 : utiliser le **RODC krbtgt account** séparé pour les RODCs