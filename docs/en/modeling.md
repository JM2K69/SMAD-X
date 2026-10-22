# Modeling a domain

SMAD-X does not just draw a directory: it models its **security attributes**, the very ones an attacker exploits and the audit checks.

## Objects

| Type | Role | Notes |
|---|---|---|
| **Domain** | root, holds delegations and GPOs linked to the domain | |
| **OU** | organizational unit: delegations, GPOs, tier classification | can contain OUs |
| **Container** | `CN=` such as Users, Computers, System | cannot contain OUs, as in a real AD |
| **User**, **Computer**, **gMSA** | accounts | account options, SPNs, Kerberos delegation |
| **Group** | scope (domain local, global, universal) and type (security, distribution) | group nesting |
| **Policy (GPO)** | created under `System\Policies` | linked to OUs, the domain or sites |
| **PSO** | fine-grained password policy | applied to users or groups |

## The Security section

![Security section of a service account](assets/screens/security-panel.png)

It mirrors real Active Directory attributes:

| Field | AD attribute | What it shows |
|---|---|---|
| Account is disabled | `userAccountControl` (ACCOUNTDISABLE) | a disabled account is not an entry point |
| Password not required | `userAccountControl` (PASSWD_NOTREQD) | an empty password is accepted |
| Password never expires | `userAccountControl` (DONT_EXPIRE_PASSWORD) | ANSSI point for privileged accounts |
| Kerberos preauthentication not required | `userAccountControl` (DONT_REQ_PREAUTH) | AS-REP roasting |
| Unconstrained delegation | `userAccountControl` (TRUSTED_FOR_DELEGATION) | TGT theft |
| Constrained delegation with protocol transition | `userAccountControl` (TRUSTED_TO_AUTH_FOR_DELEGATION) | impersonation to a service |
| Account is sensitive and cannot be delegated | `userAccountControl` (NOT_DELEGATED) | protects an administrator |
| SPNs | `servicePrincipalName` | Kerberoasting |
| Password last set, last logon | `pwdLastSet`, `lastLogonTimestamp` | dormant accounts, old krbtgt |
| Constrained delegation to | `msDS-AllowedToDelegateTo` | delegation to a domain controller |
| adminCount | `adminCount` | object protected by AdminSDHolder |
| Owner | security descriptor owner | an owner can rewrite permissions |

Options use the real `userAccountControl` bits: PowerShell export and import carry them as they are.

!!! tip "AdminSDHolder"
    Members of protected groups (Domain Admins, Account Operators, Backup Operators…) have `adminCount = 1`: the SDProp process applies AdminSDHolder's permissions to them and **disables inheritance**. SMAD-X takes this into account: a delegation set on an OU does not apply to these objects. This is why some attack paths do not exist.

## Memberships and relations

**View > Manage Relations...** (or the **Relations** button) opens four tabs:

| Tab | Use |
|---|---|
| User → Group | account membership |
| Group → Group | group nesting |
| GPO → OU | GPO links to OUs and the domain |
| PSO | accounts and groups a PSO applies to |

The audit and attack paths compute **effective membership**: an account that belongs to a group which is itself a member of Domain Admins is a domain administrator.

## Delegations

![Delegations window](assets/screens/delegations.png)

**Analyse > Delegations** (`Ctrl+D`) lists delegations: trustee, right, category, target DN, inheritance, tier. Filter by trustee, target or category, hide inherited delegations, and export to CSV.

To **delegate**, select an OU on the left, choose the trustee and the category (password reset, computer management, account unlock, attribute write, full control), then **Add**. **Remove the selected delegation** deletes it.

## The tier model

Every object is classified in a tier, following Microsoft's administration model:

| Tier | Content |
|---|---|
| **Tier 0** | domain controllers, accounts and groups that control the directory |
| **Tier 1** | servers and applications |
| **Tier 2** | workstations and users |

**Settings > Manage Tiers...** changes names, levels and colors. The audit checks tiering: an object must not be a member of a more sensitive group (TIER-01), nor receive a delegation over a more sensitive object (TIER-03).
