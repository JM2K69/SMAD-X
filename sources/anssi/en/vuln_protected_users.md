# vuln_protected_users — Privileged accounts outside of the Protected Users group

> [Contents: ANSSI — Active Directory control points](README.md) · Français : [vuln_protected_users.md](../fr/vuln_protected_users.md)

| Sheet | |
|:---|:---|
| Framework | ANSSI — Active Directory control points |
| Identifier | `vuln_protected_users` |
| ANSSI level | 3 (1: most critical) |
| Added to the checklist | 2019-06-11 |
| Link | [cert.cyber.gouv.fr/…](https://cert.cyber.gouv.fr/uploads/ad_checklist.html) |
| Cited by SMAD-X | DLG-04 |

## SMAD-X rules that cite it

- **DLG-04** — Privileged account not protected against delegation

## Official text

Some privileged accounts are not protected by the `S-1-5-21-domain-525` group.

*(Table of the affected objects, in the analysis report.)*

## Official recommendation

Privileged users must be members of the `S-1-5-21-domain-525` group so as to:

- enforce Kerberos authentication;
- reduce Kerberos ticket lifetime;
- enforce usage of strong encryption algorithms (AES);
- prevent caching of passwords on workstations;
- prevent any type of Kerberos delegation.

**Warning**: use of the `S-1-5-21-domain-525` group comes with significant functional impacts. Reference: [https://docs.microsoft.com/en-us/windows-server/security/credentials-protection-and-management/protected-users-security-group](https://docs.microsoft.com/en-us/windows-server/security/credentials-protection-and-management/protected-users-security-group).

## Source and licence

- **Source**: [https://cert.cyber.gouv.fr/uploads/ad_checklist.html](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)
- **Retrieved publication**: 2026-11-12
- **Retrieved on**: 2026-11-13, by `tools/SmadxSources`
- **Licence**: Etalab Open Licence 2.0: free reuse citing the source (CERT-FR / ANSSI) and the date of its update; official texts reproduced in French and English
