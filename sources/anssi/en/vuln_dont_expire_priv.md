# vuln_dont_expire_priv — Privileged accounts with never-expiring passwords

> [Contents: ANSSI — Active Directory control points](README.md) · Français : [vuln_dont_expire_priv.md](../fr/vuln_dont_expire_priv.md)

| Sheet | |
|:---|:---|
| Framework | ANSSI — Active Directory control points |
| Identifier | `vuln_dont_expire_priv` |
| ANSSI level | 1 (1: most critical) |
| Added to the checklist | 2018-10-12 |
| Link | [cert.cyber.gouv.fr/…](https://cert.cyber.gouv.fr/uploads/ad_checklist.html) |
| Cited by SMAD-X | PWD-01 |

## SMAD-X rules that cite it

- **PWD-01** — Privileged account whose password never expires

## Official text

Some privileged accounts have passwords that never expire.

If no security mechanism enforces a periodic password rotation, taking over an account allows any malicious user to keep their access rights in the domain for extended periods of time.

*(Table of the affected objects, in the analysis report.)*

## Official recommendation

#### General Case

Passwords should be periodically changed for all privileged group members (at most every 3 years). To enforce the domain password policy on these accounts, their `DONT_EXPIRE` flag should not be set. This account flag should then be unset, usually by unchecking the "password never expires" option in the "Account" tab of the user properties. Their passwords should then be rolled immediately.

Important note: passwords should not be renewed too often. Enforcing short password lifespans incentivizes users to choose statistically weaker passwords, which negates the intended benefits. Password lifespan should be determined by taking into account multiple factors such as usage context. The password policy may be globally enforced on all domain accounts or scoped to select accounts, through the Password Settings Object (PSO) mechanism.

When the `DONT_EXPIRE` flag is not set, Active Directory will compute the `msDS-UserPasswordExpiryTimeComputed` for the relevant accounts. This attribute contains the password expiry date, which can be used to anticipate password changes, especially for service accounts.

#### Specific Cases

If the `DONT_EXPIRE` flag has been set on accounts with difficult-to-change passwords, it may mean these accounts are not used for administration purposes (i.e., service accounts). These accounts shall not belong to any privileged group. The following must be done:

- remove those accounts from privileged groups;
- grant these accounts the required rights delegations for proper operation;
- document a specific password change procedure;
- remove the `DONT_EXPIRE` flag.

#### Unused accounts

Unused accounts should be disabled. The following procedure provides similar benefits than deletion, while retaining information relevant to audit traces. To properly disable an account:

- disable the account;
- remove all of its group memberships;
- randomly change its password. To enforce a random password, one can check the *smart card is required for interactive logon* flag in the account properties. This is equivalent to manually setting the `ADS_UF_SMARTCARD_REQUIRED` flag of its `userAccountControl` attribute;
- optionally, move the account to a dedicated OU;
- optionally, update the account description field to mention the context for disabling (date, reason, etc.).

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
