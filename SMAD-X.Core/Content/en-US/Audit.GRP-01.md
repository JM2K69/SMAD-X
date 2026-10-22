# Group nesting loop

## What the audit found
A group is, through nesting, a member of itself (A member of B, B member of A…).

## Why it is dangerous
Loops make effective permissions very hard to understand and audit, and some tools handle them poorly. They are almost always the result of a mistake.

## How to fix
Break the loop and review how groups are organized (AGDLP model: accounts in global groups, which are themselves in domain local groups that carry the permissions).
