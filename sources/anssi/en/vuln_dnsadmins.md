# vuln_dnsadmins — Dangerous permissions on the DnsAdmins group

> [Contents: ANSSI — Active Directory control points](README.md) · Français : [vuln_dnsadmins.md](../fr/vuln_dnsadmins.md)

| Sheet | |
|:---|:---|
| Framework | ANSSI — Active Directory control points |
| Identifier | `vuln_dnsadmins` |
| ANSSI level | 1 (1: most critical) |
| Added to the checklist | 2018-12-19 |
| Last change | 2022-10-26 |
| Link | [cert.cyber.gouv.fr/…](https://cert.cyber.gouv.fr/uploads/ad_checklist.html) |
| Cited by SMAD-X | PRIV-04 |

## SMAD-X rules that cite it

- **PRIV-04** — Non-privileged member of the DnsAdmins group

## Official text

The `DnsAdmins` group includes some non-privileged member accounts or have dangerous permissions set.

Members of the `DnsAdmins` group are granted access rights to manage a Microsoft DNS service, which is often hosted on domain controllers.

Those access rights grant at least the following abilities: managing the entirety of domain-level DNS zones, using debug features or undocumented features such as the ability to instantly restart or stop the DNS server.

One of these rights allows the user to make the DNS service run arbitrary code *via* the `serverlevelplugindll` feature, depending on the patch level or on specific configuration of all Windows Servers running DNS services across the forest.

Despite [an official fix being available](https://msrc.microsoft.com/update-guide/vulnerability/CVE-2021-40469), these dangerous features may still be re-enabled. Evaluating the security of the running configuration cannot be done solely based on Active Directory-stored parameters, just as it is impossible to ensure that all running Microsoft DNS servers are up to date.

This provides an attacker with significant nuisance capabilities or even could facilitate network attacks. This group should therefore be considered as privileged.

*(Table of the affected objects, in the analysis report.)*

## Official recommendation

The `DnsAdmins` must not be used: manual DNS zone delegations must be used instead to manage the service (zone creation/deletion, record management, ...). These delegations are usually granted through the `ldp` utility. This is a two-step process:

#### Step 1: allow access to RPC used by DNS management MMC snap in

In the domain naming context, under the `CN=System` container, set the following access rights on the `CN=MicrosoftDNS` container:

#### Step 2: allow DNS zone modifications

In the `DC=DomainDnsZones` naming context, on every zone you wish to delegate management, enable inheritance and set the following access rights on the `CN=MicrosoftDNS` container:

Note: to change the current *naming context* in `ldp`, go to `View`,`Tree` and use the drop-down list to select `DomainDnsZones`.

Additionally, the following operations must be carried out on the `DnsAdmins` group:

- empty it of all its members;
- make sure the group is owned by `S-1-5-21-domain-512`;
- add an Access Control Entry forbidding Everyone to change its owner, change its access rights, and write its list of members.

## Source and licence

- **Source**: [https://cert.cyber.gouv.fr/uploads/ad_checklist.html](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)
- **Retrieved publication**: 2026-11-12
- **Retrieved on**: 2026-11-13, by `tools/SmadxSources`
- **Licence**: Etalab Open Licence 2.0: free reuse citing the source (CERT-FR / ANSSI) and the date of its update; official texts reproduced in French and English
