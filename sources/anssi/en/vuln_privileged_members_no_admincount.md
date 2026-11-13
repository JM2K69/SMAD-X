# vuln_privileged_members_no_admincount — Privileged groups members having an adminCount attribute which is null or 0

> [Contents: ANSSI — Active Directory control points](README.md) · Français : [vuln_privileged_members_no_admincount.md](../fr/vuln_privileged_members_no_admincount.md)

| Sheet | |
|:---|:---|
| Framework | ANSSI — Active Directory control points |
| Identifier | `vuln_privileged_members_no_admincount` |
| ANSSI level | 2 (1: most critical) |
| Added to the checklist | 2020-01-06 |
| Last change | 2024-02-14 |
| Link | [cert.cyber.gouv.fr/…](https://cert.cyber.gouv.fr/uploads/ad_checklist.html) |
| Cited by SMAD-X | PRIV-01 |

## SMAD-X rules that cite it

- **PRIV-01** — Privileged group member without adminCount

## Official text

When an account becomes member of a privileged group, its security descriptor is copied from the `adminSDHolder` and the `adminCount` attribute is set to 1. Then, the domain controller having the PDCe role verify the security descriptor of protected accounts on a regular basis.

A member of a privileged group having an `adminCount` attribute that is empty or 0 is the result of an intentional modification of this attribute. This could aim at hiding the account from security tools.

Privileged groups in the forest contain accounts having an `adminCount` attribute which is null or 0:

*(Table of the affected objects, in the analysis report.)*

Remark : the "Domain Controllers" and "Read-only Domain Controllers" groups form an exception. They are protected by `adminSDHolder` but their members are not.

## Official recommendation

It is recommended to explain why the `adminCount` attribute has been changed for the listed accounts.

Then, it is necessary to set the attribute to the value `1`.

## Source and licence

- **Source**: [https://cert.cyber.gouv.fr/uploads/ad_checklist.html](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)
- **Retrieved publication**: 2026-11-12
- **Retrieved on**: 2026-11-13, by `tools/SmadxSources`
- **Licence**: Etalab Open Licence 2.0: free reuse citing the source (CERT-FR / ANSSI) and the date of its update; official texts reproduced in French and English
