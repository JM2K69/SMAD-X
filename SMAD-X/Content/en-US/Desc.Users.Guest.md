# 👤 Guest Account

**Built-in** guest account created automatically when the domain is promoted.

## Role
- Allows anonymous and limited access to the domain without a password
- Rights equivalent to the **Guests** group (very restricted)
- Cannot change its own password or access system settings

## Default Group Membership
| Group | Scope |
|---|---|
| Guests | Domain local |
| Domain Guests | Global |

## ⚠️ Security
> 🔴 **Disable immediately in production**

- This account is **disabled by default** since Windows Server 2008 — verify it stays disabled
- Never enable this account in a production environment
- Classic attack vector for unauthenticated access
- Monitor any activation or usage attempt (Event ID 4722, 4624)