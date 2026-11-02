# Object waiting in a transit OU

## What the audit found
An account, a computer or a group is in one of the tiering model's transit OUs: Staging (servers and PAWs being prepared), Quarantine (isolated computers) or Provisioning (new objects).

## Why it is dangerous
A transit OU is for waiting, not for working: it carries restrictions made for an object being installed or isolated, not those of its final tier. A server forgotten in Staging runs without the GPOs and local administrators of its tier; a computer forgotten in quarantine often ends up being moved without checks.

## How to fix
Finish preparing the object, then move it to the OU of its tier; or disable it and place it in the disabled objects OU if it is no longer used.

## Going further
Why do the models create transit OUs rather than leave objects in the Users and Computers containers while they are classified?
