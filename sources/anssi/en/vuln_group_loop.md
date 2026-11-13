# vuln_group_loop — Loops in group memberships

> [Contents: ANSSI — Active Directory control points](README.md) · Français : [vuln_group_loop.md](../fr/vuln_group_loop.md)

| Sheet | |
|:---|:---|
| Framework | ANSSI — Active Directory control points |
| Identifier | `vuln_group_loop` |
| ANSSI level | 4 (1: most critical) |
| Added to the checklist | 2023-12-06 |
| Link | [cert.cyber.gouv.fr/…](https://cert.cyber.gouv.fr/uploads/ad_checklist.html) |
| Cited by SMAD-X | GRP-01 |

## SMAD-X rules that cite it

- **GRP-01** — Group nesting loop

## Official text

A loop in group memberships does not present a security issue for Active Directory but can affect security tools used to secure the domain.

*(Table of the affected objects, in the analysis report.)*

## Official recommendation

For hygiene purpose it is recommended to remove the loops in group memberships.

## Source and licence

- **Source**: [https://cert.cyber.gouv.fr/uploads/ad_checklist.html](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)
- **Retrieved publication**: 2026-11-12
- **Retrieved on**: 2026-11-13, by `tools/SmadxSources`
- **Licence**: Etalab Open Licence 2.0: free reuse citing the source (CERT-FR / ANSSI) and the date of its update; official texts reproduced in French and English
