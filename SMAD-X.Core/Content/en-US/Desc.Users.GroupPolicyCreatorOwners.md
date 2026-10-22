# 👥 Group Policy Creator Owners

**Global** group created automatically when the domain is promoted.

## Role
- Members authorized to create and modify **GPOs** in the domain
- A member can only modify GPOs they created (except Domain Admins)

## Default Membership
Members: **Administrator**

## ⚠️ Security
> 🟠 **Tier 1 — Group to monitor**

- Limit members — creating malicious GPOs is a common attack vector
- Monitor GPO creation and linking (Event ID 5136, 5137)
- Any GPO linked to the Domain Controllers OU is potentially dangerous