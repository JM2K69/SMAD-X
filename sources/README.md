# SMAD-X sources

> Français : [ReadmeFR.md](ReadmeFR.md)

SMAD-X's audit rules, levels and lab recipes rely on these frameworks. Each sheet says where the item comes from, when it was retrieved, under which licence, and which SMAD-X rules cite it. Official texts are only reproduced where the licence allows it.

Retrieved on 2026-11-13, by `tools/SmadxSources`.

| Framework | Source | Retrieved publication | Sheets | Licence |
|---|---|---|---|---|
| [ANSSI — Active Directory control points](anssi/en/README.md) | [cert.cyber.gouv.fr/…](https://cert.cyber.gouv.fr/uploads/ad_checklist.html) | 2026-11-12 | 33 | Etalab Open Licence 2.0: free reuse citing the source (CERT-FR / ANSSI) and the date of its update; official texts reproduced in French and English |
| [NIST — SP 800-53 Rev. 5 and SP 800-63B](nist/en/README.md) | [csrc.nist.gov/…](https://csrc.nist.gov/pubs/sp/800/53/r5/upd1/final) | SP 800-53 Rev. 5, OSCAL 5.2.0 (2026-10-22) | 6 | public domain: a United States Government publication (17 U.S.C. § 105); official statements reproduced |
| [CIS Controls v8](cis/en/README.md) | [www.cisecurity.org/…](https://www.cisecurity.org/controls/v8) | 2025-03-19 | 5 | © Center for Internet Security, CC BY-NC-ND 4.0 licence: only the identifier and official title of each safeguard are reproduced, with the link |
| [MITRE ATT&CK® (Enterprise)](mitre-attack/en/README.md) | [attack.mitre.org](https://attack.mitre.org/) | ATT&CK v20.1 | 14 | MITRE ATT&CK terms of use: reproduction allowed with the notice below; first paragraph of each description reproduced |
| [Microsoft Learn](microsoft/en/README.md) | [learn.microsoft.com](https://learn.microsoft.com/) | — | 11 | © Microsoft: only the title and description of each page are reproduced, with the link; the content stays on Microsoft Learn |
| [Reproduced tier models](tier-models/en/README.md) | [github.com/…](https://github.com/JM2K69/SMAD-X/tree/master/SMAD-X.Core/TierModels) | — | 2 | MIT, with each project's copyright; the models are reproduced in SMAD-X.Core/TierModels |

## Layout

One folder per framework (`anssi`, `nist`, `cis`, `mitre-attack`, `microsoft`, `tier-models`), with one folder per language inside (`fr`, `en`). Each cited item has its sheet, named after the identifier SMAD-X shows: `anssi/en/vuln_spn_priv.md`, `mitre-attack/en/T1558.003.md`, `nist/en/AC-6(5).md`, `cis/en/5.4.md`; for a Microsoft Learn page, the page name (`microsoft/en/protected-users-security-group.md`). Each folder's `README.md` lists its sheets.

Once generated, the PDFs sit next to the sheets, under the same name: `anssi/en/vuln_spn_priv.pdf`. You can hand out or print the sheet of a single recommendation.

## Updating

```bash
dotnet run --project tools/SmadxSources -- update
```

The tool reads each source again on the web, rewrites these sheets and `sources/sources.json`, and shows what changed since the previous retrieval (title, level, text). If an item cited by SMAD-X has disappeared from its source, it stops without writing anything. Review the diff before committing.

```bash
dotnet run --project tools/SmadxSources -- write
```

Rewrites the sheets from `sources/sources.json`, without reading anything on the web: after a rule's title changes, for instance. A new reference in a rule needs an `update`.

## The PDFs

```bash
dotnet run --project tools/SmadxSources -- pdf
dotnet run --project tools/SmadxSources -- pdf --only vuln_spn_priv
```

One PDF per sheet and language, in SMAD-X colours, next to the sheet. `--only` generates only the sheets of one identifier (`vuln_spn_priv`) or one framework (`anssi`). The PDFs are not versioned: they are generated again from the sheets.

## In SMAD-X Studio

Studio ships this retrieval: **File > Source sheets...** shows the sheets rule by rule, exports them as PDF and reads a source again on the web to see what changed since the installed version, without the repository or the .NET SDK. The code is the same as this tool's (`SMAD-X.Core/Services/Sources`).
