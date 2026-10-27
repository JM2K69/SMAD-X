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

## The submission: `.smadx-submission.json`

A learner's submission (`"Format": "smadx-submission"`) holds their name (`Student`), the lab id (`LabId`), the date the starting domain was prepared (`StartedOn`), the date of the submission (`SavedAt`), the objectives whose hint was viewed (`HintsUsed`), the lab received (`Lab`) and the learner's domain (`Work`, a `.smad-x.json` document). It holds no grade: SMAD-X Studio grades it with the trainer's copy of the lab.

`ElapsedMinutes` gives the time spent on the lab, resumed sessions included; `EngineVersion` and `RulesVersion`, the SMAD-X version that wrote the submission and the fingerprint of its audit rules: Studio warns when they differ from its own. These three fields exist since format v2 (SMAD-X 0.9).

`SessionId` identifies the work session, drawn at random when the lab opens: the identifiers (`Id`) of the objects in the working domain are derived from it. `Seal` is the seal: the SHA-256, in hexadecimal, of the submission without the `Seal` key, written as compact JSON. It does not depend on the layout, only on content: the slightest change breaks it.

## The PowerShell import script

**File > Generate AD Import Script...** produces a script that reads an AD and writes a `.smad-x.json`. SMAD-X produces no script that changes a directory. See [Import and export](export.md).
