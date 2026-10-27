# Import and export

## Saving

**File > Save...** (`Ctrl+S`) writes the domain and its site topology to a `.smad-x.json` file. **File > Export to JSON...** (`Ctrl+E`) produces the same content under another name. The format is described in [File format](file-format.md).

## From a real Active Directory: the import script

**File > Generate AD Import Script...** (`Ctrl+I`) produces a PowerShell script to run on a domain machine, with the ActiveDirectory module. It reads the structure, security attributes (`userAccountControl`, SPNs, delegation, `adminCount`, owners, `pwdLastSet` and `lastLogonTimestamp` dates), delegations and the site topology, then writes a `.smad-x.json` to open in SMAD-X.

This is the fastest way to **audit an existing domain** and look for attack paths in it, without installing an agent or granting SMAD-X any right: the script only reads.

Only the permissions that give control are kept: full control, changing the permissions or the owner, password reset, writing the members of a group or every attribute, directory replication. Read access and denied permissions (`Deny`) are ignored.

!!! note "Reading a directory, never writing it"
    SMAD-X produces no script that changes a real Active Directory: what is learned in the simulator is done again by hand, knowingly, in a test domain.

## The documentation report

**File > Export Documentation Report...** (`Ctrl+Shift+R`):

| Option | Choices |
|---|---|
| Content | the whole domain in one file, or a selection of objects |
| Formats | Markdown (always), DOCX, PDF |
| Theme | WordLike, Plain, Technical Document, GitHub, Compact, Report |

The PDF embeds its fallback fonts (Noto, including emoji and symbols): it renders the same on every system.

## Other exports

| Export | Where |
|---|---|
| Tree image | **Export PNG** button |
| Delegations | **Delegations** window > **Export CSV** |
| Differences between two files | **Analysis > Domain timeline...** (`Ctrl+T`), CSV export |
| Lab report | lab window > **Export result** (Markdown) |
| Lab submission, to hand in to the trainer | lab window > **Save my submission** (`.smadx-submission.json`) |
