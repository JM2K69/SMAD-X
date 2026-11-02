# Object outside the tiering model's OUs

## What the audit found
The domain follows a tiering model, but an account, a computer or a group has stayed outside its classified OUs: in the Users or Computers container, in an older OU, or directly in one of the model's grouping OUs.

## Why it is dangerous
In a tiering model, the OU makes the tier: it carries the logon restriction GPOs, the delegations limited to one team and the local administrators. An object placed elsewhere receives none of them. A server left in Computers is protected by no logon restriction, and an account left in Users stays within reach of the delegations inherited from the former organisation.

## How to fix
Move the object to the OU of its tier and role (servers, workstations, users, groups, service accounts, administration accounts). An object whose tier is not decided yet goes to the model's transit OU (Staging, Provisioning) while it is classified.

## Going further
HardenAD redirects the creation of new accounts and computers to its Provisioning OU (redircmp and redirusr commands). Why is this useful, and what would happen without this redirection?
