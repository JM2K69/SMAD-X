# Non-privileged member of the DnsAdmins group

## What the audit found
An account that is not a domain administrator is a member of the **DnsAdmins** group.

## Why it is dangerous
Members of DnsAdmins can make the DNS service load an arbitrary DLL, and that service runs as SYSTEM on domain controllers. Being a member of DnsAdmins is therefore equivalent to being able to take control of the DCs.

## How to fix
Remove the account from the group and treat DnsAdmins as a Tier 0 group.
