# vuln_guest — Guest account enabled

> [Contents: ANSSI — Active Directory control points](README.md) · Français : [vuln_guest.md](../fr/vuln_guest.md)

| Sheet | |
|:---|:---|
| Framework | ANSSI — Active Directory control points |
| Identifier | `vuln_guest` |
| ANSSI level | 2 (1: most critical) |
| Added to the checklist | 2021-12-02 |
| Link | [cert.cyber.gouv.fr/…](https://cert.cyber.gouv.fr/uploads/ad_checklist.html) |
| Cited by SMAD-X | ACC-01 |

## SMAD-X rules that cite it

- **ACC-01** — Active Guest account

## Official text

The *Guest* account is enabled.

The *Guest* account allows unauthenticated network users to log on as a *Guest* with no password. These unauthorized users could access any resources that are accessible to the *Guest* account over the network. This capability means that any objects or shared folders with permissions that allow access to the *Guest* account, the *Domain Guests* group, the *Guests* group, or the *Everyone* group are accessible over the network, which could lead to the exposure or corruption of data.

*(Table of the affected objects, in the analysis report.)*

## Official recommendation

Disable the *Guest* account.

## Source and licence

- **Source**: [https://cert.cyber.gouv.fr/uploads/ad_checklist.html](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)
- **Retrieved publication**: 2026-11-12
- **Retrieved on**: 2026-11-13, by `tools/SmadxSources`
- **Licence**: Etalab Open Licence 2.0: free reuse citing the source (CERT-FR / ANSSI) and the date of its update; official texts reproduced in French and English
