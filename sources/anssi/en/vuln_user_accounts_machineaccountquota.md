# vuln_user_accounts_machineaccountquota — Unrestricted domain join

> [Contents: ANSSI — Active Directory control points](README.md) · Français : [vuln_user_accounts_machineaccountquota.md](../fr/vuln_user_accounts_machineaccountquota.md)

| Sheet | |
|:---|:---|
| Framework | ANSSI — Active Directory control points |
| Identifier | `vuln_user_accounts_machineaccountquota` |
| ANSSI level | 4 (1: most critical) |
| Added to the checklist | 2022-07-29 |
| Link | [cert.cyber.gouv.fr/…](https://cert.cyber.gouv.fr/uploads/ad_checklist.html) |
| Cited by SMAD-X | ACC-03 |

## SMAD-X rules that cite it

- **ACC-03** — Any user can join computers to the domain

## Official text

By default authenticated users may join up to 10 computers to the domain. This value is controlled by the `ms-DS-MachineAccountQuota` attribute in the root object.

Machine accounts added that way are owned by the used account, offering it full control. The CVE-2021-42287 vulnerability used such machine account to elevate privileges.

*(Table of the affected objects, in the analysis report.)*

## Official recommendation

Set the `ms-DS-MachineAccountQuota` attribute to 0 in the domain root objects. Fixing usually requires using `dsa.msc`, `adsiedit.msc` or the `ldp` utility.

## Source and licence

- **Source**: [https://cert.cyber.gouv.fr/uploads/ad_checklist.html](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)
- **Retrieved publication**: 2026-11-12
- **Retrieved on**: 2026-11-13, by `tools/SmadxSources`
- **Licence**: Etalab Open Licence 2.0: free reuse citing the source (CERT-FR / ANSSI) and the date of its update; official texts reproduced in French and English
