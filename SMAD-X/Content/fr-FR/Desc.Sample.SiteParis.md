# 🏙 Site-Paris — Datacenter Principal

Site Active Directory hébergeant les serveurs principaux du domaine **contoso.com**.

## Périmètre réseau
| Réseau | VLAN | Usage |
|--------|------|-------|
| 10.0.1.0/24 | 10 | Serveurs AD / Infrastructure |

## Contrôleurs de domaine
- **DC01** — PDC Emulator, RID Master, Schema Master, Domain Naming Master, Infrastructure Master
- **DC02** — DC secondaire, Catalogue Global

## Rôle dans la topologie
- Site **principal** et **hub de réplication** pour tous les sites de la forêt
- Héberge les rôles FSMO critiques sur DC01
- Réplication intra-site immédiate entre DC01 et DC02

## ⚠️ Bonnes pratiques
- Au moins 2 DCs par site pour la haute disponibilité
- Éviter de placer toute l'infrastructure Tier 0 dans un unique datacenter