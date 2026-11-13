# vuln_silo_priv — Privileged group members not in an authentication silo

> [Contents: ANSSI — Active Directory control points](README.md) · Français : [vuln_silo_priv.md](../fr/vuln_silo_priv.md)

| Sheet | |
|:---|:---|
| Framework | ANSSI — Active Directory control points |
| Identifier | `vuln_silo_priv` |
| ANSSI level | 4 (1: most critical) |
| Added to the checklist | 2020-07-03 |
| Last change | 2025-01-23 |
| Link | [cert.cyber.gouv.fr/…](https://cert.cyber.gouv.fr/uploads/ad_checklist.html) |
| Cited by SMAD-X | TIER-04 · TIER-09 |

## SMAD-X rules that cite it

- **TIER-04** — Privileged account logged on to a less sensitive machine
- **TIER-09** — Privileged account outside an enforced authentication silo

## Official text

Some privileged group members are not member of an authentication silo, or it's not properly configured. The authentication policy silo must be enforced (`msDS-AuthNPolicySiloEnforced` must be set to TRUE). Silo members must also be assigned to the silo, by checking the "Assign Authentication Policy Silo" box, which sets the `msDS-AssignedAuthNPolicySilo` LDAP attribute. This value is available in the data associated with the "Privileged groups members" informative item.

*(Table of the affected objects, in the analysis report.)*

## Official recommendation

All privileged group members must be in a properly configured authentication silo.

> #### Context: privileged groups
>
> Privileged groups include administrator groups and operator groups which have full control over the Active Directory forest either by design, or by granting themselves such privileges:
>
> - « *Administrators* »;
> - « *Domain Controllers* »;
> - « *Schema Admins* »;
> - « *Enterprise Admins* »;
> - « *Domain Admins* »: these administrators are able to read the authentication database and extract password hashes and other secrets of all privileged accounts;
> - « *Key Admins* » and « *Enterprise Key Admins* »: these administrators can set arbitrary values to attributes relating to Windows Hello for Business, for all users except those protected by the `adminSDHolder` mechanism. If certificate-based authentication has been enabled, they can generate a certificate that they control, assign it to privileged accounts (for instance, a domain controller), and log on as them;
> - « *Account Operators* »: these operators can manage user accounts, machine accounts and groups except accounts protected by the `adminSDHolder` mechanism;
> - « *Server Operators* »: these operators can manage domain controllers, which grants them the ability to recover privileged user secrets directly from the authentication database;
> - « *Backup Operators* »: these operators can back up a domain controller, which grants them the ability to recover privileged user secrets from that backup;
> - « *Print Operators* »: these operators can load arbitrary printer drivers on domain controllers, which grants them the ability to recover privileged user secrets directly from the authentication database (by loading a malicious driver).
>
> The « operators » groups are empty by default and must not have any member.

## Source and licence

- **Source**: [https://cert.cyber.gouv.fr/uploads/ad_checklist.html](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)
- **Retrieved publication**: 2026-11-12
- **Retrieved on**: 2026-11-13, by `tools/SmadxSources`
- **Licence**: Etalab Open Licence 2.0: free reuse citing the source (CERT-FR / ANSSI) and the date of its update; official texts reproduced in French and English
