# Sources

Les règles d'audit, les fiches et les chemins d'attaque s'appuient sur ces références. Chaque règle cite les siennes : voir [Règles d'audit](regles-audit.md).

## ANSSI

- [Points de contrôle Active Directory](https://cert.cyber.gouv.fr/uploads/ad_checklist.html) : la checklist du CERT-FR, dont SMAD-X reprend les identifiants (`vuln_spn_priv`, `vuln_dnsadmins`…) et les niveaux officiels.
- [Recommandations pour l'administration sécurisée des SI reposant sur AD (ANSSI-PA-099)](https://messervices.cyber.gouv.fr/guides/recommandations-pour-ladministration-securisee-des-si-reposant-sur-ad) : le cloisonnement en tiers.

## NIST

- [SP 800-53 Rev. 5](https://csrc.nist.gov/pubs/sp/800/53/r5/upd1/final) : contrôles de sécurité (moindre privilège AC-6, séparation des tâches AC-5, gestion des authentifiants IA-5…).
- [SP 800-63B](https://pages.nist.gov/800-63-4/sp800-63b.html) : authentification et gestion des mots de passe.

## CIS

- [CIS Controls v8, contrôle 5](https://cas.docs.cisecurity.org/en/latest/source/Controls5/) : gestion des comptes.
- [CIS Controls v8, contrôle 6](https://cas.docs.cisecurity.org/en/latest/source/Controls6/) : gestion des accès.

## MITRE ATT&CK

| Technique | Utilisée pour |
|---|---|
| [T1558.003 Kerberoasting](https://attack.mitre.org/techniques/T1558/003/) | comptes avec SPN |
| [T1558.004 AS-REP Roasting](https://attack.mitre.org/techniques/T1558/004/) | comptes sans préauthentification |
| [T1558.001 Golden Ticket](https://attack.mitre.org/techniques/T1558/001/) | mot de passe de krbtgt |
| [T1558 Steal or Forge Kerberos Tickets](https://attack.mitre.org/techniques/T1558/) | délégation Kerberos |
| [T1550.003 Pass the Ticket](https://attack.mitre.org/techniques/T1550/003/) | comptes privilégiés délégables |
| [T1484.001 Group Policy Modification](https://attack.mitre.org/techniques/T1484/001/) | GPO |
| [T1222.001 Windows Permissions Modification](https://attack.mitre.org/techniques/T1222/001/) | propriétaires et permissions |
| [T1098 Account Manipulation](https://attack.mitre.org/techniques/T1098/) | groupes et comptes |
| [T1078.002 Domain Accounts](https://attack.mitre.org/techniques/T1078/002/) | comptes dormants, mots de passe non requis |

## Microsoft

- [Enterprise Access Model](https://learn.microsoft.com/en-us/security/privileged-access-workstations/privileged-access-access-model) : le modèle d'accès en tiers.
- [Comptes et groupes protégés (AdminSDHolder)](https://learn.microsoft.com/en-us/windows-server/identity/ad-ds/plan/security-best-practices/appendix-c--protected-accounts-and-groups-in-active-directory)
- [Comptes et groupes privilégiés](https://learn.microsoft.com/en-us/windows-server/identity/ad-ds/plan/security-best-practices/appendix-b--privileged-accounts-and-groups-in-active-directory)
- [Groupe Protected Users](https://learn.microsoft.com/en-us/windows-server/security/credentials-protection-and-management/protected-users-security-group)
- [Délégation Kerberos contrainte](https://learn.microsoft.com/en-us/windows-server/security/kerberos/kerberos-constrained-delegation-overview)
- [Réinitialiser le mot de passe de krbtgt](https://learn.microsoft.com/en-us/windows-server/identity/ad-ds/manage/forest-recovery-guide/ad-forest-recovery-reset-the-krbtgt-password)
- [Modèles d'administration au moindre privilège](https://learn.microsoft.com/en-us/windows-server/identity/ad-ds/plan/security-best-practices/implementing-least-privilege-administrative-models)
- [Comptes de service gérés de groupe (gMSA)](https://learn.microsoft.com/en-us/windows-server/security/group-managed-service-accounts/group-managed-service-accounts-overview)

!!! note
    SMAD-X **estime** un niveau à la manière de l'ANSSI : ce n'est ni l'outil ADS de l'ANSSI, ni un audit officiel.
