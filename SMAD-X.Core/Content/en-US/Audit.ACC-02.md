# Dormant account

## What the audit found
An enabled user account has not logged on for more than 45 days (or never).

## Why it is dangerous
A dormant account often belongs to someone who has left, to a contractor, or to a forgotten test. No one will notice if an attacker uses it, and its password is no longer monitored.

## How to fix
- Disable the account, then delete it after an observation period.
- Tie the account lifecycle to the HR process (joiners, leavers, internal moves).

## Going further
The 45-day threshold comes from the CIS Controls (5.3). The lastLogonTimestamp attribute is only replicated with a delay of 9 to 14 days: it gives an order of magnitude, not an exact date.
