# SP 800-53 Rev. 5 AC-2 — Account Management

> [Contents: NIST — SP 800-53 Rev. 5 and SP 800-63B](README.md) · Français : [AC-2.md](../fr/AC-2.md)

| Sheet | |
|:---|:---|
| Framework | NIST — SP 800-53 Rev. 5 and SP 800-63B |
| Identifier | `SP 800-53 Rev. 5 AC-2` |
| Version | 5.2.0 |
| Link | [csrc.nist.gov/…](https://csrc.nist.gov/pubs/sp/800/53/r5/upd1/final) |
| Cited by SMAD-X | PWD-06 · ACC-03 · ACC-01 · ACC-02 · PRIV-02 · TIER-07 · GRP-01 |

## SMAD-X rules that cite it

- **PWD-06** — Computer account whose password no longer changes
- **ACC-03** — Any user can join computers to the domain
- **ACC-01** — Active Guest account
- **ACC-02** — Dormant account
- **PRIV-02** — Orphaned adminCount
- **TIER-07** — Object waiting in a transit OU
- **GRP-01** — Group nesting loop

## Official text

- a. Define and document the types of accounts allowed and specifically prohibited for use within the system;
- b. Assign account managers;
- c. Require [Assignment: prerequisites and criteria] for group and role membership;
- d. Specify:
  - 1. Authorized users of the system;
  - 2. Group and role membership; and
  - 3. Access authorizations (i.e., privileges) and [Assignment: attributes (as required)] for each account;
- e. Require approvals by [Assignment: personnel or roles] for requests to create accounts;
- f. Create, enable, modify, disable, and remove accounts in accordance with [Assignment: policy, procedures, prerequisites, and criteria];
- g. Monitor the use of accounts;
- h. Notify account managers and [Assignment: personnel or roles] within:
  - 1. [Assignment: time period] when accounts are no longer required;
  - 2. [Assignment: time period] when users are terminated or transferred; and
  - 3. [Assignment: time period] when system usage or need-to-know changes for an individual;
- i. Authorize access to the system based on:
  - 1. A valid access authorization;
  - 2. Intended system usage; and
  - 3. [Assignment: attributes (as required)];
- j. Review accounts for compliance with account management requirements [Assignment: frequency];
- k. Establish and implement a process for changing shared or group account authenticators (if deployed) when individuals are removed from the group; and
- l. Align account management processes with personnel termination and transfer processes.

## Source and licence

- **Source**: [https://csrc.nist.gov/pubs/sp/800/53/r5/upd1/final](https://csrc.nist.gov/pubs/sp/800/53/r5/upd1/final)
- **Retrieved publication**: SP 800-53 Rev. 5, OSCAL 5.2.0 (2026-10-22)
- **Retrieved on**: 2026-11-13, by `tools/SmadxSources`
- **Licence**: public domain: a United States Government publication (17 U.S.C. § 105); official statements reproduced
