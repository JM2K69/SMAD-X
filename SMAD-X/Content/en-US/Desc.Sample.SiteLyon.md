# 🏙 Site-Lyon — Secondary Site

Active Directory site hosting the **Lyon** domain controller, connected to Paris via a WAN link.

## Network scope
| Network | VLAN | Usage |
|---------|------|-------|
| 10.0.2.0/24 | 20 | AD / Infrastructure servers |

## Domain Controllers
- **DC03** — Secondary DC, local Global Catalog

## Role in topology
- **Secondary site** connected to Site-Paris via **DEFAULTIPSITELINK** (cost 100)
- Reduces authentication latency for Lyon users
- Ensures continuity in case of Paris–Lyon WAN failure (local authentication)

## ⚠️ Best practices
- Regularly check replication status: `Get-ADReplicationPartnerMetadata -Target DC03 -Scope Server`
- Monitor replication errors (Event IDs 1311, 1388, 1645)