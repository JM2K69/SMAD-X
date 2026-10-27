# 👥 GG-Tier0-Admins (Admin OU)

Global group of the **Tier 0 administrators**: the named administration accounts (`adm-…`).

## Role
- The group is a member of **Domain Admins**: rights are given to a business group rather than to accounts one by one (AGDLP model)
- Adding or removing an administrator happens here, without touching the built-in groups

## ⚠️ Security
> 🔴 **Tier 0 — Ultra-privileged group**

- Keep membership at the **absolute minimum**
- Audit members monthly; any addition goes through an approval process
- Monitor Event ID 4728 (member added) and 4729 (member removed)
