# vuln_privileged_members_perm — Dangerous ACLs expose members of privileged groups (attack path)

> [Contents: ANSSI — Active Directory control points](README.md) · Français : [vuln_privileged_members_perm.md](../fr/vuln_privileged_members_perm.md)

| Sheet | |
|:---|:---|
| Framework | ANSSI — Active Directory control points |
| Identifier | `vuln_privileged_members_perm` |
| ANSSI levels | 1, 2 (1: most critical; one threshold per level, see the text) |
| Added to the checklist | 2019-01-11 |
| Last change | 2023-11-08 |
| Link | [cert.cyber.gouv.fr/…](https://cert.cyber.gouv.fr/uploads/ad_checklist.html) |
| Cited by SMAD-X | ACL-02 · TIER-03 |

## SMAD-X rules that cite it

- **ACL-02** — Control path to the domain through permissions
- **TIER-03** — Tiering violation: delegation to a less sensitive tier

## Official text

> #### Dangerous ACLs and attack paths
>
> Dangerous permissions allow taking control of a target object, on which those permissions are set, by writing sensitive attributes, privileged right delegation or through specific features.
>
> Once granted control of privileged objects, an attacker may then repeat the same strategy to gain control of other objects, leading to privilege escalation and possibly a full Active Directory compromise. The succession of exploited permissions constitutes an **attack path** or a control path.
>
> Control paths reveal how an attacker could leverage a chain of defective or dangerous permissions to gain control of some targets. They can also be leveraged to quickly identify domain mismanagement, to validate the actual security boundary of a particular scope (making sure some object of interest cannot be taken over by unauthorized users through misconfiguration). Indicators of compromise may also be found through control path review.

This assessment raises an alert depending on varying thresholds: at level **1** only dangerous access control entries that can be exploited without any major prerequisite are taken into account; at level **2** all potentially dangerous access control entries are taken into account.

Dangerous ACLs expose members of privileged groups. These access rights grant non-privileged users the ability to take control of privileged accounts.

*(Table of the affected objects, in the analysis report.)*

## Official recommendation

Most of the items shown here are due to the "`Dangerous permissions on the adminSDHolder object`" and "`Dangerous ACLs expose domain controller objects`" checks. Fixing it will take care of most of these items.

For all remaining permissions, relevant dangerous entries should be removed. The fix is usually applied through the `adsiedit.msc` or `ldp` utilities.

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

> #### Context: privileged groups
>
> Privileged groups include administrator groups and operator groups which have full control over the Active Directory forest either by design, or by granting themselves such privileges:
>
> - « *Administrators* »;
> - « *Domain Controllers* »;
> - « *Schema Admins* »;
> - « *Enterprise Admins* »;
> - « *Domain Admins* »: these administrators are able to read the authentication database and extract password hashes and other secrets of all privileged accounts;
> - « *Key Admins* » and « *Enterprise Key Admins* »: these administrators can set arbitrary values to attributes relating to Windows Hello for Business, for all users except those protected by the `adminSDHolder` mechanism. If certificate-based authentication has been enabled, they can generate a certificate that they control, assign it to privileged accounts (for instance, a domain controller), and log on as them;
> - « *Account Operators* »: these operators can manage user accounts, machine accounts and groups except accounts protected by the `adminSDHolder` mechanism;
> - « *Server Operators* »: these operators can manage domain controllers, which grants them the ability to recover privileged user secrets directly from the authentication database;
> - « *Backup Operators* »: these operators can back up a domain controller, which grants them the ability to recover privileged user secrets from that backup;
> - « *Print Operators* »: these operators can load arbitrary printer drivers on domain controllers, which grants them the ability to recover privileged user secrets directly from the authentication database (by loading a malicious driver).
>
> The « operators » groups are empty by default and must not have any member.

> #### Risk Acceptance
>
> If special permissions are required, delegated groups or accounts should be considered as privileged. Therefore, these objects must be adequately protected, and their own access control list should be at least as strict as that of the `adminSDHolder` object. "Secure" ACLs should therefore be implemented on these delegated objects, which will not open a new control path exposure. To remove inheritance and restrict ACEs, the following command may be used: `Set-ADSyncRestrictedPermissions` found in the `AdSyncConfig.psm1` module from Azure AD Connect.
>
> For **delegated groups**, strengthening permissions using this command is not enough: the permissions of this group's members must also be strengthened by applying this command to them, or they can be removed from the delegated group.
>
> Example: applying "secure" permissions on a privileged account through `Set-ADSyncRestrictedPermissions`:
>
> ```
> Import-Module .\AdSyncConfig.psm1
> $credential = Get-Credential
> Set-ADSyncRestrictedPermissions -ADConnectorAccountDN "CN=ExampleUserAccount,CN=Users,DC=example,DC=ads" -Credential $credential
> ```
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
> If the `adminSDHolder` object has been modified, do make sure that the conditions above are still met, since the ACEs that are set by the `Set-ADSyncRestrictedPermissions` command might not match the modified `adminSDHolder` object anymore.
>
> Once protected, accounts are listed in the information item "Additional Tier 0 accounts".

## Source and licence

- **Source**: [https://cert.cyber.gouv.fr/uploads/ad_checklist.html](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)
- **Retrieved publication**: 2026-11-12
- **Retrieved on**: 2026-11-13, by `tools/SmadxSources`
- **Licence**: Etalab Open Licence 2.0: free reuse citing the source (CERT-FR / ANSSI) and the date of its update; official texts reproduced in French and English
