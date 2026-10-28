# Security audit

**Analysis > Security audit** (`Ctrl+Shift+A`) checks the domain against **35 rules**. Each one relies on recognised sources:

- the [ANSSI Active Directory checklist](https://cert.cyber.gouv.fr/uploads/ad_checklist.html), with each control point's official identifier and level;
- **NIST**: SP 800-53 (security controls) and SP 800-63B (authentication);
- **CIS Controls** v8;
- **MITRE ATT&CK**, for the matching attack techniques;
- **Microsoft** documentation (tiered access model, protected accounts, delegation).

![Audit window](assets/screens/audit.png)

## Reading the result

| Element | Meaning |
|---|---|
| **Score** | 100 minus one penalty per failed rule: 12 (critical), 6 (high), 3 (medium), 1 (low). A rule counts once, whether it affects one object or ten, so that a widespread issue does not overshadow a rare but critical one. |
| **Estimated ANSSI level** | the level of the most critical failed ANSSI control point, from 1 (most severe) to 5 (no failed point). It is an ANSSI-style estimate, not the official computation of ANSSI's ADS tool. |
| **Counters** | findings per severity, and a severity filter |
| **Finding list** | rule, affected object and detail (for example "password unchanged for 730 days") |
| **Sheet** | what the audit found, why it is dangerous, how to fix, going further, and clickable references |

**Show in tree** selects the affected object; the tree also shows an `AUDIT` badge on every object at fault. Fix, then **Run audit again**.

The screenshot shows the sample domain with a weakness added for this documentation: the help desk owns the Schema Admins group (ACL-01).

## Rule families

| Prefix | Area | Examples |
|---|---|---|
| `KRB` | Kerberos | kerberoastable accounts, AS-REP roasting, old krbtgt |
| `DLG` | delegation | unconstrained delegation outside DCs, constrained delegation to a DC, delegable privileged accounts |
| `PWD` | passwords | expiry, password not required, unchanged passwords (privileged accounts, DCs, computers, managed service accounts) |
| `ACC` | accounts | Guest enabled, dormant accounts |
| `PRIV` | privileged groups | groups that should stay empty, DnsAdmins, missing or orphaned adminCount, protected group outside Tier 0 |
| `ACL` | owners and permissions | non-administrative owner of a critical object (DC GPOs included), dangerous permissions on the domain, AdminSDHolder or the DCs (DCSync, Shadow Credentials, RBCD), control path through permissions from an ordinary account |
| `TIER` | tiering | membership, GPO, delegation or local administration across tiers, administrator session on a less sensitive machine |
| `LAPS` | workstations and servers | computer whose local administrator password is not managed by LAPS |
| `GRP` | groups | nesting loops |

## A fresh domain, like a real one

The SMAD-X fresh domain has the same default permissions as a freshly installed Active Directory: Enterprise Admins full control on the root, replication for the domain controllers, `msDS-KeyCredentialLink` write for Key Admins and Enterprise Key Admins, Enterprise Admins and Domain Admins rights on AdminSDHolder; every joined computer has Domain Admins as local administrator. These permissions all go to privileged groups: the audit does not report them, just as it would not on a real fresh domain. What it finds there (password policy too short, machine account quota, built-in administrator) is what it would find on an AD fresh out of setup.

The full list, with each rule's severity, ANSSI level and references, is on the [Audit rules](reference/audit-rules.md) page, generated from the code.

## Discuss rather than apply

Sheets point out where frameworks disagree. Example: NIST (SP 800-63B) advises against forcing periodic password changes **on users**, while ANSSI requires expiry for **privileged accounts**. Both views meet: a long, unique password, changed as soon as a compromise is possible.
