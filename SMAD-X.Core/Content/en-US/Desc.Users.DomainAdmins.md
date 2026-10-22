# 👥 Domain Admins Group

**Global** group created automatically when the domain is promoted.

## Role
- Designated administrators of the domain
- Members are automatically added to the **Administrators** group on every domain-joined machine
- Full control over all objects in the domain

## Default Membership
Members: **Administrator**

## ⚠️ Security
> 🔴 **Tier 0 — Ultra-privileged group**

- Limit membership to the bare minimum (principle of least privilege)
- Never use a Domain Admin account for day-to-day tasks
- Use dedicated administration accounts (PAW — Privileged Access Workstation)
- Enable monitoring for any modification (Event ID 4728, 4729)
- Domain Admin members are **Administrators** on **every** machine in the domain — major lateral movement vector