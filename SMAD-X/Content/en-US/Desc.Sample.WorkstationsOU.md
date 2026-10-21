# 💻 Workstations OU (Tier 2)

Organizational unit containing the **user workstations** of the organization.

## Role
- Hosts employees' laptops and desktops
- Target of configuration, security and software deployment GPOs
- Primary entry point for attackers in a corporate network

## ⚠️ Security
> 🟢 **Tier 2 — Primary intrusion vector**

- Apply **CIS Benchmarks** or equivalent for Windows 10/11 via GPO
- Enable **BitLocker** on all workstations (disk encryption)
- Disable **LLMNR** and **NetBIOS over TCP/IP** (prevents Responder / MITM)
- Restrict USB ports and removable media (via GPO or MDM solution)
- Deploy an **EDR** on all workstations
- Enable **Windows Defender Credential Guard** and **Device Guard**
- Monitor connections to unusual network shares (lateral movement)