# SP 800-53 Rev. 5 AC-6 — Least Privilege

> [Contents: NIST — SP 800-53 Rev. 5 and SP 800-63B](README.md) · Français : [AC-6.md](../fr/AC-6.md)

| Sheet | |
|:---|:---|
| Framework | NIST — SP 800-53 Rev. 5 and SP 800-63B |
| Identifier | `SP 800-53 Rev. 5 AC-6` |
| Version | 5.2.0 |
| Link | [csrc.nist.gov/…](https://csrc.nist.gov/pubs/sp/800/53/r5/upd1/final) |
| Cited by SMAD-X | DLG-01 · DLG-02 · DLG-03 · PRIV-04 · ACL-01 · ACL-02 · ACL-03 · TIER-01 · TIER-03 |

## SMAD-X rules that cite it

- **DLG-01** — Unconstrained Kerberos delegation
- **DLG-02** — Constrained delegation with protocol transition to a domain controller
- **DLG-03** — Constrained delegation to a domain controller
- **PRIV-04** — Non-privileged member of the DnsAdmins group
- **ACL-01** — Critical object with an inappropriate owner
- **ACL-02** — Control path to the domain through permissions
- **ACL-03** — Dangerous permissions on the domain, AdminSDHolder or the domain controllers
- **TIER-01** — Tiering violation: membership in a more sensitive group
- **TIER-03** — Tiering violation: delegation to a less sensitive tier

## Official text

Employ the principle of least privilege, allowing only authorized accesses for users (or processes acting on behalf of users) that are necessary to accomplish assigned organizational tasks.

## Source and licence

- **Source**: [https://csrc.nist.gov/pubs/sp/800/53/r5/upd1/final](https://csrc.nist.gov/pubs/sp/800/53/r5/upd1/final)
- **Retrieved publication**: SP 800-53 Rev. 5, OSCAL 5.2.0 (2026-10-22)
- **Retrieved on**: 2026-11-13, by `tools/SmadxSources`
- **Licence**: public domain: a United States Government publication (17 U.S.C. § 105); official statements reproduced
