# vuln_spn_priv — Privileged accounts with SPN

> [Contents: ANSSI — Active Directory control points](README.md) · Français : [vuln_spn_priv.md](../fr/vuln_spn_priv.md)

| Sheet | |
|:---|:---|
| Framework | ANSSI — Active Directory control points |
| Identifier | `vuln_spn_priv` |
| ANSSI level | 1 (1: most critical) |
| Added to the checklist | 2018-09-27 |
| Link | [cert.cyber.gouv.fr/…](https://cert.cyber.gouv.fr/uploads/ad_checklist.html) |
| Cited by SMAD-X | KRB-01 |

## SMAD-X rules that cite it

- **KRB-01** — Privileged account vulnerable to Kerberoasting

## Official text

The `servicePrincipalName` (SPN) attribute is set on some privileged user accounts.

An attacker could use this to attempt a brute force password cracking attempt, which may succeed for accounts with weak passwords.

*(Table of the affected objects, in the analysis report.)*

## Official recommendation

The `servicePrincipalName` (SPN) attribute allows linking Kerberos service names to accounts. Whenever an account owns a Kerberos service name, it becomes possible for any user to request a ticket for that service. In that case, the received ticket is encrypted with one of the corresponding account Kerberos encryption keys. Thus, it is possible to start an offline brute force attack on the ticket to recover the account password if it is not strong enough. By default, only computer accounts have SPNs and the attribute must remain empty for all privileged accounts.

#### General case: clear SPNs

In the event that accounts currently with an SPN set must be privileged, their `servicePrincipalName` attribute must be cleared and their password must be changed.

Note: a privileged account may hold an SPN granted the following criteria are met:

- the account is a *Managed Service Account* (`sMSA` / `gMSA`);
- **OR**
  - a *Password Settings Object* (PSO) applies to that account with a **32** or more character length requirement;
  - a *Password Settings Object* (PSO) applies to that account with an expiration period of less than **3 years**;
  - the account password was changed since the last PSO modification;
- the account can only use AES encryption algorithms.

*Password Settings Objects* cannot be taken into account if ORADAD has not been able to retrieve them. In case of failure, a level 3 warning named "Missing fine-grained password policies (PSO)" will appear; its recommendations should be followed.

#### Specific cases: remove the account from privileged groups

In the event that accounts currently with an SPN set do not require their current privileged group membership, they should be removed from those groups and be granted the relevant access rights delegations.

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
