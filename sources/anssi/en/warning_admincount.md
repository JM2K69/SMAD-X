# warning_admincount — Accounts with adminCount attribute

> [Contents: ANSSI — Active Directory control points](README.md) · Français : [warning_admincount.md](../fr/warning_admincount.md)

| Sheet | |
|:---|:---|
| Framework | ANSSI — Active Directory control points |
| Identifier | `warning_admincount` |
| ANSSI levels | 1, 3 (1: most critical; one threshold per level, see the text) |
| Added to the checklist | 2018-09-27 |
| Link | [cert.cyber.gouv.fr/…](https://cert.cyber.gouv.fr/uploads/ad_checklist.html) |
| Cited by SMAD-X | PRIV-02 |

## SMAD-X rules that cite it

- **PRIV-02** — Orphaned adminCount

## Official text

Several active accounts in the forest have their `adminCount` attribute set, however they don't belong to privileged groups anymore, which are protected by the adminSDHolder mechanism. Privileged groups are groups with administrative rights and privileges over the entire forest, or who can trivially grant themselves these rights ("`Administrators`", "`Enterprise Admins`", "`Schema Admins`" and "`Domain Admins`") as well as operator groups ("`Accounts Operators`", "`Server Operators`", "`Backup Operators`" and "`Print Operators`").

Note:

- at grade **1** this warning shows the non-privileged accounts with `adminCount` = 1;
- at grade **3** it shows all the accounts with `adminCount` = 0 (bad practice).

*(Table of the affected objects, in the analysis report.)*

Accounts with DACL inheritance  risk being excluded from an organization's Active Directory delegation model, despite them no longer being exceptional accounts due to domain-level privileges.

## Official recommendation

This situation can occur after temporarily adding users to the most privileged administrative groups in Active Directory, which is a bad administration practice.

The following modifications occur whenever an account joins a privileged group protected by `adminSDHolder`:

- its `adminCount` attribute is set to 1;
- permission inheritance is disabled;
- permissions are periodically copied from the `adminSDHolder` object (the actual mechanism called `adminSDHolder` protection).

When an account leaves all groups protected by `adminSDHolder`, permissions are not copied onto it anymore. However, its `adminCount` attribute remains set to 1, and inheritance remains disabled. This results in an unexpected state called *orphaned adminSDHolder*.

This mechanism allows detecting accounts who have been member of a privileged group in the past, thus who have held considerable privileges. As a matter of fact, despite being removed from these privileged groups, it is impossible to guarantee that the account lost all the access rights and privileges it held. For instance, users could still own objects in Active Directory (GPO, computers, users, etc.)

The `adminCount` attribute, whenever set to 1, indicates that an account (user or computer) has been a member of the most privileged group(s) in Active Directory, at some point in time.

Such accounts must be dealt with in two steps:

- Find out and document the reason (if possible) for their `adminCount` attribute being set to 1 and, if need be, ban administration methods involving temporarily adding users to the most privileged Active Directory groups;
- Find out (if possible) what actions have been performed by these accounts;
- If the risk is accepted, update the `adminCount` attribute to 0. Document this action and keep a list of these accounts.
- If the risk is not accepted, disable the account and add the `ADS_UF_SMARTCARD_REQUIRED` property in the `userAccountControl` attribute.

## Source and licence

- **Source**: [https://cert.cyber.gouv.fr/uploads/ad_checklist.html](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)
- **Retrieved publication**: 2026-11-12
- **Retrieved on**: 2026-11-13, by `tools/SmadxSources`
- **Licence**: Etalab Open Licence 2.0: free reuse citing the source (CERT-FR / ANSSI) and the date of its update; official texts reproduced in French and English
