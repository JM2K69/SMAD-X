# Audit rules

!!! info "Generated page"
    This page is produced from the SMAD-X code (`tools/DocGenerator`): it lists exactly the 31 rules the application evaluates.

Each failed rule takes points off the score, once whatever the number of objects involved: **12** for a critical rule, **6** high, **3** medium, **1** low. The estimated ANSSI level is the level of the most critical failed control point (5 if none); it is not the official computation of ANSSI's ADS tool.

## Kerberos

| Rule | Severity | ANSSI level | References |
|---|---|---|---|
| **KRB-01** Privileged account vulnerable to Kerberoasting | Critical | 1 | [ANSSI · vuln_spn_priv](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)<br>[MITRE ATT&CK · T1558.003](https://attack.mitre.org/techniques/T1558/003/)<br>[NIST · SP 800-53 Rev. 5 AC-6(5)](https://csrc.nist.gov/pubs/sp/800/53/r5/upd1/final)<br>[CIS · CIS Controls v8 5.4](https://cas.docs.cisecurity.org/en/latest/source/Controls5/)<br>[Microsoft · Microsoft Learn](https://learn.microsoft.com/en-us/windows-server/security/group-managed-service-accounts/group-managed-service-accounts-overview) |
| **KRB-02** User account vulnerable to Kerberoasting | Medium | — | [ANSSI · info_spn_present](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)<br>[MITRE ATT&CK · T1558.003](https://attack.mitre.org/techniques/T1558/003/)<br>[NIST · SP 800-53 Rev. 5 IA-5](https://csrc.nist.gov/pubs/sp/800/53/r5/upd1/final)<br>[CIS · CIS Controls v8 5.5](https://cas.docs.cisecurity.org/en/latest/source/Controls5/)<br>[Microsoft · Microsoft Learn](https://learn.microsoft.com/en-us/windows-server/security/group-managed-service-accounts/group-managed-service-accounts-overview) |
| **KRB-03** Privileged account without Kerberos preauthentication | Critical | 1 | [ANSSI · vuln_kerberos_properties_preauth_priv](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)<br>[MITRE ATT&CK · T1558.004](https://attack.mitre.org/techniques/T1558/004/)<br>[NIST · SP 800-53 Rev. 5 AC-6(5)](https://csrc.nist.gov/pubs/sp/800/53/r5/upd1/final) |
| **KRB-04** Account without Kerberos preauthentication | High | 2 | [ANSSI · vuln_kerberos_properties_preauth](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)<br>[MITRE ATT&CK · T1558.004](https://attack.mitre.org/techniques/T1558/004/)<br>[NIST · SP 800-53 Rev. 5 IA-5](https://csrc.nist.gov/pubs/sp/800/53/r5/upd1/final) |
| **KRB-05** krbtgt account password too old | High | 2 | [ANSSI · vuln_krbtgt](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)<br>[MITRE ATT&CK · T1558.001](https://attack.mitre.org/techniques/T1558/001/)<br>[NIST · SP 800-53 Rev. 5 IA-5](https://csrc.nist.gov/pubs/sp/800/53/r5/upd1/final)<br>[Microsoft · Microsoft Learn](https://learn.microsoft.com/en-us/windows-server/identity/ad-ds/manage/forest-recovery-guide/ad-forest-recovery-reset-the-krbtgt-password) |

## Delegation

| Rule | Severity | ANSSI level | References |
|---|---|---|---|
| **DLG-01** Unconstrained Kerberos delegation | Critical | 1 | [ANSSI · vuln_delegation_t4d](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)<br>[MITRE ATT&CK · T1558](https://attack.mitre.org/techniques/T1558/)<br>[MITRE ATT&CK · T1550.003](https://attack.mitre.org/techniques/T1550/003/)<br>[NIST · SP 800-53 Rev. 5 AC-6](https://csrc.nist.gov/pubs/sp/800/53/r5/upd1/final)<br>[Microsoft · Microsoft Learn](https://learn.microsoft.com/en-us/windows-server/security/credentials-protection-and-management/protected-users-security-group) |
| **DLG-02** Constrained delegation with protocol transition to a domain controller | Critical | 1 | [ANSSI · vuln_delegation_t2a4d](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)<br>[MITRE ATT&CK · T1558](https://attack.mitre.org/techniques/T1558/)<br>[NIST · SP 800-53 Rev. 5 AC-6](https://csrc.nist.gov/pubs/sp/800/53/r5/upd1/final)<br>[Microsoft · Microsoft Learn](https://learn.microsoft.com/en-us/windows-server/security/kerberos/kerberos-constrained-delegation-overview) |
| **DLG-03** Constrained delegation to a domain controller | Critical | 1 | [ANSSI · vuln_delegation_a2d2](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)<br>[MITRE ATT&CK · T1558](https://attack.mitre.org/techniques/T1558/)<br>[NIST · SP 800-53 Rev. 5 AC-6](https://csrc.nist.gov/pubs/sp/800/53/r5/upd1/final)<br>[Microsoft · Microsoft Learn](https://learn.microsoft.com/en-us/windows-server/security/kerberos/kerberos-constrained-delegation-overview) |
| **DLG-04** Privileged account not protected against delegation | Medium | 3 | [ANSSI · vuln_protected_users](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)<br>[MITRE ATT&CK · T1550.003](https://attack.mitre.org/techniques/T1550/003/)<br>[NIST · SP 800-53 Rev. 5 AC-6(5)](https://csrc.nist.gov/pubs/sp/800/53/r5/upd1/final)<br>[Microsoft · Microsoft Learn](https://learn.microsoft.com/en-us/windows-server/security/credentials-protection-and-management/protected-users-security-group) |

## Passwords

| Rule | Severity | ANSSI level | References |
|---|---|---|---|
| **PWD-01** Privileged account whose password never expires | Critical | 1 | [ANSSI · vuln_dont_expire_priv](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)<br>[NIST · SP 800-53 Rev. 5 AC-6(5)](https://csrc.nist.gov/pubs/sp/800/53/r5/upd1/final)<br>[NIST · SP 800-53 Rev. 5 IA-5](https://csrc.nist.gov/pubs/sp/800/53/r5/upd1/final)<br>[NIST · SP 800-63B](https://pages.nist.gov/800-63-4/sp800-63b.html) |
| **PWD-02** Password never expires | High | 2 | [ANSSI · vuln_dont_expire](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)<br>[NIST · SP 800-53 Rev. 5 IA-5](https://csrc.nist.gov/pubs/sp/800/53/r5/upd1/final)<br>[NIST · SP 800-63B](https://pages.nist.gov/800-63-4/sp800-63b.html) |
| **PWD-03** Password not required | High | — | [MITRE ATT&CK · T1078.002](https://attack.mitre.org/techniques/T1078/002/)<br>[NIST · SP 800-53 Rev. 5 IA-5](https://csrc.nist.gov/pubs/sp/800/53/r5/upd1/final)<br>[CIS · CIS Controls v8 5.2](https://cas.docs.cisecurity.org/en/latest/source/Controls5/) |
| **PWD-04** Privileged account password unchanged for more than 3 years | Critical | 1 | [ANSSI · vuln_password_change_priv](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)<br>[NIST · SP 800-53 Rev. 5 AC-6(5)](https://csrc.nist.gov/pubs/sp/800/53/r5/upd1/final)<br>[NIST · SP 800-53 Rev. 5 IA-5](https://csrc.nist.gov/pubs/sp/800/53/r5/upd1/final) |
| **PWD-05** Domain controller machine password unchanged | Critical | 1 | [ANSSI · vuln_password_change_dc_no_change](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)<br>[NIST · SP 800-53 Rev. 5 IA-5](https://csrc.nist.gov/pubs/sp/800/53/r5/upd1/final) |
| **PWD-06** Computer account whose password no longer changes | High | 2 | [ANSSI · vuln_password_change_server_no_change_90](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)<br>[NIST · SP 800-53 Rev. 5 AC-2](https://csrc.nist.gov/pubs/sp/800/53/r5/upd1/final)<br>[CIS · CIS Controls v8 5.3](https://cas.docs.cisecurity.org/en/latest/source/Controls5/) |
| **PWD-07** Managed service account whose password no longer changes | High | 2 | [ANSSI · vuln_password_change_msa_no_change_90](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)<br>[NIST · SP 800-53 Rev. 5 IA-5](https://csrc.nist.gov/pubs/sp/800/53/r5/upd1/final)<br>[Microsoft · Microsoft Learn](https://learn.microsoft.com/en-us/windows-server/security/group-managed-service-accounts/group-managed-service-accounts-overview) |
| **PWD-08** Domain password policy too short | Medium | — | [NIST · SP 800-63B](https://pages.nist.gov/800-63-4/sp800-63b.html)<br>[CIS · CIS Controls v8 5.2](https://cas.docs.cisecurity.org/en/latest/source/Controls5/)<br>[NIST · SP 800-53 Rev. 5 IA-5](https://csrc.nist.gov/pubs/sp/800/53/r5/upd1/final) |
| **PWD-09** Privileged account with a weak password policy | High | 2 | [ANSSI · vuln_privileged_members_password](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)<br>[NIST · SP 800-63B](https://pages.nist.gov/800-63-4/sp800-63b.html)<br>[NIST · SP 800-53 Rev. 5 AC-6(5)](https://csrc.nist.gov/pubs/sp/800/53/r5/upd1/final) |

## Accounts

| Rule | Severity | ANSSI level | References |
|---|---|---|---|
| **ACC-03** Any user can join computers to the domain | Low | 4 | [ANSSI · vuln_user_accounts_machineaccountquota](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)<br>[Microsoft · Microsoft Learn](https://learn.microsoft.com/en-us/windows/win32/adschema/a-ms-ds-machineaccountquota)<br>[MITRE ATT&CK · T1136.002](https://attack.mitre.org/techniques/T1136/002/)<br>[NIST · SP 800-53 Rev. 5 AC-2](https://csrc.nist.gov/pubs/sp/800/53/r5/upd1/final) |
| **ACC-01** Active Guest account | High | 2 | [ANSSI · vuln_guest](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)<br>[NIST · SP 800-53 Rev. 5 AC-2](https://csrc.nist.gov/pubs/sp/800/53/r5/upd1/final)<br>[MITRE ATT&CK · T1078.002](https://attack.mitre.org/techniques/T1078/002/) |
| **ACC-02** Dormant account | Medium | — | [ANSSI · info_user_accounts_dormant](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)<br>[CIS · CIS Controls v8 5.3](https://cas.docs.cisecurity.org/en/latest/source/Controls5/)<br>[NIST · SP 800-53 Rev. 5 AC-2](https://csrc.nist.gov/pubs/sp/800/53/r5/upd1/final)<br>[MITRE ATT&CK · T1078.002](https://attack.mitre.org/techniques/T1078/002/) |

## Privileged groups

| Rule | Severity | ANSSI level | References |
|---|---|---|---|
| **PRIV-01** Privileged group member without adminCount | High | 2 | [ANSSI · vuln_privileged_members_no_admincount](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)<br>[Microsoft · Microsoft Learn](https://learn.microsoft.com/en-us/windows-server/identity/ad-ds/plan/security-best-practices/appendix-c--protected-accounts-and-groups-in-active-directory)<br>[NIST · SP 800-53 Rev. 5 AC-6(5)](https://csrc.nist.gov/pubs/sp/800/53/r5/upd1/final) |
| **PRIV-02** Orphaned adminCount | Low | — | [ANSSI · warning_admincount](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)<br>[Microsoft · Microsoft Learn](https://learn.microsoft.com/en-us/windows-server/identity/ad-ds/plan/security-best-practices/appendix-c--protected-accounts-and-groups-in-active-directory)<br>[NIST · SP 800-53 Rev. 5 AC-2](https://csrc.nist.gov/pubs/sp/800/53/r5/upd1/final) |
| **PRIV-03** Privileged group that should be empty | Medium | 3 | [ANSSI · warning_privileged_members_not_empty](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)<br>[Microsoft · Microsoft Learn](https://learn.microsoft.com/en-us/windows-server/identity/ad-ds/plan/security-best-practices/appendix-b--privileged-accounts-and-groups-in-active-directory)<br>[NIST · SP 800-53 Rev. 5 AC-6(5)](https://csrc.nist.gov/pubs/sp/800/53/r5/upd1/final)<br>[CIS · CIS Controls v8 5.4](https://cas.docs.cisecurity.org/en/latest/source/Controls5/) |
| **PRIV-04** Non-privileged member of the DnsAdmins group | Critical | 1 | [ANSSI · vuln_dnsadmins](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)<br>[Microsoft · Microsoft Learn](https://learn.microsoft.com/en-us/windows-server/identity/ad-ds/plan/security-best-practices/appendix-b--privileged-accounts-and-groups-in-active-directory)<br>[NIST · SP 800-53 Rev. 5 AC-6](https://csrc.nist.gov/pubs/sp/800/53/r5/upd1/final) |
| **PRIV-05** Protected group classified outside Tier 0 | Medium | — | [Microsoft · Microsoft Learn](https://learn.microsoft.com/en-us/windows-server/identity/ad-ds/plan/security-best-practices/appendix-b--privileged-accounts-and-groups-in-active-directory)<br>[ANSSI · ANSSI-PA-099](https://messervices.cyber.gouv.fr/guides/recommandations-pour-ladministration-securisee-des-si-reposant-sur-ad)<br>[Microsoft · Microsoft Learn](https://learn.microsoft.com/en-us/security/privileged-access-workstations/privileged-access-access-model) |

## Owners and permissions

| Rule | Severity | ANSSI level | References |
|---|---|---|---|
| **ACL-01** Critical object with an inappropriate owner | Medium | 3 | [ANSSI · vuln_owner](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)<br>[MITRE ATT&CK · T1222.001](https://attack.mitre.org/techniques/T1222/001/)<br>[MITRE ATT&CK · T1098](https://attack.mitre.org/techniques/T1098/)<br>[NIST · SP 800-53 Rev. 5 AC-6](https://csrc.nist.gov/pubs/sp/800/53/r5/upd1/final) |

## Tiering

| Rule | Severity | ANSSI level | References |
|---|---|---|---|
| **TIER-01** Tiering violation: membership in a more sensitive group | High | — | [ANSSI · ANSSI-PA-099](https://messervices.cyber.gouv.fr/guides/recommandations-pour-ladministration-securisee-des-si-reposant-sur-ad)<br>[Microsoft · Microsoft Learn](https://learn.microsoft.com/en-us/security/privileged-access-workstations/privileged-access-access-model)<br>[NIST · SP 800-53 Rev. 5 AC-5](https://csrc.nist.gov/pubs/sp/800/53/r5/upd1/final)<br>[NIST · SP 800-53 Rev. 5 AC-6](https://csrc.nist.gov/pubs/sp/800/53/r5/upd1/final)<br>[CIS · CIS Controls v8 6.8](https://cas.docs.cisecurity.org/en/latest/source/Controls6/) |
| **TIER-02** Tiering violation: GPO from a less sensitive tier | High | — | [ANSSI · vuln_permissions_gpo_priv](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)<br>[ANSSI · ANSSI-PA-099](https://messervices.cyber.gouv.fr/guides/recommandations-pour-ladministration-securisee-des-si-reposant-sur-ad)<br>[MITRE ATT&CK · T1484.001](https://attack.mitre.org/techniques/T1484/001/)<br>[Microsoft · Microsoft Learn](https://learn.microsoft.com/en-us/security/privileged-access-workstations/privileged-access-access-model) |
| **TIER-03** Tiering violation: delegation to a less sensitive tier | High | — | [ANSSI · vuln_privileged_members_perm](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)<br>[ANSSI · ANSSI-PA-099](https://messervices.cyber.gouv.fr/guides/recommandations-pour-ladministration-securisee-des-si-reposant-sur-ad)<br>[MITRE ATT&CK · T1098](https://attack.mitre.org/techniques/T1098/)<br>[NIST · SP 800-53 Rev. 5 AC-6](https://csrc.nist.gov/pubs/sp/800/53/r5/upd1/final)<br>[Microsoft · Microsoft Learn](https://learn.microsoft.com/en-us/windows-server/identity/ad-ds/plan/security-best-practices/implementing-least-privilege-administrative-models) |

## Groups

| Rule | Severity | ANSSI level | References |
|---|---|---|---|
| **GRP-01** Group nesting loop | Low | 4 | [ANSSI · vuln_group_loop](https://cert.cyber.gouv.fr/uploads/ad_checklist.html)<br>[NIST · SP 800-53 Rev. 5 AC-2](https://csrc.nist.gov/pubs/sp/800/53/r5/upd1/final) |

