# 👥 Allowed RODC Password Replication Group

**Domain local** group created automatically when the domain is promoted.

## Role
- Defines accounts whose **passwords CAN be cached** on RODCs
- Empty by default — no passwords are replicated to RODCs by default

## ⚠️ Security
> 🔴 **Tier 0 — Controls RODC replication**

- **Never add** Tier 0 accounts (admins, krbtgt, critical service accounts)
- Compromising a RODC only exposes accounts whose hash is in its cache
- Use the **Denied RODC Password Replication Group** to protect sensitive accounts
- Regularly audit cached accounts on each RODC via `Get-ADDomainControllerPasswordReplicationPolicy`