# Account without Kerberos preauthentication

## What the audit found
The "Do not require Kerberos preauthentication" option is checked on an account.

## Why it is dangerous
Anyone on the network can request a TGT for this account and obtain data encrypted with its password, then crack it offline (**AS-REP roasting**). The compromised account then serves as an entry point into the domain.

## How to fix
- Uncheck the option.
- If a legacy application really requires it: a very long password, an account with no privileges at all, and monitoring of TGT requests (event 4768 with a preauthentication type of 0).
