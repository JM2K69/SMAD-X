# 👥 Schema Admins Group

**Universal** group created automatically. Exists only in the **forest root domain**.

## Role
- Only members authorized to modify the **Active Directory schema** (class/attribute structure)
- Schema modifications are irreversible and apply to the entire forest

## Default Membership
Members: **Administrator** (forest root only)

## ⚠️ Security
> 🔴 **Tier 0 — Ultra-critical group (entire forest)**

- **Keep empty** at all times — only add members for the duration of a planned schema modification
- All schema changes must be tested in a test environment first
- Monitor any modification — Event ID 4728, 4729
- Enforce strict change management for schema modifications