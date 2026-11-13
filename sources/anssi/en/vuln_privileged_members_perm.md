# vuln_privileged_members_perm — Dangerous ACLs expose members of privileged groups (attack path)

> [Contents: ANSSI — Active Directory control points](README.md) · Français : [vuln_privileged_members_perm.md](../fr/vuln_privileged_members_perm.md)

| Sheet | |
|:---|:---|
| Framework | ANSSI — Active Directory control points |
| Identifier | `vuln_privileged_members_perm` |
| ANSSI levels | 1, 2 (1: most critical; one threshold per level, see the text) |
| Added to the checklist | 2019-01-11 |
| Last change | 2023-11-08 |
| Link | [cert.cyber.gouv.fr/…](https://cert.cyber.gouv.fr/uploads/ad_checklist.html) |
| Cited by SMAD-X | ACL-02 · TIER-03 |

## SMAD-X rules that cite it

- **ACL-02** — Control path to the domain through permissions
- **TIER-03** — Tiering violation: delegation to a less sensitive tier

## Official text

This assessment raises an alert depending on varying thresholds: at level **1** only dangerous access control entries that can be exploited without any major prerequisite are taken into account; at level **2** all potentially dangerous access control entries are taken into account.

Dangerous ACLs expose members of privileged groups. These access rights grant non-privileged users the ability to take control of privileged accounts.

*(Table of the affected objects, in the analysis report.)*

## Official recommendation

Most of the items shown here are due to the "`Dangerous permissions on the adminSDHolder object`" and "`Dangerous ACLs expose domain controller objects`" checks. Fixing it will take care of most of these items.

For all remaining permissions, relevant dangerous entries should be removed. The fix is usually applied through the `adsiedit.msc` or `ldp` utilities.

## Source and licence

- **Source**: [https://cert.cyber.gouv.fr/uploads/ad_checklist.html](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)
- **Retrieved publication**: 2026-11-12
- **Retrieved on**: 2026-11-13, by `tools/SmadxSources`
- **Licence**: Etalab Open Licence 2.0: free reuse citing the source (CERT-FR / ANSSI) and the date of its update; official texts reproduced in French and English
