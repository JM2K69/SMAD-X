# Privileged group that should be empty

## What the audit found
A group that should remain empty outside of one-off operations has members: Schema Admins, Enterprise Admins, or an "Operators" group.

## Why it is dangerous
- **Schema Admins** and **Enterprise Admins** grant control of the entire forest: they should only be populated for the duration of an operation.
- **Account, Server, Print and Backup Operators** are legacy groups with poorly understood rights: they allow logging on to DCs and, through several well-known paths, becoming a domain administrator.

## How to fix
Empty these groups and grant the necessary rights through fine-grained delegation. Add an account to Schema Admins temporarily, only for a schema extension, then remove it.

## Going further
In a new domain, the Administrator account is a member of Enterprise Admins and Schema Admins: ANSSI recommends emptying them anyway.
