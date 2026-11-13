# vuln_permissions_gpo_priv — Permissions dangereuses sur les objets de GPO s'appliquant aux membres des groupes privilégiés (chemins de contrôle)

> [Sommaire : ANSSI — Points de contrôle Active Directory](README.md) · English: [vuln_permissions_gpo_priv.md](../en/vuln_permissions_gpo_priv.md)

| Fiche | |
|:---|:---|
| Référentiel | ANSSI — Points de contrôle Active Directory |
| Identifiant | `vuln_permissions_gpo_priv` |
| Niveau ANSSI | 1 (1 : le plus critique) |
| Ajouté à la checklist | 2020-04-06 |
| Dernière modification | 2023-11-08 |
| Lien | [cert.cyber.gouv.fr/…](https://cert.cyber.gouv.fr/uploads/ad_checklist.html) |
| Cité par SMAD-X | TIER-02 |

## Règles de SMAD-X qui le citent

- **TIER-02** — Violation du cloisonnement : GPO d'un tier moins sensible

## Recommandation officielle

Il est recommandé de revoir les permissions positionnées sur les GPO.

La correction doit être effectuée à l'aide du composant logiciel enfichable de gestion des GPO `gpmc.msc` afin de maintenir la synchronisation des permissions entre l'annuaire LDAP et le répertoire SYSVOL.

La correction s'effectue dans l'onglet "Délégation", il est toutefois recommandé d'utiliser la vue "avancée" de cet éditeur.

Avant d'accepter le risque (voir ci-dessous), il est fortement recommandé de s'assurer qu'il est bien intentionnel de considérer comme des administrateurs T0 l'ensemble des *principals* pour lesquels il existe un chemin de contrôle vers ces GPOs. En cas d'acceptation du risque, l'ensemble des *principals* (y compris leurs membres pour les groupes, récursivement) pour lesquels il existe un chemin de contrôle vers ces GPOs doivent être considérés comme privilégiés, et doivent être protégés.

## Source et licence

- **Source** : [https://cert.cyber.gouv.fr/uploads/ad_checklist.html](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)
- **Publication relevée** : 2026-11-12
- **Relevé le** : 2026-11-13, par `tools/SmadxSources`
- **Licence** : licence ouverte Etalab 2.0 : réutilisation libre en mentionnant la source (CERT-FR / ANSSI) et la date de sa mise à jour ; textes officiels repris en français et en anglais
