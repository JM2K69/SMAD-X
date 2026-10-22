# User account vulnerable to Kerberoasting

## What the audit found
A (non-privileged) **user** account has an SPN: it is usually a service account.

## Why it is dangerous
As with privileged accounts, any domain user can obtain a ticket encrypted with this account's password and crack it offline. Service accounts often have old passwords that are never changed, and significant rights on the servers they run on.

## How to fix
- Migrate the service to a **gMSA** when the application supports it.
- Otherwise: a long, random password, AES encryption, and an inventory of service accounts (CIS 5.5).
- Remove SPNs that no longer match any service.

## Going further
A gMSA often has an SPN without any risk: its password cannot be cracked in practice. That is why the rule only targets user accounts.
