# Tiering violation: membership in a more sensitive group

## What the audit found
An account or group from one tier is a member (directly or through nesting) of a group from a more sensitive tier.

## Why it is dangerous
The tiering model relies on a simple rule: **a tier must never be able to control a more sensitive tier**. A Tier 2 account, used on exposed workstations, that becomes a member of a Tier 0 group makes the domain as fragile as the most exposed workstation.

## How to fix
- Remove the membership, or reclassify the object into the correct tier.
- Create dedicated administrative accounts per tier (the same administrator has as many accounts as the tiers they administer).
