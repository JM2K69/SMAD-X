# vuln_permissions_dc — Dangerous ACLs expose domain controller objects (attack path)

> [Contents: ANSSI — Active Directory control points](README.md) · Français : [vuln_permissions_dc.md](../fr/vuln_permissions_dc.md)

| Sheet | |
|:---|:---|
| Framework | ANSSI — Active Directory control points |
| Identifier | `vuln_permissions_dc` |
| ANSSI levels | 1, 2 (1: most critical; one threshold per level, see the text) |
| Added to the checklist | 2019-02-22 |
| Last change | 2023-11-08 |
| Link | [cert.cyber.gouv.fr/…](https://cert.cyber.gouv.fr/uploads/ad_checklist.html) |
| Cited by SMAD-X | ACL-03 |

## SMAD-X rules that cite it

- **ACL-03** — Dangerous permissions on the domain, AdminSDHolder or the domain controllers

## Official recommendation

Accounts which are granted permissions on domain controllers must be considered privileged. Therefore, they must be protected through the `adminSDHolder` mechanism. These accounts must belong to either the `S-1-5-21-domain-519` or the `S-1-5-21-domain-512` group.

When deploying an administration forest, granting access rights on domain controllers is tolerated given the following cumulative criteria are met:

- delegated groups must be of `DOMAIN LOCAL` type
- permissions on that group must be stricter than those applied to `adminSDHolder`

## Source and licence

- **Source**: [https://cert.cyber.gouv.fr/uploads/ad_checklist.html](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)
- **Retrieved publication**: 2026-11-12
- **Retrieved on**: 2026-11-13, by `tools/SmadxSources`
- **Licence**: Etalab Open Licence 2.0: free reuse citing the source (CERT-FR / ANSSI) and the date of its update; official texts reproduced in French and English
