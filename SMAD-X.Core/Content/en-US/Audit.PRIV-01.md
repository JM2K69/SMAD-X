# Privileged group member without adminCount

## What the audit found
An account is a member (directly or through nesting) of a protected group, but its adminCount attribute is not set to 1.

Domain controllers are not concerned: they are members of Domain Controllers through their primary group alone, which SDProp ignores, so they never have adminCount.

## Why it is dangerous
Every hour, the **SDProp** process applies the permissions of the AdminSDHolder object to members of protected groups and sets adminCount = 1. A member without adminCount has therefore not received this protection: it keeps the permissions inherited from its OU, and someone with rights on the OU can take control of an administrator.

## How to fix
Check the membership: nesting through a distribution group, or a very recent membership, may explain it. Let SDProp run (or trigger it) and then check again.
