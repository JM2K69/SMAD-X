# 🖧 DC02 — Secondary Domain Controller — Site Paris (Tier 0)

Secondary domain controller hosted in the **Site-Paris** site (10.0.1.0/24).

## Role
- Ensures **AD replication** from DC01 within the same site
- Handles authentications if DC01 becomes unavailable
- Hosts a full replica of the **Global Catalog** for the forest
- Ideal for **Kerberos load distribution** on the main site

## FSMO Roles
- No primary FSMO roles (secondary DC)
- Can be promoted if DC01 fails via `Move-ADDirectoryServerOperationMasterRole`

## Network infrastructure
- IP address: 10.0.1.2/24
- AD Site: **Site-Paris**
- Intra-site replication: immediate (< 15 seconds)

## ⚠️ Security
> 🔴 **Tier 0 — Critical AD infrastructure**

- Isolated in the same secure VLAN as DC01 (Tier 0 VLAN)
- Same monitoring as DC01 (Event IDs 4624, 4662, 4769, 4776)
- Included in scheduled System State backups