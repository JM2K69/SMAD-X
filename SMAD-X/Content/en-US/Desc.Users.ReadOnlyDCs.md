# 👥 Read-Only Domain Controllers Group

**Global** group created automatically when the domain is promoted.

## Role
- Contains **RODCs** (Read-Only Domain Controllers) in the domain
- RODCs replicate only objects authorized via the **Password Replication Policy**

## ⚠️ Security
> 🔴 **Tier 0 — Critical group**

- Monitor additions to this group
- Compromising a RODC exposes only the accounts whose passwords are cached on that RODC
- Carefully configure the **Allowed RODC Password Replication Group** to limit exposure