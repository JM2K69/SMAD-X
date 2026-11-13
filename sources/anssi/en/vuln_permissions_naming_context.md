# vuln_permissions_naming_context — Dangerous ACLs expose a naming context root (attack path)

> [Contents: ANSSI — Active Directory control points](README.md) · Français : [vuln_permissions_naming_context.md](../fr/vuln_permissions_naming_context.md)

| Sheet | |
|:---|:---|
| Framework | ANSSI — Active Directory control points |
| Identifier | `vuln_permissions_naming_context` |
| ANSSI levels | 1, 2 (1: most critical; one threshold per level, see the text) |
| Added to the checklist | 2019-02-22 |
| Last change | 2026-07-15 |
| Link | [cert.cyber.gouv.fr/…](https://cert.cyber.gouv.fr/uploads/ad_checklist.html) |
| Cited by SMAD-X | ACL-03 |

## SMAD-X rules that cite it

- **ACL-03** — Dangerous permissions on the domain, AdminSDHolder or the domain controllers

## Official recommendation

An Active Directory has several naming contexts:

- `Domain`, contains domain object information, such as users, groups, computers, ...;
- `Configuration`, contains forest configuration settings;
- `Schema`, contains object attribute definitions;
- `DomainDns`, contains domain-level DNS zones;
- `ForestDns`, contains forest-level DNZ zones.

Changing default permissions to these various naming context roots is strongly discouraged. They should be reverted to default permissions. The fix is usually applied through the `adsiedit.msc` or `ldp` utilities.

Some of these access rights, especially regarding the Microsoft Exchange service, can be fixed by relevant security updates which must be applied. It is notably the case for the following principals:

- Exchange Windows Permissions
- Exchange Trusted Subsystem

- For Exchange Server-related issues: [https://support.microsoft.com/en-us/help/4490059/using-shared-permissions-model-to-run-exchange-server](https://support.microsoft.com/en-us/help/4490059/using-shared-permissions-model-to-run-exchange-server)
- For Entra ID Connect Accounts (MSOL): [https://docs.microsoft.com/en-us/azure/active-directory/hybrid/how-to-connect-configure-ad-ds-connector-account](https://docs.microsoft.com/en-us/azure/active-directory/hybrid/how-to-connect-configure-ad-ds-connector-account)  Use the cmdlet `Set-ADSyncRestrictedPermissions` with the `-SkipAdminSdHolders` command flag.
- For Entra ID Cloud Sync Accounts (ProvAgentgMSA$): [https://learn.microsoft.com/en-us/entra/identity/hybrid/cloud-sync/how-to-gmsa-cmdlets#using-set-aadcloudsyncrestrictedpermissions](https://learn.microsoft.com/en-us/entra/identity/hybrid/cloud-sync/how-to-gmsa-cmdlets#using-set-aadcloudsyncrestrictedpermissions)  Use the cmdlet `Set-AADCloudSyncRestrictedPermissions`.

## Source and licence

- **Source**: [https://cert.cyber.gouv.fr/uploads/ad_checklist.html](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)
- **Retrieved publication**: 2026-11-12
- **Retrieved on**: 2026-11-13, by `tools/SmadxSources`
- **Licence**: Etalab Open Licence 2.0: free reuse citing the source (CERT-FR / ANSSI) and the date of its update; official texts reproduced in French and English
