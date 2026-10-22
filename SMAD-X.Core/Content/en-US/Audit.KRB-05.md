# krbtgt account password too old

## What the audit found
The password of the `krbtgt` account has not been changed for more than a year.

## Why it is dangerous
All Kerberos tickets (TGTs) in the domain are signed with the `krbtgt` key. An attacker who stole it at some point (for example via DCSync) can forge **Golden Tickets**: administrator tickets that remain valid as long as this key does not change. Never renewing `krbtgt` leaves a permanent open door after a past compromise.

## How to fix
- Reset the password **twice**, waiting for the maximum ticket lifetime (10 hours by default) between the two resets: the domain keeps the previous key in memory.
- Do this at least once a year, and immediately after any compromise.
- Microsoft provides a reset procedure and script (see references).
