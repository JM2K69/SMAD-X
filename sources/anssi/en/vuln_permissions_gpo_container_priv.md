# vuln_permissions_gpo_container_priv — Dangerous ACLs expose privileged object containers (attack path)

> [Contents: ANSSI — Active Directory control points](README.md) · Français : [vuln_permissions_gpo_container_priv.md](../fr/vuln_permissions_gpo_container_priv.md)

| Sheet | |
|:---|:---|
| Framework | ANSSI — Active Directory control points |
| Identifier | `vuln_permissions_gpo_container_priv` |
| ANSSI level | 2 (1: most critical) |
| Added to the checklist | 2022-07-29 |
| Last change | 2023-11-08 |
| Link | [cert.cyber.gouv.fr/…](https://cert.cyber.gouv.fr/uploads/ad_checklist.html) |
| Cited by SMAD-X | ACL-02 |

## SMAD-X rules that cite it

- **ACL-02** — Control path to the domain through permissions

## Official recommendation

Access rights on those containers need to be carefully reviewed or reverted to their default state. …

## Source and licence

- **Source**: [https://cert.cyber.gouv.fr/uploads/ad_checklist.html](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)
- **Retrieved publication**: 2026-11-12
- **Retrieved on**: 2026-11-13, by `tools/SmadxSources`
- **Licence**: Etalab Open Licence 2.0: free reuse citing the source (CERT-FR / ANSSI) and the date of its update; official texts reproduced in French and English
