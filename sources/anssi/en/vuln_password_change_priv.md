# vuln_password_change_priv — Privileged account passwords age too old

> [Contents: ANSSI — Active Directory control points](README.md) · Français : [vuln_password_change_priv.md](../fr/vuln_password_change_priv.md)

| Sheet | |
|:---|:---|
| Framework | ANSSI — Active Directory control points |
| Identifier | `vuln_password_change_priv` |
| ANSSI level | 1 (1: most critical) |
| Added to the checklist | 2018-10-01 |
| Link | [cert.cyber.gouv.fr/…](https://cert.cyber.gouv.fr/uploads/ad_checklist.html) |
| Cited by SMAD-X | PWD-04 |

## SMAD-X rules that cite it

- **PWD-04** — Privileged account password unchanged for more than 3 years

## Official text

Some privileged accounts in the forest have not changed their password for more than three years.

*(Table of the affected objects, in the analysis report.)*

## Official recommendation

#### General Case

Passwords should be periodically changed for all privileged group members (at most every 3 years). To enforce the domain password policy on these accounts, their `DONT_EXPIRE` flag should not be set. This account flag should then be unset, usually by unchecking the "password never expires" option in the "Account" tab of the user properties. Their passwords should then be rolled immediately.

The built-in Administrator account (RID 500, used as a last resort only) must also have its password changed at most every 3 years. It is crucial to periodically ensure this account is in working condition and that its usage guidelines and procedures are up to date.

Important note: passwords should not be renewed too often. Enforcing short password lifespans incentivizes users to choose statistically weaker passwords, which negates the intended benefits. Password lifespan should be determined by taking into account multiple factors such as usage context. The password policy may be globally enforced on all domain accounts or scoped to select accounts, through the Password Settings Object (PSO) mechanism.

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

## Source and licence

- **Source**: [https://cert.cyber.gouv.fr/uploads/ad_checklist.html](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)
- **Retrieved publication**: 2026-11-12
- **Retrieved on**: 2026-11-13, by `tools/SmadxSources`
- **Licence**: Etalab Open Licence 2.0: free reuse citing the source (CERT-FR / ANSSI) and the date of its update; official texts reproduced in French and English
