# vuln_password_change_dc_no_change — Domain controllers with passwords unchanged for more than 45 days

> [Contents: ANSSI — Active Directory control points](README.md) · Français : [vuln_password_change_dc_no_change.md](../fr/vuln_password_change_dc_no_change.md)

| Sheet | |
|:---|:---|
| Framework | ANSSI — Active Directory control points |
| Identifier | `vuln_password_change_dc_no_change` |
| ANSSI level | 1 (1: most critical) |
| Added to the checklist | 2019-03-21 |
| Link | [cert.cyber.gouv.fr/…](https://cert.cyber.gouv.fr/uploads/ad_checklist.html) |
| Cited by SMAD-X | PWD-05 |

## SMAD-X rules that cite it

- **PWD-05** — Domain controller machine password unchanged

## Official text

Some domain controllers have not changed their password for more than 45 days, indicating their secrets are not renewed.

*(Table of the affected objects, in the analysis report.)*

## Official recommendation

Default domain controller settings have them change their passwords automatically every 30 days. The reason for this change not to occur properly must be investigated as it may be indicative of a compromise.

…

## Source and licence

- **Source**: [https://cert.cyber.gouv.fr/uploads/ad_checklist.html](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)
- **Retrieved publication**: 2026-11-12
- **Retrieved on**: 2026-11-13, by `tools/SmadxSources`
- **Licence**: Etalab Open Licence 2.0: free reuse citing the source (CERT-FR / ANSSI) and the date of its update; official texts reproduced in French and English
