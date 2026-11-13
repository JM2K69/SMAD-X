# vuln_kerberos_properties_preauth_priv — Kerberos preauthentication disabled for privileged accounts

> [Contents: ANSSI — Active Directory control points](README.md) · Français : [vuln_kerberos_properties_preauth_priv.md](../fr/vuln_kerberos_properties_preauth_priv.md)

| Sheet | |
|:---|:---|
| Framework | ANSSI — Active Directory control points |
| Identifier | `vuln_kerberos_properties_preauth_priv` |
| ANSSI level | 1 (1: most critical) |
| Added to the checklist | 2018-10-01 |
| Last change | 2019-11-06 |
| Link | [cert.cyber.gouv.fr/…](https://cert.cyber.gouv.fr/uploads/ad_checklist.html) |
| Cited by SMAD-X | KRB-03 |

## SMAD-X rules that cite it

- **KRB-03** — Privileged account without Kerberos preauthentication

## Official text

The `DONT_REQUIRE_PREAUTH` flag is set for some privileged users.

*(Table of the affected objects, in the analysis report.)*

## Official recommendation

Kerberos preauthentication ensures that users requesting a Ticket Granting Ticket (TGT) know a given authentication secret. Without preauthentication, it is possible to acquire a ticket encrypted with one of the Kerberos keys associated with the requested account. It is then possible to carry out a brute force or dictionary guessing attack to crack an account password if it is not strong enough.

All affected accounts must have their `DONT_REQUIRE_PREAUTH` flag unset then their password changed immediately.

By default, all user accounts require preauthentication because their `DONT_REQUIRE_PREAUTH` flag is not set. That property was designed for backward compatibility with older Kerberos implementations. It must never be set for privileged domain accounts. Any incompatible software must be upgraded.

## Source and licence

- **Source**: [https://cert.cyber.gouv.fr/uploads/ad_checklist.html](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)
- **Retrieved publication**: 2026-11-12
- **Retrieved on**: 2026-11-13, by `tools/SmadxSources`
- **Licence**: Etalab Open Licence 2.0: free reuse citing the source (CERT-FR / ANSSI) and the date of its update; official texts reproduced in French and English
