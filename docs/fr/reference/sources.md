# Sources

Les règles d'audit, les fiches et les chemins d'attaque s'appuient sur ces références. Chaque règle cite les siennes : voir [Règles d'audit](regles-audit.md).

## Les fiches des sources

Chaque élément cité par une règle a sa fiche dans le dépôt, en français et en anglais : [`sources/`](https://github.com/JM2K69/SMAD-X/tree/master/sources). Il y a un dossier par référentiel (`anssi`, `nist`, `cis`, `mitre-attack`, `microsoft`, `tier-models`), avec dedans un dossier par langue (`fr`, `en`). Chaque élément y a sa fiche, nommée d'après l'identifiant que SMAD-X affiche : `anssi/fr/vuln_spn_priv.md`, `mitre-attack/fr/T1558.003.md`, `nist/fr/AC-6(5).md`, `cis/fr/5.4.md`. Pour une page Microsoft Learn, c'est le nom de la page. Le `README.md` de chaque dossier en est le sommaire.

Une fiche donne :

- l'élément : son référentiel, son identifiant, ses niveaux ANSSI, ses dates (ajout à la checklist, dernière modification, version) et son lien ;
- les règles de SMAD-X qui le citent ;
- le texte officiel, quand la licence permet de le reprendre ;
- la source et la licence : la page relevée, la date du relevé, la mention à reproduire.

| Référentiel | Ce que la fiche reprend |
|---|---|
| ANSSI | le texte et la recommandation officiels, en français et en anglais (licence ouverte Etalab 2.0) |
| NIST | l'énoncé du contrôle, en anglais (domaine public) |
| MITRE ATT&CK | le premier paragraphe de la description, en anglais, avec la mention de MITRE |
| CIS | l'identifiant et le titre seulement : la licence (CC BY-NC-ND 4.0) n'en permet pas plus |
| Microsoft | le titre et la description de la page, avec son lien |
| Modèles de cloisonnement | la version reproduite, son commit et sa licence (MIT) |

Un texte qui n'existe qu'en anglais reste en anglais dans la fiche française, qui le signale : c'est la version qui fait foi.

Un point ANSSI peut avoir plusieurs niveaux, chacun avec son seuil. `vuln_privileged_members_perm` est de niveau 1 pour les permissions exploitables sans prérequis, et de niveau 2 pour toutes les permissions dangereuses. Le niveau d'une règle de SMAD-X est l'un d'eux. Un test vérifie que chaque référence citée par une règle a sa fiche dans les deux langues, et que le niveau ANSSI de chaque règle est l'un de ceux de la checklist.

Dans SMAD-X Studio, **Fichier > Fiches des sources...** montre ces fiches règle par règle, les exporte en PDF et relit une source sur le web, sans rien installer d'autre : voir [Les fiches des sources](../exercices/studio.md#les-fiches-des-sources). Studio n'a pas besoin des fichiers Markdown du dépôt. Il embarque le relevé, `sources/sources.json`, et met en page chaque fiche à partir de lui, à l'écran comme en PDF, avec le même code que celui qui écrit les fiches du dépôt.

### Le PDF d'une fiche

Chaque fiche a son PDF aux couleurs de SMAD-X, rangé à côté d'elle sous le même nom (`anssi/fr/vuln_spn_priv.pdf`). On peut ainsi donner ou imprimer la seule recommandation qui sert. Le PDF dit d'où il vient : SMAD-X et sa version, la suite SMA-X et la fiche sur GitHub.

Chaque page porte un bandeau bleu nuit, avec le logo, le référentiel et la date du relevé. Le pied de page donne la provenance et le numéro de page. La fiche contient, dans l'ordre :

- les pastilles du référentiel et du niveau ANSSI, le titre officiel et l'identifiant ;
- le tableau d'identité ;
- les règles de SMAD-X qui citent la source, chacune à la couleur de sa gravité ;
- le texte officiel ;
- la recommandation officielle, dans un encadré vert ;
- la source et la licence, dans un cartouche gris.

Les PDF ne sont pas versionnés. On les génère depuis la racine du dépôt :

```bash
dotnet run --project tools/SmadxSources -- pdf
dotnet run --project tools/SmadxSources -- pdf --only vuln_spn_priv
dotnet run --project tools/SmadxSources -- pdf --only anssi
```

`--only` limite la génération à un identifiant ou à un référentiel. Un PDF ouvert dans une visionneuse ne peut pas être remplacé. L'outil le laisse tel quel, le signale et passe au suivant : il suffit de le fermer et de relancer.

### Mettre les fiches à jour

```bash
dotnet run --project tools/SmadxSources -- update
```

L'outil relit chaque source sur le web. Il réécrit les fiches et `sources/sources.json`, puis affiche ce qui a changé depuis le relevé précédent : un titre, un niveau, un texte. Si un élément cité par SMAD-X a disparu de sa source, il s'arrête sans rien écrire. La fiche d'un élément que SMAD-X ne cite plus est supprimée, avec son PDF. Une nouvelle référence dans une règle demande aussi un `update`.

`-- write` réécrit les fiches depuis `sources/sources.json`, sans rien relire sur le web. Il sert par exemple après le changement du titre d'une règle.

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
| [T1003.006 DCSync](https://attack.mitre.org/techniques/T1003/006/) | permissions dangereuses sur le domaine |
| [T1003.001 LSASS Memory](https://attack.mitre.org/techniques/T1003/001/) | comptes privilégiés connectés sur une machine moins sensible, silos d'authentification |
| [T1550.002 Pass the Hash](https://attack.mitre.org/techniques/T1550/002/) | ordinateurs sans LAPS |
| [T1078.003 Local Accounts](https://attack.mitre.org/techniques/T1078/003/) | ordinateurs sans LAPS |
| [T1136.002 Domain Account](https://attack.mitre.org/techniques/T1136/002/) | ordinateurs joints au domaine par tout utilisateur |

## Microsoft

- [Enterprise Access Model](https://learn.microsoft.com/en-us/security/privileged-access-workstations/privileged-access-access-model) : le modèle d'accès en tiers.
- [Comptes et groupes protégés (AdminSDHolder)](https://learn.microsoft.com/en-us/windows-server/identity/ad-ds/plan/security-best-practices/appendix-c--protected-accounts-and-groups-in-active-directory)
- [Comptes et groupes privilégiés](https://learn.microsoft.com/en-us/windows-server/identity/ad-ds/plan/security-best-practices/appendix-b--privileged-accounts-and-groups-in-active-directory)
- [Groupe Protected Users](https://learn.microsoft.com/en-us/windows-server/security/credentials-protection-and-management/protected-users-security-group)
- [Délégation Kerberos contrainte](https://learn.microsoft.com/en-us/windows-server/security/kerberos/kerberos-constrained-delegation-overview)
- [Réinitialiser le mot de passe de krbtgt](https://learn.microsoft.com/en-us/windows-server/identity/ad-ds/manage/forest-recovery-guide/ad-forest-recovery-reset-the-krbtgt-password)
- [Modèles d'administration au moindre privilège](https://learn.microsoft.com/en-us/windows-server/identity/ad-ds/plan/security-best-practices/implementing-least-privilege-administrative-models)
- [Comptes de service gérés de groupe (gMSA)](https://learn.microsoft.com/en-us/windows-server/security/group-managed-service-accounts/group-managed-service-accounts-overview)
- [Windows LAPS](https://learn.microsoft.com/en-us/windows-server/identity/laps/laps-overview)
- [Stratégies et silos d'authentification](https://learn.microsoft.com/en-us/windows-server/security/credentials-protection-and-management/authentication-policies-and-authentication-policy-silos)
- [L'attribut ms-DS-MachineAccountQuota](https://learn.microsoft.com/en-us/windows/win32/adschema/a-ms-ds-machineaccountquota)

## Modèles de cloisonnement

Les deux modèles que SMAD-X applique (voir [Modèles de cloisonnement](../cloisonnement.md)), reproduits depuis une version publiée, sous licence MIT :

- [Microsoft Active Directory Tier Model](https://github.com/microsoft/ActiveDirectoryTierModel) v2.1.0, Copyright (c) Microsoft Corporation : règles TIER-05 à TIER-08.
- [HardenAD](https://github.com/LoicVeirman/HardenAD) 2.9.9, Copyright (c) 2021 HardenAD.

!!! note
    SMAD-X **estime** un niveau à la manière de l'ANSSI : ce n'est ni l'outil ADS de l'ANSSI, ni un audit officiel.
