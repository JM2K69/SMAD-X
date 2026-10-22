# Tiering violation: GPO from a less sensitive tier

## What the audit found
A GPO classified in a less sensitive tier is linked to an OU in a more sensitive tier.

## Why it is dangerous
Anyone who can modify the GPO can run code (scripts, scheduled tasks) on every machine in the OU. If the GPO is administered from a less sensitive tier, that tier effectively controls the more sensitive tier.

## How to fix
Use, for each tier, GPOs administered within that tier, or reclassify the GPO and restrict its modification rights to the administrators of the most sensitive tier.
