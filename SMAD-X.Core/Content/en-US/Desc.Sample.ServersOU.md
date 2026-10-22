# 🖥️ Servers OU (Tier 1)

Organizational unit containing the **infrastructure servers** of the domain.

## Role
- Hosts file servers, application servers, print servers, etc.
- Separated from user workstations (Tier 2) and domain controllers (Tier 0)
- Target of server configuration and hardening GPOs

## ⚠️ Security
> 🟠 **Tier 1 — Infrastructure**

- Apply **CIS Benchmarks** or equivalent for Windows Server via GPO
- Block Tier 2 accounts (standard users) from logging into Tier 1 servers
- Enable **Windows Defender Credential Guard** on all servers
- Monitor interactive logons (Event ID 4624 type 2 and 10)
- Deploy an **EDR** on all servers