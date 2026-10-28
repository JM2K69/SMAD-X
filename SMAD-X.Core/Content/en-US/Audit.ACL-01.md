# Critical object with an inappropriate owner

## What the audit found
The owner of a critical object (domain, protected group, privileged account, domain controller, GPO linked to the domain or to the Domain Controllers OU) is neither an administrative group nor a privileged account.

## Why it is dangerous
The owner of an object can **always modify its permissions**, regardless of the ACLs in place. They can therefore grant themselves full control over an administrator or a privileged group: this is a direct attack path.

## How to fix
Make Domain Admins (or Enterprise Admins for the forest) the owner. An object created by a non-administrator account inherits that account as its owner: review the creation delegations.
