# 🏙 Site-Lyon — Site Secondaire

Site Active Directory hébergeant le contrôleur de domaine de **Lyon**, connecté à Paris via un lien WAN.

## Périmètre réseau
| Réseau | VLAN | Usage |
|--------|------|-------|
| 10.0.2.0/24 | 20 | Serveurs AD / Infrastructure |

## Contrôleurs de domaine
- **DC03** — DC secondaire, Catalogue Global local

## Rôle dans la topologie
- Site **secondaire** connecté à Site-Paris via **DEFAULTIPSITELINK** (coût 100)
- Réduit la latence d'authentification pour les utilisateurs lyonnais
- Assure la continuité en cas de panne du lien WAN Paris–Lyon (authentification locale)

## ⚠️ Bonnes pratiques
- Vérifier régulièrement l'état de la réplication : `Get-ADReplicationPartnerMetadata -Target DC03 -Scope Server`
- Surveiller les erreurs de réplication (Event IDs 1311, 1388, 1645)