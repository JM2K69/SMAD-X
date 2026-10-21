# 💻 WKS-001 — John Doe's Workstation (Tier 2)

Workstation assigned to developer John Doe.

## Role
- Standard Windows 10/11 workstation joined to the domain
- Receives GPOs from the Workstations OU (configuration, security, deployment)
- Accesses network resources using jdoe's credentials

## ⚠️ Security
> 🟢 **Tier 2**

- BitLocker enabled with recovery key stored in AD
- No local administrator rights for jdoe
- Connections to Tier 1 resources only via authorized groups
- EDR deployed and SOC monitoring active