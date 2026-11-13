# info_spn_present — User accounts with SPN

> [Contents: ANSSI — Active Directory control points](README.md) · Français : [info_spn_present.md](../fr/info_spn_present.md)

| Sheet | |
|:---|:---|
| Framework | ANSSI — Active Directory control points |
| Identifier | `info_spn_present` |
| Link | [cert.cyber.gouv.fr/…](https://cert.cyber.gouv.fr/uploads/ad_checklist.html) |
| Cited by SMAD-X | KRB-02 |

## SMAD-X rules that cite it

- **KRB-02** — User account vulnerable to Kerberoasting

## Official text

The `servicePrincipalName` (SPN) attribute allows linking Kerberos service names to accounts. Whenever an account owns a Kerberos service name, it becomes possible for any user to request a ticket for that service. In that case, the received ticket is encrypted with one of the corresponding account Kerberos encryption keys. Thus, it is possible to start an offline brute force attack on the ticket to recover the account password if it is not strong enough. By default, only computer accounts have SPNs.

The following SPNs are set:

*(Table of the affected objects, in the analysis report.)*

## Source and licence

- **Source**: [https://cert.cyber.gouv.fr/uploads/ad_checklist.html](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)
- **Retrieved publication**: 2026-11-12
- **Retrieved on**: 2026-11-13, by `tools/SmadxSources`
- **Licence**: Etalab Open Licence 2.0: free reuse citing the source (CERT-FR / ANSSI) and the date of its update; official texts reproduced in French and English
