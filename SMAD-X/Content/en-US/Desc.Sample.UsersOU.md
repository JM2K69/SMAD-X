# 👤 Users OU (Tier 2)

Organizational unit containing the **standard user accounts** of the organization.

## Role
- Hosts employee accounts for daily use (email, files, applications)
- Target of workstation configuration GPOs and user restrictions
- Can be subdivided by department (HR, Finance, IT, Management…)

## ⚠️ Security
> 🟢 **Tier 2 — Standard users**

- Apply **least privilege**: no account in this OU should be a local administrator
- Enable **Windows Defender Credential Guard** to protect in-memory credentials
- Deploy a **script restriction GPO** (SRP or WDAC) to limit PowerShell, WScript execution, etc.
- Train users on **phishing** and social engineering attacks
- Monitor logons outside normal hours and unusual data volumes