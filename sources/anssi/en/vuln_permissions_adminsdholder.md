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

## Official text

> #### Dangerous ACLs and attack paths
>
> Dangerous permissions allow taking control of a target object, on which those permissions are set, by writing sensitive attributes, privileged right delegation or through specific features.
>
> Once granted control of privileged objects, an attacker may then repeat the same strategy to gain control of other objects, leading to privilege escalation and possibly a full Active Directory compromise. The succession of exploited permissions constitutes an **attack path** or a control path.
>
> Control paths reveal how an attacker could leverage a chain of defective or dangerous permissions to gain control of some targets. They can also be leveraged to quickly identify domain mismanagement, to validate the actual security boundary of a particular scope (making sure some object of interest cannot be taken over by unauthorized users through misconfiguration). Indicators of compromise may also be found through control path review.

> #### Alert threshold
>
> This assessment raises an alert depending on varying thresholds:
>
> - at level **1** only dangerous access control entries that can be exploited without any major prerequisite are taken into account;
> - at level **2** all potentially dangerous access control entries are taken into account.

Dangerous permissions are set on the `adminSDHolder` object. Permissions set on this object may grant trusted objects full control over the Active Directory. In a tiered administrative model, these permissions allow compromise of Tier 0 objects from lower trust tiers.

*(Table of the affected objects, in the analysis report.)*

## Official recommendation

#### General Case

Permissions set on the `adminSDHolder` object are periodically copied to all protected AD objects (privileged built-in group members). By default, only privileged objects are granted access rights on the `adminSDHolder` object. This mechanism protects the most privileged Active directory users and groups from accidental misconfigurations.

Modifying default permissions set on this object is strongly discouraged. Removing dangerous permissions is strongly advised to return the object to its default state. Fixing usually requires using `adsiedit.msc` or the `ldp` utility.

#### Specific Cases

When deploying an administration forest, granting access rights on the `adminSDHolder` object to a group is tolerated given the following cumulative criteria are met:

- access rights on the trusted group are stricter than those initially granted on the `adminSDHolder` object;
- permission inheritance is disabled for the trusted group.

If a flagged access right relates to Exchange, (such as `WRITE_SPN` ), the latest relevant cumulative updates must be applied. Documentation on access rights granted to Exchange Server is available at the following URL:

- [https://support.microsoft.com/en-us/help/4490059/using-shared-permissions-model-to-run-exchange-server](https://support.microsoft.com/en-us/help/4490059/using-shared-permissions-model-to-run-exchange-server) Note: if flagged, the `WRITE_ALT_IDENTITY` access right granted to `Exchange Trusted Subsystem` is a configuration defect that has been fixed by Microsoft in a previous update.

If a flagged access right relates to an Entra ID Connect connector account (MSOL), its relevant access rights must be reconfigured. Documentation is available at the following URL:

- [https://docs.microsoft.com/en-us/azure/active-directory/hybrid/how-to-connect-configure-ad-ds-connector-account](https://docs.microsoft.com/en-us/azure/active-directory/hybrid/how-to-connect-configure-ad-ds-connector-account) PowerShell cmdlet `Set-ADSyncRestrictedPermissions` with `-SkipAdminSdHolders` may be used during the configuration phase.

If a flagged access right relates to an Entra ID Cloud Sync provioning agent (ProvAgentgMSA$), its relevant access rights must be reconfigured. Documentation is available at the following URL:

- [https://learn.microsoft.com/en-us/entra/identity/hybrid/cloud-sync/how-to-gmsa-cmdlets#using-set-aadcloudsyncrestrictedpermissions](https://learn.microsoft.com/en-us/entra/identity/hybrid/cloud-sync/how-to-gmsa-cmdlets#using-set-aadcloudsyncrestrictedpermissions) PowerShell cmdlet `Set-AADCloudSyncRestrictedPermissions`.

> #### Context: control path exposure
>
> A control path materializes through a set of granted access permissions: each granted permission can be expressed as one object controlling another through a specific property, attribute or setting. Control paths reveal how an attacker could leverage a chain of defective or dangerous permissions to gain control of some targets. They can also be leveraged to quickly identify domain mismanagement, to validate the actual security boundary of a particular scope (making sure some object of interest cannot be taken over by unauthorized users through misconfiguration). Indicators of compromise may also be found through control path review.
>
> #### Limitations
>
> Some control paths may have fixes that renders them non-exploitable. Such a case usually requires having all domain controllers equally up to date. Furthermore, dangerous features may sometimes be re-enabled through a registry or dsHeuristics setting. It is therefore still recommended to clean the affected permissions from your Active Directory to prevent an accidental re-occurrence.
>
> List of known specific fixes:
>
> - [KB5008383](https://support.microsoft.com/en-us/topic/kb5008383-active-directory-permissions-updates-cve-2021-42291-536d5555-ffba-4248-a60e-d6cbc849cde1) fixes the `OWNER` and `WRITE_OWNER` control path on computer accounts only, but the dangerous behavior can still be re-enabled.
> - Various Exchange-related control paths have been fixed through specific cumulative updates; refer to the dedicated section below if applicable.
>
> #### Microsoft Exchange Server Specifics
>
> Some control paths under this item may appear because of Exchange Server management groups with excessive permissions. The cause can be:
>
> - either the offending groups come from a legacy Exchange Server 2007 or earlier instance ("Exchange Enterprise Servers", "Exchange Domain Servers" ou "Exchange Services") which has been migrated to version 2010 or later, it is then best to remove those groups and associated permissions. [Microsoft provides relevant guidance to fix these issues](https://learn.microsoft.com/en-us/previous-versions/office/exchange-server-2007/bb288905(v=exchg.80)?redirectedfrom=MSDN);
> - or these are current Exchange Server management groups, a specific analysis will help select the applicable solution (see below).
>
> The following controls have been fixed by Microsoft, only if pertaining to Exchange Server groups:
>
> - `WRITE_SPN`
> - `WRITE_DAC`
> - `WRITE_OWNER`
> - `WRITE_MANAGED_BY`
> - `WRITE_ALT_IDENTITY`
>
> Warning: some LDAP attributes (`servicePrincipalName` and `altSecurityIdentities`) behind these controls belong to the `Public-Information` *property set*. Therefore, these write permissions may be granted on an attribute basis or to the whole *property set*, in which case only this property set will be visible in the object's DACL.
>
> Applying the official fix involves the following steps:
>
> - Exchange Server 2019: install may 2022 SU on CU12 ([KB5014261](https://support.microsoft.com/en-us/topic/description-of-the-security-update-for-microsoft-exchange-server-2016-and-2019-may-10-2022-kb5014261-cd5ecb59-a0eb-47ef-ae35-f62b13c8b817));
> - Exchange Server 2016: install may 2022 SU on CU23 ([KB5014261](https://support.microsoft.com/en-us/topic/description-of-the-security-update-for-microsoft-exchange-server-2016-and-2019-may-10-2022-kb5014261-cd5ecb59-a0eb-47ef-ae35-f62b13c8b817));
> - Exchange Server 2013: install may 2022 SU on CU23 ([KB5014260](https://support.microsoft.com/en-us/topic/description-of-the-security-update-for-microsoft-exchange-server-2013-may-10-2022-kb5014260-701fda2b-7417-4ffa-979b-0b2bc8e93b1c));
> - for all version, after applying the above security updates: run the "/PrepareAllDomains" setup phase
> - on Exchange Server 2019 and 2016: `Setup.exe /IAcceptExchangeServerLicenseTerms_DiagnosticDataON /PrepareAllDomains` or `Setup.exe /IAcceptExchangeServerLicenseTerms_DiagnosticDataOFF /PrepareAllDomains` from your Exchange Server installation path;
> - on Exchange Server 2013: `Setup.exe /IAcceptExchangeServerLicenseTerms /PrepareAllDomains` from your Exchange Server installation path;
> - Please note Exchange Server 2010 is not supported anymore by Microsoft.
>
> Any other control path shall be manually investigated to properly address the issue.
>
> A list of published Exchange Server security updates is [available here](https://learn.microsoft.com/en-us/exchange/new-features/build-numbers-and-release-dates).

## Source and licence

- **Source**: [https://cert.cyber.gouv.fr/uploads/ad_checklist.html](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)
- **Retrieved publication**: 2026-11-12
- **Retrieved on**: 2026-11-13, by `tools/SmadxSources`
- **Licence**: Etalab Open Licence 2.0: free reuse citing the source (CERT-FR / ANSSI) and the date of its update; official texts reproduced in French and English
