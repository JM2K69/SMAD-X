# SMAD-X — Roadmap: Domain Evolution & AD Delegations

> Last updated: 2025-06-01

---

## Part 1 — Domain Timeline / Evolution

### Step 1 — Model `DomainSnapshot`
**File:** `SMAD-X\Models\DomainSnapshot.cs`
- `FilePath` (string) — path to the `.smad-x.json` source file
- `DomainName` (string) — root domain name
- `SnapshotDate` (DateTime) — extracted from the `ModifiedDate` field of the root object
- `Root` (ADObject) — fully deserialized tree

---

### Step 2 — Diff Model `ADChangeItem`
**File:** `SMAD-X\Models\ADChangeItem.cs`
- `ChangeType` : enum `Added | Removed | Modified`
- `ObjectName` (string)
- `DistinguishedName` (string) — stable key for comparison
- `ObjectType` (ADObjectType)
- `ChangedFields` : `List<(string Field, string OldValue, string NewValue)>`
  - Tracked fields: `Tier`, `Description`, `MemberOf`, `LinkedGPOs`

---

### Step 3 — Service `ADDiffService`
**File:** `SMAD-X\Services\ADDiffService.cs`

Main method:
```csharp
public List<ADChangeItem> Compare(ADObject before, ADObject after)
```
- Recursive diff indexed by `DistinguishedName`
- Detects: **additions**, **removals**, **field modifications**

---

### Step 4 — `DomainTimelineViewModel`
**File:** `SMAD-X\ViewModels\DomainTimelineViewModel.cs`
- `ObservableCollection<DomainSnapshot> Snapshots`
- `DomainSnapshot? SnapshotBefore` / `SnapshotAfter` (the two snapshots to compare)
- `ObservableCollection<ADChangeItem> Changes` (diff result)
- `FilterType` : All / Added / Removed / Modified
- `FilterText` (string)

Commands:
- `AddSnapshotCommand` — `OpenFilePicker` multi-select `*.smad-x.json`
- `RemoveSnapshotCommand`
- `CompareCommand` — calls `ADDiffService.Compare`

---

### Step 5 — View `DomainTimelineWindow`
**Files:** `SMAD-X\Views\DomainTimelineWindow.axaml` / `.axaml.cs`

Layout:
- **Left panel**: list of loaded snapshots (date + domain name), Add / Remove buttons
- **Selectors**: "Before" / "After" ComboBoxes fed by `Snapshots`
- **Right panel**: `DataGrid` with columns:
  - Change type (colored icon: green = Added, red = Removed, orange = Modified)
  - Object, DN, AD Type, Modified fields

---

### Step 6 — Main menu integration
**Files:** `MainWindow.axaml` + `MainWindowViewModel.cs`
- Menu: `Analyse > Domain Evolution` — shortcut `Ctrl+T`
- Command: `ShowTimelineCommand`

---

## Part 2 — Active Directory Delegations

Delegations in Active Directory express **who** (trustee: user or group) can perform **what action** (right)
on **which scope** (OU or Container). This includes:

- **Password reset**: a group (e.g. `Helpdesk_L1`) can reset passwords for all user accounts in a given OU.
- **Computer account creation**: a group can create/delete computer objects in a specific OU.
- **Account unlock**: a group can unlock user accounts.
- **Attribute write**: a group can modify specific attributes (e.g. `telephoneNumber`, `member`).
- **Generic control**: full control over objects in an OU (e.g. `GenericAll`, `GenericWrite`).

These delegations are stored in the ACL (`Get-Acl`) of each OU/Container and must be captured
during the PowerShell AD analysis, then visualized in a dedicated view.

---

### Step 7 — Model `ADDelegation`
**File:** `SMAD-X\Models\ADDelegation.cs`
- `TrusteeName` (string) — SAMAccountName of the trustee (user or group)
- `TrusteeType` : enum `User | Group | Computer`
- `TargetDN` (string) — DN of the OU/Container where the delegation is defined
- `Right` (string) — e.g. `ResetPassword`, `CreateChild:computer`, `GenericAll`, `WriteProperty:member`
- `RightCategory` : enum `PasswordReset | ComputerManagement | AccountUnlock | AttributeWrite | FullControl | Other`
- `IsInherited` (bool) — whether the ACE is inherited from a parent OU
- `Tier` (string?) — inherited from the target object context

---

### Step 8 — Add `Delegations` to `ADObject`
**File:** `SMAD-X\Models\ADObject.cs`

Add the property:
```csharp
public ObservableCollection<ADDelegation> Delegations { get; set; } = new();
```

---

### Step 9 — PowerShell export of delegations
**File:** `SMAD-X\Services\ADImportPowerShellService.cs`

In the `Build-NodeJson` function of the generated script:
- For OUs and Containers, call `Get-Acl` on the AD object
- Extract `ActiveDirectoryAccessRule` entries (non-inherited by default, with an option for inherited)
- Filter out default/system ACEs (e.g. `NT AUTHORITY\SYSTEM`, `BUILTIN\Administrators`)
- Categorize each ACE into a `RightCategory`:
  - `ResetPassword` extended right → `PasswordReset`
  - `CreateChild` for computer objects → `ComputerManagement`
  - `WriteProperty:lockoutTime` → `AccountUnlock`
  - `WriteProperty:<attribute>` → `AttributeWrite`
  - `GenericAll` / `GenericWrite` → `FullControl`
- Serialize under the `"Delegations"` key in the JSON output

Expected JSON format per delegation:
```json
{
  "TrusteeName": "Helpdesk_L1",
  "TrusteeType": "Group",
  "TargetDN": "OU=Users,DC=contoso,DC=com",
  "Right": "ResetPassword",
  "RightCategory": "PasswordReset",
  "IsInherited": false,
  "Tier": "Tier 1"
}
```

---

### Step 10 — `DelegationsViewModel`
**File:** `SMAD-X\ViewModels\DelegationsViewModel.cs`

- Recursively walks the entire `ADObject` tree and flattens all `Delegations` from every node
- `ObservableCollection<ADDelegation> AllDelegations`
- `ObservableCollection<ADDelegation> FilteredDelegations`

Filters:
- `FilterTrustee` (string) — filter by trustee SAMAccountName
- `FilterRight` (string) — filter by right/category
- `FilterTier` (string) — filter by Tier
- `FilterInherited` (bool?) — include / exclude inherited ACEs
- `FilterTargetOU` (string) — filter by target OU DN

Commands:
- `ApplyFiltersCommand`
- `ExportToCsvCommand` — exports `FilteredDelegations` to CSV

---

### Step 11 — View `DelegationsWindow`
**Files:** `SMAD-X\Views\DelegationsWindow.axaml` / `.axaml.cs`

Layout:
- **Filter bar** at the top: Trustee, Right/Category, Tier, Target OU, "Include inherited" checkbox
- **DataGrid** with columns: Trustee, Type, Right, Category, Target OU, Inherited yes/no, Tier
- **Export CSV** button

---

### Step 12 — Main menu integration
**Files:** `MainWindow.axaml` + `MainWindowViewModel.cs`
- Menu: `Analyse > Delegations` — shortcut `Ctrl+D`
- Command: `ShowDelegationsCommand`

---

## Implementation order

| # | Step | Dependencies |
|---|------|--------------|
| 1 | Model DomainSnapshot | — |
| 2 | Model ADChangeItem | — |
| 3 | Service ADDiffService | Steps 1, 2 |
| 4 | DomainTimelineViewModel | Step 3 |
| 5 | View DomainTimelineWindow | Step 4 |
| 6 | Menu integration (Timeline) | Step 5 |
| 7 | Model ADDelegation | — |
| 8 | Delegations property in ADObject | Step 7 |
| 9 | PowerShell export of delegations | Step 8 |
| 10 | DelegationsViewModel | Steps 7, 8 |
| 11 | View DelegationsWindow | Step 10 |
| 12 | Menu integration (Delegations) | Step 11 |

> **Note:** Part 1 (Timeline) and Part 2 (Delegations) are independent and can be developed in parallel.
