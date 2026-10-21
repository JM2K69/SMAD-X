# 🔑 PSO-Tier0-Admins — Admin Password Policy

Password Settings Object applied to **Tier 0 administration accounts**.

## Enhanced Security Settings
| Parameter | Value |
|---|---|
| Minimum length | **16 characters** |
| Complexity | Enabled |
| History | 24 passwords |
| Maximum age | **60 days** |
| Minimum age | 2 days |
| Lockout threshold | **3 attempts** |
| Lockout duration | **1 hour** (or manual unlock) |

## Targeted Objects
- **Domain Admins** group
- **Administrator** account

## ⚠️ Security
> 🔴 **Tier 0 — Critical policy**

- Precedence: **10** (high priority over Default Domain Policy)
- Consider **passphrases** (e.g.: 4 random words ≥ 20 characters) for memorability while maintaining security
- Pair with an **enterprise password manager** (CyberArk, BeyondTrust, etc.)
- This PSO should target **groups**, not individual users