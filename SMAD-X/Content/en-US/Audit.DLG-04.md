# Privileged account not protected against delegation

## What the audit found
A privileged account is neither a member of the **Protected Users** group nor marked "Account is sensitive and cannot be delegated".

## Why it is dangerous
An administrator's credentials can then be delegated to a compromised server, or left in memory in reusable forms (NTLM, DES, RC4). An attacker can replay them (**pass-the-ticket**, pass-the-hash).

## How to fix
- Add administrative accounts to the **Protected Users** group: no more NTLM, no more delegation, 4-hour tickets, no credential caching.
- Failing that, check "Account is sensitive and cannot be delegated".

## Going further
Test before rolling it out widely: Protected Users blocks NTLM, which can break legacy applications. Never put service accounts or computer accounts in it.
