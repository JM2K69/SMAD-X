# Sources

Audit rules, sheets and attack paths rely on these references. Each rule cites its own: see [Audit rules](audit-rules.md).

## ANSSI

- [Active Directory control points](https://cert.cyber.gouv.fr/uploads/ad_checklist.html): the CERT-FR checklist, whose identifiers (`vuln_spn_priv`, `vuln_dnsadmins`…) and official levels SMAD-X uses.
- [Recommendations for the secure administration of AD-based information systems (ANSSI-PA-099)](https://messervices.cyber.gouv.fr/guides/recommandations-pour-ladministration-securisee-des-si-reposant-sur-ad), in French: tiering.

## NIST

- [SP 800-53 Rev. 5](https://csrc.nist.gov/pubs/sp/800/53/r5/upd1/final) : security controls (least privilege AC-6, separation of duties AC-5, authenticator management IA-5…).
- [SP 800-63B](https://pages.nist.gov/800-63-4/sp800-63b.html) : authentication and password management.

## CIS

- [CIS Controls v8, control 5](https://cas.docs.cisecurity.org/en/latest/source/Controls5/): account management.
- [CIS Controls v8, control 6](https://cas.docs.cisecurity.org/en/latest/source/Controls6/): access control management.

## MITRE ATT&CK

| Technique | Used for |
|---|---|
| [T1558.003 Kerberoasting](https://attack.mitre.org/techniques/T1558/003/) | accounts with an SPN |
| [T1558.004 AS-REP Roasting](https://attack.mitre.org/techniques/T1558/004/) | accounts without preauthentication |
| [T1558.001 Golden Ticket](https://attack.mitre.org/techniques/T1558/001/) | krbtgt password |
| [T1558 Steal or Forge Kerberos Tickets](https://attack.mitre.org/techniques/T1558/) | Kerberos delegation |
| [T1550.003 Pass the Ticket](https://attack.mitre.org/techniques/T1550/003/) | delegable privileged accounts |
| [T1484.001 Group Policy Modification](https://attack.mitre.org/techniques/T1484/001/) | GPO |
| [T1222.001 Windows Permissions Modification](https://attack.mitre.org/techniques/T1222/001/) | owners and permissions |
| [T1098 Account Manipulation](https://attack.mitre.org/techniques/T1098/) | groups and accounts |
| [T1078.002 Domain Accounts](https://attack.mitre.org/techniques/T1078/002/) | dormant accounts, passwords not required |

## Microsoft

- [Enterprise Access Model](https://learn.microsoft.com/en-us/security/privileged-access-workstations/privileged-access-access-model): the tiered access model.
- [Protected accounts and groups (AdminSDHolder)](https://learn.microsoft.com/en-us/windows-server/identity/ad-ds/plan/security-best-practices/appendix-c--protected-accounts-and-groups-in-active-directory)
- [Privileged accounts and groups](https://learn.microsoft.com/en-us/windows-server/identity/ad-ds/plan/security-best-practices/appendix-b--privileged-accounts-and-groups-in-active-directory)
- [Protected Users group](https://learn.microsoft.com/en-us/windows-server/security/credentials-protection-and-management/protected-users-security-group)
- [Kerberos constrained delegation](https://learn.microsoft.com/en-us/windows-server/security/kerberos/kerberos-constrained-delegation-overview)
- [Resetting the krbtgt password](https://learn.microsoft.com/en-us/windows-server/identity/ad-ds/manage/forest-recovery-guide/ad-forest-recovery-reset-the-krbtgt-password)
- [Least-privilege administrative models](https://learn.microsoft.com/en-us/windows-server/identity/ad-ds/plan/security-best-practices/implementing-least-privilege-administrative-models)
- [Group managed service accounts (gMSA)](https://learn.microsoft.com/en-us/windows-server/security/group-managed-service-accounts/group-managed-service-accounts-overview)

!!! note
    SMAD-X **estimates** an ANSSI-style level: it is neither ANSSI's ADS tool nor an official audit.
