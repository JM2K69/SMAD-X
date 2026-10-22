# 🖧 DC03 — Domain Controller — Site Lyon (Tier 0)

Secondary domain controller hosted in the **Site-Lyon** site (10.0.2.0/24).

## Role
- Local authentication for Lyon site users (**reduces WAN latency**)
- Maintains a full local replica of the AD directory
- Avoids costly cross-site authentication traffic
- Continuity point in case of WAN outage with Paris

## FSMO Roles
- No FSMO roles (can be designated backup PDC Emulator)

## Network infrastructure
- IP address: 10.0.2.1/24
- AD Site: **Site-Lyon**
- Cross-site replication: **DEFAULTIPSITELINK** — cost 100, interval 15 min

## ⚠️ Security
> 🔴 **Tier 0 — Critical AD infrastructure**

- Physically secure the Lyon server room (badge access, CCTV)
- Apply BitLocker to DC volumes to protect AD data in case of physical theft
- Replication traffic is encrypted (Kerberos + RPC/SChannel)
- Monitor Event IDs 4741 (machine account creation) and 4743 (deletion)