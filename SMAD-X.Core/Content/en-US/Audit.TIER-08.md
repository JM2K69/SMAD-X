# Privileged account or group outside Tier 0

## What the audit found
An account or a group that controls the domain (a member, directly or through nesting, of a group such as Domain Admins, Enterprise Admins or Account Operators) is placed in an OU of a less sensitive tier of the tiering model: the users, the servers or a Legacy tier.

## Why it is dangerous
The OUs of a less sensitive tier are administered by the teams of that tier: the helpdesk resets user passwords, the server administrators manage their service accounts. A Tier 0 account placed with them receives their GPOs and is within reach of their delegations, except for AdminSDHolder's protection, which covers neither GPOs nor the GPO links set on the OU. This is the opposite of tiering: a less sensitive tier acts on Tier 0.

## How to fix
Move the account or group to the Tier 0 administration accounts or groups OU. If it is an everyday account that became privileged, remove it from the privileged group and create a separate administration account, reserved for Tier 0.

## Going further
The Microsoft model and HardenAD keep each tier's administration accounts in a dedicated OU. Why does an administrator then need several accounts?
