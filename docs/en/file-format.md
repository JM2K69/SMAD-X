# File format

## The domain: `.smad-x.json`

A readable JSON file that versions well in Git:

```json
{
  "Version": 2,
  "Domain": {
    "Name": "contoso.com",
    "Type": "Domain",
    "DistinguishedName": "DC=contoso,DC=com",
    "Children": [ { "Name": "Users", "Type": "Container", "Children": [ ] } ]
  },
  "SitesTopology": { "Sites": [ ], "SiteLinks": [ ] },
  "TierConfigurations": [ ]
}
```

| Field | Content |
|---|---|
| `Version` | format version, currently **2** |
| `Domain` | the object tree: name, type, DN, tier, Markdown description, memberships, linked GPOs, delegations, security attributes, children |
| `SitesTopology` | sites, subnets, domain controllers and replication links |
| `TierConfigurations` | custom tiers (names, levels, colors), if any |

### Versions

| Version | Structure |
|---|---|
| 1 | the domain object directly at the root of the file, no `Version` field |
| 2 | the document above |

On load, a version 1 file is **converted** automatically, and the status bar says so. A file written by a **newer** version of SMAD-X is refused with a message giving both versions, rather than being half-read.

## The lab: `.smadx-lab.json`

A lab holds a statement, a full starting domain (a `.smad-x.json` document, migrated on load like any file) and objectives. Its structure is described in [Creating a lab](labs/create.md). The `"Exam": true` key, written only when set, puts the lab in exam mode.

Format v3 (SMAD-X 0.10) adds:

- `Questions`, the comprehension questions: `Id`, `Title`, `Hint`, `Kind` (`SingleChoice`, `MultipleChoice`, `TrueFalse`, `Matching`, `Text`), `Points`, `Choices` (`Id`, `Text`), `Prompts` for a matching question (`Id`, `Text`), `Partial`;
- the answer key, which only exists in the trainer's copy: `Correct` on the right choices, `Match` on each matching item (the `Id` of its proposal), `AcceptedAnswers` for a written answer;
- `Partial` on an objective: partial credit.

The **subject** is the same file without the answer key and, for an exam, without the hints; a matching question's proposals are shuffled in it. An exam that still holds hints or an answer key is the trainer's copy: SMAD-X refuses to open it.

## The submission: `.smadx-submission.json`

A learner's submission (`"Format": "smadx-submission"`) holds their name (`Student`), the lab id (`LabId`), the date the starting domain was prepared (`StartedOn`), the date of the submission (`SavedAt`), the objectives whose hint was viewed (`HintsUsed`), the lab received (`Lab`) and the learner's domain (`Work`, a `.smad-x.json` document). It holds no grade: SMAD-X Studio grades it with the trainer's copy of the lab.

`ElapsedMinutes` gives the time spent on the lab, resumed sessions included; `EngineVersion` and `RulesVersion`, the SMAD-X version that wrote the submission and the fingerprint of its audit rules: Studio warns when they differ from its own. These three fields exist since format v2 (SMAD-X 0.9).

Format v3 (SMAD-X 0.10) adds `Answers`, the answers to the questions by question id (`Choices`, the ticked choices; `Matches`, each item and the proposal chosen; or `Text`), and `StudentId`, the learner's optional ID.

`SessionId` identifies the work session, drawn at random when the lab opens: the identifiers (`Id`) of the objects in the working domain are derived from it. `Seal` is the seal: the SHA-256, in hexadecimal, of the submission without the `Seal` key, written as compact JSON. It does not depend on the layout, only on content: the slightest change breaks it.

## The trainer's files

| File | Content |
|---|---|
| `1-trainer/fixed-domain.smad-x.json` | the fixed domain of a lab created from recommendations: a `.smad-x.json` document, with the dates of the starting domain (`<id>.smadx-lab.solution.smad-x.json` for a lab saved on its own) |
| `1-trainer/trainer-notes.md` | the trainer notes (`<id>.smadx-lab.notes.md` for a lab saved on its own) |
| `.smadx-marks.json` | beside a submission (`"Format": "smadx-marks"`): `LabId` and `SessionId` of the graded submission, `Questions` (per question, the `Points` given by hand and the answer graded, `Answer`) and the comment, `Comment` |

## The PowerShell import script

**File > Generate AD Import Script...** produces a script that reads an AD and writes a `.smad-x.json`. SMAD-X produces no script that changes a directory. See [Import and export](export.md).
