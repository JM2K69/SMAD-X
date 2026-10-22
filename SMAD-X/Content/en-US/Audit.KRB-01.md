# Privileged account vulnerable to Kerberoasting

## What the audit found
A **user** account that is a member (directly or through nesting) of a privileged group has one or more SPNs (servicePrincipalName).

## Why it is dangerous
Any domain user can request a service ticket (TGS) for an SPN. This ticket is encrypted with a key derived from the **service account's password**: the attacker takes it offline and tries to crack it by brute force, without triggering any lockout. If the password gives way, the attacker becomes an administrator.

## How to fix
- Remove the account from privileged groups: a service account almost never needs to be a domain administrator.
- Replace the account with a **gMSA** (random 120-character password, renewed automatically).
- Failing that: a long password (more than 25 characters) and AES encryption only.

## Going further
ANSSI tolerates a privileged account with an SPN only in specific configurations (check `warning_spn_priv`). The `krbtgt` accounts (SPN `kadmin/changepw`) and gMSAs are not affected.
