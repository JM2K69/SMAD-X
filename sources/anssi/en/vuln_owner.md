# vuln_owner — Incorrect object owners

> [Contents: ANSSI — Active Directory control points](README.md) · Français : [vuln_owner.md](../fr/vuln_owner.md)

| Sheet | |
|:---|:---|
| Framework | ANSSI — Active Directory control points |
| Identifier | `vuln_owner` |
| ANSSI level | 3 (1: most critical) |
| Added to the checklist | 2019-08-27 |
| Last change | 2021-05-31 |
| Link | [cert.cyber.gouv.fr/…](https://cert.cyber.gouv.fr/uploads/ad_checklist.html) |
| Cited by SMAD-X | ACL-01 |

## SMAD-X rules that cite it

- **ACL-01** — Critical object with an inappropriate owner

## Official text

Some objects, created for more than 7 days, have non-standard owners. Note: Only the first 20 objects are shown for the first 10000 owners.

*(Table of the affected objects, in the analysis report.)*

## Official recommendation

All objects (users, groups, sMSA, gMSA, computers, OU, GPO) must be owned by one of the following objects:

- « *Domain Admins* »;
- « *Enterprise Admins* »;
- « *Administrators* »;
- « *Local System* ».

## Source and licence

- **Source**: [https://cert.cyber.gouv.fr/uploads/ad_checklist.html](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)
- **Retrieved publication**: 2026-11-12
- **Retrieved on**: 2026-11-13, by `tools/SmadxSources`
- **Licence**: Etalab Open Licence 2.0: free reuse citing the source (CERT-FR / ANSSI) and the date of its update; official texts reproduced in French and English
