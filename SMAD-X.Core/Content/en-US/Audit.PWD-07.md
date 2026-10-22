# Managed service account whose password no longer changes

## What the audit found
The password of a gMSA has not changed for more than 90 days.

## Why it is dangerous
A gMSA normally renews its password automatically (every 30 days by default). If it no longer does, either the account is no longer in use or renewal has been disabled: the whole point of the gMSA is lost.

## How to fix
Check that the service is still running; otherwise delete the gMSA. Check the renewal interval (msDS-ManagedPasswordInterval).
