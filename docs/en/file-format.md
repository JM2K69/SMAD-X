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

A lab holds a statement, a full starting domain (a `.smad-x.json` document, migrated on load like any file) and objectives. Its structure is described in [Creating a lab](labs/create.md).

## PowerShell scripts

| Script | Produced by | Purpose |
|---|---|---|
| Export | **File > Export to PowerShell...** | recreates the structure in an AD |
| Import | **File > Generate AD Import Script...** | reads an AD and writes a `.smad-x.json` |

See [Import and export](export.md).
