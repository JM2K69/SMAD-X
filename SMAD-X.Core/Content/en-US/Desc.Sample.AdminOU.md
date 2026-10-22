# 🏛️ Admin OU (Tier 0)

Organizational unit containing the **top-level administration accounts and groups** of the domain.

## Role
- Hosts named (non-shared) administrator accounts used exclusively for domain administration tasks
- Contains Tier 0 administration groups
- Target of hardening GPOs specific to privileged accounts

## ⚠️ Security
> 🔴 **Tier 0 — Critical zone**

- Apply a **strict hardening GPO** to this OU (logon restrictions, enhanced auditing)
- Block GPO inheritance and only link validated GPOs
- Read/write access to this OU: **Domain Admins only**
- Monitor any object created or modified in this OU (Event ID 5137, 5136)
- These accounts must be used exclusively from **PAW (Privileged Access Workstation)**