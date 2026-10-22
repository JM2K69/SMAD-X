# 📋 Default Domain Controllers Policy

> ⚠️ **FIXED GUID**: `{6AC1786C-016F-11D2-945F-00C04fB984F9}`

This GPO is **automatically created** with the domain and applies only to the **Domain Controllers OU**.

## Default Managed Settings

### 🛡️ User Rights Assignment
| Setting | Default Value |
|---|---|
| Log on locally | Administrators, Account Operators, Backup Operators, Print Operators, Server Operators |
| Access network | Authenticated Users, Everyone |
| Shut down system | Administrators, Account Operators, Backup Operators, Print Operators, Server Operators |
| Manage audit and security log | Administrators |

### 🔍 Audit Policy
| Category | Default Value |
|---|---|
| Account logon | Success, Failure |
| Account management | Success |
| Directory service access | Success |
| Logon events | Success, Failure |
| Policy change | Success |
| Privilege use | Failure |
| Process tracking | None |
| System events | Success |

### 🔒 Security Options
- SMB server signing required: **Enabled**
- NTLMv2 only: **Configured**

## ⚠️ Important Warnings
> ❌ **Never delete** this GPO — it ensures minimum security for domain controllers.

> ⚠️ **Never disable** the link of this GPO on the Domain Controllers OU.

> ✅ **Best practice**: Add complementary hardening GPOs (CIS Benchmarks, etc.) rather than modifying this one.

> ℹ️ **RODC**: Read-Only Domain Controllers also inherit this GPO via the Domain Controllers OU.

## Technical Information
- **DN**: `CN={6AC1786C-016F-11D2-945F-00C04fB984F9},CN=Policies,CN=System,DC=domain,DC=com`
- **SYSVOL**: `\\domain\SYSVOL\domain\Policies\{6AC1786C-016F-11D2-945F-00C04fB984F9}`
- **Version**: Auto-incremented on each modification