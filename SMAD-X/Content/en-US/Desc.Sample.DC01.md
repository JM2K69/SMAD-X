# 🖧 DC01 — Primary Domain Controller (Tier 0)

First domain controller of the contoso.com domain.

## Role
- Hosts all **FSMO roles** during initial installation:
  - **Schema Master**: controls AD schema modifications
  - **Domain Naming Master**: manages domain add/removal in the forest
  - **RID Master**: distributes RID pools for security object creation
  - **PDC Emulator**: time synchronization, account lockout management, NT4 compatibility
  - **Infrastructure Master**: maintains cross-domain references

## ⚠️ Security
> 🔴 **Tier 0 — Critical AD infrastructure**

- Never install third-party applications on a DC (minimize attack surface)
- Enable **Windows Defender Credential Guard** on the DC
- Restrict inbound/outbound network connections to the strict minimum (Windows Firewall)
- Monitor critical Event IDs: 4662 (AD object access), 4769 (Kerberos ticket), 4776 (NTLM)
- Set up a **standby DC** in a separate site/VLAN
- Include in a **Disaster Recovery plan** with tested System State backups