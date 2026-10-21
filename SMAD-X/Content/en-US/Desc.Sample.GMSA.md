# 🔐 svc-webapp — Group Managed Service Account (Tier 1)

Group Managed Service Account for the organization's web application.

## Role
- Service identity used by the web application hosted on SRV-APP-01
- Password is **automatically managed by AD** (rotated every 30 days, transparent to the application)
- Accessible only to authorized servers via the `msDS-GroupMSAMembership` attribute
- No interactive logon possible

## Advantages over classic service accounts
| Criterion | User account | gMSA |
|---|---|---|
| Password rotation | Manual | Automatic |
| Interactive logon | Possible | Impossible |
| Multi-server usage | Risky | Native |
| Centralized management | No | Yes |

## ⚠️ Security
> 🟠 **Tier 1 — Service account**

- Strictly control servers authorized to use this gMSA (`msDS-GroupMSAMembership`)
- Apply **least privilege**: only the permissions required by the application
- Monitor use of this account on unauthorized servers (Event ID 4624)
- Document resources accessed by this account