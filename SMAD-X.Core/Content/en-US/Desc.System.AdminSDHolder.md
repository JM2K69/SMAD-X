# 🛡️ AdminSDHolder

Template of the permissions of protected accounts and groups (Domain Admins, Administrators, operators…).

## Role
- Every hour, the **SDProp** process copies the AdminSDHolder permissions onto each protected object (`adminCount = 1`)
- A delegation set on an OU therefore does not apply to the administrators it contains

## ⚠️ Security
> 🔴 **Tier 0 — A known persistence point**

- Anyone who can change AdminSDHolder gets, one hour later, rights over every administrator
- Never add a permission to it; monitor any change (Event ID 5136)
