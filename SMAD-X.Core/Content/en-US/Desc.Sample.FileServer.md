# 💾 SRV-FILE-01 — File Server (Tier 1)

Primary file server of the organization.

## Role
- Hosts organization network shares (business data, roaming profiles, work folders)
- Uses **NTFS ACLs** and **Share Permissions** for access control
- Typical shares: `\\SRV-FILE-01\Data`, `\\SRV-FILE-01\Profiles$`, `\\SRV-FILE-01\Home$`

## ⚠️ Security
> 🟠 **Tier 1 — Sensitive data**

- Enforce **SMB Signing** (prevents MITM / Relay attacks)
- Disable **SMBv1** (vulnerable to EternalBlue / WannaCry)
- Monitor share access (Event ID 5140, 5145) to detect ransomware
- Deploy a **honeypot file** in each share (decoy file alerting on abnormal access)
- Implement **least privilege** on shares and NTFS ACLs
- Regular backups with **immutable offline backups**