# vuln_permissions_gpo_priv — Dangerous ACLs expose GPOs applied to privileged group members (attack path)

> [Contents: ANSSI — Active Directory control points](README.md) · Français : [vuln_permissions_gpo_priv.md](../fr/vuln_permissions_gpo_priv.md)

| Sheet | |
|:---|:---|
| Framework | ANSSI — Active Directory control points |
| Identifier | `vuln_permissions_gpo_priv` |
| ANSSI level | 1 (1: most critical) |
| Added to the checklist | 2020-04-06 |
| Last change | 2023-11-08 |
| Link | [cert.cyber.gouv.fr/…](https://cert.cyber.gouv.fr/uploads/ad_checklist.html) |
| Cited by SMAD-X | TIER-02 |

## SMAD-X rules that cite it

- **TIER-02** — Tiering violation: GPO from a less sensitive tier

## Official recommendation

GPO LDAP object permissions should be reviewed.

The fix should be applied is usually applied using the `gpmc.msc` utility which ensures permissions as synchronized with the SYSVOL folder.

Before accepting the risk (see below), review the list of principals and ensure that they should really be considered as T0 administrators. In case of risk acceptance, all principals (including their members for groups, recursively) for whom a control path exists to a GPO that is applied to privileged group members must also be considered as privileged and protected as such.

## Source and licence

- **Source**: [https://cert.cyber.gouv.fr/uploads/ad_checklist.html](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)
- **Retrieved publication**: 2026-11-12
- **Retrieved on**: 2026-11-13, by `tools/SmadxSources`
- **Licence**: Etalab Open Licence 2.0: free reuse citing the source (CERT-FR / ANSSI) and the date of its update; official texts reproduced in French and English
