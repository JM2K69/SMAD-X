# vuln_delegation_t4d — Unconstrained authentication delegation

> [Contents: ANSSI — Active Directory control points](README.md) · Français : [vuln_delegation_t4d.md](../fr/vuln_delegation_t4d.md)

| Sheet | |
|:---|:---|
| Framework | ANSSI — Active Directory control points |
| Identifier | `vuln_delegation_t4d` |
| ANSSI level | 1 (1: most critical) |
| Added to the checklist | 2018-09-26 |
| Last change | 2024-04-02 |
| Link | [cert.cyber.gouv.fr/…](https://cert.cyber.gouv.fr/uploads/ad_checklist.html) |
| Cited by SMAD-X | DLG-01 |

## SMAD-X rules that cite it

- **DLG-01** — Unconstrained Kerberos delegation

## Official text

Some accounts in the forest have been granted unconstrained authentication delegations. This grants them the ability to act as any user that will authenticate against them.

*(Table of the affected objects, in the analysis report.)*

## Official recommendation

Unconstrained authentication delegation allows an account to authenticate to any Kerberos service acting on behalf of any user who has tried to authenticate to that account. It allows that account to elevate privileges and compromise the forest. By default, unconstrained authentication delegation is only granted to domain controllers, and must not be granted to any other account.

For all of the accounts listed above, remove the `TRUSTED_FOR_DELEGATION` flag in their `userAccountControl` attribute. This can be performed through the "Delegation" tab of the Active directory Users and Computers management console. If a Kerberos delegation really is required, use a constrained one.

## Source and licence

- **Source**: [https://cert.cyber.gouv.fr/uploads/ad_checklist.html](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)
- **Retrieved publication**: 2026-11-12
- **Retrieved on**: 2026-11-13, by `tools/SmadxSources`
- **Licence**: Etalab Open Licence 2.0: free reuse citing the source (CERT-FR / ANSSI) and the date of its update; official texts reproduced in French and English
