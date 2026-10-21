# 🔑 Password Settings Container

System container holding all **Password Settings Objects (PSO)** of the domain.

## Role
- Allows defining **granular password policies** per group or user (Fine-Grained Password Policy — FGPP)
- Available since Windows Server 2008 (domain functional level 2008+)
- Priority: the PSO with the lowest `msDS-PasswordSettingsPrecedence` value applies in case of conflict

## ⚠️ Security
> 🔴 **Tier 0 — Password policies**

- Create a strict PSO for Tier 0 accounts (length ≥ 16, rotation ≤ 60 days, lockout after 3 attempts)
- The PSO overrides the Default Domain Policy for targeted accounts
- Monitor PSO modifications (Event ID 5136)