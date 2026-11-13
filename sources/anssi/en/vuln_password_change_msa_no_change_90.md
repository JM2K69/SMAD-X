# vuln_password_change_msa_no_change_90 — Managed service accounts with passwords unchanged for more than 90 days

> [Contents: ANSSI — Active Directory control points](README.md) · Français : [vuln_password_change_msa_no_change_90.md](../fr/vuln_password_change_msa_no_change_90.md)

| Sheet | |
|:---|:---|
| Framework | ANSSI — Active Directory control points |
| Identifier | `vuln_password_change_msa_no_change_90` |
| ANSSI level | 2 (1: most critical) |
| Added to the checklist | 2021-03-15 |
| Link | [cert.cyber.gouv.fr/…](https://cert.cyber.gouv.fr/uploads/ad_checklist.html) |
| Cited by SMAD-X | PWD-07 |

## SMAD-X rules that cite it

- **PWD-07** — Managed service account whose password no longer changes

## Official text

Some managed service accounts have not changed their passwords for more than 90 days, indicating their secrets are not renewed. Disabled service accounts are not taken into account.

*(Table of the affected objects, in the analysis report.)*

## Official recommendation

Default managed service Accounts settings have them change their passwords automatically every 30 days. The reason for this change not to occur properly must be investigated as it may be indicative of a compromise.

First, check the following registry values:

- `HKLM\System\CurrentControlSet\Services\Netlogon\Parameters\DisablePasswordChange` must be set to 0 or inexistent;
- `HKLM\System\CurrentControlSet\Services\Netlogon\Parameters\MaximumPasswordAge` must be set to 30.

Incorrect values should be reset to their default setting, and GPOs must be checked to ensure they do not override those.

## Source and licence

- **Source**: [https://cert.cyber.gouv.fr/uploads/ad_checklist.html](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)
- **Retrieved publication**: 2026-11-12
- **Retrieved on**: 2026-11-13, by `tools/SmadxSources`
- **Licence**: Etalab Open Licence 2.0: free reuse citing the source (CERT-FR / ANSSI) and the date of its update; official texts reproduced in French and English
