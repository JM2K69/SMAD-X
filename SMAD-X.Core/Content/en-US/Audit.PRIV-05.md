# Protected group classified outside Tier 0

## What the audit found
A group protected by AdminSDHolder is classified in a less sensitive tier than Tier 0.

## Why it is dangerous
These groups grant control of the domain controllers, either directly or through well-known paths. Classifying them in Tier 1 or 2 leads to administering them from less protected workstations and to putting less closely monitored accounts in them.

## How to fix
Classify the group in Tier 0, and apply the Tier 0 rules to its members: dedicated accounts, dedicated administrative workstations, no logons on lower tiers.
