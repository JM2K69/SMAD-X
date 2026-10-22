# 🔐 svc-webapp — Group Managed Service Account (Tier 1)

Compte de service géré de groupe pour l'application web de l'organisation.

## Rôle
- Identité de service utilisée par l'application web hébergée sur SRV-APP-01
- Le mot de passe est **géré automatiquement par AD** (rotation tous les 30 jours, transparent pour l'application)
- Accessible uniquement aux serveurs autorisés via l'attribut `msDS-GroupMSAMembership`
- Aucune connexion interactive possible

## Avantages par rapport aux comptes de service classiques
| Critère | Compte utilisateur | gMSA |
|---|---|---|
| Rotation du mot de passe | Manuelle | Automatique |
| Connexion interactive | Possible | Impossible |
| Usage multi-serveurs | Risqué | Natif |
| Gestion centralisée | Non | Oui |

## ⚠️ Sécurité
> 🟠 **Tier 1 — Compte de service**

- Contrôler strictement les serveurs autorisés à utiliser ce gMSA (`msDS-GroupMSAMembership`)
- Appliquer le **principe du moindre privilège** : uniquement les permissions nécessaires à l'application
- Surveiller l'utilisation de ce compte sur des serveurs non autorisés (Event ID 4624)
- Documenter les ressources accédées par ce compte