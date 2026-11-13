# SP 800-53 Rev. 5 IA-5 — Authenticator Management

> [Contents: NIST — SP 800-53 Rev. 5 and SP 800-63B](README.md) · Français : [IA-5.md](../fr/IA-5.md)

| Sheet | |
|:---|:---|
| Framework | NIST — SP 800-53 Rev. 5 and SP 800-63B |
| Identifier | `SP 800-53 Rev. 5 IA-5` |
| Version | 5.2.0 |
| Link | [csrc.nist.gov/…](https://csrc.nist.gov/pubs/sp/800/53/r5/upd1/final) |
| Cited by SMAD-X | KRB-02 · KRB-04 · KRB-05 · PWD-01 · PWD-02 · PWD-03 · PWD-04 · PWD-05 · PWD-07 · PWD-08 · LAPS-01 |

## SMAD-X rules that cite it

- **KRB-02** — User account vulnerable to Kerberoasting
- **KRB-04** — Account without Kerberos preauthentication
- **KRB-05** — krbtgt account password too old
- **PWD-01** — Privileged account whose password never expires
- **PWD-02** — Password never expires
- **PWD-03** — Password not required
- **PWD-04** — Privileged account password unchanged for more than 3 years
- **PWD-05** — Domain controller machine password unchanged
- **PWD-07** — Managed service account whose password no longer changes
- **PWD-08** — Domain password policy too short
- **LAPS-01** — Computer without LAPS

## Official text

Manage system authenticators by:
- a. Verifying, as part of the initial authenticator distribution, the identity of the individual, group, role, service, or device receiving the authenticator;
- b. Establishing initial authenticator content for any authenticators issued by the organization;
- c. Ensuring that authenticators have sufficient strength of mechanism for their intended use;
- d. Establishing and implementing administrative procedures for initial authenticator distribution, for lost or compromised or damaged authenticators, and for revoking authenticators;
- e. Changing default authenticators prior to first use;
- f. Changing or refreshing authenticators [Assignment: time period by authenticator type] or when [Assignment: events] occur;
- g. Protecting authenticator content from unauthorized disclosure and modification;
- h. Requiring individuals to take, and having devices implement, specific controls to protect authenticators; and
- i. Changing authenticators for group or role accounts when membership to those accounts changes.

## Source and licence

- **Source**: [https://csrc.nist.gov/pubs/sp/800/53/r5/upd1/final](https://csrc.nist.gov/pubs/sp/800/53/r5/upd1/final)
- **Retrieved publication**: SP 800-53 Rev. 5, OSCAL 5.2.0 (2026-10-22)
- **Retrieved on**: 2026-11-13, by `tools/SmadxSources`
- **Licence**: public domain: a United States Government publication (17 U.S.C. § 105); official statements reproduced
