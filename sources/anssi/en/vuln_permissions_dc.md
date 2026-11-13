# vuln_permissions_dc — Dangerous ACLs expose domain controller objects (attack path)

> [Contents: ANSSI — Active Directory control points](README.md) · Français : [vuln_permissions_dc.md](../fr/vuln_permissions_dc.md)

| Sheet | |
|:---|:---|
| Framework | ANSSI — Active Directory control points |
| Identifier | `vuln_permissions_dc` |
| ANSSI levels | 1, 2 (1: most critical; one threshold per level, see the text) |
| Added to the checklist | 2019-02-22 |
| Last change | 2023-11-08 |
| Link | [cert.cyber.gouv.fr/…](https://cert.cyber.gouv.fr/uploads/ad_checklist.html) |
| Cited by SMAD-X | ACL-03 |

## SMAD-X rules that cite it

- **ACL-03** — Dangerous permissions on the domain, AdminSDHolder or the domain controllers

## Official recommendation

Accounts which are granted permissions on domain controllers must be considered privileged. Therefore, they must be protected through the `adminSDHolder` mechanism. These accounts must belong to either the *Enterprise Admins* or the *Domain Admins* group.

When deploying an administration forest, granting access rights on domain controllers is tolerated given the following cumulative criteria are met:

- delegated groups must be of `DOMAIN LOCAL` type
- permissions on that group must be stricter than those applied to `adminSDHolder`

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

> #### Risk Acceptance
>
> If special permissions are required, delegated groups or accounts should be considered as privileged. Therefore, these objects must be adequately protected, and their own access control list should be at least as strict as that of the `adminSDHolder` object. "Secure" ACLs should therefore be implemented on these delegated objects, which will not open a new control path exposure. To remove inheritance and restrict ACEs, the following command may be used: `` found in the `AdSyncConfig.psm1` module from Azure AD Connect.
>
> For **delegated groups**, strengthening permissions using this command is not enough: the permissions of this group's members must also be strengthened by applying this command to them, or they can be removed from the delegated group.
>
> Example: applying "secure" permissions on a privileged account through ``:
>
> Note: the `AdSyncConfig.psm1` module may be extracted from the Entra ID Connect installer with `msiexec`: `msiexec /a "AzureADConnect.msi" /qb TARGETDIR=c:\temp\`
>
> The installation package may be downloaded from [https://go.microsoft.com/fwlink/?LinkId=615771](https://go.microsoft.com/fwlink/?LinkId=615771).
>
> #### Accounts that are regarded as protected
>
> An object is regarded as protected "`adminSDHolder`-like" if the following conditions are all met:
>
> - Inheritance is disabled (flag `SE_DACL_PROTECTED`);
> - All ACEs on the object's security descriptor belong in at least one of these groups:
> - ACEs that are exactly identical to ACEs that are set on the forest's `adminSDHolder` object;
> - Benign ACEs: ACEs for `Self`, for *Enterprise Domain Controllers* extended right "Sent-To";
> - If the object is a group, its members must also be similarly protected. Sub-groups are not allowed.
>
> If the `adminSDHolder` object has been modified, do make sure that the conditions above are still met, since the ACEs that are set by the `` command might not match the modified `adminSDHolder` object anymore.
>
> Once protected, accounts are listed in the information item "Additional Tier 0 accounts".

## Source and licence

- **Source**: [https://cert.cyber.gouv.fr/uploads/ad_checklist.html](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)
- **Retrieved publication**: 2026-11-12
- **Retrieved on**: 2026-11-13, by `tools/SmadxSources`
- **Licence**: Etalab Open Licence 2.0: free reuse citing the source (CERT-FR / ANSSI) and the date of its update; official texts reproduced in French and English
