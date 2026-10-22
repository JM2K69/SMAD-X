# Privileged account whose password never expires

## What the audit found
The "Password never expires" option is checked on a privileged account.

## Why it is dangerous
An administrator password that never changes remains usable indefinitely if it ever leaked (phishing, configuration file, stolen hash database). ANSSI rates this check at level 1, the most critical.

## How to fix
Uncheck the option and apply a dedicated password policy (PSO) to privileged accounts: a substantial length and regular renewal.

## Going further
NIST (SP 800-63B) advises against forcing periodic password changes **on users**, because it pushes them toward predictable passwords. ANSSI, on the other hand, requires expiration for privileged accounts. The two views converge: a long, unique password, changed as soon as a compromise is possible. A good topic for classroom debate!

By default, the Administrator account of a new domain has this option set: a freshly installed AD is not compliant.
