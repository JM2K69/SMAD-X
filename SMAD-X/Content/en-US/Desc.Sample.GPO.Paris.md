# 📋 GPO-Paris-Workstations

> 🖥️ GPO applied to site **Site-Paris** — workstations.

## Purpose
Manage workstation configuration at the Paris datacenter: software deployments, network drive mappings and printers.

## Key Settings
| Category | Value |
|---|---|
| Software Deployment | Office 365, VPN Client |
| Network Drives | P: → \\srv-paris\shares |
| Wallpaper | Paris corporate branding |
| Printers | AUTO (site-aware) |

## ⚠️ Best practices
- Test on a pilot group before production deployment
- Use WMI filters to target Windows 10/11 only