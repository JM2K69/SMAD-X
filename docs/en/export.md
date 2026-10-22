# Import and export

## Saving

**File > Save...** (`Ctrl+S`) writes the domain and its site topology to a `.smad-x.json` file. **File > Export to JSON...** (`Ctrl+E`) produces the same content under another name. The format is described in [File format](file-format.md).

## To a real Active Directory: the PowerShell script

**File > Export to PowerShell...** (`Ctrl+P`) generates a script that recreates the structure in a real AD: OUs, accounts, groups and memberships, linked GPOs, PSOs, and the modeled security attributes.

!!! danger "Review before running"
    The script runs with your rights, on the directory of your choice. Run it in a **test domain** first, never in production without reviewing it.

The script is written to be safe:

- names are escaped, so an apostrophe or a `$` in a name or description neither breaks the script nor gets interpreted;
- the file is UTF-8 with BOM, so that Windows PowerShell 5.1 reads accented characters correctly;
- security attributes are **added** without overwriting what exists.

## From a real Active Directory: the import script

**File > Generate AD Import Script...** (`Ctrl+I`) produces a PowerShell script to run on a domain machine, with the ActiveDirectory module. It reads the structure, security attributes (`userAccountControl`, SPNs, delegation, `adminCount`, owners, `pwdLastSet` and `lastLogonTimestamp` dates), delegations and the site topology, then writes a `.smad-x.json` to open in SMAD-X.

This is the fastest way to **audit an existing domain** and look for attack paths in it, without installing an agent or granting SMAD-X any right: the script only reads.

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
| Differences between two files | **Analyse > Domain Timeline** (`Ctrl+T`), CSV export |
| Lab report | lab window > **Export result** (Markdown) |
