# 👥 DnsUpdateProxy Group

**Global** group created automatically when the domain is promoted.

## Role
- Members authorized to **register DNS records** on behalf of other clients
- Typically used by **DHCP** servers for dynamic DNS updates

## ⚠️ Security
> 🟠 **Tier 1 — DNS squatting risk**

- **Limit** members to authorized DHCP servers only
- Records created by this group are initially ownerless — vector for **DNS hijacking**
- Configure the DHCP server with a dedicated account rather than using this group if possible
- If used, enable **DNS dynamic update credentials** on the DHCP server