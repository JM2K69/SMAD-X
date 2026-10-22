# Unconstrained Kerberos delegation

## What the audit found
An account that is not a domain controller is configured for unconstrained delegation (TRUSTED_FOR_DELEGATION).

## Why it is dangerous
When a user authenticates to this server, their **TGT** is copied into the server's memory so that it can act on their behalf anywhere in the domain. An attacker who takes over the server collects the TGTs of everyone who connects to it, and can even force a domain controller to authenticate to it ("printer bug" style attacks) to steal its TGT.

## How to fix
- Switch to **constrained delegation** to only the required services, or to resource-based constrained delegation.
- Protect privileged accounts: the **Protected Users** group or the "Account is sensitive and cannot be delegated" option.

## Going further
Unconstrained delegation is normal on a domain controller, which is why the rule excludes them.
