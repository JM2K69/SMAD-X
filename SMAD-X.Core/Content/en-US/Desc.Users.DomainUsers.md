# 👥 Domain Users Group

**Global** group created automatically when the domain is promoted.

## Role
- Contains **all** domain user accounts (added automatically)
- Used as the base group for permissions on shared resources
- Member of the local **Users** group on every domain-joined machine

## ⚠️ Security
> 🟢 **Tier 2 — Standard group**

- Avoid granting elevated permissions to this group — it contains all users
- Audit resources accessible by this group (shares, GPOs)
- Useful for applying policies to all domain users