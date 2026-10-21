# ⚙️ System Container

Active Directory system container holding configuration objects and system settings.

## Typical content
- `Password Settings Container`: PSO objects (Fine-Grained Password Policies)
- `Policies`: domain GPOs (AD object storage, outside SYSVOL)
- `WMI Filters`: WMI filters for GPO targeting
- `AdminSDHolder`: security template for protected objects

## ⚠️ Security
> 🔴 **Tier 0 — Do not modify manually**

- **AdminSDHolder**: monitor ACL modifications — any object matching it will be propagated to protected groups (Domain Admins, etc.) every 60 minutes by the SDProp process
- Never delete or move system objects from this container