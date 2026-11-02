# Privileged account outside an enforced authentication silo

## What the audit found
The domain has authentication silos, but a privileged account (a member, directly or through nesting, of a group such as Domain Admins or Enterprise Admins) is in no silo, or its silo is in audit mode.

## Why it is dangerous
An enforced silo only lets its accounts authenticate from its own computers: the domain controllers and the Tier 0 administration workstations. An administrator of the silo can then no longer open a session on a less sensitive workstation or server, and their stolen credentials do not work from another machine. In audit mode, the silo denies nothing: denials are only logged by the domain controllers. Outside any silo, the account authenticates from any computer.

## How to fix
Put every privileged account in the Tier 0 silo, with the computers it administers from (domain controllers, administration workstations), then switch the silo and its policy to "enforce", after checking in the audit-mode logs that no legitimate use is denied.

## Going further
Microsoft's model creates its silos in audit mode and leaves it to the administrator to put the administration accounts in them. Why not enforce them right at deployment?
