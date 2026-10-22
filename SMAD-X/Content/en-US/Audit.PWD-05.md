# Domain controller machine password unchanged

## What the audit found
The password of a DC's computer account has not changed for more than 45 days.

## Why it is dangerous
A computer normally renews its password every 30 days. A DC that no longer does so is either **powered off or abandoned** (but still known to the domain), or in an abnormal state (renewal disabled, replication problem). A frozen machine key makes it easier to forge service tickets (Silver Tickets) after a compromise.

## How to fix
Check the state of the DC: bring it back into service, demote it cleanly if it has been abandoned, or fix the policy that blocks renewal.
