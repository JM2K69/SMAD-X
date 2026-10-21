# 📋 Default Domain Policy

> ⚠️ **FIXED GUID**: `{31B2F340-016D-11D2-945F-00C04FB984F9}`

This GPO is **automatically created** when the first domain controller is promoted and applies to the **entire domain**.

## Default Managed Settings

### 🔑 Password Policy (Computer Configuration)
| Setting | Default Value |
|---|---|
| Minimum password length | 7 characters |
| Password complexity | Enabled |
| Password history | 24 passwords |
| Maximum password age | 42 days |
| Minimum password age | 1 day |

### 🔒 Account Lockout Policy
| Setting | Default Value |
|---|---|
| Lockout threshold | 0 (disabled) |
| Lockout duration | Not defined |

### 🎫 Kerberos Policy
| Setting | Default Value |
|---|---|
| Ticket lifetime | 10 hours |
| Service ticket lifetime | 600 minutes |
| Maximum clock skew | 5 minutes |

## ⚠️ Important Warnings
> ❌ **Never delete** this GPO — it breaks security policies for the entire domain.

> ⚠️ **Never disable** the link of this GPO on the domain.

> ✅ **Best practice**: Only modify password and Kerberos settings here. All other configuration should be done in dedicated GPOs.

## Technical Information
- **DN**: `CN={31B2F340-016D-11D2-945F-00C04FB984F9},CN=Policies,CN=System,DC=domain,DC=com`
- **SYSVOL**: `\\domain\SYSVOL\domain\Policies\{31B2F340-016D-11D2-945F-00C04FB984F9}`
- **Version**: Auto-incremented on each modification