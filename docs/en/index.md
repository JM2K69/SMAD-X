---
title: Home
---

# SMAD-X

**Simulate, Model and Audit Active Directory eXpert**: an Active Directory simulator built for **learning**. Build a domain that matches a real installation, audit it against **ANSSI**, **NIST**, **CIS** and **MITRE ATT&CK** recommendations, find out **who can take control of the domain**, and practise on **automatically graded labs**.

No infrastructure needed: everything happens in the application, on Windows, Linux or macOS.

[Download SMAD-X](installation.md){ .md-button .md-button--primary }
[Get started](getting-started.md){ .md-button }

![The SMAD-X main window](assets/screens/main.png)

## What you can do

<div class="grid cards" markdown>

-   :material-file-tree: **Model a domain**

    ---

    A structure identical to a freshly installed AD (Builtin, Users, Domain Controllers, System…), then your OUs, accounts, groups, GPOs, PSOs, delegations and sites. Every object has a teaching sheet.

    [:octicons-arrow-right-24: Model](modeling.md)

-   :material-shield-search: **Audit**

    ---

    35 security rules, each linked to its sources: the ANSSI AD checklist, NIST SP 800-53 and 800-63B, CIS Controls, MITRE ATT&CK, Microsoft. A score and an estimated ANSSI level.

    [:octicons-arrow-right-24: Security audit](audit.md)

-   :material-sword-cross: **Find attack paths**

    ---

    Who can take control of the domain, and through which steps: Kerberoasting, delegations, owners, GPOs, Kerberos delegation… The path is highlighted in the graph.

    [:octicons-arrow-right-24: Attack paths](attack-paths.md)

-   :material-school: **Practise**

    ---

    Realistic labs in English and French, graded by the audit and attack paths, with hints and a report. And a guide to write your own.

    [:octicons-arrow-right-24: Labs](labs/index.md)

</div>

## Who is it for?

- **Students and trainee administrators**: understand AD structure, the tier model and the misconfigurations that lead to compromise, without risking a real domain.
- **Trainers**: set up a situation, hand it out as a lab and let SMAD-X grade it.
- **Administrators**: document an existing AD (import script), produce a report, and spot its weaknesses before an audit.

## At a glance

| | |
|---|---|
| **Platforms** | Windows, Linux, macOS (Intel and Apple Silicon) |
| **Installation** | a single executable, everything included |
| **Languages** | English and French, switched at runtime |
| **Format** | `.smad-x.json` (domain), `.smadx-lab.json` (lab) |
| **Exports** | JSON, Markdown / DOCX / PDF report, PNG image |
| **License** | Apache 2.0, including in paid training |

!!! warning "A simulator, not an administration tool"
    SMAD-X never connects to a real directory. Import goes through a PowerShell script you run yourself, which only reads; SMAD-X produces no script that changes a directory.
