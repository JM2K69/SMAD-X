# Creating a SMAD-X lab

A lab is a `.smadx-lab.json` file. It holds a statement, a starting domain and objectives that SMAD-X grades automatically, using the security audit, attack paths and a comparison with the starting domain.

This guide goes from the idea to a tested file. The labs shipped with the application (**Labs** menu) were written this way and serve as models.

---

## 1. Pick a method

| Method | For | Result |
|---|---|---|
| **A. From the application** | a teacher, no programming | a file to hand out to learners |
| **B. With a generator** | a repository contributor | a lab embedded in the exe, tested automatically |

Both produce the same format: you can start with A and finish with B.

---

## 2. Method A: from the application

### 2.1 Prepare the starting domain
1. **File > New domain** (or open a saved domain).
2. Build the starting situation: OUs, accounts, groups, delegations, **Security** section options.
3. Add the **weakness(es)** the learner must find. Check that they show:
   - **Analyse > Security audit** (`Ctrl+Shift+A`): the expected finding appears;
   - **Analyse > Attack paths** (`Ctrl+Shift+K`): the expected path appears.
4. Populate the domain like a real AD (departments, users, workstations): the weakness must be searched for, not sitting alone in an empty domain.
5. Save the domain (`.smad-x.json`) so you can come back to it.

### 2.2 Generate the skeleton
**Labs > Create a lab from the current domain...**

SMAD-X writes a `.smadx-lab.json` file with:
- the current domain as the starting domain;
- one "Clear finding …" objective per failed audit rule;
- a statement and a title to replace.

### 2.3 Complete the file
Open the file in a text editor (VS Code understands JSON) and edit:
- `Id`, `Title`, `Level`, `DurationMinutes`;
- `Statement`: the Markdown statement (see § 4);
- `Objectives`: keep the useful ones, add the others (see § 5).

Do not edit `Start` by hand: rebuild the domain in the application and generate the skeleton again.

### 2.4 Test
1. **Labs > Open a lab...** and pick the file.
2. Click **Check** without changing anything: no main objective may be met, and the guard objectives (see § 5.2) must already be met.
3. Apply the expected fix, then **Check**: everything must be met.
4. **Start over**, then try the wrong solutions (delete the account, empty the group…): an objective must fail every time.
5. **Export result** produces the Markdown report the learner can hand in.

---

## 3. The `.smadx-lab.json` format

```json
{
  "Format": "smadx-lab",
  "FormatVersion": 1,
  "Id": "my-lab",
  "Title": { "en-US": "Contoso: the access review" },
  "Language": "en-US",
  "Category": "attack-paths",
  "Level": "Intermediate",
  "DurationMinutes": 25,
  "Statement": { "en-US": "# Title\n\n## Context\n..." },
  "ReferenceDate": "2026-10-22T00:00:00",
  "Start": { "Version": 2, "Domain": { "...": "..." }, "SitesTopology": { "...": "..." } },
  "Objectives": [
    {
      "Id": "o1",
      "Points": 4,
      "Title": { "en-US": "The path from \"GG-Team\" is cut" },
      "Hint":  { "en-US": "Look at which groups GG-Team belongs to." },
      "Check": { "Type": "NoAttackPath", "Object": "GG-Team" }
    }
  ]
}
```

| Field | Purpose |
|---|---|
| `Format`, `FormatVersion` | Always `"smadx-lab"` and `1`. A file from a newer version is refused with a clear message. |
| `Id` | Stable identifier, no spaces. Also used for the report file name. |
| `Title`, `Statement` | Texts per language (`fr-FR`, `en-US`). When the interface language is missing, SMAD-X falls back to French, then to the first text. |
| `Language` | Optional. Set it when the **domain** is written in one language only (OU and account names…): the menu then only offers the lab in that language. Leave it empty for a bilingual lab. |
| `Category` | Optional. `"attack-paths"` puts the lab under **Labs > Attack paths**; empty, it goes under **Built-in labs**. |
| `Level` | `Beginner`, `Intermediate` or `Advanced`. |
| `DurationMinutes` | Indicative duration. |
| `ReferenceDate` | Date at which the domain's dates were frozen. When the lab opens, every date is shifted by "today − ReferenceDate": an account that logged on "yesterday" still did, whatever day the lab is taken. |
| `Start` | The starting domain: a full `.smad-x.json` document, migrated on load like any file. |
| `Objectives` | The graded objectives (§ 5). The point total is computed. |

---

## 4. Writing a good statement

The statement is Markdown: `#` headings, lists, **bold**, `code`, links. Recommended structure:

```markdown
# Organisation: the problem in a few words

## Context
Who the organisation is (business, size), who you are (administrator who joined a month ago,
contractor, auditor), and the event that triggers the work: penetration test, EDR alert,
CERT bulletin, pre-certification audit, handover from a former administrator…

## Your mission
- What must be found and fixed.
- Business constraints: "the business application must keep running", "the team keeps its delegations".

## Your tools
- Analyse > Attack paths, Security audit, Before / after…
```

Rules:
- **Do not give the answer away**: no detailed path and no fix in the statement. These go into the objectives' **hints**, which the learner reveals one at a time.
- **Give realistic constraints**: they are what rules out the brute-force solution ("delete the account") and forces understanding.
- **Do not publish the solutions**: keep them to yourself, or in the repository's tests.
- **A credible, fictional organisation**: use fictional names (Contoso, Wide World Importers…). Never a real organisation.
- **One domain per lab**: change the domain name from one lab to the next.

---

## 5. Writing objectives

Each objective has a unique `Id` (`o1`, `o2`…), `Points`, a `Title`, a `Hint` and a `Check`.

### 5.1 Available checks

| `Type` | Fields used | Met when |
|---|---|---|
| `RuleClear` | `RuleId`, `Object` (optional) | the audit rule reports no finding (on that object if `Object` is set) |
| `NoFindingAtOrAbove` | `Severity` (`Critical`, `High`, `Medium`, `Low`) | no finding of that severity or worse is left |
| `ScoreAtLeast` | `Score` | the audit score is at least `Score` |
| `NoAttackPath` | `Object` (optional) | no path to domain control is left from `Object`; `"any-domain-user"` means "any domain user"; without `Object`, from no source outside Tier 0 |
| `ObjectExists` / `ObjectAbsent` | `Object`, `ObjectType` (optional) | the object exists / no longer exists |
| `MemberOf` / `NotMemberOf` | `Object`, `Group`, `Effective` | the object is / is no longer a member of the group; `Effective: true` counts nesting, `false` direct membership only |
| `HasFlag` / `NotHasFlag` | `Object`, `Flag` (e.g. `DontRequirePreauth`) | the account option is / is no longer set |
| `TierIs` | `Object`, `Tier` | the object is classified in that tier |
| `DelegationExists` | `Trustee`, `Target`, `RightCategory` | the trustee has a direct delegation of that category on the target |
| `DelegationOnly` | `Trustee`, `AllowedCategories` | the trustee has at least one delegation, all in the allowed categories |
| `NotModified` | `Object` | the object has not changed since the start |

Audit rule identifiers (`KRB-01`, `DLG-01`, `PRIV-04`…) are the ones shown in the Audit window; each sheet lists its ANSSI, NIST and MITRE references. `Object` names an object, which must be unique in the lab's domain.

### 5.2 Guard objectives
A good lab combines:
1. **main objectives**: cut the path (`NoAttackPath`), clear the audit rule (`RuleClear`);
2. **guard objectives**, already met at the start, which fail if the learner "cheats":
   - `ObjectExists` on the service account, server or domain controller that must not be deleted;
   - `MemberOf` on a user who must stay in their team;
   - `DelegationExists` on a legitimate delegation (the service desk keeps password reset);
   - `NotModified` on an object that must not be touched.

Also consider the "**no path left outside Tier 0**" objective (`NoAttackPath` without `Object`): it shows that cutting one link is not always enough. When it fails, the grader shows the remaining path step by step.

### 5.3 Points and hints
- 3 to 4 points for the main objective, 2 for "outside Tier 0" and critical rules, 1 for guards.
- One hint per objective, pointing in the right direction without giving the exact action: "Look at which groups X itself belongs to" rather than "Remove X from Domain Admins".

---

## 6. Method B: a lab shipped with the application

Shipped labs live in `SMAD-X/Labs/` and are embedded in the exe (see `SMAD-X.csproj`). They are **generated**, never written by hand:

| Generator | Labs | Command |
|---|---|---|
| `tools/LabGenerator` | general bilingual labs (`01-premier-audit`…) | `dotnet run --project tools/LabGenerator -- SMAD-X/Labs` |
| `tools/SampleGenerator` | the "Attack paths" series, one file per language (`chemin-*`, `path-*`), plus the samples in `Samples/AttackPaths` | `dotnet run --project tools/SampleGenerator -- Samples/AttackPaths SMAD-X/Labs` |

To add an attack-path scenario in `tools/SampleGenerator/Program.cs`:
1. Add a `Scenario("13-my-scenario", "domain-fr.local", "domain-en.local", d => { ... })` call, with a different domain name in each language.
2. Inside, create the objects with `Ou`, `User`, `Group`, `Computer`, `Delegate`. Write names and descriptions with `T("français", "english")`.
3. Describe the lab with `Lab(title, level, minutes, context, mission, objectives...)`, using `CutPath`, `NoPathLeft`, `RuleClear`, `Exists` and `Obj`.
4. Return the sample's title and explanation, with the `## The path` and `## Fix it` sections.
5. Regenerate, then run the tests.

The generator is **deterministic**: identifiers derived from the DN, dates frozen before the reference date, language set per file. Regenerating only changes what really changed. The starting domain is checked: the generator stops if the "healthy" domain, before the weakness is added, reports an audit finding.

### Automated tests
Every shipped lab must have tests (models: `SMAD-X.Tests/LabTests.cs` and `SMAD-X.Tests/AttackPathLabTests.cs`):
- **not solved at start**: main objectives fail, guards pass;
- **the solution gets all points**;
- **every shortcut is refused**: deleting the guarded object, removing the member, removing the legitimate delegation;
- **the statement does not contain the answer** and every objective has a hint.

```bash
dotnet test SMAD-X.slnx
```

---

## 7. Checklist

- [ ] The domain has a name of its own and is populated like a real AD.
- [ ] The weakness shows in the audit or in attack paths.
- [ ] The statement gives a realistic context, a mission and constraints, but not the answer.
- [ ] Every objective has a hint; points reflect importance.
- [ ] Guard objectives rule out brute-force solutions.
- [ ] At start: main objectives fail, guards are met.
- [ ] The expected solution gets all points; every shortcut makes a guard fail.
- [ ] `Language` is set if the domain exists in one language only; `Category` if the lab belongs to a series.
- [ ] For a shipped lab: generated, tested, and `dotnet test` is green.
