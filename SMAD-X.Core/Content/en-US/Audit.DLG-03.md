# Constrained delegation to a domain controller

## What the audit found
An account is allowed to delegate to a service hosted on a domain controller.

## Why it is dangerous
The account can present to the domain controller the identities of users who authenticate to it. If a domain administrator connects to it, the attacker who controls this account can act on the DC on their behalf.

## How to fix
Remove the delegation to DC services, or keep it only for an account that is itself administered as a Tier 0 account.
