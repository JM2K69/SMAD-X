# 🔑 krbtgt Account

**Kerberos Key Distribution Center (KDC)** service account created automatically when the domain is promoted.

## Role
- Used exclusively by the **KDC** service to sign and encrypt Kerberos tickets (TGT)
- Its password serves as the encryption key for all **Ticket Granting Tickets** in the domain
- Disabled by default — never enable

## Default Group Membership
| Group | Scope |
|---|---|
| Domain Users | Global |

## ⚠️ Security
> 🔴 **Tier 0 — Ultra-critical account**

- **Never delete** this account — it renders the domain unusable
- **Never enable** this account interactively
- Compromising this account allows creating **Golden Tickets** (Mimikatz attack)
- **Reset the password TWICE** after a suspected compromise (two consecutive resets required to invalidate existing tickets)
- Preventive rotation recommended **every 180 days** in production
- Monitor password changes (Event ID 4723, 4724)
- Since Windows Server 2016: use the separate **RODC krbtgt account** for RODCs