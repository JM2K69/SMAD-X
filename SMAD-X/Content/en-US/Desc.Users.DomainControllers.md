# 👥 Domain Controllers Group

**Global** group created automatically when the domain is promoted.

## Role
- Contains **all** domain controllers (DCs) in the domain
- Automatically added when a server is promoted to DC

## ⚠️ Security
> 🔴 **Tier 0 — Critical group**

- Monitor any modification (DC added/removed) — Event ID 4728, 4729
- A compromised domain controller compromises the entire domain