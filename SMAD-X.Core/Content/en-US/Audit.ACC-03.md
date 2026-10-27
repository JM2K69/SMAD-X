# Any user can join computers to the domain

## What the audit found
The domain's ms-DS-MachineAccountQuota attribute is above 0: any authenticated user can create computer accounts, 10 by default.

## Why it is dangerous
Whoever creates a computer account becomes its owner, with full control over it. An attacker holding the smallest account can thus craft computers at will: this is the first step of resource-based constrained delegation (RBCD) and of the noPac attack (CVE-2021-42287). ANSSI ranks this check at level 4.

## How to fix
Set the quota to 0 (select the domain, "Password policy" section), then delegate joining computers to a dedicated group, on the workstations OU ("Create computer objects" right).
