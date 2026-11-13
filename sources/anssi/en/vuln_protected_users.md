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

Some privileged accounts are not protected by the *Protected Users* group.

*(Table of the affected objects, in the analysis report.)*

## Official recommendation

Privileged users must be members of the *Protected Users* group so as to:

- enforce Kerberos authentication;
- reduce Kerberos ticket lifetime;
- enforce usage of strong encryption algorithms (AES);
- prevent caching of passwords on workstations;
- prevent any type of Kerberos delegation.

**Warning**: use of the *Protected Users* group comes with significant functional impacts. Reference: [https://docs.microsoft.com/en-us/windows-server/security/credentials-protection-and-management/protected-users-security-group](https://docs.microsoft.com/en-us/windows-server/security/credentials-protection-and-management/protected-users-security-group).

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
