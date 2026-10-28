# Dangerous permissions on the domain, AdminSDHolder or the domain controllers

## What the audit found
An unprivileged account or group (or Everyone, Authenticated Users, Domain Users) holds a control right on the domain root, on the AdminSDHolder object, on the Domain Controllers OU or on a domain controller: full control, generic write, modifying permissions or owner, replication, all extended rights, writing msDS-KeyCredentialLink or msDS-AllowedToActOnBehalfOfOtherIdentity, linking a GPO, resetting the password.

## Why it is dangerous
Each of these rights gives the domain away in one step:
- **replication** on the domain lets one extract the secrets of every account, krbtgt included (DCSync);
- **AdminSDHolder** is a template: SDProp copies its permissions every hour onto every protected account and group;
- on a **domain controller**, writing msDS-KeyCredentialLink (Shadow Credentials) or msDS-AllowedToActOnBehalfOfOtherIdentity (RBCD) lets one authenticate as it, or get an administrator ticket to it;
- **linking a GPO** to the domain or to the Domain Controllers OU runs code on the DCs.

On a fresh domain, only privileged groups hold these rights: Enterprise Admins, Domain Admins, Domain Controllers, Key Admins, Enterprise Key Admins.

## How to fix
Remove the ACE. A real need (a synchronisation tool that replicates passwords, a DC support group) is handled with a dedicated account, classified and protected like a Tier 0 administrator.
