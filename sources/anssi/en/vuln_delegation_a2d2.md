# vuln_delegation_a2d2 — Constrained authentication delegation to privileged service

> [Contents: ANSSI — Active Directory control points](README.md) · Français : [vuln_delegation_a2d2.md](../fr/vuln_delegation_a2d2.md)

| Sheet | |
|:---|:---|
| Framework | ANSSI — Active Directory control points |
| Identifier | `vuln_delegation_a2d2` |
| ANSSI level | 1 (1: most critical) |
| Added to the checklist | 2018-10-01 |
| Last change | 2021-05-31 |
| Link | [cert.cyber.gouv.fr/…](https://cert.cyber.gouv.fr/uploads/ad_checklist.html) |
| Cited by SMAD-X | DLG-03 |

## SMAD-X rules that cite it

- **DLG-03** — Constrained delegation to a domain controller

## Official text

Some accounts have constrained authentication delegations to privileged services. These grant the subject account the ability to elevate its on privileged Active Directory service.

*(Table of the affected objects, in the analysis report.)*

## Official recommendation

Constrained authentication delegation allows an account to authenticate to a Kerberos service with the identity of a third-party user who has authenticated to that account. If such a delegation is authorized to a privileged resource (including domain controllers), it allows that account to elevate privileges and compromise the forest.

Constrained delegation to privileged services must not be allowed.

For each of the aforementioned accounts, all *Service Principal Names* (SPNs) referencing privileged services must be removed from their `msDS-AllowedToDelegateTo` attribute. This can be performed through the "Delegation" tab of the Active directory Users and Computers management console.

## Source and licence

- **Source**: [https://cert.cyber.gouv.fr/uploads/ad_checklist.html](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)
- **Retrieved publication**: 2026-11-12
- **Retrieved on**: 2026-11-13, by `tools/SmadxSources`
- **Licence**: Etalab Open Licence 2.0: free reuse citing the source (CERT-FR / ANSSI) and the date of its update; official texts reproduced in French and English
