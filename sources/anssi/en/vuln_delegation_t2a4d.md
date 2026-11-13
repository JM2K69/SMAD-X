# vuln_delegation_t2a4d — Constrained delegation with protocol transition to a privileged service

> [Contents: ANSSI — Active Directory control points](README.md) · Français : [vuln_delegation_t2a4d.md](../fr/vuln_delegation_t2a4d.md)

| Sheet | |
|:---|:---|
| Framework | ANSSI — Active Directory control points |
| Identifier | `vuln_delegation_t2a4d` |
| ANSSI level | 1 (1: most critical) |
| Added to the checklist | 2018-10-01 |
| Last change | 2021-05-31 |
| Link | [cert.cyber.gouv.fr/…](https://cert.cyber.gouv.fr/uploads/ad_checklist.html) |
| Cited by SMAD-X | DLG-02 |

## SMAD-X rules that cite it

- **DLG-02** — Constrained delegation with protocol transition to a domain controller

## Official text

Some accounts have constrained authentication delegations with protocol transition to a privileged service. These grant the subject account the ability to elevate its privileges. The following services are handled:

- domain controller services;
- `krbtgt` service.

*(Table of the affected objects, in the analysis report.)*

## Official recommendation

Constrained delegation with protocol transition allows an account to temporarily act on behalf of any user or computer. If that delegation is authorized to a privileged resource (including domain controllers), it allows the account to assume the privileges of that resource and compromise the forest.

Constrained delegation with protocol transition to privileged services must not be allowed.

For each of the aforementioned accounts, all *Service Principal Names* (SPNs) referencing domain controllers must be removed from their `msDS-AllowedToDelegateTo` attribute. This can be performed through the "Delegation" tab of the Active directory Users and Computers management console.

## Source and licence

- **Source**: [https://cert.cyber.gouv.fr/uploads/ad_checklist.html](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)
- **Retrieved publication**: 2026-11-12
- **Retrieved on**: 2026-11-13, by `tools/SmadxSources`
- **Licence**: Etalab Open Licence 2.0: free reuse citing the source (CERT-FR / ANSSI) and the date of its update; official texts reproduced in French and English
