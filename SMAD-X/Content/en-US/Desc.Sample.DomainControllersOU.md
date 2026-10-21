# 🏛️ Domain Controllers OU (Tier 0)

System organizational unit containing all **domain controllers**.

## Role
- Automatically created when the first DC is promoted
- All DCs are automatically placed here upon promotion
- Exclusive target of the **Default Domain Controllers Policy** and DC hardening GPOs

## ⚠️ Security
> 🔴 **Tier 0 — Most critical zone of the domain**

- The **Default Domain Controllers Policy** must remain linked and must never be deleted
- Apply additional hardening GPOs: CIS DC Benchmark, etc.
- Prohibit interactive logons by Tier 1 or Tier 2 accounts on DCs
- Enable advanced auditing on all DCs (logons, AD modifications, Kerberos)
- Monitor Kerberos exploitation attempts (AS-REP Roasting, Kerberoasting) via Event IDs 4768, 4769, 4771
- Schedule and test regular System State backups