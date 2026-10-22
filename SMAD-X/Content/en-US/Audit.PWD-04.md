# Privileged account password unchanged for more than 3 years

## What the audit found
The password of a privileged account (pwdLastSet) has not changed for more than three years.

## Why it is dangerous
The older an administrator password is, the more opportunities it has had to leak, and the more likely its stored form (NTLM hash, possibly a legacy algorithm) has been copied.

## How to fix
Change the password and apply a dedicated password policy (PSO) to administrative accounts.
