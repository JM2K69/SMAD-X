# Verified Windows rules

SMAR-X's Windows engine reproduces what a real Windows does. `tools/SmarxWindowsCheck` recreates the sample server on a Windows Server 2025 domain controller, reads its owners and ACLs in their real order, then opens each file and each folder with each of the 14 special rights, under each account's token: 1,512 opens, all identical to the simulator. This page gathers the rules SMAR-X applies, including those these measurements uncovered.

## AccessCheck: the order of ACEs

Windows reads the DACL **in order** and stops as soon as it can answer:

- an **Allow** ACE that applies to the token grants its rights; as soon as every requested right is granted, access is allowed;
- a **Deny** ACE blocks access if it covers a requested right **not yet granted**; a deny does not take back a right already granted;
- at the end of the DACL, what no ACE granted is denied.

The canonical order puts **explicit ACEs before inherited ones**, and among explicit ones, **denies before allows**. An explicit allow is therefore read before an inherited deny, and wins. The Windows warning ("deny entries take precedence") only holds for ACEs of the same rank.

## The token

- The token of a local session holds the account, its groups and, step by step, the groups of its groups, **Everyone**, **Authenticated Users**, **INTERACTIVE** and **Users**.
- **User Account Control (UAC)**: without elevation, an administrator's **Administrators** group is "deny only". It counts for denies, never for allows. The built-in **Administrator** account is not filtered.
- An administrator in an elevated session has the **Take ownership of files** privilege.
- The folders on the path are not checked: everyone has the **Bypass traverse checking** privilege.

## Groups: AGDLP

The sample server is a member of the **CORP** domain and follows **AGDLP**, the method Microsoft recommends:

- the domain **accounts** (alice, bob, dave) are in **global groups**, one per role: `G_Employes`, `G_Compta`, `G_RH`, `G_Stagiaires`;
- the global groups are members of **domain local groups**, one per resource and per right: `DL_<resource>_L` (read), `_LM` (read and modify), `_CT` (full control), `_Refuser` (deny);
- NTFS **permissions** name only domain local groups and the built-in identities (Administrators, SYSTEM, CREATOR OWNER, Authenticated Users…).

carol and Administrator are local accounts of the server. A domain account is written `CORP\alice`, a server account `SRV-FIC\carol`, as in `icacls`.

The token holds the account's groups **and the groups of those groups**: alice, a member of `G_Compta`, gets `DL_Compta_LM`, because `G_Compta` is a member of it. The tester shows it from the first step; the **Accounts** card writes it under each account ("through nesting: …"); the **Nesting** tab of the **Accounts and groups** window draws it (see below).

Nesting follows Active Directory's rules. In **Select users or groups**, an impossible choice is grayed out, and its reason is written below it:

| Group | Can contain |
|---|---|
| global (`G_`) | domain accounts, global groups |
| domain local (`DL_`) | domain accounts, global groups, domain local groups |
| local to the server (Administrators…) | any account, global and domain local groups, never another local group |

A local account of the server only joins a local group. A nesting that would loop is refused.

### The Accounts and groups window

In the Windows workspace, the **Accounts** card lists the accounts; its **Accounts and groups…** button opens a window that manages them like the *Active Directory Users and Computers* console. It stays open next to the workspace: each change goes at once into the tester, the matrix and the whoami window.

- **On the left, the tree**: the domain (`CORP`) with its **Users**, its **Global groups (G_)** and its **Domain local groups (DL_)**, then the server with its **Local users** and its **Local groups** (Administrators, Users…). A server outside a domain only has this second part.
- **On the right, the selected object**: its name, what it is and its qualified name (`CORP\bob`, `SRV-FIC\carol`, `BUILTIN\Administrators`), then three tabs:
    - **Member Of**: the groups it is a direct member of;
    - **Members**: for a group, its direct members, accounts and groups;
    - **Nesting**: the tree of the account, its groups and the groups of those groups, each path written on one line (`bob → G_Compta → DL_Compta_LM`), and the list of the groups received through nesting. This is what `whoami` flattens into a list.
- **Add…** opens **Select users or groups**: tick one or more accounts or groups, a filter narrows the list. What Active Directory would refuse is grayed out, with its reason: already a member, a local account of the server in a domain group, a domain local group in a global group, a nesting that would loop. **Remove** removes the selected link.
- **New** creates an account or a group: domain user, global group, domain local group, local user or local group of the server. It is selected in the tree at once.
- The **Users** group holds every account of the server and the domain, without adding them: its members are not changed.

Below the tabs, the commands that would do the same on a real server: the domain's, `New-ADGroup -GroupScope Global` or `DomainLocal` and `Add-ADGroupMember`, or the server's, `net localgroup`.

![The SMAR-X Accounts and groups window](../assets/screens/smarx-directory.png)

*Screenshot: the domain local group DL_Compta_LM, in the Members tab: the global group G_Compta is a member of it.*

![The Nesting tab of the Accounts and groups window](../assets/screens/smarx-nesting.png)

*Screenshot: bob's nesting. He is a member of three global groups, which make him a member of four domain local groups.*

![Select users or groups](../assets/screens/smarx-picker.png)

*Screenshot: Add… in the Members tab of G_RH. bob is already a member; carol and Administrator, local accounts of the server, cannot join a domain group; a domain local group cannot join a global group.*

The measurement on the domain controller creates these global and domain local groups and their nesting. Every open that goes through a nested group is identical to the simulator.

## whoami: an account's token

**whoami of this account…**, under **Who?** in the tester and in the **Accounts** card, opens what `whoami /user /groups` shows: the account and its SID, then each identity of the token with its type, its SID and its attributes. The **Why** column explains it: direct member, "through G_Compta" for a group received through nesting, session open on the server or network session.

![The SMAR-X whoami window](../assets/screens/smarx-whoami.png)

*Screenshot: dave was just added to G_RH. The token of his next session gets G_RH and, through it, DL_RH_LM; in his open session, whoami still shows the old token.*

- **Type**: `Well-known group` for a predefined identity, `Alias` for a local group (`BUILTIN\…`) or a domain local group (with the `Local Group` attribute), `Group` for a global group.
- **Administrators**: without elevation, `Group used for deny only`; in an elevated session, `Group owner`, and the integrity level goes from `Medium` to `High`. Through a share, a local administrator account stays filtered.
- The window follows the chosen account, session (on the server, or network through a share) and elevation. It stays open alongside and updates when groups change.

**Groups only count from the next session.** Windows reads an account's groups when its session opens; after a change, `whoami` still shows the old token. The window says so and marks what changes: `+` added, `−` removed, `~` attributes changed. **Sign out and in again** gives the account its new token. In SMAR-X, sessions open when the server is loaded or when a lab starts. The tester and the matrix, for their part, always compute with the current groups, as after a new sign-in.

The text is that of an English Windows, like the lab's computers. A real token holds a few more identities (`LOCAL`, `CONSOLE LOGON`, `This Organization`, `Domain Users`…): they change nothing to the accesses SMAR-X simulates, which does not show them.

## The owner

The owner always has **Read permissions** and **Change permissions**, even without an ACE, unless the DACL holds an **OWNER RIGHTS** ACE: it then replaces these implicit rights.

## What the parent folder grants

Opening a file or a folder goes through AccessCheck on its DACL, then through two NTFS rules that look at the parent folder. The measurements confirmed them:

- **Delete** is granted if the parent folder grants **Delete subfolders and files**;
- **Read attributes** is granted if the parent folder grants **List folder**. Listing a folder already shows the attributes of what it contains. On the sample server, dave has no right on `D:\Partages\Compta`, yet he reads its attributes, because he can list `D:\Partages`.

SMAR-X's **Effective Access** tab (Advanced window) says so: "(from the parent folder)".

## Inheritance

- `(OI)`: files inherit; `(CI)`: subfolders inherit; `(IO)`: the ACE does not apply to the folder that holds it; `(NP)`: only direct children inherit.
- A subfolder keeps an `(OI)`-only ACE as "inherit only", to pass it on to its files.
- **CREATOR OWNER** becomes each child's owner, and is still passed on as "inherit only".
- **No duplicates among inherited ACEs.** An ACE already inherited is not copied again, and an ACE that would grant the object what an inherited ACE **read before it** already grants keeps only its inheritable part, as "inherit only". Under a folder created by an administrator:
    - if the root holds `Administrators (OI)(CI)(F)` **before** `CREATOR OWNER (OI)(CI)(IO)(F)`, the folder gets `Administrators (I)(OI)(CI)(F)` and `CREATOR OWNER (I)(OI)(CI)(IO)(F)`;
    - if CREATOR OWNER comes **first**, it gets `Administrators (I)(F)`, `CREATOR OWNER (I)(OI)(CI)(IO)(F)` and `Administrators (I)(OI)(CI)(IO)(F)`.

## Denies from the Security tab

Almost every file open asks for **Synchronize**. The Security tab therefore leaves it out of a deny. Denying Write there writes `(WD,AD,WA,WEA)`, whereas `icacls … /deny account:(W)` also denies Synchronize: the account could no longer open the file, not even to read it. Only the **Full control** deny keeps Synchronize. The rest of the boxes' behavior is described in [The Security tab](security-tab.md#the-edit-window).

## Through a share

Over the network come the share permissions, the network token (UAC remote restrictions) and access-based enumeration: see [Access through a share](shares.md). Through a share, **Synchronize** alone is decided like **Read attributes**.

## Comparing with a real server

To compare SMAR-X's output with a server's:

- `icacls` shows ACEs **in the order they are stored**, as SMAR-X does;
- `icacls … /grant` adds each ACE **at the head** of the explicit ones, and puts those that are not inherited before the others: commands given in another order give another order;
- `Get-Acl` **sorts explicit ACEs by SID**: its order is not the DACL order.
