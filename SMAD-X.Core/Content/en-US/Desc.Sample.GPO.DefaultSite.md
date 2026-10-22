# 📋 GPO-Baseline-DC

> 🔒 Security GPO applied to site **Default-First-Site-Name**.

## Purpose
Define the security baseline for all domain controllers in the default site.

## Key Settings
| Category | Value |
|---|---|
| Logon Audit | Success, Failure |
| Windows Firewall | Enabled (all profiles) |
| Local Rights | Administrators only |
| SMB Signing | Required |

## ⚠️ Best practices
- Do not modify without validation in a test environment
- Supplement with CIS Level 1 benchmarks for DCs