# warning_privileged_members_not_empty — Privileged groups not empty

> [Contents: ANSSI — Active Directory control points](README.md) · Français : [warning_privileged_members_not_empty.md](../fr/warning_privileged_members_not_empty.md)

| Sheet | |
|:---|:---|
| Framework | ANSSI — Active Directory control points |
| Identifier | `warning_privileged_members_not_empty` |
| ANSSI level | 3 (1: most critical) |
| Added to the checklist | 2019-01-24 |
| Link | [cert.cyber.gouv.fr/…](https://cert.cyber.gouv.fr/uploads/ad_checklist.html) |
| Cited by SMAD-X | PRIV-03 |

## SMAD-X rules that cite it

- **PRIV-03** — Privileged group that should be empty

## Official text

Privileged groups in the forest are not empty or contain accounts other than built-in administrative ones (RID 500):

*(Table of the affected objects, in the analysis report.)*

Note: the user count doesn't include built-in administrative accounts.

## Official recommendation

Implementing an administrative model which allows minimizing the number of privileged accounts is crucial, especially by establishing well-separated administrative roles. In doing so, administrative actions should be inventoried and affected to specific accounts through relevant rights delegations. For instance, a user management account that needs to be able to create and delete user accounts will benefit from the following delegations:

- child object creation;
- child object deletion;
- write to object attribute;
- reset password.

Note : the Active Directory "Delegation of Control" Wizard also helps delegating access rights. While of easier use, this wizard grants excessive access rights for some delegation categories. `ldp` should therefore be preferred.

The overall model must follow the following rules regarding group membership:

- operator groups must be empty;
- replication groups must be empty;
- forest-level administrative groups (enterprise and schema) must be empty and only temporarily populated during operations which require those privileges;
- domain-level administrative groups must not contain any service account.

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
