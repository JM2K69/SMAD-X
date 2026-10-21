# 👥 Enterprise Key Admins Group

**Universal** group created automatically (Windows Server 2016+). Forest root domain.

## Role
- Equivalent of **Key Admins** but with scope across **all forest domains**
- Manages `msDS-KeyCredentialLink` attributes at forest level

## ⚠️ Security
> 🔴 **Tier 0 — Shadow Credentials risk (entire forest)**

- Same attack vector as **Key Admins** but with forest-wide scope
- **Keep empty** at all times unless there is a specific operational need
- Monitor any modification — Event ID 4728, 4729 and 5136