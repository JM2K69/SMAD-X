# Tiering violation: delegation to a less sensitive tier

## What the audit found
A delegation (full control, attribute write, or password reset) grants an account or group from a less sensitive tier rights over an object in a more sensitive tier.

## Why it is dangerous
These rights are enough to take control of the targeted objects: resetting an administrator's password, changing a group's membership, writing a delegation attribute… This is a **control path** from the less sensitive tier to the more sensitive one.

## How to fix
Remove the delegation, or assign it to a group from the tier of the targeted object.
