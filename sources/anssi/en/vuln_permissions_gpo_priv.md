# vuln_permissions_gpo_priv — Dangerous ACLs expose GPOs applied to privileged group members (attack path)

> [Contents: ANSSI — Active Directory control points](README.md) · Français : [vuln_permissions_gpo_priv.md](../fr/vuln_permissions_gpo_priv.md)

| Sheet | |
|:---|:---|
| Framework | ANSSI — Active Directory control points |
| Identifier | `vuln_permissions_gpo_priv` |
| ANSSI level | 1 (1: most critical) |
| Added to the checklist | 2020-04-06 |
| Last change | 2023-11-08 |
| Link | [cert.cyber.gouv.fr/…](https://cert.cyber.gouv.fr/uploads/ad_checklist.html) |
| Cited by SMAD-X | TIER-02 |

## SMAD-X rules that cite it

- **TIER-02** — Tiering violation: GPO from a less sensitive tier

## Official text

> #### Dangerous ACLs and attack paths
>
> Dangerous permissions allow taking control of a target object, on which those permissions are set, by writing sensitive attributes, privileged right delegation or through specific features.
>
> Once granted control of privileged objects, an attacker may then repeat the same strategy to gain control of other objects, leading to privilege escalation and possibly a full Active Directory compromise. The succession of exploited permissions constitutes an **attack path** or a control path.
>
> Control paths reveal how an attacker could leverage a chain of defective or dangerous permissions to gain control of some targets. They can also be leveraged to quickly identify domain mismanagement, to validate the actual security boundary of a particular scope (making sure some object of interest cannot be taken over by unauthorized users through misconfiguration). Indicators of compromise may also be found through control path review.

Dangerous ACLs expose GPOs that are applied to privileged group members. An attacker gaining access to these GPOs may execute code on those privilege account workstations to escalate their privileges.

*(Table of the affected objects, in the analysis report.)*

## Official recommendation

GPO LDAP object permissions should be reviewed.

The fix should be applied is usually applied using the `gpmc.msc` utility which ensures permissions as synchronized with the SYSVOL folder.

Before accepting the risk (see below), review the list of principals and ensure that they should really be considered as T0 administrators. In case of risk acceptance, all principals (including their members for groups, recursively) for whom a control path exists to a GPO that is applied to privileged group members must also be considered as privileged and protected as such.

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
