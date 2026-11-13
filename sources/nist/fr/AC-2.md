# SP 800-53 Rev. 5 AC-2 — Account Management

> [Sommaire : NIST — SP 800-53 Rev. 5 et SP 800-63B](README.md) · English: [AC-2.md](../en/AC-2.md)

| Fiche | |
|:---|:---|
| Référentiel | NIST — SP 800-53 Rev. 5 et SP 800-63B |
| Identifiant | `SP 800-53 Rev. 5 AC-2` |
| Version | 5.2.0 |
| Lien | [csrc.nist.gov/…](https://csrc.nist.gov/pubs/sp/800/53/r5/upd1/final) |
| Cité par SMAD-X | PWD-06 · ACC-03 · ACC-01 · ACC-02 · PRIV-02 · TIER-07 · GRP-01 |

## Règles de SMAD-X qui le citent

- **PWD-06** — Compte d'ordinateur dont le mot de passe ne change plus
- **ACC-03** — Tout utilisateur peut joindre des ordinateurs au domaine
- **ACC-01** — Compte Invité actif
- **ACC-02** — Compte dormant
- **PRIV-02** — adminCount orphelin
- **TIER-07** — Objet en attente dans une OU de transit
- **GRP-01** — Boucle d'imbrication de groupes

## Texte officiel (en anglais : c'est la version qui fait foi)

- a. Define and document the types of accounts allowed and specifically prohibited for use within the system;
- b. Assign account managers;
- c. Require [Assignment: prerequisites and criteria] for group and role membership;
- d. Specify:
  - 1. Authorized users of the system;
  - 2. Group and role membership; and
  - 3. Access authorizations (i.e., privileges) and [Assignment: attributes (as required)] for each account;
- e. Require approvals by [Assignment: personnel or roles] for requests to create accounts;
- f. Create, enable, modify, disable, and remove accounts in accordance with [Assignment: policy, procedures, prerequisites, and criteria];
- g. Monitor the use of accounts;
- h. Notify account managers and [Assignment: personnel or roles] within:
  - 1. [Assignment: time period] when accounts are no longer required;
  - 2. [Assignment: time period] when users are terminated or transferred; and
  - 3. [Assignment: time period] when system usage or need-to-know changes for an individual;
- i. Authorize access to the system based on:
  - 1. A valid access authorization;
  - 2. Intended system usage; and
  - 3. [Assignment: attributes (as required)];
- j. Review accounts for compliance with account management requirements [Assignment: frequency];
- k. Establish and implement a process for changing shared or group account authenticators (if deployed) when individuals are removed from the group; and
- l. Align account management processes with personnel termination and transfer processes.

## Source et licence

- **Source** : [https://csrc.nist.gov/pubs/sp/800/53/r5/upd1/final](https://csrc.nist.gov/pubs/sp/800/53/r5/upd1/final)
- **Publication relevée** : SP 800-53 Rev. 5, OSCAL 5.2.0 (2026-10-22)
- **Relevé le** : 2026-11-13, par `tools/SmadxSources`
- **Licence** : domaine public : publication du gouvernement des États-Unis (17 U.S.C. § 105) ; énoncés officiels repris, en anglais
