# Computer without LAPS

## What the audit found
The password of this computer's local administrator account is not managed by LAPS: no LAPS password expiration date is recorded in the directory.

## Why it is dangerous
Without LAPS, the local administrator password is often the same on every machine, set by the deployment image. Whoever retrieves it on one workstation (or its hash) uses it on all the others: this is the most common lateral movement (pass-the-hash), up to a machine where an administrator has a session.

## How to fix
Deploy Windows LAPS through a GPO on workstations and servers: each machine gets a unique, random and renewed password, stored in the directory. Then restrict who can read it to an administration group from the machine's tier: too broad a read right is itself an attack path.
