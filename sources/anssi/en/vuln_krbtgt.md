# vuln_krbtgt — Krbtgt account password unchanged for more than a year

> [Contents: ANSSI — Active Directory control points](README.md) · Français : [vuln_krbtgt.md](../fr/vuln_krbtgt.md)

| Sheet | |
|:---|:---|
| Framework | ANSSI — Active Directory control points |
| Identifier | `vuln_krbtgt` |
| ANSSI level | 2 (1: most critical) |
| Added to the checklist | 2018-09-26 |
| Link | [cert.cyber.gouv.fr/…](https://cert.cyber.gouv.fr/uploads/ad_checklist.html) |
| Cited by SMAD-X | KRB-05 |

## SMAD-X rules that cite it

- **KRB-05** — krbtgt account password too old

## Official text

The `krbtgt` account password has not been changed for more than a year.

The `krbtgt` Kerberos infrastructure account in all Active Directory domains is used as a support for key storage in all Kerberos Key Distribution Centers (KDC).

Taking over a `krbtgt` account allows an attacker to forge Kerberos tickets of *Ticket Granting Ticket* (TGT) type (so-called *golden tickets*), then authenticate to any resource (server, workstation, etc.) in the domain with full administrative privileges in a more or less stealth manner.

The `krbtgt` account does not change its password automatically, so if the account database was extracted (for instance, by a former administrator, during an audit, or for a password strength assessment), it is possible to use information from that database to extract the `krbtgt` secrets. A malicious user can then compromise all Active Directory services many years after the information was extracted.

*(Table of the affected objects, in the analysis report.)*

## Official recommendation

The `krbtgt` Kerberos infrastructure account in all Active Directory domains is used as a support for key storage in all Kerberos Key Distribution Centers (KDC). In order to renew Kerberos keys used to encrypt TGTs, the `krbtgt` account must have its password manually changed periodically. Using the [Microsoft-provided script is recommended](https://github.com/microsoft/New-KrbtgtKeys.ps1).

The password change must be carried out twice in a row to effectively prevent fraudulent usage of previous passwords (the current and previous passwords are both valid at any one time).

Warning: any password change operation for the `krbtgt` account must be carried out in an Active Directory environment with working, healthy domain controller replication. A sufficient delay must be observed between the two consecutive password changes for the values to be replicated to all domain controllers.

The `krbtgt` can be manually rolled, the same way one can force change of a user password. If the provided script is not used, a delay of 24 hours should be observed between the two consecutive password changes as well as ensuring replication is properly working between all domain controllers. An alternative strategy could be rolling this password only once every 6 months, thus ensuring proper revocation of all previous passwords every year.

## Source and licence

- **Source**: [https://cert.cyber.gouv.fr/uploads/ad_checklist.html](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)
- **Retrieved publication**: 2026-11-12
- **Retrieved on**: 2026-11-13, by `tools/SmadxSources`
- **Licence**: Etalab Open Licence 2.0: free reuse citing the source (CERT-FR / ANSSI) and the date of its update; official texts reproduced in French and English
