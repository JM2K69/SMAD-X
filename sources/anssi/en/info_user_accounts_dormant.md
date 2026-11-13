# info_user_accounts_dormant — Dormant accounts

> [Contents: ANSSI — Active Directory control points](README.md) · Français : [info_user_accounts_dormant.md](../fr/info_user_accounts_dormant.md)

| Sheet | |
|:---|:---|
| Framework | ANSSI — Active Directory control points |
| Identifier | `info_user_accounts_dormant` |
| Added to the checklist | 2018-09-26 |
| Last change | 2024-04-23 |
| Link | [cert.cyber.gouv.fr/…](https://cert.cyber.gouv.fr/uploads/ad_checklist.html) |
| Cited by SMAD-X | ACC-02 |

## SMAD-X rules that cite it

- **ACC-02** — Dormant account

## Official text

Many accounts in the forest are dormant (… out of … enabled accounts, or …%). These are:

- user or computer accounts,
- that are not disabled,
- whose password was last set more than three years ago,
- that have not authenticated against the Active Directory for more than a year, or were created more than a year ago and have never authenticated.

Dormant accounts are either legitimate accounts which are rarely used, or obsolete accounts.

Obsolete accounts can give people illegitimate accesses (e.g., after they left the company), which is even more problematic if the accounts are privileged. Their mere existence also increases the amount of work needed to keep track of all accounts and their access rights.

## Source and licence

- **Source**: [https://cert.cyber.gouv.fr/uploads/ad_checklist.html](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)
- **Retrieved publication**: 2026-11-12
- **Retrieved on**: 2026-11-13, by `tools/SmadxSources`
- **Licence**: Etalab Open Licence 2.0: free reuse citing the source (CERT-FR / ANSSI) and the date of its update; official texts reproduced in French and English
