# Limits

SMAR-X simulates file access rights, and its engines are compared with real systems (see [Faithful to real systems](index.md#faithful-to-real-systems)). What follows, it does not simulate, or only in part. A lab should not rely on it.

## What is measured, and on what

- The comparisons were made on a Linux 6.19 kernel (Debian 13, `setfacl` 2.3.2), Samba 4.22, Windows Server 2025 and an English Windows 11. Other versions may differ on a detail.
- Only the sample servers were measured. A server built by the trainer follows the same rules, but its special cases were not checked one by one.
- The Security tab was recorded on an English Windows 11; its French labels are those of a French Windows, not recorded.
- The cheat sheet's commands were replayed as they are written, on a lab Windows 11 (Windows PowerShell 5.1, NTFSSecurity module 4.2.6) and on a Debian 13 (`tools/SmarxGlossaryCheck`). They ran on a copy of the sample folders (`C:\SmxGlossary`), with local groups of the same names instead of those of the `CORP` domain. Three exceptions: `Add-ADGroupMember`, which would change the directory, is only checked in its syntax; `smbpasswd -a` was replayed with `-s` (the password read from standard input); `Install-Module` with `-Force`, without confirmation.
- The `icacls` commands SMAR-X suggests give the special rights in the order of the Advanced window, (WD,AD,WA,WEA) for example; `icacls` accepts them in any order, but shows them in its own (see [How icacls writes rights](windows-rules.md#how-icacls-writes-rights)).

## What SMAR-X represents

- **One Linux server and one Windows server** per `.smar-x.json` file, each with a single volume. No network between several servers, no firewall, no DFS.
- **From the network**, access goes through an SMB share (Windows) or a Samba share (Linux). The client computer is not simulated: not its SMB version, nor signing or encryption, nor its cache, which the measurement had to guard against (see [Access through a share](shares.md#how-it-is-verified)).
- **The operations** are the tester's: read, write, execute, list, enter, create, delete, change the permissions, the owner or the group. Renaming or moving is not an operation of its own.

## Linux

- The permissions are the kernel's: owner, group, `rwx` bits, setuid, setgid, sticky, `umask`, POSIX ACLs and default ACLs, mask, and `root`.
- **Not simulated**: SELinux and AppArmor, capabilities (`CAP_DAC_OVERRIDE`…) other than `root`'s, `sudo`, `chattr` attributes (`+i`, `+a`), mount options (`ro`, `noexec`, `nosuid`), NFS, directories (LDAP, SSSD).
- **Samba**: only the parameters of the Samba sharing window (see [Samba share](samba.md)). No `admin users`, `guest ok`, `hosts allow`, `inherit permissions`, `inherit acls`, nor NT ACLs stored by Samba (`vfs_acl_xattr`).

## Windows

- The permissions are those of NTFS and AccessCheck: DACL in order, inheritance, owner, OWNER RIGHTS, CREATOR OWNER, the 14 special rights, what the parent folder grants, User Account Control, shares and access-based enumeration (see [Verified Windows rules](windows-rules.md)).
- **A single domain**, with no trust or forest, with global, domain local and server-local groups. No universal groups, no predefined domain groups (Domain Users, Domain Admins…).
- **The predefined identities** are Everyone, Authenticated Users, INTERACTIVE, NETWORK, Users, Administrators, SYSTEM, CREATOR OWNER and OWNER RIGHTS. No Anonymous, Guest or null session.
- **Privileges**: Take ownership (administrator in an elevated session) and Bypass traverse checking (everyone). No Backup or Restore, which override ACLs.
- **Not simulated**: the SACL and auditing, integrity control on files (the integrity level is only shown in whoami), Dynamic Access Control (claims, central access policies), the Read-only attribute, EFS, alternate data streams, links and junctions, previous versions.
- **whoami** shows the identities SMAR-X simulates, not `LOCAL`, `CONSOLE LOGON`, `This Organization` or `Domain Users`. The text is that of an English Windows.
- **A session's groups**: Windows reads them when the session opens, but the tester and the matrix always compute with the current groups, as after signing in again. The whoami window shows the gap (see [whoami](windows-rules.md#whoami-an-accounts-token)).

## Studio's recipes

- The **business rules** are fixed: each department on its folder, the Direction everywhere, the second department reading the first one. They cannot be chosen one by one yet.
- Each department has **two accounts**, with first names taken from a list; a department has a single folder and a single file.
- The recipes write **no questions**; the trainer can add some in Studio.
- Some recipes exist on one server only: `SMX-NOMINATIF` and `SMX-PARTAGE` on Windows, `SMX-AUDIT` on Linux.

## Labs and copies

- A lab checks only its objectives: the accesses, rules and guards the trainer chose. "Exactly the accesses needed" only covers the accounts, paths and operations it names.
- The **sealed copy** spots a copy edited by hand, the copy of another lab or a classmate's. The format is public and SMAR-X runs on the learner's computer: the seal does not stop a determined forger who would rewrite the whole file consistently.
- **Exam mode** hides hints, the score and the explanation of objectives. It is not surveillance: the learner keeps the rest of SMAR-X, the tester included.
