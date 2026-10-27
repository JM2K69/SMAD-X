# Privileged account with a weak password policy

## What the audit found
A privileged account follows a password policy (the domain's or a PSO) that accepts fewer than 8 characters, or requires no renewal within 3 years.

## Why it is dangerous
A domain administrator's password opens the whole directory. Short, it is cracked offline; never renewed, a hash stolen years ago still works. ANSSI ranks this check at level 2.

## How to fix
Apply a dedicated PSO to privileged accounts (to the administrators' group): at least 8 characters, preferably 15 or more, and a maximum age of 3 years at most. Check that no weaker PSO with a lower precedence applies to them.

## Going further
Renewing too often is counter-productive: both ANSSI and NIST point out that it leads to weaker passwords. Why does ANSSI still set a 3-year maximum for privileged accounts?
