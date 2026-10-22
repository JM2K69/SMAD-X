# 🔑 PSO-Standard-Users — Standard User Password Policy

Password Settings Object applied to **Tier 2 standard user accounts**.

## Standard Security Settings
| Parameter | Value |
|---|---|
| Minimum length | **12 characters** |
| Complexity | Enabled |
| History | 12 passwords |
| Maximum age | **90 days** |
| Minimum age | 1 day |
| Lockout threshold | **5 attempts** |
| Lockout duration | **30 minutes** |

## Targeted Objects
- Users **jdoe**, **asmith**

## ⚠️ Security
> 🟢 **Tier 2 — Standard policy**

- Precedence: **50** (lower priority than PSO-Tier0-Admins)
- Enable **self-service password reset (SSPR)** to reduce helpdesk load
- Consider integrating **Microsoft Entra ID Password Protection** to block common/compromised passwords (HaveIBeenPwned)