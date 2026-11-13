# vuln_dont_expire — Accounts with never-expiring passwords

> [Contents: ANSSI — Active Directory control points](README.md) · Français : [vuln_dont_expire.md](../fr/vuln_dont_expire.md)

| Sheet | |
|:---|:---|
| Framework | ANSSI — Active Directory control points |
| Identifier | `vuln_dont_expire` |
| ANSSI level | 2 (1: most critical) |
| Added to the checklist | 2018-09-28 |
| Last change | 2022-11-10 |
| Link | [cert.cyber.gouv.fr/…](https://cert.cyber.gouv.fr/uploads/ad_checklist.html) |
| Cited by SMAD-X | PWD-02 |

## SMAD-X rules that cite it

- **PWD-02** — Password never expires

## Official text

More than 10% of accounts have passwords which never expire. Should an attacker compromise one of these accounts, he would be able to maintain long-term access to the Active Directory domain.

*(Table of the affected objects, in the analysis report.)*

## Official recommendation

#### General Case

In order to make Active Directory enforce periodic password change, accounts must not have the `DONT_EXPIRE` flag set. This account flag should then be unset, usually by unchecking the "password never expires" option in the "Account" tab of the user properties. Their passwords should then be rolled immediately.

While it may be counterproductive to enforce a short password lifespan, one should avoid never-changing passwords. Some passwords may have been last changed more than 10 years ago, increasing the likelihood of an exposure, reuse or weakness.

Such an exception shall not put the organization at risk of never being able to change any specific password.

Finally, Active Directory user accounts are not technically distinguishable from non-managed (non-gMSA/sMSA) service accounts. The domain-enforced password policy can either be global or differ on a population category basis through the Password Settings Object (PSO) feature. While a long password lifespan may be allowed by the organization, this must be explicitly reflected by the password policy settings. Diversified password policies may help manage passwords of some account categories and ensure proper documentation of password change procedures.

Using the `DONT_EXPIRE`flag usually results in important accounts never having their password change, not rolling those passwords after a security breach, or even may legitimize using products or setting up shared accounts with such a property, for which no password change procedure will be documented.

#### Service accounts

In order to be able to change passwords of service accounts and trace their usage, it is required to document how they are used. Thus, a list of all service accounts and their use must be kept up to date. It must include, for each service account:

- all information about the account (`samAccountName`, description, AD group membership, specificities, etc.);
- applications or systems using that account;
- the last time its password was changed;
- a procedure to change its password;
- a person or a team responsible for its lifecycle.

## Source and licence

- **Source**: [https://cert.cyber.gouv.fr/uploads/ad_checklist.html](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)
- **Retrieved publication**: 2026-11-12
- **Retrieved on**: 2026-11-13, by `tools/SmadxSources`
- **Licence**: Etalab Open Licence 2.0: free reuse citing the source (CERT-FR / ANSSI) and the date of its update; official texts reproduced in French and English
