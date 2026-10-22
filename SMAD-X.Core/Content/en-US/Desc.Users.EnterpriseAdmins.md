# 👥 Enterprise Admins Group

**Universal** group created automatically. Exists only in the **forest root domain**.

## Role
- Enterprise administrators with control over **all domains in the forest**
- Can add/remove domains from the forest
- Automatically member of **Administrators** in every domain in the forest

## Default Membership
Members: **Administrator** (forest root only)

## ⚠️ Security
> 🔴 **Tier 0 — Ultra-critical group (entire forest)**

- **Keep empty** at all times — only add members for forest-level operations
- Cross-forest attack vector affecting all domains
- Monitor any modification — Event ID 4728, 4729
- Members have administrator access on **all** domain controllers in the forest