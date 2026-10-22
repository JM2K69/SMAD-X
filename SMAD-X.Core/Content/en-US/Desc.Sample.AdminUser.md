# 👤 Administrator Account (Tier 0)

**Named** administrator account dedicated to domain administration.

## Role
- Used only for Active Directory administration tasks requiring Domain Admins rights
- Distinct from the domain's built-in `Administrator` account
- Must never be used for day-to-day tasks

## ⚠️ Security
> 🔴 **Tier 0 — High-value account**

- Use exclusively from a **PAW (Privileged Access Workstation)**
- Enable **MFA** (Windows Hello for Business, smart card)
- Add to **Protected Users** group to block NTLM and Kerberos delegation
- Monitor all logon events (Event ID 4624, 4625, 4648)
- Never use this account to browse the internet or read emails
- Rotate password every 60 days minimum