# 👤 Compte Administrator

Compte administrateur **intégré** créé automatiquement lors de la promotion du domaine.

## Rôle
- Accès complet et illimité à toutes les ressources du domaine
- Seul compte qui ne peut pas être verrouillé par la politique de verrouillage de compte
- Membre permanent du groupe **Domain Admins** et **Administrators**

## Appartenance par défaut
| Groupe | Portée |
|---|---|
| Administrators | Domaine local |
| Domain Admins | Global |
| Group Policy Creator Owners | Global |
| Schema Admins (forêt racine) | Universel |
| Enterprise Admins (forêt racine) | Universel |

## ⚠️ Sécurité
> 🔴 **Tier 0 — Compte critique**

- **Renommer** ce compte pour ne pas exposer le nom prévisible `Administrator`
- **Désactiver** ce compte en production et créer un compte nommé dédié pour l'administration
- Activer la **politique de verrouillage** pour les attaques par force brute (via Fine-Grained Password Policy)
- Ce compte est une cible privilégiée des attaques **Pass-the-Hash** et **Mimikatz**
- Surveiller avec des alertes sur toute ouverture de session de ce compte
- Activer **Protected Users** (Windows Server 2012 R2+) ne s'applique pas à Administrator — gérer manuellement