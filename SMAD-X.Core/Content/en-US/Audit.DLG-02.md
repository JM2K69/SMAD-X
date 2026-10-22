# Constrained delegation with protocol transition to a domain controller

## What the audit found
An account can delegate, **with protocol transition**, to a service hosted on a domain controller.

## Why it is dangerous
Protocol transition (S4U2Self) allows the account to obtain a ticket on behalf of **any user, without that user having authenticated**. Combined with delegation to a DC service (for example `ldap/` or `cifs/`), it allows impersonating a domain administrator to the DC: compromising this account amounts to compromising the domain.

## How to fix
- Remove the delegation to domain controller services.
- Treat the account as a Tier 0 account for as long as the delegation exists.
