# vuln_password_change_server_no_change_90 — Servers with passwords unchanged for more than 90 days

> [Contents: ANSSI — Active Directory control points](README.md) · Français : [vuln_password_change_server_no_change_90.md](../fr/vuln_password_change_server_no_change_90.md)

| Sheet | |
|:---|:---|
| Framework | ANSSI — Active Directory control points |
| Identifier | `vuln_password_change_server_no_change_90` |
| ANSSI level | 2 (1: most critical) |
| Added to the checklist | 2019-03-21 |
| Last change | 2025-08-01 |
| Link | [cert.cyber.gouv.fr/…](https://cert.cyber.gouv.fr/uploads/ad_checklist.html) |
| Cited by SMAD-X | PWD-06 |

## SMAD-X rules that cite it

- **PWD-06** — Computer account whose password no longer changes

## Official text

Some non-disabled servers have not changed their passwords for more than 90 days, indicating their secrets are not renewed. Servers that have not logged on for more than 90 days only appear in the level **3** vulnerability "Inactive servers".

*(Table of the affected objects, in the analysis report.)*

## Official recommendation

Default server settings have them change their passwords automatically every 30 days. The reason for this change not to occur properly must be investigated as it may be indicative of a compromise.

…

## Source and licence

- **Source**: [https://cert.cyber.gouv.fr/uploads/ad_checklist.html](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)
- **Retrieved publication**: 2026-11-12
- **Retrieved on**: 2026-11-13, by `tools/SmadxSources`
- **Licence**: Etalab Open Licence 2.0: free reuse citing the source (CERT-FR / ANSSI) and the date of its update; official texts reproduced in French and English
