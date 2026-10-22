# Orphaned adminCount

## What the audit found
An account has adminCount = 1 although it is no longer a member of any protected group.

## Why it is dangerous
SDProp never resets adminCount to 0. A former administrator therefore keeps frozen permissions with **inheritance disabled**: delegations set on its OU (for example password resets by the help desk) no longer apply to it. It is also a clue: this account used to be an administrator.

## How to fix
Reset adminCount to 0, then re-enable permission inheritance on the object.
