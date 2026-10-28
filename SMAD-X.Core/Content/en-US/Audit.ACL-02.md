# Control path to the domain through permissions

## What the audit found
From an account or group that is not privileged (or from any domain user), a chain of permissions leads to control of the domain. The finding is about the **first permission** of the path: the object it gives control of, and who holds it.

## Why it is dangerous
None of these permissions looks serious on its own: resetting a password, writing an attribute, changing the members of a group, owning an OU. Chained together, they give the domain away, and this is how attack tools (BloodHound, ANSSI's ADCP) look for them. The full path is shown in **Analysis > Attack paths**.

## How to fix
Cut the path at its first permission: remove the ACE, give it to an administration group from the tier of the targeted object, or make the object protected (a privileged account must be a member of a protected group, with adminCount set to 1). Then check that no path starts from the same source any more.
