# 👥 Cloneable Domain Controllers Group

**Global** group created automatically when the domain is promoted (Windows Server 2012+).

## Role
- Members (DCs) that can be **cloned** to rapidly deploy additional domain controllers
- Cloning feature based on Hyper-V

## ⚠️ Security
> 🔴 **Tier 0 — DC cloning feature**

- Limit to DCs that actually need to be cloned
- A DC clone inherits the source DC's secrets — process must be strictly controlled
- Verify that the Hyper-V host is secured before any cloning