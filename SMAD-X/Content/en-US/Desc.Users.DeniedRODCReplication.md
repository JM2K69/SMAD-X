# 👥 Denied RODC Password Replication Group

**Domain local** group created automatically when the domain is promoted.

## Role
- Defines accounts whose **passwords CANNOT be cached** on RODCs
- Protects critical accounts from exposure in case of RODC compromise

## Default Members
`krbtgt`, `Domain Admins`, `Schema Admins`, `Enterprise Admins`, `Group Policy Creator Owners`, `Read-Only Domain Controllers`

## ⚠️ Security
> 🟢 **Best practice — keep up to date**

- Add all critical service accounts and Tier 0 accounts to this group
- Never remove the default members
- Regularly audit the list to ensure it is comprehensive