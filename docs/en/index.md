---
title: Home
---

# The SMA-X suite

**Simulate, Model, Audit – eXpert**: three applications to **learn systems security** without risking a real infrastructure. Each simulator reproduces its subject faithfully, explains why an access is granted or denied, and serves as the basis for automatically graded labs, for a whole class.

The name of each simulator adds one letter to the **SMA** root: **D** for the directory (Active Directory), **R** for rights.

[Download](installation.md){ .md-button .md-button--primary }
[Discover SMAD-X](smad-x.md){ .md-button }
[Discover SMAR-X](smar-x/index.md){ .md-button }

!!! success "Engines verified on real systems"
    The simulators are not written "from memory". Their results were compared, one by one, with those of **real Windows Server, Windows 11, Linux kernels and Samba servers**, installed and measured for the purpose. These measurements are frozen in the tests: if an engine drifts from them, the tests fail and no release goes out. The details are below, in [Tested on real systems](#tested-on-real-systems).

## The three tools

<div class="grid cards" markdown>

-   :material-domain: **SMAD-X**

    ---

    The **Active Directory** simulator: a domain identical to a real installation, audited against ANSSI, NIST, CIS and MITRE ATT&CK, its attack paths, the tier model, and automatically graded labs.

    [:octicons-arrow-right-24: SMAD-X](smad-x.md)

-   :material-folder-key: **SMAR-X**

    ---

    The **file access rights** simulator, on Linux and Windows, on the server and through a share (SMB, Samba): who can do what, on what, and why, step by step.

    [:octicons-arrow-right-24: SMAR-X](smar-x/index.md)

-   :material-flask: **SMAD-X Studio**

    ---

    The **trainer's** tool, shared by both simulators: it creates the labs (recommendations, recipes, prepared domain or servers), writes the subject and the trainer sheet, and grades the copies of a whole class.

    [:octicons-arrow-right-24: SMAD-X Studio](labs/studio.md)

</div>

## How they fit together

1. The trainer prepares a situation in **SMAD-X** (a domain) or in **SMAR-X** (servers), or starts from a **Studio** recipe.
2. **Studio** turns it into a lab: the subject to hand out, the solution and the trainer sheet.
3. The learner does the lab in **SMAD-X** or **SMAR-X**, checks their work (outside exams) and hands in their **copy**.
4. **Studio** grades the class's copies: grades, success per objective, Moodle exports and PDF reports.

## Tested on real systems

Each engine was confronted with reality: the situation is reproduced on a real system, what it grants or denies is measured, and the simulator must give exactly the same answer.

| Tool | Real system | What is compared |
|---|---|---|
| SMAD-X | freshly promoted **Windows Server 2019, 2022 and 2025** domains | the fresh domain, object by object and permission by permission, and the audit of both (see [A fresh domain, like a real one](audit.md#a-fresh-domain-like-a-real-one)) |
| SMAD-X | a Windows Server 2025 domain hardened by **HardenAD** 2.9.9 | the applied model: OUs, groups, GPOs and their settings, memberships, delegations |
| SMAR-X | **Linux kernel** 6.19, Debian 13 | 925 accesses and 140 file creations, Unix permissions and POSIX ACLs |
| SMAR-X | **Samba** 4.22 | 20 connections, 160 operations and 72 creations through the shares |
| SMAR-X | **Windows Server 2025**, domain controller | 1,512 opens under each account's token, owners, the real order of the ACLs |
| SMAR-X | Windows Server 2025, from a domain-joined **Windows 11** | 1,540 opens through the shares and 50 folders listed |
| SMAR-X | the **Windows 11 Explorer** | the Security tab: 96 account views, 103 entries of the Advanced window, 8 sequences of ticked boxes |
| SMAR-X | **Windows 11** (PowerShell 5.1, NTFSSecurity module) and **Debian 13** | the 50 commands of the [cheat sheet](smar-x/index.md#the-cheat-sheet), replayed as they are written; each NTFSSecurity command leaves the same ACEs as its `icacls` equivalent |

The SMAR-X details, and what these measurements taught, are in [Faithful to real systems](smar-x/index.md#faithful-to-real-systems) and [Verified Windows rules](smar-x/windows-rules.md). What is not simulated is stated plainly: see the [SMAR-X limits](smar-x/limits.md).

## Who is it for?

- **Students and trainee administrators**: understand an Active Directory, the tier model, Linux and NTFS permissions, shares, and the misconfigurations that lead to compromise.
- **Trainers**: prepare a situation, hand it out as a lab and let Studio grade the copies.
- **Administrators**: document an existing AD (SMAD-X import script), produce a report, and spot its weaknesses before an audit.

## At a glance

| | |
|---|---|
| **Platforms** | Windows, Linux, macOS (Intel and Apple Silicon) |
| **Installation** | one archive per application, a single executable each, everything included |
| **Languages** | French and English, switched live |
| **Formats** | `.smad-x.json` (domain), `.smar-x.json` (servers), `.smadx-lab.json` and `.smarx-lab.json` (labs) |
| **License** | Apache 2.0, including in paid training |

!!! warning "Simulators, not administration tools"
    No application of the suite ever connects to a real directory or server. Importing a domain into SMAD-X goes through a PowerShell script that you run yourself and that only reads. The commands SMAR-X shows (`icacls`, `chmod`, `setfacl`, PowerShell) are there to be tried on a lab, by you.
