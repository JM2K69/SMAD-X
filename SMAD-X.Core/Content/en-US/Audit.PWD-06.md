# Computer account whose password no longer changes

## What the audit found
The password of a computer account has not changed for more than 90 days.

## Why it is dangerous
An active computer renews its password every 30 days. Beyond 90 days, the machine is probably **powered off, stolen, or decommissioned** without its account being disabled. This account remains usable by anyone who has obtained its key, and it clutters the inventory.

## How to fix
Disable the account, then delete it after an observation period. Set up a decommissioning process that includes Active Directory.

## Going further
The ANSSI check targets **servers**; the rule applies it to all computers, because the reasoning is the same for a workstation.
