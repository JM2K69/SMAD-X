# Tiering models

SMAD-X classifies each object in a tier (see [Modeling](modeling.md#the-tier-model)). It can also apply a **published tiering model** to a domain, as its deployment script would: the OUs of each tier, the administration groups and accounts, the GPOs and their links, the delegations.

## The two models

| Model | What it creates | Specific features |
|---|---|---|
| [Microsoft Active Directory Tier Model](https://github.com/microsoft/ActiveDirectoryTierModel) (v2.1.0) | 31 OUs, 29 groups, 3 service accounts, 131 GPOs, 87 delegations | one OU per tier and role (member servers, accounts, groups, PAWs, service accounts), staging and quarantine OUs, "Account Restrictions" GPOs that deny logon from one tier to another |
| [HardenAD](https://github.com/LoicVeirman/HardenAD) (2.9.9) | 76 OUs, 292 groups, 10 administration accounts, 119 GPOs, 58 delegations | `_Administration`, `Harden_T0`, `Harden_T12` (Tier 1 and Tier 2), `Harden_TL` (Legacy tiers) and `Provisioning` branches; AGDLP groups (`G-S-…` into `L-S-…`); `HAD-LoginRestrictions`, `HAD-LocalAdmins` and `HAD-LocalRDU` GPOs per tier; an apply group and a deny group per GPO |

HardenAD adds two tiers, **Tier 1 Legacy** and **Tier 2 Legacy**, for isolated older systems: SMAD-X adds them to the document's tiers.

## Applying a model

- **When creating a domain**: **File > New Domain...**, **Tiering** list. *None* classifies nothing, *Tier 0, 1 and 2 labels* classifies the built-in objects (how previous versions worked), and each model also creates its structure. The source and license of the chosen model are shown under the list.
- **On the displayed domain**: **Settings > Apply a tiering model...** shows what the model creates, its source and what SMAD-X does not reproduce, then applies it.

Applying a model **adds** what is missing and neither moves nor changes any existing object: an imported domain keeps its OUs and accounts where they are. It can be replayed without creating duplicates, and is undone in one step (`Ctrl+Z`). The domain is seen once SDProp has run: an account or group that became a member of a protected group (HardenAD's Tier 0 managers are members of Enterprise Admins) receives `adminCount` and the permissions of AdminSDHolder.

The applied model and its version are shown in the domain's **Password policy** section.

## Compliance with the model

Once a model is applied, the audit checks that the domain follows it:

| Rule | What it reports |
|---|---|
| **TIER-05** | a model element missing: OU, group, account, GPO, GPO link or delegation |
| **TIER-06** | an account, computer or group outside the model's classified OUs (in Users, Computers, an older OU) |
| **TIER-07** | an object left in a transit OU (Staging, Quarantine, Provisioning) |
| **TIER-08** | a privileged account or group placed in an OU of a tier less sensitive than Tier 0 |

The accounts and groups built into AD (Administrator, Domain Admins…) and the domain controllers are not concerned: no model moves them. Without an applied model, these rules report nothing.

On a domain where a model has just been applied, TIER-06 therefore lists everything left to place: this is the migration work towards the model.

## What SMAD-X does not reproduce

- **GPO settings**: SMAD-X includes their name, role and links. The logon restriction GPOs do not yet prevent the "Session" step of attack paths.
- **The Microsoft model**: its optional components (MSA, gMSA and dMSA, Windows LAPS, authentication silos) are not included. The "*- " prefix of GPO names is removed. Links the model creates disabled stay disabled.
- **HardenAD**: every task of its sequence is included, as if all were enabled (they are disabled by default). Administration accounts keep the names of the source (`T0M-AHARDEN`…). Redirecting the default containers to Provisioning is not modelled. PAWs shared by several tiers (`PawAccess`, `PawT12L`) and their logon groups have no tier.

## Fidelity

In the SMAD-X tests, the HardenAD model is compared with a real domain hardened by HardenAD and exported by the import script: the 76 OUs, the 292 groups (names, locations, descriptions) and the 119 GPOs are identical. The names of the GPO filtering groups follow HardenAD's rule, abbreviations included (`L-S-T0-GPO_HAD-LocAdmins-T1_APPLY`).

## Sources and licenses

SMAD-X reproduces the **structure** of these models (names, locations, links and rights), not their code. Each model cites its source in SMAD-X:

| Model | Repository | Version | License | Copyright |
|---|---|---|---|---|
| Microsoft Active Directory Tier Model | [microsoft/ActiveDirectoryTierModel](https://github.com/microsoft/ActiveDirectoryTierModel) | v2.1.0 | MIT | Copyright (c) Microsoft Corporation |
| HardenAD | [LoicVeirman/HardenAD](https://github.com/LoicVeirman/HardenAD) | 2.9.9 | MIT | Copyright (c) 2021 HardenAD |

The license texts are in the `THIRD-PARTY-NOTICES.md` file of the SMAD-X repository.

## Updating a model

The models are data files (`SMAD-X.Core/TierModels/microsoft.json`, `hardenad.json`), produced from the official source by the `tools/TierModelGenerator` tool, for a published version:

```bash
dotnet run --project tools/TierModelGenerator -- microsoft v2.1.0 SMAD-X.Core/TierModels
dotnet run --project tools/TierModelGenerator -- hardenad HardenAD-2.9.9v2 SMAD-X.Core/TierModels
```

The tool downloads the source files at the version's commit (JSON configuration for Microsoft, XML task sequence for HardenAD), translates the rights of each delegation into SMAD-X categories and reports those it does not recognise. When a model evolves: run the tool again with the new version, review the diff and this report, then run the tests (consistency of each model, no attack path outside Tier 0 on a fresh domain, fidelity to a real domain).
