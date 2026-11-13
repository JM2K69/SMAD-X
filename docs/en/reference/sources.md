# Sources

Audit rules, sheets and attack paths rely on these references. Each rule cites its own: see [Audit rules](audit-rules.md).

## The source sheets

Each item cited by a rule has its sheet in the repository, in French and in English: [`sources/`](https://github.com/JM2K69/SMAD-X/tree/master/sources). There is one folder per framework (`anssi`, `nist`, `cis`, `mitre-attack`, `microsoft`, `tier-models`), with one folder per language inside (`fr`, `en`). Each item has its sheet there, named after the identifier SMAD-X shows: `anssi/en/vuln_spn_priv.md`, `mitre-attack/en/T1558.003.md`, `nist/en/AC-6(5).md`, `cis/en/5.4.md`. For a Microsoft Learn page, it is the page name. Each folder's `README.md` lists its sheets.

A sheet gives:

- the item: its framework, identifier, ANSSI levels, dates (added to the checklist, last change, version) and link;
- the SMAD-X rules that cite it;
- the official text, where the licence allows reproducing it;
- the source and licence: the page retrieved, the retrieval date, the notice to reproduce.

| Framework | What the sheet reproduces |
|---|---|
| ANSSI | the official text and recommendation, boxes included, in French and in English (Etalab 2.0 open licence) |
| NIST | the control statement, in English (public domain) |
| MITRE ATT&CK | the first paragraph of the description, in English, with MITRE's notice |
| CIS | the identifier and title only: the licence (CC BY-NC-ND 4.0) allows no more |
| Microsoft | the page title and description, with its link |
| Tiering models | the reproduced version, its commit and its licence (MIT) |

A text that only exists in English stays in English in the French sheet, which says so: it is the authoritative version.

The boxes of the ANSSI checklist ("Context: privileged groups", "Alert threshold", "Context: control path exposure", "Limits"…) are reproduced in the sheet language. They are quotes in the Markdown, and blue boxes in the PDF and in Studio. A group given by its SID gets its name, as on the page (*Administrators*, *Guest*). A dangerous right keeps its name (`WRITE_SPN`), and an access mask becomes the list of rights it grants. A command example becomes a code block.

The sheets reproduce the text as the checklist shows it. What only a domain's analysis report shows is left out:

- the computed numbers, which remain "…";
- the tables of objects found;
- the link to the graph tab.

The text of the checklist's vulnerability points was compared, line by line and in both languages, with what the site shows.

An ANSSI point can have several levels, each with its threshold. `vuln_privileged_members_perm` is level 1 for permissions exploitable without prerequisites, and level 2 for all dangerous permissions. A SMAD-X rule's level is one of them. A test checks that each reference cited by a rule has its sheet in both languages, and that each rule's ANSSI level is one of the checklist's.

In SMAD-X Studio, **File > Source sheets...** shows these sheets rule by rule, exports them as PDF and reads a source again on the web, with nothing else to install: see [The source sheets](../labs/studio.md#the-source-sheets). Studio does not need the repository's Markdown files. It ships the retrieval, `sources/sources.json`, and lays out each sheet from it, on screen and as PDF, with the same code that writes the repository's sheets.

### A sheet's PDF

Each sheet has its PDF in SMAD-X colours, stored next to it under the same name (`anssi/en/vuln_spn_priv.pdf`). You can hand out or print just the recommendation you need. The PDF says where it comes from: SMAD-X and its version, the SMA-X suite and the sheet on GitHub.

Each page has a navy band with the logo, the framework and the retrieval date. The footer gives the provenance and the page number. The sheet holds, in order:

- the framework and ANSSI level badges, the official title and the identifier;
- the identity table;
- the SMAD-X rules that cite the source, each in its severity colour;
- the official text;
- the official recommendation, in a green box;
- the source and licence, in a grey panel.

The PDFs are not versioned. Generate them from the repository root:

```bash
dotnet run --project tools/SmadxSources -- pdf
dotnet run --project tools/SmadxSources -- pdf --only vuln_spn_priv
dotnet run --project tools/SmadxSources -- pdf --only anssi
```

`--only` limits generation to one identifier or one framework. A PDF open in a viewer cannot be replaced. The tool leaves it as it is, reports it and moves on: close it and run the command again.

### Updating the sheets

```bash
dotnet run --project tools/SmadxSources -- update
```

The tool reads each source again on the web. It rewrites the sheets and `sources/sources.json`, then shows what changed since the previous retrieval: a title, a level, a text. If an item cited by SMAD-X has disappeared from its source, it stops without writing anything. The sheet of an item SMAD-X no longer cites is deleted, with its PDF. A new reference in a rule also needs an `update`.

`-- write` rewrites the sheets from `sources/sources.json`, without reading anything on the web. Use it after a rule's title changes, for instance.

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
| [T1003.006 DCSync](https://attack.mitre.org/techniques/T1003/006/) | dangerous permissions on the domain |
| [T1003.001 LSASS Memory](https://attack.mitre.org/techniques/T1003/001/) | privileged accounts logged on to a less sensitive machine, authentication silos |
| [T1550.002 Pass the Hash](https://attack.mitre.org/techniques/T1550/002/) | computers without LAPS |
| [T1078.003 Local Accounts](https://attack.mitre.org/techniques/T1078/003/) | computers without LAPS |
| [T1136.002 Domain Account](https://attack.mitre.org/techniques/T1136/002/) | computers joined to the domain by any user |

## Microsoft

- [Enterprise Access Model](https://learn.microsoft.com/en-us/security/privileged-access-workstations/privileged-access-access-model): the tiered access model.
- [Protected accounts and groups (AdminSDHolder)](https://learn.microsoft.com/en-us/windows-server/identity/ad-ds/plan/security-best-practices/appendix-c--protected-accounts-and-groups-in-active-directory)
- [Privileged accounts and groups](https://learn.microsoft.com/en-us/windows-server/identity/ad-ds/plan/security-best-practices/appendix-b--privileged-accounts-and-groups-in-active-directory)
- [Protected Users group](https://learn.microsoft.com/en-us/windows-server/security/credentials-protection-and-management/protected-users-security-group)
- [Kerberos constrained delegation](https://learn.microsoft.com/en-us/windows-server/security/kerberos/kerberos-constrained-delegation-overview)
- [Resetting the krbtgt password](https://learn.microsoft.com/en-us/windows-server/identity/ad-ds/manage/forest-recovery-guide/ad-forest-recovery-reset-the-krbtgt-password)
- [Least-privilege administrative models](https://learn.microsoft.com/en-us/windows-server/identity/ad-ds/plan/security-best-practices/implementing-least-privilege-administrative-models)
- [Group managed service accounts (gMSA)](https://learn.microsoft.com/en-us/windows-server/security/group-managed-service-accounts/group-managed-service-accounts-overview)
- [Windows LAPS](https://learn.microsoft.com/en-us/windows-server/identity/laps/laps-overview)
- [Authentication policies and silos](https://learn.microsoft.com/en-us/windows-server/security/credentials-protection-and-management/authentication-policies-and-authentication-policy-silos)
- [The ms-DS-MachineAccountQuota attribute](https://learn.microsoft.com/en-us/windows/win32/adschema/a-ms-ds-machineaccountquota)

## Tiering models

The two models SMAD-X applies (see [Tiering models](../tiering.md)), reproduced from a published version, under the MIT license:

- [Microsoft Active Directory Tier Model](https://github.com/microsoft/ActiveDirectoryTierModel) v2.1.0, Copyright (c) Microsoft Corporation: rules TIER-05 to TIER-08.
- [HardenAD](https://github.com/LoicVeirman/HardenAD) 2.9.9, Copyright (c) 2021 HardenAD.

!!! note
    SMAD-X **estimates** an ANSSI-style level: it is neither ANSSI's ADS tool nor an official audit.
