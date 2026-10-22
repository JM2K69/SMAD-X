# 👤 Administrator Account

**Built-in** administrator account created automatically when the domain is promoted.

## Role
- Full and unrestricted access to all domain resources
- The only account that cannot be locked out by the account lockout policy
- Permanent member of **Domain Admins** and **Administrators**

## Default Group Membership
| Group | Scope |
|---|---|
| Administrators | Domain local |
| Domain Admins | Global |
| Group Policy Creator Owners | Global |
| Schema Admins (forest root) | Universal |
| Enterprise Admins (forest root) | Universal |

## ⚠️ Security
> 🔴 **Tier 0 — Critical account**

- **Rename** this account to avoid exposing the predictable `Administrator` name
- **Disable** this account in production and create a dedicated named account for administration
- Enable **lockout policy** against brute-force attacks (via Fine-Grained Password Policy)
- This account is a prime target for **Pass-the-Hash** and **Mimikatz** attacks
- Monitor with alerts on any logon event for this account
- **Protected Users** group (Windows Server 2012 R2+) does not apply to Administrator — manage manually