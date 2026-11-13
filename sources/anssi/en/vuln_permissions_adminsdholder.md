# vuln_permissions_adminsdholder — Dangerous permissions on the adminSDHolder object

> [Contents: ANSSI — Active Directory control points](README.md) · Français : [vuln_permissions_adminsdholder.md](../fr/vuln_permissions_adminsdholder.md)

| Sheet | |
|:---|:---|
| Framework | ANSSI — Active Directory control points |
| Identifier | `vuln_permissions_adminsdholder` |
| ANSSI levels | 1, 2 (1: most critical; one threshold per level, see the text) |
| Added to the checklist | 2019-02-22 |
| Last change | 2026-07-15 |
| Link | [cert.cyber.gouv.fr/…](https://cert.cyber.gouv.fr/uploads/ad_checklist.html) |
| Cited by SMAD-X | ACL-03 |

## SMAD-X rules that cite it

- **ACL-03** — Dangerous permissions on the domain, AdminSDHolder or the domain controllers

## Official recommendation

#### General Case

Permissions set on the `adminSDHolder` object are periodically copied to all protected AD objects (privileged built-in group members). By default, only privileged objects are granted access rights on the `adminSDHolder` object. This mechanism protects the most privileged Active directory users and groups from accidental misconfigurations.

Modifying default permissions set on this object is strongly discouraged. Removing dangerous permissions is strongly advised to return the object to its default state. Fixing usually requires using `adsiedit.msc` or the `ldp` utility.

#### Specific Cases

When deploying an administration forest, granting access rights on the `adminSDHolder` object to a group is tolerated given the following cumulative criteria are met:

- access rights on the trusted group are stricter than those initially granted on the `adminSDHolder` object;
- permission inheritance is disabled for the trusted group.

If a flagged access right relates to Exchange, (such as  ), the latest relevant cumulative updates must be applied. Documentation on access rights granted to Exchange Server is available at the following URL:

- [https://support.microsoft.com/en-us/help/4490059/using-shared-permissions-model-to-run-exchange-server](https://support.microsoft.com/en-us/help/4490059/using-shared-permissions-model-to-run-exchange-server) Note: if flagged, the  access right granted to `Exchange Trusted Subsystem` is a configuration defect that has been fixed by Microsoft in a previous update.

If a flagged access right relates to an Entra ID Connect connector account (MSOL), its relevant access rights must be reconfigured. Documentation is available at the following URL:

- [https://docs.microsoft.com/en-us/azure/active-directory/hybrid/how-to-connect-configure-ad-ds-connector-account](https://docs.microsoft.com/en-us/azure/active-directory/hybrid/how-to-connect-configure-ad-ds-connector-account) PowerShell cmdlet `Set-ADSyncRestrictedPermissions` with `-SkipAdminSdHolders` may be used during the configuration phase.

If a flagged access right relates to an Entra ID Cloud Sync provioning agent (ProvAgentgMSA$), its relevant access rights must be reconfigured. Documentation is available at the following URL:

- [https://learn.microsoft.com/en-us/entra/identity/hybrid/cloud-sync/how-to-gmsa-cmdlets#using-set-aadcloudsyncrestrictedpermissions](https://learn.microsoft.com/en-us/entra/identity/hybrid/cloud-sync/how-to-gmsa-cmdlets#using-set-aadcloudsyncrestrictedpermissions) PowerShell cmdlet `Set-AADCloudSyncRestrictedPermissions`.

## Source and licence

- **Source**: [https://cert.cyber.gouv.fr/uploads/ad_checklist.html](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)
- **Retrieved publication**: 2026-11-12
- **Retrieved on**: 2026-11-13, by `tools/SmadxSources`
- **Licence**: Etalab Open Licence 2.0: free reuse citing the source (CERT-FR / ANSSI) and the date of its update; official texts reproduced in French and English
