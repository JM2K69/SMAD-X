# Privileged account without Kerberos preauthentication

## What the audit found
The "Do not require Kerberos preauthentication" option (DONT_REQ_PREAUTH) is checked on a privileged account.

## Why it is dangerous
Without preauthentication, the domain controller answers a TGT request for this account **without the requester proving they know the password**. The response contains data encrypted with the account's password: this is **AS-REP roasting**. There is no need to be authenticated in the domain to request it.

## How to fix
Uncheck the option. It only exists for legacy Kerberos clients and has no reason to be set on an administrative account.
