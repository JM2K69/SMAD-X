# ⚙️ SRV-APP-01 — Application Server (Tier 1)

Server hosting the organization's business applications.

## Role
- Runs enterprise applications (ERP, intranet, internal web services, etc.)
- Uses **gMSA accounts** for application identities
- Communicates with Tier 1 database servers

## ⚠️ Security
> 🟠 **Tier 1 — Critical applications**

- Use **gMSA** (Group Managed Service Accounts) for all service accounts — never user accounts with static passwords
- Enable **Windows Defender Application Control (WDAC)** to restrict allowed executables
- Isolate applications in **dedicated service accounts** with the minimum required permissions
- Apply security updates monthly (Patch Tuesday)
- Monitor unusual processes (Event ID 4688 with commandline logging enabled)