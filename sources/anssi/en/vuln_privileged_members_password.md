# vuln_privileged_members_password — Privileged group members with weak password policy

> [Contents: ANSSI — Active Directory control points](README.md) · Français : [vuln_privileged_members_password.md](../fr/vuln_privileged_members_password.md)

| Sheet | |
|:---|:---|
| Framework | ANSSI — Active Directory control points |
| Identifier | `vuln_privileged_members_password` |
| ANSSI level | 2 (1: most critical) |
| Added to the checklist | 2019-06-03 |
| Link | [cert.cyber.gouv.fr/…](https://cert.cyber.gouv.fr/uploads/ad_checklist.html) |
| Cited by SMAD-X | PWD-09 |

## SMAD-X rules that cite it

- **PWD-09** — Privileged account with a weak password policy

## Official text

Some privileged accounts have a weak password policy enforced.

*(Table of the affected objects, in the analysis report.)*

## Official recommendation

For privileged accounts, enforcing a password policy with the following requirements is recommended:

- forced change at most every 3 years;
- length of 8 or more characters.

Important note: passwords should not be renewed too often. Enforcing short password lifespans incentivizes users to choose statistically weaker passwords, which negates the intended benefits.

## Source and licence

- **Source**: [https://cert.cyber.gouv.fr/uploads/ad_checklist.html](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)
- **Retrieved publication**: 2026-11-12
- **Retrieved on**: 2026-11-13, by `tools/SmadxSources`
- **Licence**: Etalab Open Licence 2.0: free reuse citing the source (CERT-FR / ANSSI) and the date of its update; official texts reproduced in French and English
