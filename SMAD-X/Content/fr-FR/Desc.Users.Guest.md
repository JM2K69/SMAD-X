# 👤 Compte Guest

Compte invité **intégré** créé automatiquement lors de la promotion du domaine.

## Rôle
- Permet un accès anonyme et limité au domaine sans mot de passe
- Droits équivalents au groupe **Guests** (très restreints)
- Ne peut pas changer son mot de passe ni accéder aux paramètres du système

## Appartenance par défaut
| Groupe | Portée |
|---|---|
| Guests | Domaine local |
| Domain Guests | Global |

## ⚠️ Sécurité
> 🔴 **Désactiver immédiatement en production**

- Ce compte est **désactivé par défaut** depuis Windows Server 2008 — vérifier qu'il le reste
- Ne jamais activer ce compte dans un environnement de production
- Vecteur d'attaque classique pour l'accès non authentifié
- Surveiller toute tentative d'activation ou d'utilisation de ce compte (Event ID 4722, 4624)