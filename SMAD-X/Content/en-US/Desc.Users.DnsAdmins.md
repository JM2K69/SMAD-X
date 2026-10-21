# 👥 DnsAdmins Group

**Domain local** group created automatically when the DNS role is installed.

## Role
- Full administrative access to the domain **DNS** service
- Allows managing zones, records and DNS configuration

## ⚠️ Security
> 🔴 **Tier 0 — Known privilege escalation path**

- **Critical privilege escalation** vector: a DnsAdmins member can load a malicious DLL into the DNS service (which runs as SYSTEM on DCs)
- **CVE**: Documented technique by Shay Ber (2017) — `dnscmd /config /serverlevelplugindll`
- Minimize membership in this group
- Monitor use of `dnscmd` by members (Event ID 4688)
- Consider delegating DNS management differently if possible