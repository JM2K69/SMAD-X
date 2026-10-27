# Domain password policy too short

## What the audit found
The default domain policy (Default Domain Policy) accepts passwords shorter than 8 characters. This is the value of a freshly installed AD: 7 characters.

## Why it is dangerous
Every account without a PSO follows this policy. A 7-character password, even a complex one, is cracked offline in a few hours from a captured hash (Kerberoasting, database backup, NTLM).

## How to fix
Set the domain minimum length to at least 8 characters (select the domain, "Password policy" section), and preferably 12 or more. Accounts that need more get a PSO.

## Going further
The frameworks do not set the same threshold. NIST (SP 800-63B) asks for 8 characters with multi-factor authentication and 15 without, and advises against forcing periodic changes. CIS Controls 5.2 asks for 8 with MFA and 14 without. ANSSI, for privileged accounts, asks for at least 8 characters and a renewal at least every 3 years. Which threshold would you choose for your users, and why?
