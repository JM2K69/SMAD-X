# vuln_kerberos_properties_preauth — Kerberos preauthentication disabled

> [Contents: ANSSI — Active Directory control points](README.md) · Français : [vuln_kerberos_properties_preauth.md](../fr/vuln_kerberos_properties_preauth.md)

| Sheet | |
|:---|:---|
| Framework | ANSSI — Active Directory control points |
| Identifier | `vuln_kerberos_properties_preauth` |
| ANSSI level | 2 (1: most critical) |
| Added to the checklist | 2018-10-01 |
| Link | [cert.cyber.gouv.fr/…](https://cert.cyber.gouv.fr/uploads/ad_checklist.html) |
| Cited by SMAD-X | KRB-04 |

## SMAD-X rules that cite it

- **KRB-04** — Account without Kerberos preauthentication

## Official text

Some accounts have Kerberos preauthentication disabled. Without preauthentication, it is possible to acquire a ticket encrypted with one of the Kerberos keys associated with the requested account. It is then possible to carry out a brute force or dictionary guessing attack.

*(Table of the affected objects, in the analysis report.)*

## Official recommendation

Kerberos preauthentication ensures that users requesting a Ticket Granting Ticket (TGT) know a given authentication secret. By default, all user accounts require preauthentication because their `DONT_REQUIRE_PREAUTH` property is not set. That property was designed for backward compatibility with older Kerberos implementations.

#### General Case

The `DONT_REQUIRE_PREAUTH` property must be removed from these accounts. If an application is not compatible with this change, it must be upgraded.

#### Specific Cases

Any incompatible software must be upgraded. This issue can be mitigated as follows:

- defining a refined password policy through *Password Settings Objects* (PSO), enforcing a minimum password length of **32** characters;
- defining a refined password policy through *Password Settings Objects* (PSO), enforcing a maximum expiration duration of **3 years**;
- changing the affected user passwords after modifying the PSO settings;
- enforcing AES usage for the affected account.

## Source and licence

- **Source**: [https://cert.cyber.gouv.fr/uploads/ad_checklist.html](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)
- **Retrieved publication**: 2026-11-12
- **Retrieved on**: 2026-11-13, by `tools/SmadxSources`
- **Licence**: Etalab Open Licence 2.0: free reuse citing the source (CERT-FR / ANSSI) and the date of its update; official texts reproduced in French and English
