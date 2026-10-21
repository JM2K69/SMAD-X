# 🏙 Site-Paris — Main Datacenter

Active Directory site hosting the main servers of the **contoso.com** domain.

## Network scope
| Network | VLAN | Usage |
|---------|------|-------|
| 10.0.1.0/24 | 10 | AD / Infrastructure servers |

## Domain Controllers
- **DC01** — PDC Emulator, RID Master, Schema Master, Domain Naming Master, Infrastructure Master
- **DC02** — Secondary DC, Global Catalog

## Role in topology
- **Primary site** and **replication hub** for all forest sites
- Hosts critical FSMO roles on DC01
- Immediate intra-site replication between DC01 and DC02

## ⚠️ Best practices
- At least 2 DCs per site for high availability
- Avoid placing all Tier 0 infrastructure in a single datacenter